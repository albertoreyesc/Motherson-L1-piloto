"""Prepara, aplica y revierte el modelo por puesto en la base de Pruebas."""
import argparse
from datetime import datetime, timezone
import json
import os
from pathlib import Path

from catalogo_sql import BASES, sql as sql_docker


ROOT = Path(__file__).resolve().parents[1]
PRUEBAS = BASES[1]
LOCAL = ROOT / ".local"
ADMIN = LOCAL / f"admin-puestos-{PRUEBAS}.json"
CONTENEDOR = "motherson-mvp-sql-1"


def sql(base, consulta):
    return sql_docker(base, consulta, CONTENEDOR)


def leer(base, consulta):
    return json.loads(sql(base, consulta + " FOR JSON PATH, INCLUDE_NULL_VALUES;") or "[]")


def contar(base):
    modelo_nuevo = sql(base, "SELECT CASE WHEN OBJECT_ID('reporting.vw_modelo_puestos') IS NULL THEN 0 ELSE 1 END;") == "1"
    tabla = "archivo.REGISTRO_MVP" if modelo_nuevo else "dbo.REGISTRO"
    return leer(base, f"""
        SELECT (SELECT COUNT(*) FROM sys.tables WHERE schema_id=SCHEMA_ID('dbo')) tablas,
               (SELECT COUNT(*) FROM dbo.MATERIAL) materiales,
               (SELECT COUNT(*) FROM dbo.UBICACION) ubicaciones,
               (SELECT COUNT(*) FROM {tabla}) movimientos_previos,
               (SELECT COUNT(*) FROM archivo.EVENTO) eventos_maestro,
               (SELECT CHECKSUM_AGG(BINARY_CHECKSUM(*)) FROM archivo.EVENTO) huella_maestro,
               (SELECT CHECKSUM_AGG(BINARY_CHECKSUM(*)) FROM {tabla}) huella_movimientos,
               (SELECT COUNT(*) FROM reporting.vw_kpi_movimientos) movimientos_consultables,
               (SELECT COUNT(*) FROM (SELECT numero_parte FROM dbo.UBICACION GROUP BY numero_parte HAVING COUNT(*)>1) m) materiales_varias_ubicaciones
    """)[0] | {"modelo_puestos": modelo_nuevo}


def script(nombre):
    return (ROOT / "db" / nombre).read_text(encoding="utf-8")


def literal(valor):
    return "'" + valor.replace("'", "''") + "'"


def preparar_admin():
    datos = leer(PRUEBAS, """SELECT nombre_usuario usuario,hash_contrasena hash FROM dbo.USUARIOS
                            WHERE nombre_usuario=N'admin.local' AND esta_activo=1 AND perfil='ADMINISTRADOR'""")
    if len(datos) != 1:
        raise ValueError("Hace falta un acceso administrativo vigente para Pruebas.")
    LOCAL.mkdir(exist_ok=True)
    temporal = ADMIN.with_suffix(".tmp")
    fd = os.open(temporal, os.O_WRONLY | os.O_CREAT | os.O_EXCL, 0o600)
    with os.fdopen(fd, "w", encoding="utf-8") as f:
        json.dump(datos[0], f)
    os.replace(temporal, ADMIN)


def respaldo():
    fecha = datetime.now(timezone.utc).strftime("%Y%m%dT%H%M%S%fZ")
    ruta = f"/var/opt/mssql/data/mvp_puestos_pruebas_{fecha}.bak"
    sql(PRUEBAS, f"""
        BACKUP DATABASE [{PRUEBAS}] TO DISK={literal(ruta)} WITH COPY_ONLY, COMPRESSION, CHECKSUM;
        RESTORE VERIFYONLY FROM DISK={literal(ruta)} WITH CHECKSUM;
    """)
    return ruta


def carga(asignaciones):
    if not asignaciones:
        return ""
    filas = ",\n".join(f"({literal(p)},{literal(c)})" for p, c in asignaciones.items())
    return f"""
        DECLARE @mapa TABLE(parte varchar(100) COLLATE Latin1_General_100_BIN2 PRIMARY KEY,
                           codigo varchar(10) COLLATE Latin1_General_100_BIN2 NOT NULL);
        INSERT @mapa VALUES {filas};
        IF EXISTS (SELECT 1 FROM @mapa x LEFT JOIN dbo.MATERIAL m ON x.parte=m.numero_parte
                   WHERE m.numero_parte IS NULL)
            THROW 51000, 'El catálogo cambió desde el cruce.', 1;
        IF EXISTS (SELECT 1 FROM @mapa x JOIN dbo.MATERIAL m ON x.parte=m.numero_parte
                   JOIN dbo.COLOR c ON c.id_color=m.id_color WHERE c.nombre<>x.codigo)
            THROW 51000, 'Una asignación de variante existente difiere del Excel.', 1;
        INSERT dbo.COLOR(nombre)
        SELECT DISTINCT x.codigo FROM @mapa x WHERE NOT EXISTS (SELECT 1 FROM dbo.COLOR c WHERE c.nombre=x.codigo);
        UPDATE m SET id_color=c.id_color FROM dbo.MATERIAL m JOIN @mapa x ON x.parte=m.numero_parte
        JOIN dbo.COLOR c ON c.nombre=x.codigo WHERE m.id_color IS NULL;
    """


def ejecutar_sql(cuerpo):
    sql(PRUEBAS, """
        SET XACT_ABORT ON;
        BEGIN TRANSACTION;
        DECLARE @r int;
        EXEC @r=sys.sp_getapplock @Resource='MVP_INVENTARIO',@LockMode='Exclusive',
                                  @LockOwner='Transaction',@LockTimeout=10000;
        IF @r<0 THROW 51009,'Inventario ocupado.',1;
        GO
    """ + cuerpo + "\nGO\nCOMMIT;\n")


def main():
    global CONTENEDOR
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("accion", choices=("auditar", "aplicar", "turnos", "revertir"))
    parser.add_argument("--excel", type=Path)
    parser.add_argument("--informe", type=Path)
    parser.add_argument("--contenedor", default=CONTENEDOR)
    parser.add_argument("--solo-pruebas", action="store_true")
    args = parser.parse_args()
    CONTENEDOR = args.contenedor
    if args.accion in ("auditar", "aplicar") and not args.excel:
        parser.error("--excel es obligatorio para auditar y aplicar.")
    antes = contar(PRUEBAS)
    informe = {"accion": args.accion, "pruebas_antes": antes}
    if args.excel:
        from variantes_puestos import cruzar

        informe["cruce"] = {
            base: cruzar(args.excel, leer(base, "SELECT numero_parte,id_puesto FROM dbo.MATERIAL"),
                         leer(base, "SELECT id_puesto,nombre FROM dbo.PUESTO_LOGISTICO"))
            for base in ((PRUEBAS,) if args.solo_pruebas else BASES)
        }
        if not args.solo_pruebas:
            informe["desarrollo"] = contar(BASES[0])
    if args.accion == "aplicar":
        mapa = informe["cruce"][PRUEBAS]["asignaciones"]
        if not mapa:
            raise ValueError("El cruce no produjo asignaciones para Pruebas.")
        cambio = carga(mapa)
        if not antes["modelo_puestos"]:
            preparar_admin()
            informe["respaldo_verificado"] = respaldo()
            # Mantiene el contrato público de movimientos con la atribución histórica.
            historico = (script("002_vistas.sql").split("\nGO", 1)[0]
                         .replace("dbo.REGISTRO r", "archivo.REGISTRO_MVP r")
                         .replace("dbo.USUARIOS usr", "archivo.CUENTA_MVP usr"))
            permisos = """
                GRANT EXECUTE ON dbo.IniciarAtencion TO mvp_pruebas_app;
                GRANT EXECUTE ON dbo.FinalizarAtencion TO mvp_pruebas_app;
                GRANT EXECUTE ON dbo.GuardarAccesoPuesto TO mvp_pruebas_app;
                DENY SELECT,INSERT,UPDATE,DELETE ON SCHEMA::respaldo_puestos TO mvp_pruebas_app,mvp_pruebas_lectura;
                GO
            """
            cambio = script("007_puestos.sql") + historico + "\nGO\n" + permisos + cambio
        ejecutar_sql(cambio + "\nGO\n" + script("009_turno_registro.sql"))
        informe["catalogo"] = leer(PRUEBAS, """SELECT m.numero_parte,c.nombre variante_revision FROM dbo.MATERIAL m
                                             JOIN dbo.COLOR c ON c.id_color=m.id_color ORDER BY m.numero_parte""")
    elif args.accion == "turnos":
        if not antes["modelo_puestos"]:
            raise ValueError("Pruebas debe usar el modelo por puesto.")
        if sql(PRUEBAS, "SELECT COL_LENGTH('dbo.REGISTRO','id_turno');") == "NULL":
            informe["respaldo_verificado"] = respaldo()
        ejecutar_sql(script("009_turno_registro.sql"))
        informe["fk_turno"] = sql(PRUEBAS, """
            SELECT COUNT(*) FROM sys.foreign_keys
            WHERE parent_object_id=OBJECT_ID('dbo.REGISTRO') AND name='FK_REGISTRO_TURNO'
              AND is_disabled=0 AND is_not_trusted=0;
        """) == "1"
    elif args.accion == "revertir":
        if not antes["modelo_puestos"]:
            raise ValueError("Pruebas ya usa el modelo previo.")
        ejecutar_sql(script("008_revertir_puestos.sql") + script("002_vistas.sql") + "\nGO\n"
                     + script("003_movimientos.sql") + "\nGO\n" + script("006_prueba_fallo.sql")
                     + "\nGO\nGRANT EXECUTE ON dbo.RegistrarMovimiento TO mvp_pruebas_app;\n")
    informe["pruebas_despues"] = contar(PRUEBAS)
    if args.informe:
        args.informe.parent.mkdir(parents=True, exist_ok=True)
        args.informe.write_text(json.dumps(informe, indent=2, ensure_ascii=False) + "\n", encoding="utf-8")
    print(json.dumps({k: v for k, v in informe.items() if k not in ("cruce", "catalogo")}, ensure_ascii=False))
    if "cruce" in informe:
        for base, cruce in informe["cruce"].items():
            print(base, cruce["totales_filas"], "asignaciones", len(cruce["asignaciones"]))


if __name__ == "__main__":
    main()
