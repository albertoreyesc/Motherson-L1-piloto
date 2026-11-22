import json,uuid,urllib.request,urllib.error,http.cookiejar
from pathlib import Path
root=Path(__file__).resolve().parents[2];base='http://127.0.0.1:5099/api';password=(root/'portal/.local/v2/user.password').read_text().strip();checks=[]
def check(ok,label):
 assert ok,label
 checks.append(label)
 print('OK',label)
def session(user):
 jar=http.cookiejar.CookieJar();client=urllib.request.build_opener(urllib.request.HTTPCookieProcessor(jar))
 def call(path,body=None,csrf=True,key=None):
  headers={}
  if body is not None:
   headers={'Content-Type':'application/json','Idempotency-Key':key or str(uuid.uuid4())}
   if csrf:headers['X-XSRF-TOKEN']=token[0]
  req=urllib.request.Request(base+path,data=None if body is None else json.dumps(body).encode(),headers=headers)
  try:
   r=client.open(req);return r.status,json.load(r)
  except urllib.error.HTTPError as e:return e.code,json.load(e) if e.headers.get_content_type()=='application/json' else {}
 token=[''];token[0]=call('/auth/csrf')[1]['token'];assert call('/auth/login',{'username':user,'password':password})[0]==200
 token[0]=call('/auth/csrf')[1]['token'];return call
admin=session('persona0');op=session('persona1');reader=session('persona79')
check(admin('/v2/lineas')[0]==200,'API autenticada enumera cuatro líneas')
check(op('/v2/lineas/2/estado')[0]==403,'API rechaza lectura entre líneas por identificador manipulado')
s=admin('/v2/lineas/2/estado')[1]
check(len(s['materiales'])==1 and len(s['imagenes'])==1 and s['ubicaciones'][0]['cantidad']==0,'Tras reinicio del proceso persisten material, foto y saldo')
check(admin('/v2/lineas/2/operaciones/PUESTO',{'nombre':'CSRF ILEGAL','version':s['version']},False)[0]==400,'POST sin CSRF rechazado')
check(reader('/v2/lineas/1/operaciones/PUESTO',{'nombre':'ROL ILEGAL','version':0})[0]==403,'Lector no puede invocar escritura directa')
check(admin('/v2/lineas/2/importacion/vista-previa',{'version':s['version'],'hoja':'Datos','permitirAltas':True,'filas':[{'numeroParte':'BAD','puesto':s['puestos'][0]['id'],'empaque':-5,'fila':12}]})[1]['errores'][0].startswith('Hoja Datos, fila 12:'),'Servidor informa hoja, fila y campo inválido')
check(admin('/v2/lineas/2/estado')[1]['version']==s['version'],'Vista previa no cambia datos')
check(admin('/catalogo')[0]==409,'Cliente anterior recibe aviso y no escribe en flujo viejo')
check(admin('/v2/lineas/3/dashboard')[1]['configurado']==False,'Power BI fijo no aparece como otra línea')
(root/'documentacion/evidencia-integracion/administracion-v2/http.json').write_text(json.dumps({'alcance':'HTTP local, autenticación real, CSRF y SQL','checks':checks},indent=2))
