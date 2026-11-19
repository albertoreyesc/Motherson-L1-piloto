"""Pruebas HTTP reales. Solo autoriza la base de Pruebas y un servidor localhost."""
import argparse,concurrent.futures,http.cookiejar,json,threading,time,urllib.request,urllib.error,uuid
from pathlib import Path
ROOT=Path(__file__).resolve().parents[1]
class Sesion:
 def __init__(self,url):self.url=url;self.cookies=http.cookiejar.CookieJar();self.http=urllib.request.build_opener(urllib.request.HTTPCookieProcessor(self.cookies));self.token=''
 def call(self,path,body=None,key=None):
  h={} if body is None else {'Content-Type':'application/json','X-XSRF-TOKEN':self.token,'Idempotency-Key':key or str(uuid.uuid4())};r=urllib.request.Request(self.url+'/api'+path,data=None if body is None else json.dumps(body).encode(),headers=h)
  try:
   with self.http.open(r,timeout=45) as response:return response.status,json.loads(response.read())
  except urllib.error.HTTPError as e:return e.code,json.loads(e.read() or b'{}')
 def login(self,name,pwd):
  self.token=self.call('/auth/csrf')[1]['token'];s,d=self.call('/auth/login',dict(username=name,password=pwd));assert s==200,(s,d);self.token=self.call('/auth/csrf')[1]['token'];return self

def ejecutar(url):
 acceso=json.loads((ROOT/'.local/acceso-inicial-Motherson_L1_MVP_Pruebas.json').read_text());admin=Sesion(url).login('admin.local',acceso['password']);resultados=[]
 def test(nombre,fn):
  start=time.monotonic()
  try:fn();resultados.append(dict(caso=nombre,resultado='APROBADO',duracion_s=round(time.monotonic()-start,3)))
  except Exception as e:resultados.append(dict(caso=nombre,resultado='FALLO',detalle=str(e)));raise
 def inventario(parte):
  s,rows=admin.call('/inventario');assert s==200;return next(r for r in rows if r['numero_parte']==parte)
 def entrada(row,n,key=None):return admin.call('/central/entries',dict(ubicacion=row['id_ubicacion'],cajas=n,version=row['version_concurrencia']),key)
 def salida(row,n,key=None):return admin.call('/movimientos/salidas',dict(ubicacion=row['id_ubicacion'],piezas=n,version=row['version_concurrencia']),key)
 actor=admin.call('/auth/me');assert actor[0]==200 and actor[1]['permissions'] and actor[1]['name']=='admin.local'
 health=admin.call('/health')[1];assert health['baseDatos']=='Motherson_L1_MVP_Pruebas','No se prueba la base de desarrollo'
 row=inventario('LAB-MAT-001');key=str(uuid.uuid4());before=row['cantidad']
 def ciclo():
  assert before==72,'La prueba ya fue ejecutada; no se reinicializan saldos automáticamente'
  s,r=entrada(row,2,key);assert s==200 and r['saldo_posterior']==120,(s,r)
  s,r2=entrada(row,2,key);assert s==200 and r2['id_registro']==r['id_registro'];assert inventario('LAB-MAT-001')['cantidad']==120
  s,r=salida(inventario('LAB-MAT-001'),15);assert s==200 and r['saldo_posterior']==105
 test('72 + 2×24 − 15 = 105; repetición idempotente',ciclo)
 def distinto():
  assert entrada(row,1,key)[0]==409;assert inventario('LAB-MAT-001')['cantidad']==105
 test('Reutilización de identificador con otro contenido',distinto)
 def negativas():
  for n in [0,-1,1.5,1000001]:assert entrada(inventario('LAB-MAT-001'),n)[0]==400
  assert salida(inventario('LAB-MAT-001'),106)[0]==409
  assert salida(inventario('LAB-PENDIENTE'),1)[0]==409
  assert salida({**row,'id_ubicacion':2147483000},1)[0]==409
  assert inventario('LAB-MAT-001')['cantidad']==105
 test('Cantidades, saldo insuficiente, apertura pendiente y referencia inválida',negativas)
 def concurrencia(parte,fn,esperado):
  r=inventario(parte);barrier=threading.Barrier(2)
  def run(_):barrier.wait();return fn(r)
  with concurrent.futures.ThreadPoolExecutor(max_workers=2) as pool:values=list(pool.map(run,range(2)))
  assert sorted(x[0] for x in values)==[200,409],values
  assert inventario(parte)['cantidad']==esperado
 def fallo_parcial():
  r=inventario('LAB-ROLLBACK');assert entrada(r,1)[0]==409;assert inventario('LAB-ROLLBACK')['cantidad']==72
 test('Fallo de comprobante revierte actualización de saldo',fallo_parcial)
 test('Dos salidas superpuestas no generan saldo negativo',lambda:concurrencia('LAB-CONC-SAL',lambda r:salida(r,50),22))
 test('Dos entradas superpuestas no exceden capacidad',lambda:concurrencia('LAB-CONC-ENT',lambda r:entrada(r,2),120))
 def permisos():
  consulta=Sesion(url).login('consulta.prueba',acceso['password']);entrada_user=Sesion(url).login('entrada.prueba',acceso['password']);r=inventario('LAB-MAT-001');b=dict(ubicacion=r['id_ubicacion'],piezas=1,version=r['version_concurrencia'])
  assert consulta.call('/inventario')[0]==200;assert consulta.call('/movimientos/salidas',b)[0]==403;assert entrada_user.call('/movimientos/salidas',b)[0]==403
  assert Sesion(url).call('/inventario')[0]==401
 test('Permisos comprobados por backend y sesión obligatoria',permisos)
 def historico():
  s,h=admin.call('/historial?origen=HISTORICO_21');assert s==200 and h['total']==230901,(s,h.get('total'))
  assert all(x['id_movimiento'].startswith('H21:') and x['es_simulado'] for x in h['filas'])
  s,h=admin.call('/historial?origen=MVP&parte=LAB-MAT-001');assert s==200;assert {r['tipo_movimiento'] for r in h['filas']}=={'APERTURA','ENTRADA','SALIDA'}
  ids={x['id_movimiento'] for x in h['filas']};assert len(ids)==3
  s,h=admin.call('/historial?ubicacion=MVP:'+str(row['id_ubicacion']));assert s==200 and h['total']==3
  assert admin.call('/historial?desde=fecha-invalida')[0]==400
 test('Historial unificado, IDs sin colisión y filtros de ubicación/fecha',historico)
 def rutas():
  for path in ['/annual/catalog','/central/consumption/status','/central/admin','/central/reservations']:assert admin.call(path)[0]==404
 test('Módulos retirados no tienen rutas activas',rutas)
 def lecturas():
  count=admin.call('/historial?origen=MVP')[1]['total']
  for _ in range(3):assert admin.call('/inventario')[0]==200
  assert admin.call('/historial?origen=MVP')[1]['total']==count
 test('Consultar no genera registros',lecturas)
 (ROOT/'evidencias').mkdir(exist_ok=True);(ROOT/'evidencias/pruebas-http.json').write_text(json.dumps(dict(base=health['baseDatos'],casos=resultados,saldo_final=inventario('LAB-MAT-001')['cantidad']),ensure_ascii=False,indent=2));print(json.dumps(resultados,ensure_ascii=False))
if __name__=='__main__':
 p=argparse.ArgumentParser();p.add_argument('--url',default='http://127.0.0.1:5088');a=p.parse_args();assert a.url.startswith('http://127.0.0.1:');ejecutar(a.url)
