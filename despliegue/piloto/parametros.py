"""Crea parámetros privados para ARM; nunca muestra contraseñas."""
from pathlib import Path
import getpass,json,secrets,re,os
p=Path(__file__).resolve().parents[2]/'portal/.local/piloto-despliegue'
p.mkdir(parents=True,exist_ok=True);p.chmod(0o700)
suffix=input('Sufijo único (letras minúsculas, números y guiones): ').strip()
if not re.fullmatch('[a-z0-9][a-z0-9-]{2,23}',suffix):raise SystemExit('Sufijo no válido.')
ip=input('IP pública IPv4 exacta del operador: ').strip()
import ipaddress
if not isinstance(ipaddress.ip_address(ip),ipaddress.IPv4Address) or not ipaddress.ip_address(ip).is_global:raise SystemExit('Se requiere una IPv4 pública exacta.')
params={'location':'canadacentral','suffix':suffix,'operatorIp':ip,'sqlAdminPassword':'Aa1!'+secrets.token_hex(24),'sqlAppPassword':'Aa1!'+secrets.token_hex(24)}
f=p/'parametros.json'
fd=os.open(f,os.O_WRONLY|os.O_CREAT|os.O_EXCL,0o600)
with os.fdopen(fd,'w') as out:json.dump({'$schema':'https://schema.management.azure.com/schemas/2019-04-01/deploymentParameters.json#','contentVersion':'1.0.0.0','parameters':{k:{'value':v} for k,v in params.items()}},out,indent=2)
print('Parámetros privados creados en portal/.local/piloto-despliegue/parametros.json. No se crearon recursos.')
