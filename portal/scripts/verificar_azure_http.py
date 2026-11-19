"""Verificación real contra Azure: requiere aprobación de capturas en demostración.
Deja comprobantes de prueba en SQL y restaura saldos/capacidades/mínimos con operaciones compensatorias.
No crea objetos ni usuarios; no borra historial. Usa exclusivamente las claves privadas locales.
"""
import argparse,concurrent.futures,http.cookiejar,json,math,os,signal,subprocess,time,urllib.request,urllib.error,uuid
from pathlib import Path
p=argparse.ArgumentParser();p.add_argument('--capturas-demo',action='store_true',required=True);args=p.parse_args()
root=Path(__file__).resolve().parents[1];base='http://127.0.0.1:5093';passed=[];operations=[]
private=json.loads((root/'.local/accesos-iniciales-azure.json').read_text())
jar=http.cookiejar.CookieJar();client=urllib.request.build_opener(urllib.request.HTTPCookieProcessor(jar));token=None
report=root/'.local/verificacion-azure-http.json'
def save(state):report.write_text(json.dumps({'estado':state,'entorno':'AZURE_SQL_REAL','casos':passed,'total':len(passed),'operaciones':operations},ensure_ascii=False,indent=2))
def check(ok,name):
 if not ok:save('DETENIDO_REVISAR');raise RuntimeError(name)
 passed.append(name);save('EN_CURSO')
def request(path,body=None,key=None,csrf=True):
 headers={'Content-Type':'application/json'}
 if token and csrf:headers['X-XSRF-TOKEN']=token
 if key:headers['Idempotency-Key']=str(key)
 req=urllib.request.Request(base+path,data=json.dumps(body).encode() if body is not None else None,headers=headers)
 try:
  with client.open(req,timeout=60) as response:return response.status,json.load(response)
 except urllib.error.HTTPError as e:return e.code,json.loads(e.read())
def write(path,body,key=None):
 key=key or str(uuid.uuid4());operations.append({'id':key,'ruta':path,'solicitud':body});save('EN_CURSO');return request(path,body,key)
def rows():
 status,data=request('/api/inventario?alarmas=true');check(status==200,'Inventario disponible con SQL real');return data['filas']
def move(row,kind,quantity=1,key=None):return write('/api/continuacion/movimientos',{'ubicacion':row['id_ubicacion'],'tipo':kind,'cantidad':quantity,'version':row['version_concurrencia'],'esConsumo':False},key)
def location(id):return next(r for r in rows() if r['id_ubicacion']==id)
status,data=request('/api/auth/csrf');token=data['token']
status,data=request('/api/auth/login',{'username':'admin.local','password':private['portal']['password']});check(status==200,'Login real de admin.local')
status,data=request('/api/auth/csrf');token=data['token']
initial=rows();check(len(initial)==85 and all(r['es_simulado'] for r in initial),'85 ubicaciones marcadas como simulación')
initial_balances={r['id_ubicacion']:r['cantidad'] for r in initial};row=next(r for r in initial if r['cantidad']>=r['cantidad_por_caja']);id=row['id_ubicacion']
key=str(uuid.uuid4());status,receipt=move(row,'SALIDA',key=key);check(status==200,'Salida HTTP confirmada y registrada en Azure')
status,repeated=move(row,'SALIDA',key=key);check(status==200 and receipt==repeated,'Repetición devuelve comprobante idéntico sin duplicar')
check(location(id)['cantidad']==initial_balances[id]-row['cantidad_por_caja'],'Salida descuenta una caja exacta')
status,_=move(location(id),'ENTRADA');check(status==200 and location(id)['cantidad']==initial_balances[id],'Entrada compensatoria recupera el saldo original')
current=location(id);status,_=move(current,'SALIDA',math.ceil(current['cantidad']/current['cantidad_por_caja'])+1);check(status==409,'Salida insuficiente rechazada sin cambiar saldo')
check(location(id)['cantidad']==initial_balances[id],'Saldo intacto tras salida rechazada')
current=location(id)
with concurrent.futures.ThreadPoolExecutor(2) as pool:
 futures=[pool.submit(move,current,'SALIDA') for _ in range(2)];results=[f.result() for f in futures]
check(sorted(s for s,_ in results)==[200,409],'Dos capturas concurrentes: una confirma y otra rechaza versión obsoleta')
status,_=move(location(id),'ENTRADA');check(status==200 and location(id)['cantidad']==initial_balances[id],'Compensación de concurrencia conserva el saldo original')
current=location(id);status,_=request('/api/continuacion/movimientos',{'ubicacion':id,'tipo':'SALIDA','cantidad':1,'version':current['version_concurrencia']},str(uuid.uuid4()),csrf=False);check(status==400,'Captura sin protección CSRF rechazada')
rack=row['id_estanteria'];capacity=row['cajas_estanteria']
status,_=write('/api/catalogo/capacidad',{'estanteria':rack,'anterior':capacity,'nueva':capacity+1});check(status==200,'Aumento de capacidad confirmado en Azure')
status,_=write('/api/catalogo/capacidad',{'estanteria':rack,'anterior':capacity+1,'nueva':capacity});check(status==200,'Disminución compensatoria de capacidad confirmada')
if capacity>1:
 occupied=sum(math.ceil(r['cantidad']/r['cantidad_por_caja']) for r in initial if r['id_estanteria']==rack)
 if occupied>1:
  status,_=write('/api/catalogo/capacidad',{'estanteria':rack,'anterior':capacity,'nueva':occupied-1});check(status==409,'Capacidad inferior a ocupación rechazada')
materials=list({r['numero_parte']:r for r in initial if r['minimo'] is not None}.values())[:2]
changes=[{'parte':r['numero_parte'],'campo':'minimo','anterior':str(r['minimo']),'nuevo':str(r['minimo']+1)} for r in materials]
bad=[dict(c) for c in changes];bad[1]['anterior']=str(materials[1]['minimo']+99)
status,_=write('/api/catalogo/materiales',{'cambios':bad});check(status==409,'Importación con fila obsoleta rechazada completa')
check(all(next(r for r in rows() if r['numero_parte']==m['numero_parte'])['minimo']==m['minimo'] for m in materials),'Importación rechazada no deja cambios parciales')
status,_=write('/api/catalogo/materiales',{'cambios':changes});check(status==200,'Aplicación completa de dos materiales confirmada')
restore=[{**c,'anterior':c['nuevo'],'nuevo':c['anterior']} for c in changes]
status,_=write('/api/catalogo/materiales',{'cambios':restore});check(status==200,'Restauración de mínimos original registrada')
check({r['id_ubicacion']:r['cantidad'] for r in rows()}==initial_balances,'Todos los saldos finales coinciden con la demostración inicial')
status,hist=request('/api/historial');check(status==200,'Historial consultable desde SQL sin archivo JSONL')
# Reiniciar únicamente el proceso creado para esta instalación local.
process=json.loads((root/'.local/backend-azure-proceso.json').read_text());pid=process['pid']
command=subprocess.check_output(['ps','-p',str(pid),'-o','command='],text=True)
assert 'local.py' in command and '--puerto 5093' in command
os.killpg(pid,signal.SIGTERM);time.sleep(1)
log=open(root/'.local/backend-azure.log','a');child=subprocess.Popen(['python3',str(root/'scripts/local.py'),'--compartida','--puerto','5093'],stdout=log,stderr=log,start_new_session=True)
(root/'.local/backend-azure-proceso.json').write_text(json.dumps({'pid':child.pid,'puerto':5093}))
for i in range(100):
 try:
  if request('/api/health')[0]==200:break
 except (OSError,urllib.error.URLError):pass
 time.sleep(.3)
else:raise RuntimeError('Backend no volvió a iniciar')
check(request('/api/auth/me')[0]==200,'Sesión sobrevive al reinicio del backend')
check({r['id_ubicacion']:r['cantidad'] for r in rows()}==initial_balances,'Existencias SQL sobreviven al reinicio')
check(request('/api/historial')[0]==200,'Historial SQL disponible después de reiniciar')
save('CORRECTO');print(json.dumps({'resultado':'CORRECTO','total':len(passed),'informe':str(report)},ensure_ascii=False))
