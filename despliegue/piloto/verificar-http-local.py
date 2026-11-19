"""Pruebas HTTP SOLO contra el servidor efímero local 5097. Nunca usa Azure."""
import base64,concurrent.futures,http.cookiejar,json,time,urllib.request,urllib.error,urllib.parse
from pathlib import Path
root=Path(__file__).resolve().parents[2];folder=root/'portal/.local/pilot-tests';password=json.loads((folder/'runtime.json').read_text())['password'];base='http://127.0.0.1:5097';checks=[]
def check(ok,name):
 if not ok:raise AssertionError(name)
 checks.append(name);print('OK',name)
class Client:
 def __init__(self):
  self.jar=http.cookiejar.LWPCookieJar(str(folder/'sesion.cookies'));self.http=urllib.request.build_opener(urllib.request.HTTPCookieProcessor(self.jar));self.csrf=None
 def call(self,path,body=None,csrf=True,extra=None):
  h={'Content-Type':'application/json'}
  if self.csrf and csrf:h['X-XSRF-TOKEN']=self.csrf
  if extra:h.update(extra)
  data=body if isinstance(body,bytes) else json.dumps(body).encode() if body is not None else None
  try:
   with self.http.open(urllib.request.Request(base+path,data=data,headers=h),timeout=90) as r:
    raw=r.read();return r.status,json.loads(raw) if 'json' in r.headers.get('Content-Type','') else raw
  except urllib.error.HTTPError as e:
   raw=e.read();return e.code,json.loads(raw) if 'json' in e.headers.get('Content-Type','') else raw
 def login(self,index):
  status,data=self.call('/api/auth/csrf');self.csrf=data['token'];s,d=self.call('/api/auth/login',{'username':'persona'+str(index),'password':password});assert s==200
  status,data=self.call('/api/auth/csrf');self.csrf=data['token'];return self
c=Client();check(c.call('/api/inventario')[0]==401,'Inventario requiere sesión');check(c.call('/api/health')[0]==401,'Estado SQL no expuesto anónimamente');c.login(0)
check(c.call('/api/dashboard/embed-token',{},csrf=False)[0]==400,'CSRF ausente rechazado');check(c.call('/api/dashboard/embed-token',{})[0]==503,'Power BI pendiente no interrumpe el portal')
s,d=c.call('/api/inventario?alarmas=true');check(s==200 and len(d['filas'])==85 and sum(r['cantidad'] for r in d['filas'])==10474,'Inventario piloto concilia con simulados')
row=next(r for r in d['filas'] if r['cantidad']>=r['cantidad_por_caja']);version=row['version_concurrencia'];id=row['id_ubicacion'];import uuid
op=str(uuid.uuid4());body={'ubicacion':id,'tipo':'SALIDA','cantidad':1,'version':version,'esConsumo':False}
a=c.call('/api/continuacion/movimientos',body,extra={'Idempotency-Key':op});b=c.call('/api/continuacion/movimientos',body,extra={'Idempotency-Key':op});check(a[0]==200 and a==b,'Repetición HTTP devuelve comprobante idéntico')
body.update(tipo='ENTRADA',version=version+1);check(c.call('/api/continuacion/movimientos',body,extra={'Idempotency-Key':str(uuid.uuid4())})[0]==200,'Entrada compensatoria confirmada')
# Dos capturas con la misma versión: exactamente una se confirma.
body.update(tipo='SALIDA',version=version+2)
with concurrent.futures.ThreadPoolExecutor(max_workers=2) as pool:
 parallel=list(pool.map(lambda _:c.call('/api/continuacion/movimientos',body,extra={'Idempotency-Key':str(uuid.uuid4())})[0],range(2)))
check(sorted(parallel)==[200,409],'Capturas concurrentes detectan conflicto sin doble descuento')
body.update(tipo='ENTRADA',version=version+3)
check(c.call('/api/continuacion/movimientos',body,extra={'Idempotency-Key':str(uuid.uuid4())})[0]==200,'Saldo restaurado tras conflicto concurrente')
check(c.call('/api/historial')[0]==200,'Historial de movimientos accesible a administración');check(c.call('/api/kpis/opciones')[0]==200,'Filtros de KPIs disponibles')
cat=c.call('/api/catalogo')[1]
material=cat['materiales'][0]
change={'parte':material['numero_parte'],'campo':'descripcion','anterior':material['descripcion'],'nuevo':'Prueba individual piloto'}
check(c.call('/api/catalogo/materiales',{'cambios':[change]},extra={'Idempotency-Key':str(uuid.uuid4())})[0]==200,'Cambio de catálogo confirmado con cuenta personal')
change.update(anterior='Prueba individual piloto',nuevo=material['descripcion'])
check(c.call('/api/catalogo/materiales',{'cambios':[change]},extra={'Idempotency-Key':str(uuid.uuid4())})[0]==200,'Catálogo restaurado mediante ajuste trazable')
rack=cat['estanterias'][0]
check(c.call('/api/catalogo/capacidad',{'estanteria':rack['id_estanteria'],'anterior':rack['cajas_estanteria'],'nueva':rack['cajas_estanteria']+1},extra={'Idempotency-Key':str(uuid.uuid4())})[0]==200,'Capacidad ajustada con cuenta personal')
check(c.call('/api/catalogo/capacidad',{'estanteria':rack['id_estanteria'],'anterior':rack['cajas_estanteria']+1,'nueva':rack['cajas_estanteria']},extra={'Idempotency-Key':str(uuid.uuid4())})[0]==200,'Capacidad restaurada con ajuste trazable')
part=cat['materiales'][0]['numero_parte'];path='/api/materiales/imagen?parte='+urllib.parse.quote(part)
image=base64.b64decode('iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+/a9sAAAAASUVORK5CYII=')
check(c.call(path,image,extra={'Content-Type':'image/png'})[0]==200,'Imagen subida a almacenamiento privado');check(c.call(path)[0]==200,'Imagen consultable con sesión')
check(c.call('/api/materiales/imagen/eliminar?parte='+urllib.parse.quote(part),{})[0]==200,'Imagen de prueba eliminada por administración')
reader=Client().login(79);check(reader.call('/api/historial')[0]==403,'Lector sin historial global');check(reader.call('/api/continuacion/movimientos',body,extra={'Idempotency-Key':str(uuid.uuid4())})[0]==403,'Lector sin escritura');check(reader.call(path,image,extra={'Content-Type':'image/png'})[0]==403,'Lector no sube imágenes')
operator=Client().login(1);check(operator.call('/api/dashboard-equipo2')[0]==403,'Operador sin Dashboard global');check(operator.call('/api/puestos/acceso',{'puesto':1,'usuario':'otro','password':'NoSeAplica123!','revocar':False})[0]==403,'Operador sin administración de accesos')
# 80 sesiones distintas: mide el servidor LOCAL, no atribuir este resultado a F1 o red de planta.
def scenario(i):
 cl=Client().login(i);t=time.perf_counter();s,d=cl.call('/api/inventario?alarmas=true');return s,time.perf_counter()-t
with concurrent.futures.ThreadPoolExecutor(max_workers=20) as pool:sessions=list(pool.map(lambda i:Client().login(i),range(80)))
def request_one(cl):
 t=time.perf_counter();s,d=cl.call('/api/inventario?alarmas=true');return s,time.perf_counter()-t
with concurrent.futures.ThreadPoolExecutor(max_workers=80) as pool:results=list(pool.map(request_one,sessions))
check(all(s==200 for s,t in results),'80 sesiones locales consultan inventario concurrentemente')
lat=sorted(t for s,t in results);metrics={'sesiones':80,'solicitudes':80,'concurrencia_maxima_cliente':80,'p95_segundos':round(lat[75],3),'max_segundos':round(lat[-1],3),'alcance':'LOCAL; no certifica Azure F1 ni dispositivos de planta'}
check(c.call('/api/auth/logout',{})[0]==200 and c.call('/api/auth/me')[0]==401,'Cerrar sesión impide nuevas consultas autenticadas')
s=Client().login(0);s.jar.save(ignore_discard=True,ignore_expires=True)
(folder/'csrf-reinicio.json').write_text(json.dumps({'token':s.csrf}))
(root/'documentacion/evidencia-integracion/PILOTO_HTTP_LOCAL.json').write_text(json.dumps({'estado':'APROBADO','checks':checks,'carga':metrics},indent=2,ensure_ascii=False));print(json.dumps(metrics))
