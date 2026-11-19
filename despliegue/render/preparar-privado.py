"""Genera únicamente configuración privada local; no publica ni imprime secretos."""
from pathlib import Path
import base64,json,os,secrets,subprocess
root=Path(__file__).resolve().parents[2];folder=root/'portal/.local/render';folder.mkdir(parents=True,exist_ok=True);folder.chmod(0o700)
settings=folder/'configuracion.json'
if settings.exists():raise SystemExit('La configuración privada ya existe. No se rota automáticamente.')
p={k:v['value'] for k,v in json.loads((root/'portal/.local/piloto-despliegue/parametros.json').read_text())['parameters'].items()}
password=secrets.token_urlsafe(40);secret=folder/'certificado.password';secret.write_text(password);secret.chmod(0o600)
key=folder/'certificado.key';cert=folder/'certificado.pem';pfx=folder/'certificado.pfx'
subprocess.run(['openssl','req','-x509','-newkey','rsa:3072','-keyout',str(key),'-out',str(cert),'-days','730','-nodes','-subj','/CN=Motherson-L1-Piloto-Session-Keys'],check=True,capture_output=True)
key.chmod(0o600)
subprocess.run(['openssl','pkcs12','-export','-inkey',str(key),'-in',str(cert),'-out',str(pfx),'-passout','file:'+str(secret)],check=True,capture_output=True);pfx.chmod(0o600)
server='motherson-piloto-'+p['suffix']+'.database.windows.net'
c={'Pilot__Enabled':'true','Pilot__SqlPersistence':'true','Pilot__LoopbackProxy':'true','MVP_SQL_TRANSACCIONAL':'true','MVP_AZURE_SERVER':server,'MVP_BASE_COMPARTIDA':'Motherson_L1','ASPNETCORE_ENVIRONMENT':'Production','MVP_ROOT':'/tmp/motherson-piloto','PowerBI__Mode':'AppOwnsData','PowerBI__AllowTrialTokens':'false','PowerBI__CapacityReady':'false','ConnectionStrings__Sql':'Server=tcp:'+server+',1433;Database=Motherson_L1;User Id=portal_app;Password='+p['sqlAppPassword']+';Encrypt=True;TrustServerCertificate=False;Connect Timeout=90','Pilot__KeyEncryptionCertificate':base64.b64encode(pfx.read_bytes()).decode(),'Pilot__KeyEncryptionPassword':password}
fd=os.open(settings,os.O_WRONLY|os.O_CREAT|os.O_EXCL,0o600)
with os.fdopen(fd,'w') as out:json.dump(c,out,indent=2)
key.unlink()
print('Configuración privada y certificado preparados en portal/.local/render; ninguna credencial en salida.')
