"""Exporta o restaura el modelo por puesto en un contenedor SQL Server Linux."""
import argparse
import hashlib
import json
import os
import secrets
import subprocess
import uuid
from pathlib import Path

BASE = "Motherson_L1_MVP_Pruebas"
LOCAL = Path(__file__).resolve().parents[1] / ".local"
SQLCMD = ('SQLCMDPASSWORD="$MSSQL_SA_PASSWORD" /opt/mssql-tools18/bin/sqlcmd '
          '-S localhost -U sa -C -b -I -y 0 -w 65535 -t 600')


def docker(*args, entrada=None):
    result = subprocess.run(["docker", *args], input=entrada, capture_output=True,
                            encoding="utf-8", errors="replace")
    if result.returncode:
        raise RuntimeError("Falló Docker o SQL Server. Revisa el contenedor, la versión de SQL y el espacio disponible.")
    return result.stdout.strip()


def literal(value):
    return "N'" + value.replace("'", "''") + "'"


def sha(path):
    with path.open("rb") as source:
        return hashlib.file_digest(source, "sha256").hexdigest()


def guardar_privado(path, value):
    fd = os.open(path, os.O_WRONLY | os.O_CREAT | os.O_EXCL, 0o600)
    with os.fdopen(fd, "w", encoding="utf-8") as stream:
        json.dump(value, stream, indent=2)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("accion", choices=("exportar", "comprobar", "restaurar"))
    parser.add_argument("--bak", type=Path, required=True)
    parser.add_argument("--contenedor", required=True)
    parser.add_argument("--demo", action="store_true", help="Usa la base de ocho tablas en dbo.")
    parser.add_argument("--admin", type=Path, help="Configuración privada de admin.local para restaurar Demo.")
    args = parser.parse_args()
    base = BASE
    modelo = "puestos"
    marcador = "reporting.vw_modelo_puestos"
    if args.demo:
        from preparar_demo import DEMO, PAQUETE, configurar_accesos, verificar_paquete, verificar_sql

        base = DEMO
        modelo = "demo-dbo"
        marcador = "dbo.vw_modelo_puestos"
    bak = args.bak.resolve()
    manifest = bak.with_suffix(".json")
    cont = args.contenedor

    def sql(query, base="master"):
        return docker("exec", "-i", cont, "sh", "-c", SQLCMD + " -d " + base,
                      entrada="SET NOCOUNT ON;\n" + query).replace("\n", "")

    info = json.loads(docker("inspect", cont))[0]
    ports = info["NetworkSettings"]["Ports"].get("1433/tcp") or []
    if not info["State"]["Running"] or not ports:
        raise ValueError("Inicia el contenedor SQL y publica su puerto 1433/tcp.")
    port = int(ports[0]["HostPort"])
    token = uuid.uuid4().hex
    remote = "/var/opt/mssql/data/mvp_puestos_" + token + ".bak"
    disk = " FROM DISK=" + literal(remote)
    exists = sql(f"SELECT COUNT(*) FROM sys.databases WHERE name={literal(base)}") == "1"

    if args.accion == "exportar":
        if args.demo:
            verificar_paquete(PAQUETE)
            verificar_sql(cont)
        if not exists or sql(f"SELECT OBJECT_ID('{marcador}','V')", base) == "NULL":
            raise ValueError("La base seleccionada no tiene aplicado el modelo por puesto.")
        if bak.exists() or manifest.exists():
            raise ValueError("El respaldo o su manifiesto ya existe.")
        bak.parent.mkdir(parents=True, exist_ok=True)
        files = json.loads(sql("SELECT name,tipo=type_desc FROM sys.database_files ORDER BY file_id FOR JSON PATH", base))
        sql(f"BACKUP DATABASE [{base}] TO DISK={literal(remote)} WITH COPY_ONLY,COMPRESSION,CHECKSUM;")
        try:
            sql("RESTORE VERIFYONLY" + disk + " WITH CHECKSUM;")
            docker("cp", cont + ":" + remote, str(bak))
            if os.name != "nt":
                bak.chmod(0o600)
            manifest.write_text(json.dumps(dict(base=base, modelo=modelo, archivos=files, sha256=sha(bak), **(
                {"sha256_manifiesto_historico": sha(PAQUETE / "manifiesto.json")} if args.demo else {})), indent=2),
                                encoding="utf-8")
        finally:
            docker("exec", "-u", "0", cont, "rm", "-f", remote)
        print("Respaldo y manifiesto verificados.")
        return

    metadata = json.loads(manifest.read_text(encoding="utf-8"))
    if metadata["base"] != base or metadata["modelo"] != modelo or sha(bak) != metadata["sha256"]:
        raise ValueError("El respaldo no coincide con el manifiesto del modelo seleccionado.")
    if args.demo:
        datos = verificar_paquete(PAQUETE)
        if metadata["sha256_manifiesto_historico"] != sha(PAQUETE / "manifiesto.json"):
            raise ValueError("El respaldo de Demo pertenece a otro paquete histórico.")
    if args.accion == "comprobar":
        print(json.dumps(dict(sha256="correcto", sql_accesible=True, base_destino_existe=exists, puerto=port)))
        return
    if exists:
        raise ValueError("Ya existe la base destino. Usa una instancia SQL independiente para esta copia.")
    if args.demo:
        if not args.admin or not args.admin.is_file():
            raise ValueError("Para restaurar Demo indica --admin con el archivo privado de admin.local.")
        acceso = json.loads(args.admin.read_text(encoding="utf-8"))
        if acceso.get("usuario") != "admin.local" or not acceso.get("hash"):
            raise ValueError("Archivo de acceso administrativo inválido.")
    connections = LOCAL / f"conexiones-{base}.json"
    admin = LOCAL / f"admin-puestos-{base}.json"
    if connections.exists() or admin.exists():
        raise ValueError("Ya hay archivos de acceso local para la base destino. Usa otra copia del portal.")

    moves = []
    for index, file in enumerate(metadata["archivos"]):
        extension = ".ldf" if file["tipo"] == "LOG" else ".mdf" if index == 0 else ".ndf"
        destination = f"/var/opt/mssql/data/mvp_puestos_{token}_{index}{extension}"
        moves.append(f"MOVE {literal(file['name'])} TO {literal(destination)}")
    docker("cp", str(bak), cont + ":" + remote)
    try:
        docker("exec", "-u", "0", cont, "chmod", "644", remote)
        sql("RESTORE VERIFYONLY" + disk + " WITH CHECKSUM;")
        sql(f"RESTORE DATABASE [{base}]" + disk + " WITH " + ",".join(moves) + ",CHECKSUM,RECOVERY;")
        sql(f"DBCC CHECKDB([{base}]) WITH NO_INFOMSGS;")
    finally:
        docker("exec", "-u", "0", cont, "rm", "-f", remote)

    if sql(f"SELECT OBJECT_ID('{marcador}','V')", base) == "NULL":
        raise ValueError("El respaldo restaurado no contiene el modelo por puesto.")
    if args.demo:
        resultado = verificar_sql(cont)
        if resultado["fecha_stock"] != datos["corte"]["fecha_stock"]:
            raise ValueError("El corte restaurado difiere del histórico.")
        # Los usuarios restaurados conservan el SID del servidor de origen.
        # Quita esos usuarios huérfanos antes de crear los accesos de la instancia local.
        sql("""
            DECLARE @q nvarchar(max)=N'';
            SELECT @q+=N'DROP USER '+QUOTENAME(name)+N';'
            FROM sys.database_principals
            WHERE type='S' AND name LIKE 'mvp[_]demo[_]%';
            EXEC sys.sp_executesql @q;
        """, base)
        configurar_accesos(cont, args.admin)
        print(json.dumps(resultado, ensure_ascii=False))
        print("Demo restaurada con sus conexiones locales y el histórico verificado.")
        return
    initial = json.loads(sql("""SELECT nombre_usuario usuario,hash_contrasena hash
        FROM respaldo_puestos.USUARIOS WHERE nombre_usuario=N'admin.local'
        AND esta_activo=1 AND perfil='ADMINISTRADOR'
        FOR JSON PATH, WITHOUT_ARRAY_WRAPPER""", BASE))
    credentials = {}
    setup = ["SET XACT_ABORT ON; BEGIN TRANSACTION;"]
    for purpose in ("app", "lectura"):
        principal = "mvp_pruebas_" + purpose
        login = "mvp_puestos_" + token[:12] + "_" + purpose
        password = "Mvp!" + secrets.token_hex(24)
        setup.append(f"USE master; CREATE LOGIN [{login}] WITH PASSWORD={literal(password)},CHECK_POLICY=ON;")
        # Reasigna los usuarios restaurados sin perder sus permisos de base de datos.
        setup.append(f"USE [{BASE}]; ALTER USER [{principal}] WITH LOGIN=[{login}];")
        credentials[purpose] = (f"Server=127.0.0.1,{port};Database={BASE};User Id={login};"
                                f"Password={password};Encrypt=True;TrustServerCertificate=True")
    sql(" ".join(setup + ["COMMIT;"]))
    LOCAL.mkdir(exist_ok=True)
    guardar_privado(admin, initial)
    guardar_privado(connections, credentials)
    print(sql("""SELECT DB_NAME() base_datos,(SELECT COUNT(*) FROM dbo.MATERIAL) materiales,
        (SELECT COUNT(*) FROM archivo.EVENTO) eventos_maestro,(SELECT COUNT(*) FROM dbo.REGISTRO) atenciones
        FOR JSON PATH, WITHOUT_ARRAY_WRAPPER""", BASE))
    print("Conexiones SQL locales creadas. La cuenta web admin.local conserva su contraseña.")


if __name__ == "__main__":
    main()
