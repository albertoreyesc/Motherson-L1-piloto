"""Después de aprobación: permite SOLO las IP de salida exactas de la nueva Web App en el nuevo servidor SQL."""
import subprocess,json,re,sys,ipaddress
if len(sys.argv)!=3:raise SystemExit('Uso: python3 firewall-sql.py GRUPO SUFIJO')
group,suffix=sys.argv[1:];name='motherson-piloto-'+suffix
if not group.startswith('rg-motherson-piloto-') or not re.fullmatch('[a-z0-9][a-z0-9-]{2,23}',suffix):raise SystemExit('Destino no autorizado para piloto.')
def az(*args):return json.loads(subprocess.check_output(['az',*args,'-o','json'],text=True))
app=az('webapp','show','-g',group,'-n',name)
ips=sorted(set((app.get('possibleOutboundIpAddresses') or app['outboundIpAddresses']).split(',')))
for i,ip in enumerate(ips):
 if not ipaddress.ip_address(ip).is_global:raise SystemExit('IP inesperada; revisar manualmente.')
 az('sql','server','firewall-rule','create','-g',group,'-s',name,'-n','WebAppSalida'+str(i),'--start-ip-address',ip,'--end-ip-address',ip)
print('Reglas aplicadas para '+str(len(ips))+' IP exactas; no se habilitó acceso global de Azure.')
