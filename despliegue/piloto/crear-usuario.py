"""Aprovisiona una persona; entrada secreta por terminal, no en argumentos ni registros."""
import getpass,json,os,subprocess
from pathlib import Path
root=Path(__file__).resolve().parents[2]
local_runtime=root/'portal/.local/dotnet/dotnet'
dotnet=str(local_runtime) if local_runtime.is_file() else 'dotnet'
user=input('Usuario personal: ').strip()
name=input('Nombre: ').strip()
role=input('Rol (ADMIN, PUESTO, LECTOR): ').strip()
puesto=int(input('ID de puesto: ')) if role=='PUESTO' else None
password=getpass.getpass('Contraseña (mínimo 12 caracteres): ')
if password!=getpass.getpass('Repite contraseña: '):raise SystemExit('Las contraseñas no coinciden.')
# ConnectionStrings__Sql debe ser una sesión administrativa temporal del servidor NUEVO.
r=subprocess.run([dotnet,str(root/'portal/backend/Motherson.Api/bin/Release/net10.0/Motherson.Api.dll'),'--piloto-usuario'],input=json.dumps(dict(usuario=user,nombre=name,rol=role,puesto=puesto,password=password)),text=True,capture_output=True)
print('Cuenta creada.' if r.returncode==0 else 'No se creó la cuenta. Revisa la conexión privada, el usuario y el rol; no se muestran credenciales.')
raise SystemExit(r.returncode)
