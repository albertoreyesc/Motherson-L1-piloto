"""Después de aprobación y creación de recursos, instala SOLO el nuevo SQL piloto y carga simulados."""
from pathlib import Path
import json,os,subprocess,secrets
root=Path(__file__).resolve().parents[2];folder=root/'portal/.local/piloto-despliegue'
p={k:v['value'] for k,v in json.loads((folder/'parametros.json').read_text())['parameters'].items()}
server='motherson-piloto-'+p['suffix']+'.database.windows.net'
env=os.environ.copy();env.update(Pilot__Enabled='true',MVP_AZURE_SERVER=server,MVP_BASE_COMPARTIDA='Motherson_L1')
env['ConnectionStrings__Sql']='Server=tcp:'+server+',1433;Database=Motherson_L1;User Id=piloto_instalador;Password='+p['sqlAdminPassword']+';Encrypt=True;TrustServerCertificate=False;Connect Timeout=90'
dll=root/'portal/backend/Motherson.Api/bin/Release/net10.0/Motherson.Api.dll'
subprocess.run(['dotnet',str(dll),'--piloto-inicializar',str(root)],env=env,check=True)
subprocess.run(['dotnet',str(dll),'--piloto-permisos',str(root)],input=json.dumps({'app':p['sqlAppPassword'],'reader':'Aa1!'+secrets.token_hex(24)}),text=True,env=env,check=True)
print('Instalación finalizada. Crea las cuentas personales con crear-usuario.py usando configuración administrativa temporal del mismo servidor. No uses estas credenciales administrativas en la Web App.')
