"""Concilia SQL y prueba el portal Demo en 5091. Ejecutar antes de usarlo para capturar atenciones."""
import argparse
from collections import defaultdict
from datetime import datetime
import gzip
import http.cookiejar
import json
from pathlib import Path
import re
import secrets
import time
from urllib.error import HTTPError
from urllib.parse import urlencode
from urllib.request import HTTPCookieProcessor, Request, build_opener

from preparar_demo import DEMO, ORIGEN, PAQUETE, TABLAS, sql, verificar_paquete, verificar_sql


class Sesion:
    def __init__(self, url):
        self.url = url
        self.http = build_opener(HTTPCookieProcessor(http.cookiejar.CookieJar()))
        self.csrf = None

    def pedir(self, ruta, *, datos=None, estado=200, jsonl=False):
        headers = {}
        body = None
        if datos is not None:
            if self.csrf is None:
                self.csrf = self.pedir("/auth/csrf")["token"]
            headers = {"Content-Type": "application/json", "X-XSRF-TOKEN": self.csrf}
            body = json.dumps(datos).encode()
        req = Request(self.url + "/api" + ruta, data=body, headers=headers)
        try:
            with self.http.open(req, timeout=45) as respuesta:
                assert respuesta.status == estado, f"{ruta}: se esperaba HTTP {estado}"
                if jsonl:
                    assert respuesta.headers.get_content_type() == "application/x-ndjson"
                    assert "attachment" in respuesta.headers["Content-Disposition"]
                    return [json.loads(fila) for fila in respuesta]
                contenido = respuesta.read()
                if ruta == "/auth/login":
                    self.csrf = None
                return json.loads(contenido) if contenido else None
        except HTTPError as error:
            assert error.code == estado, f"{ruta}: HTTP {error.code}, esperado {estado}"
            contenido = error.read()
            return json.loads(contenido) if contenido else None


def json_sql(contenedor, base, consulta):
    return json.loads(sql(contenedor, base, consulta + " FOR JSON PATH,INCLUDE_NULL_VALUES;") or "[]")


def conciliacion(contenedor):
    def columnas(base):
        return json_sql(contenedor, base, """
          SELECT OBJECT_NAME(c.object_id) tabla,c.name columna,TYPE_NAME(c.user_type_id) tipo,
                 c.max_length,c.precision,c.scale,c.is_nullable,c.is_identity,c.collation_name
          FROM sys.columns c JOIN sys.tables t ON t.object_id=c.object_id
          WHERE t.schema_id=SCHEMA_ID('dbo') ORDER BY tabla,c.column_id""")

    def relaciones(base):
        return json_sql(contenedor, base, """
          SELECT OBJECT_NAME(f.parent_object_id) hija,COL_NAME(c.parent_object_id,c.parent_column_id) columna,
                 OBJECT_NAME(f.referenced_object_id) padre,
                 COL_NAME(c.referenced_object_id,c.referenced_column_id) referencia,
                 f.is_disabled,f.is_not_trusted
          FROM sys.foreign_keys f JOIN sys.foreign_key_columns c ON f.object_id=c.constraint_object_id
          WHERE OBJECT_SCHEMA_NAME(f.parent_object_id)='dbo'
          ORDER BY hija,columna""")

    assert columnas(DEMO) == columnas(ORIGEN)
    fk = relaciones(DEMO)
    assert fk == relaciones(ORIGEN) and len(fk) == 8
    assert all(not x["is_disabled"] and not x["is_not_trusted"] for x in fk)
    dbml = Path(__file__).resolve().parents[2] / "documentacion/diagramas/mvp-puestos-fisico.dbml"
    contenido = dbml.read_text(encoding="utf-8")
    referencias = re.findall(r"^Ref: dbo\.(\w+)\.(\w+) > dbo\.(\w+)\.(\w+)$", contenido, re.M)
    assert set(referencias) == {(x["hija"], x["columna"], x["padre"], x["referencia"]) for x in fk}
    assert set(re.findall(r"^Table dbo\.(\w+) \{", contenido, re.M)) == set(TABLAS)

    assert len(json_sql(contenedor, DEMO, """
        SELECT name FROM sys.schemas WHERE name IN('archivo','respaldo_puestos','reporting')
    """)) == 0

    diferencias = []
    for tabla, columnas_tabla in TABLAS.items():
        anterior = f"SELECT {columnas_tabla} FROM [{ORIGEN}].dbo.{tabla}"
        nuevo = f"SELECT {columnas_tabla} FROM dbo.{tabla}"
        diferencias.append(f"SELECT 1 AS diferencia FROM ({anterior} EXCEPT {nuevo}) a")
        diferencias.append(f"SELECT 1 FROM ({nuevo} EXCEPT {anterior}) b")
    assert sql(contenedor, DEMO, "SELECT COUNT(*) FROM (" + " UNION ALL ".join(diferencias) + ") x;") == "0"
    assert json_sql(contenedor, DEMO, """
        SELECT numero_parte,cantidad,fecha_stock FROM dbo.vw_kpi_inventario_actual ORDER BY numero_parte
    """) == json_sql(contenedor, ORIGEN, """
        SELECT numero_parte,cantidad,fecha_stock FROM reporting.vw_kpi_inventario_actual ORDER BY numero_parte
    """)
    return {"sql": verificar_sql(contenedor), "fk": fk, "filas_operativas_distintas": 0}


def kpi(contenedor, corte):
    grupos = defaultdict(lambda: [0, 0])
    with gzip.open(PAQUETE / "movimientos.jsonl.gz", "rt", encoding="utf-8") as archivo:
        for linea in archivo:
            fila = json.loads(linea)
            key = (fila["espacio_identificadores"], fila["tipo_movimiento"], fila["id_turno"])
            grupos[key][0] += 1
            grupos[key][1] += fila["cantidad_piezas"] or 0
    referencia = json_sql(contenedor, ORIGEN, """
        SELECT espacio_identificadores origen,tipo_movimiento tipo,id_turno turno,COUNT(*) filas,
               SUM(CONVERT(bigint,cantidad_piezas)) piezas
        FROM reporting.vw_kpi_movimientos GROUP BY espacio_identificadores,tipo_movimiento,id_turno
        ORDER BY origen,tipo,turno
    """)
    for fila in referencia:
        assert grupos.pop((fila["origen"], fila["tipo"], fila["turno"])) == [fila["filas"], fila["piezas"]]
    assert not grupos
    assert sum(x["filas"] for x in referencia) == corte["movimientos_consultables"]
    return referencia


def equivalentes(filas):
    for fila in filas:
        for campo in ("fecha_movimiento", "fecha_hora_inicio", "fecha_hora_fin"):
            if fila[campo]:
                fila[campo] = datetime.fromisoformat(fila[campo].removesuffix("Z"))
    return filas


def probar(args, reporte, corte):
    usuario = json.loads(args.acceso.read_text(encoding="utf-8"))
    admin, operario = Sesion(args.url), Sesion(args.url)
    salud = admin.pedir("/health")
    assert salud["baseDatos"] == DEMO
    admin.pedir("/historial", estado=401)
    admin.pedir("/auth/login", datos={"username": usuario["usuario"], "password": usuario["password"]})
    assert "administrar_accesos" in admin.pedir("/auth/me")["permissions"]
    catalogo = admin.pedir("/catalogo")
    assert catalogo["exportacion_historica"]
    assert catalogo["corte"]["corte_fuente"] == corte["corte_fuente"]
    inventario = admin.pedir("/inventario?alarmas=true")
    assert datetime.fromisoformat(inventario["corte"].removesuffix("Z")) == datetime.fromisoformat(corte["fecha_stock"])
    assert sum(x["stock_total_material"] for x in inventario["filas"] if x["stock_total_material"] is not None) == reporte["sql"]["piezas"]
    comienzo = time.perf_counter()
    pagina = admin.pedir("/historial")
    frio = time.perf_counter() - comienzo
    assert pagina["total"] == corte["movimientos_consultables"] and len(pagina["filas"]) == 50
    referencia = json_sql(args.contenedor, ORIGEN, """
        SELECT TOP(50) * FROM reporting.vw_kpi_movimientos
        ORDER BY fecha_movimiento DESC,id_movimiento DESC
    """)
    assert equivalentes(pagina["filas"]) == equivalentes(referencia)
    comienzo = time.perf_counter()
    segunda = admin.pedir("/historial?pagina=2")
    caliente = time.perf_counter() - comienzo
    assert segunda["total"] == pagina["total"] and segunda["filas"][0]["id_movimiento"] != pagina["filas"][0]["id_movimiento"]
    # Compara un corte local de México y una parte presente en el histórico.
    filtros = {"parte": "5TA 972 596", "desde": "2026-10-05",
               "hasta": "2026-10-05", "origen": "HISTORICO_21"}
    query = urlencode(filtros)
    seleccion = admin.pedir("/historial?" + query)
    exportacion = admin.pedir("/historial/exportar?" + query, jsonl=True)
    exactos = json_sql(args.contenedor, ORIGEN, """
        SELECT * FROM reporting.vw_kpi_movimientos
        WHERE espacio_identificadores='HISTORICO_21' AND numero_parte LIKE '%5TA 972 596%'
          AND fecha_movimiento>='2026-10-05T06:00:00' AND fecha_movimiento<'2026-10-06T06:00:00'
        ORDER BY fecha_movimiento DESC,id_movimiento DESC
    """)
    assert len(exactos) > 0 and seleccion["total"] == len(exportacion) == len(exactos)
    assert equivalentes(exportacion) == equivalentes(exactos)
    anterior = admin.pedir("/historial?origen=MVP")
    assert anterior["total"] == corte["movimientos_previos"]
    movimiento = next(fila for fila in anterior["filas"] if fila["id_ubicacion"] is not None)
    id_ubicacion = movimiento["id_ubicacion"]
    ubicacion = admin.pedir(f"/historial?ubicacion=MVP%3A{id_ubicacion}")
    assert ubicacion["filas"] and all(f["espacio_identificadores"] == "MVP" and f["id_ubicacion"] == id_ubicacion for f in ubicacion["filas"])
    admin.pedir("/historial?origen=INVALIDO", estado=400)

    puesto = next((x["id_puesto"] for x in catalogo["puestos"] if not x["acceso_habilitado"]
                   and any(f["id_puesto"] == x["id_puesto"] for f in inventario["filas"])), None)
    assert puesto is not None, "Se requiere un puesto sin acceso para una prueba reversible."
    propia = next(f["id_ubicacion"] for f in inventario["filas"] if f["id_puesto"] == puesto)
    ajena = next(f["id_ubicacion"] for f in inventario["filas"] if f["id_puesto"] != puesto)
    admin.pedir("/atenciones/iniciar", datos={"ubicacion": propia}, estado=403)
    nombre = "prueba-demo-" + secrets.token_hex(5)
    clave = "Prueba!" + secrets.token_hex(20)
    registros = set()
    admin.pedir("/puestos/acceso", datos={"puesto": puesto, "usuario": nombre, "password": clave})
    try:
        operario.pedir("/auth/login", datos={"username": nombre, "password": clave})
        assert operario.pedir("/auth/me")["puestoId"] == puesto
        operario.pedir("/historial/exportar", estado=403)
        operario.pedir("/historial", estado=403)
        operario.pedir("/atenciones/iniciar", datos={"ubicacion": ajena}, estado=403)
        abierta = operario.pedir("/atenciones/iniciar", datos={"ubicacion": propia})
        registro = abierta["id_registro"]
        registros.add(registro)
        assert abierta["fecha_hora_fin"] is None
        assert operario.pedir("/atenciones/iniciar", datos={"ubicacion": propia})["id_registro"] == registro
        final = operario.pedir("/atenciones/finalizar", datos={"registro": registro})
        assert final["fecha_hora_fin"] and final["id_turno"] == abierta["id_turno"]
        turno = final["id_turno"] or 0
        assert any(f["id_registro"] == registro for f in admin.pedir(f"/atenciones?turno={turno}")["filas"])
        otro_turno = next(t["id_turno"] for t in catalogo["turnos"] if t["id_turno"] != turno)
        assert all(f["id_registro"] != registro for f in admin.pedir(f"/atenciones?turno={otro_turno}")["filas"])
    finally:
        admin.pedir("/puestos/acceso", datos={"puesto": puesto, "revocar": True})
        if registros:
            ids = ",".join(str(int(x)) for x in registros)
            sql(args.contenedor, DEMO, f"DELETE FROM dbo.REGISTRO WHERE id_registro IN ({ids}) AND id_puesto={int(puesto)};")
    operario.pedir("/auth/me", estado=401)
    reporte["api"] = {
        "movimientos": pagina["total"], "exportacion_muestra": len(exportacion),
        "primera_consulta_segundos": round(frio, 3), "segunda_consulta_segundos": round(caliente, 3),
        "filtros": ["material", "fecha de México", "origen", "ubicación", "página", "turno"],
        "permisos_y_atenciones": "validados; cuenta temporal revocada y atención de prueba retirada",
    }


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--acceso", type=Path, required=True)
    parser.add_argument("--url", default="http://127.0.0.1:5091")
    parser.add_argument("--contenedor", default="motherson-mvp-sql-1")
    parser.add_argument("--evidencia", type=Path, required=True)
    args = parser.parse_args()
    corte = verificar_paquete(PAQUETE)["corte"]
    reporte = conciliacion(args.contenedor)
    reporte["historico"] = corte
    reporte["kpi_por_origen_tipo_turno"] = kpi(args.contenedor, corte)
    probar(args, reporte, corte)
    assert conciliacion(args.contenedor)["filas_operativas_distintas"] == 0
    args.evidencia.write_text(json.dumps(reporte, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    print(json.dumps(reporte["api"], ensure_ascii=False))


if __name__ == "__main__":
    main()
