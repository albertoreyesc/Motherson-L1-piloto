"""Pruebas v2 en una base NUEVA por ejecución, dentro del contenedor SQL local aislado.
No conecta a Azure. No borra ni reabre bases existentes. Secretos solo en .local.
Uso: python3 portal/scripts/probar_administracion_local.py [--servir]
"""
import json, os, secrets, subprocess, sys, uuid
from pathlib import Path
root = Path(__file__).resolve().parents[2]
private = root / 'portal/.local/v2'
private.mkdir(parents=True, exist_ok=True, mode=0o700)
container = 'motherson-admin-v2-local'
instance = json.loads(subprocess.check_output(['docker','inspect',container]))[0]
ports = instance['NetworkSettings']['Ports']['1433/tcp']
assert len(ports) == 1 and ports[0]['HostIp'] == '127.0.0.1', 'Solo instancia aislada en loopback'
variables = dict(v.split('=',1) for v in instance['Config']['Env'] if '=' in v)
sa = variables['MSSQL_SA_PASSWORD']
app = secrets.token_urlsafe(32) + 'A1!'
password = secrets.token_urlsafe(32) + 'A1!'
database = 'MES_Integracion_Pruebas_' + uuid.uuid4().hex[:12]
query = f'CREATE DATABASE [{database}];\nGO\nALTER DATABASE [{database}] SET CONTAINMENT=PARTIAL;\nGO\n'
# La instancia debe tener habilitada contained database authentication, como Azure SQL.
result = subprocess.run(['docker','exec','-i','-e','SQLCMDPASSWORD='+sa,container,'/opt/mssql-tools18/bin/sqlcmd','-S','localhost','-U','sa','-C','-b'],input=query,text=True,capture_output=True)
if result.returncode:
    raise SystemExit('No se creó la base local. Revisa la instancia aislada y el soporte de usuarios contenidos.')
def connection(user, pwd):
    return f'Server=127.0.0.1,{ports[0]["HostPort"]};Database={database};User ID={user};Password="{pwd}";Encrypt=True;TrustServerCertificate=True'
settings = {'Pilot:Enabled':'true','MVP_SQL_TRANSACCIONAL':'true','MVP_ADMIN_V2':'true','MVP_BASE_COMPARTIDA':database,'MVP_ROOT':str(private/'data'),'MVP_KEYS_PATH':str(private/'keys'),'MVP_IMAGES_PATH':str(private/'images'),'ConnectionStrings:Sql':connection('portal_app',app)}
for filename, value in [('config.json',json.dumps(settings)),('user.password',password)]:
    dest=private/filename
    dest.write_text(value)
    dest.chmod(0o600)
runtime = root/'portal/.local/dotnet/dotnet'
assert runtime.is_file(), 'Se requiere el runtime local .NET 10; ver documentación del proyecto'
subprocess.run(['dotnet','build',str(root/'portal/pruebas-administracion')],check=True)
env=os.environ.copy()
env.update(MES_V2_ADMIN_SQL=connection('sa',sa),MES_V2_APP_SQL=connection('portal_app',app),MES_V2_PASSWORD=password)
completed=subprocess.run([str(runtime),str(root/'portal/pruebas-administracion/bin/Debug/net10.0/Administracion.Pruebas.dll'),str(root)],env=env)
if completed.returncode:raise SystemExit(completed.returncode)
if '--servir' in sys.argv:
    env.update({k.replace(':','__'):v for k,v in settings.items()})
    env.update(ASPNETCORE_ENVIRONMENT='Development',ASPNETCORE_URLS='http://127.0.0.1:5099')
    raise SystemExit(subprocess.call([str(runtime),str(root/'portal/backend/Motherson.Api/bin/Debug/net10.0/Motherson.Api.dll')],cwd=root/'portal/backend/Motherson.Api',env=env))
