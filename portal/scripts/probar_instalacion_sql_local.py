"""Instala SOLO en una base nueva MES_Integracion_Pruebas_* del contenedor local existente.
No acepta servidor remoto. No modifica bases previas ni imprime credenciales.
"""
import json,re,subprocess,uuid
from pathlib import Path
ROOT=Path(__file__).resolve().parents[1]
CONTAINER='motherson-kpis-20261106-sql-1'
db='MES_Integracion_Pruebas_'+uuid.uuid4().hex[:10]
def sql(text,database='master'):
    r=subprocess.run(['docker','exec','-i',CONTAINER,'bash','-c',
      'SQLCMDPASSWORD="$MSSQL_SA_PASSWORD" exec /opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -C -b -I -w 65535 -y 0 -d "$1"','sql-test',database],input=text,text=True,capture_output=True)
    if r.returncode: raise RuntimeError(r.stdout+r.stderr)
    return r.stdout
sql(f'CREATE DATABASE [{db}]; ALTER DATABASE [{db}] SET RECOVERY SIMPLE;')
folder=ROOT/'db/azure'
script=(folder/'instalar.sql').read_text()
script=script.replace("DB_NAME() <> N'Motherson_L1'",f"DB_NAME() <> N'{db}'")
script='\n'.join((folder/line[3:]).read_text() if line.startswith(':r ') else line for line in script.splitlines() if not line.startswith(':On Error'))
print(sql(script,db).strip())
local=ROOT/'.local';local.mkdir(exist_ok=True)
(local/'ultima-prueba-sql.json').write_text(json.dumps({'container':CONTAINER,'database':db}))
print('Base de pruebas creada:',db)
