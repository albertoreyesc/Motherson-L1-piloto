"""Prepara SQL de demostración y un paquete histórico verificado a partir de Pruebas."""
import argparse
import gzip
import json
import os
import re
import secrets
import subprocess
import tempfile
from pathlib import Path

from restaurar_puestos import BASE as ORIGEN, LOCAL, SQLCMD, docker, guardar_privado, literal, sha

DEMO = "Motherson_L1_MVP_Demo"
RAIZ = Path(__file__).resolve().parents[1]
PAQUETE = LOCAL / "historico-demo"
TABLAS = {
    "ESTACION": "id_estacion,id_linea,nombre_estacion",
    "ESTANTERIA": "id_estanteria,id_estacion,estanteria,nivel,cajas_estanteria",
    "PUESTO_LOGISTICO": "id_puesto,nombre,usuario,[password]",
    "COLOR": "id_color,nombre",
    "MATERIAL": "numero_parte,id_puesto,descripcion,uso,cantidad_por_caja,minimo,id_color,stock",
    "UBICACION": "id_ubicacion,numero_parte,id_estanteria",
    "TURNO": "id_turno,nombre_turno,hora_inicio,hora_fin",
    "REGISTRO": "id_registro,id_puesto,id_ubicacion,fecha_hora_inicio,fecha_hora_fin,id_turno",
}


def sql(contenedor, base, consulta):
    return docker("exec", "-i", contenedor, "sh", "-c", f"{SQLCMD} -d {base}",
                  entrada="SET NOCOUNT ON;\n" + consulta).replace("\n", "")


def filas(contenedor, consulta, *, conservar_lineas=False):
    """Cada fila se serializa por separado. Evita fragmentos de un JSON de cientos de MB."""
    with tempfile.TemporaryFile() as entrada:
        entrada.write(("SET NOCOUNT ON;\n" + consulta).encode("utf-8"))
        entrada.seek(0)
        comando = ["docker", "exec", "-i", contenedor, "sh", "-c",
                   f"{SQLCMD} -d {ORIGEN}"]
        with subprocess.Popen(comando, stdin=entrada, stdout=subprocess.PIPE,
                              encoding="utf-8", errors="strict") as proceso:
            try:
                for linea in proceso.stdout:
                    if conservar_lineas:
                        yield linea.rstrip("\n")
                    elif linea.strip():
                        yield linea.strip()
            finally:
                if proceso.poll() is None:
                    proceso.stdout.close()
            if proceso.wait():
                raise RuntimeError("Falló la lectura de una fuente SQL del histórico.")


def exportar_filas(contenedor, ruta, tabla, orden, campos="r.*"):
    consulta = f"""
        SELECT (SELECT {campos} FOR JSON PATH, INCLUDE_NULL_VALUES, WITHOUT_ARRAY_WRAPPER)
        FROM {tabla} r ORDER BY {orden};
    """
    cantidad = 0
    with gzip.open(ruta, "wt", encoding="utf-8", newline="\n") as salida:
        for linea in filas(contenedor, consulta):
            fila = json.loads(linea)
            if not isinstance(fila, dict):
                raise ValueError("Una fila histórica tiene un formato inesperado.")
            if tabla == "reporting.vw_kpi_movimientos":
                for campo in ("fecha_movimiento", "fecha_hora_inicio", "fecha_hora_fin"):
                    if fila[campo] is not None:
                        fila[campo] += "Z"
                linea = json.dumps(fila, ensure_ascii=False, separators=(",", ":"))
            salida.write(linea + "\n")
            cantidad += 1
    return dict(nombre=ruta.name, filas=cantidad, sha256=sha(ruta))


def verificar_paquete(ruta):
    datos = json.loads((ruta / "manifiesto.json").read_text(encoding="utf-8"))
    if datos["version"] != 1 or not datos["corte"]["sellado"]:
        raise ValueError("El paquete histórico no corresponde a una versión sellada.")
    archivos = {f["nombre"]: f for f in datos["archivos"]}
    requeridos = {"movimientos.jsonl.gz", "movimientos-mvp-originales.jsonl.gz",
                  "registros.jsonl.gz"}
    if len(archivos) != 12 or len(archivos) != len(datos["archivos"]) or not requeridos <= archivos.keys():
        raise ValueError("El manifiesto tiene archivos faltantes o duplicados.")
    for nombre, ficha in archivos.items():
        if not re.fullmatch(r"[a-z0-9_-]+\.jsonl\.gz", nombre) or sha(ruta / nombre) != ficha["sha256"]:
            raise ValueError(f"Huella incorrecta para {nombre}.")
    return datos


def exportar(contenedor, destino):
    if destino.exists():
        return verificar_paquete(destino)
    consulta = """
    SELECT c.maestro,c.revision,c.corte_fuente,c.fecha_carga,c.sellado,c.huella_fuentes,
           s.fecha_stock,(SELECT MIN(fecha_movimiento) FROM archivo.EVENTO) inicio_eventos,
           (SELECT MAX(fecha_movimiento) FROM archivo.EVENTO) fin_eventos,
           (SELECT COUNT(*) FROM archivo.EVENTO) eventos_maestro,
           (SELECT COUNT(*) FROM archivo.REGISTRO_MVP) movimientos_previos,
           (SELECT COUNT(*) FROM reporting.vw_kpi_movimientos) movimientos_consultables
    FROM archivo.CORTE c CROSS JOIN respaldo_puestos.CORTE s
    FOR JSON PATH, WITHOUT_ARRAY_WRAPPER;
    """
    corte = json.loads(sql(contenedor, ORIGEN, consulta))
    if not corte["sellado"]:
        raise ValueError("El histórico de origen debe estar sellado.")
    fuentes = json.loads(sql(contenedor, ORIGEN, """
        SELECT nombre,sha256,filas FROM archivo.FUENTE ORDER BY nombre FOR JSON PATH;
    """))
    if len(fuentes) != 10 or any(not re.fullmatch(r"[a-z0-9_]+\.jsonl\.gz", f["nombre"]) for f in fuentes):
        raise ValueError("El origen debe contener las diez fuentes históricas aprobadas.")
    destino.parent.mkdir(parents=True, exist_ok=True, mode=0o700)
    with tempfile.TemporaryDirectory(prefix="historico-demo-", dir=destino.parent) as temporal:
        carpeta = Path(temporal) / "paquete"
        carpeta.mkdir()
        for fuente in fuentes:
            nombre = fuente["nombre"]
            consulta = f"""
                SELECT CONVERT(varchar(max),SUBSTRING(f.contenido_gzip,1+n.value*16000,16000),2)
                FROM archivo.FUENTE f
                CROSS APPLY GENERATE_SERIES(0,CONVERT(int,(DATALENGTH(f.contenido_gzip)-1)/16000)) n
                WHERE f.nombre={literal(nombre)} ORDER BY n.value;
            """
            with (carpeta / nombre).open("wb") as salida:
                for linea in filas(contenedor, consulta):
                    salida.write(bytes.fromhex(linea))
            if sha(carpeta / nombre) != fuente["sha256"].lower():
                raise ValueError(f"La fuente {nombre} difiere de su huella en SQL.")
        if next(f["filas"] for f in fuentes if f["nombre"] == "registros.jsonl.gz") != corte["eventos_maestro"]:
            raise ValueError("El histórico maestro no concilia con la fuente sellada.")
        previos = exportar_filas(contenedor, carpeta / "movimientos-mvp-originales.jsonl.gz",
                                 "archivo.REGISTRO_MVP", "r.id_registro")
        movimientos = exportar_filas(contenedor, carpeta / "movimientos.jsonl.gz",
                                      "reporting.vw_kpi_movimientos",
                                      "r.fecha_movimiento DESC,r.id_movimiento DESC")
        if previos["filas"] != corte["movimientos_previos"] or movimientos["filas"] != corte["movimientos_consultables"]:
            raise ValueError("Los movimientos exportados no concilian con SQL.")
        datos = dict(version=1, base_origen=ORIGEN, corte=corte, archivos=[*fuentes, previos, movimientos])
        (carpeta / "manifiesto.json").write_text(json.dumps(datos, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
        verificar_paquete(carpeta)
        for archivo in carpeta.iterdir():
            if os.name != "nt":
                archivo.chmod(0o400)
        carpeta.rename(destino)
    return datos


def instalar(contenedor, datos):
    existe = sql(contenedor, "master", f"SELECT COUNT(*) FROM sys.databases WHERE name={literal(DEMO)};") == "1"
    if not existe:
        sql(contenedor, "master", f"CREATE DATABASE [{DEMO}] COLLATE SQL_Latin1_General_CP1_CI_AS;")
        sql(contenedor, "master", f"ALTER DATABASE [{DEMO}] SET RECOVERY SIMPLE;")
    if sql(contenedor, DEMO, "SELECT COUNT(*) FROM sys.tables WHERE is_ms_shipped=0;") != "0":
        if sql(contenedor, DEMO, "SELECT CONVERT(varchar(33),fecha_stock,126) FROM dbo.vw_modelo_puestos;") != datos["corte"]["fecha_stock"]:
            raise ValueError("La candidata existente pertenece a otro corte. Revísala antes de continuar.")
        return
    fecha = datos["corte"]["fecha_stock"]
    copia = []
    for tabla, columnas in TABLAS.items():
        usar_identidad = tabla != "MATERIAL"
        if usar_identidad:
            copia.append(f"SET IDENTITY_INSERT dbo.{tabla} ON;")
        copia.append(f"INSERT dbo.{tabla} ({columnas}) SELECT {columnas} FROM [{ORIGEN}].dbo.{tabla} WITH(HOLDLOCK);")
        if usar_identidad:
            copia.append(f"SET IDENTITY_INSERT dbo.{tabla} OFF;")
    # Copia la lógica operativa ya aplicada, incluida la versión final de turnos.
    nombres = ("reporting.vw_kpi_inventario_actual", "dbo.TurnoDeAtencion",
               "reporting.vw_kpi_atenciones", "dbo.GuardarAccesoPuesto",
               "dbo.IniciarAtencion", "dbo.FinalizarAtencion")
    modulos = [f"CREATE VIEW dbo.vw_modelo_puestos AS SELECT CONVERT(datetime2(7),{literal(fecha)},126) fecha_stock;"]
    for nombre in nombres:
        consulta = f"SELECT definition FROM sys.sql_modules WHERE object_id=OBJECT_ID({literal(nombre)})"
        definicion = "\n".join(filas(contenedor, consulta, conservar_lineas=True))
        if not definicion:
            raise ValueError(f"Falta {nombre} en la base de referencia.")
        modulos.append(definicion.replace("reporting.", "dbo.")
                       .replace("respaldo_puestos.CORTE", "dbo.vw_modelo_puestos"))
    ddl = (RAIZ / "db/010_demo_dbo.sql").read_text(encoding="utf-8")
    sql(contenedor, DEMO, "SET XACT_ABORT ON; BEGIN TRANSACTION;\nGO\n" + ddl + "\n"
        + "\n".join(copia) + "\nGO\n" + "\nGO\n".join(modulos) + "\nGO\nCOMMIT;\n")


def configurar_accesos(contenedor, admin_origen=None):
    admin = LOCAL / f"admin-puestos-{DEMO}.json"
    if not admin.exists():
        original = admin_origen or LOCAL / f"admin-puestos-{ORIGEN}.json"
        if not original.exists():
            raise ValueError("Falta la configuración privada de admin.local.")
        datos_admin = json.loads(original.read_text(encoding="utf-8"))
        if datos_admin["usuario"] != "admin.local" or not datos_admin["hash"]:
            raise ValueError("Configuración privada de administración inválida.")
        guardar_privado(admin, datos_admin)

    archivo = LOCAL / f"conexiones-{DEMO}.json"
    if not archivo.exists():
        info = json.loads(docker("inspect", contenedor))[0]
        port = int(info["NetworkSettings"]["Ports"]["1433/tcp"][0]["HostPort"])
        valores = {}
        for fin in ("app", "lectura"):
            nombre = f"mvp_demo_{secrets.token_hex(5)}_{fin}"
            clave = "Mvp!" + secrets.token_hex(24)
            valores[fin] = (f"Server=127.0.0.1,{port};Database={DEMO};User Id={nombre};"
                             f"Password={clave};Encrypt=True;TrustServerCertificate=True")
        guardar_privado(archivo, valores)
    credenciales = json.loads(archivo.read_text(encoding="utf-8"))
    comandos = ["SET XACT_ABORT ON; BEGIN TRANSACTION;"]
    for fin in ("app", "lectura"):
        nombre, clave = re.search(r"User Id=(mvp_demo_[a-f0-9]{10}_" + fin + r");Password=(Mvp![a-f0-9]{48});",
                                   credenciales[fin]).groups()
        comandos.extend([
            "USE master;",
            f"IF SUSER_ID({literal(nombre)}) IS NULL CREATE LOGIN [{nombre}] WITH PASSWORD={literal(clave)},CHECK_POLICY=ON;",
            f"USE [{DEMO}];",
            f"IF USER_ID({literal(nombre)}) IS NULL CREATE USER [{nombre}] FOR LOGIN [{nombre}];",
        ])
        for vista in ("vw_kpi_atenciones", "vw_kpi_inventario_actual"):
            comandos.append(f"GRANT SELECT ON dbo.{vista} TO [{nombre}];")
        if fin == "app":
            comandos.append(f"GRANT SELECT ON dbo.vw_modelo_puestos TO [{nombre}];")
            for tabla in TABLAS.keys() - {"REGISTRO"}:
                comandos.append(f"GRANT SELECT ON dbo.{tabla} TO [{nombre}];")
            for procedimiento in ("IniciarAtencion", "FinalizarAtencion", "GuardarAccesoPuesto"):
                comandos.append(f"GRANT EXECUTE ON dbo.{procedimiento} TO [{nombre}];")
    sql(contenedor, DEMO, "\n".join([*comandos, "COMMIT;"]))


def verificar_sql(contenedor):
    consulta = """
        SELECT DB_NAME() base_datos,
          (SELECT COUNT(*) FROM sys.tables WHERE is_ms_shipped=0) tablas,
          (SELECT COUNT(*) FROM sys.foreign_keys WHERE is_disabled=0 AND is_not_trusted=0
              AND OBJECT_SCHEMA_NAME(parent_object_id)='dbo' AND OBJECT_SCHEMA_NAME(referenced_object_id)='dbo') fk,
          (SELECT COUNT(*) FROM sys.schemas WHERE schema_id BETWEEN 5 AND 16383) esquemas_extra,
          (SELECT COUNT(*) FROM dbo.MATERIAL) materiales,
          (SELECT COUNT(*) FROM dbo.UBICACION) ubicaciones,
          (SELECT SUM(CONVERT(bigint,stock)) FROM dbo.MATERIAL) piezas,
          (SELECT COUNT(*) FROM dbo.REGISTRO) atenciones,
          (SELECT fecha_stock FROM dbo.vw_modelo_puestos) fecha_stock
        FOR JSON PATH, WITHOUT_ARRAY_WRAPPER;
    """
    resultado = json.loads(sql(contenedor, DEMO, consulta))
    if (resultado["tablas"], resultado["fk"], resultado["esquemas_extra"]) != (8, 8, 0):
        raise ValueError("La base candidata difiere del modelo físico final.")
    return resultado


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("accion", choices=("preparar", "verificar"))
    parser.add_argument("--contenedor", default="motherson-mvp-sql-1")
    args = parser.parse_args()
    if args.accion == "preparar":
        datos = exportar(args.contenedor, PAQUETE)
        instalar(args.contenedor, datos)
        configurar_accesos(args.contenedor)
    else:
        datos = verificar_paquete(PAQUETE)
    resultado = verificar_sql(args.contenedor)
    if resultado["fecha_stock"] != datos["corte"]["fecha_stock"]:
        raise ValueError("La fecha de corte SQL no coincide con el manifiesto.")
    print(json.dumps(resultado | {"historico": datos["corte"]}, ensure_ascii=False))


if __name__ == "__main__":
    main()
