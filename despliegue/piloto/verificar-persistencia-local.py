"""Ejecutar preparar, reiniciar el servidor local 5097, luego verificar. Usa sesión privada del ensayo."""
from pathlib import Path
import base64,hashlib,http.cookiejar,json,sys,urllib.request,urllib.parse
root=Path(__file__).resolve().parents[2];folder=root/'portal/.local/pilot-tests';base='http://127.0.0.1:5097'
jar=http.cookiejar.LWPCookieJar(str(folder/'sesion.cookies'));jar.load(ignore_discard=True,ignore_expires=True);client=urllib.request.build_opener(urllib.request.HTTPCookieProcessor(jar));csrf=json.loads((folder/'csrf-reinicio.json').read_text())['token']
def req(path,data=None,ctype='application/json'):
 with client.open(urllib.request.Request(base+path,data=data,headers={'X-XSRF-TOKEN':csrf,'Content-Type':ctype}),timeout=45) as r:
  return r.read()
if sys.argv[1]=='preparar':
 data=json.loads(req('/api/catalogo'));part=data['materiales'][0]['numero_parte'];path='/api/materiales/imagen?parte='+urllib.parse.quote(part)
 image=base64.b64decode('iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+/a9sAAAAASUVORK5CYII=');req(path,image,'image/png')
 inv=json.loads(req('/api/inventario?alarmas=true'));state={'path':path,'part':part,'sha256':hashlib.sha256(req(path)).hexdigest(),'pieces':sum(r['cantidad'] for r in inv['filas'])}
 (folder/'persistencia.json').write_text(json.dumps(state));print('Estado e imagen de prueba guardados antes del reinicio.')
else:
 state=json.loads((folder/'persistencia.json').read_text());assert json.loads(req('/api/auth/me'))['name']=='persona0';assert hashlib.sha256(req(state['path'])).hexdigest()==state['sha256'];assert sum(r['cantidad'] for r in json.loads(req('/api/inventario?alarmas=true'))['filas'])==state['pieces']==10474
 req('/api/materiales/imagen/eliminar?parte='+urllib.parse.quote(state['part']),b'{}')
 (root/'documentacion/evidencia-integracion/PILOTO_PERSISTENCIA_LOCAL.json').write_text(json.dumps({'estado':'APROBADO','alcance':'Reinicio backend local; no App Service','checks':['Sesion conservada con claves persistentes','Imagen privada conservada','10474 piezas conservadas en SQL']},indent=2))
 print('Sesión, imagen y saldo conservados tras reinicio del backend local; imagen de ensayo retirada.')
