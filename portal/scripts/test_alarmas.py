"""Pruebas de lectura y movimientos aislados del módulo de alarmas."""
import argparse,json,uuid
from pathlib import Path
from test_integracion import Sesion
ROOT=Path(__file__).resolve().parents[1]
def login(db,puerto,usuario=None):
 a=json.loads((ROOT/'.local'/('acceso-inicial-Motherson_L1_MVP_'+db+'.json')).read_text())
 return Sesion('http://127.0.0.1:'+str(puerto)).login(usuario or a['usuario'],a['password'])
def estado(s):
 code,d=s.call('/inventario?alarmas=true');assert code==200,(code,d)
 assert d['politica']=='COBERTURA_60_30_15_V1' and d['simulado'] is True
 assert len(d['filas'])==len(d['alarmas'])
 assert len(d['minimos'])==len({r['numero_parte'] for r in d['filas']})
 for r in d['filas']:
  a=next(a for a in d['alarmas'] if a['id_ubicacion']==r['id_ubicacion']);v=a['resultado']
  if r['cantidad']==0:assert v['estado']=='AGOTADO'
  elif r['cantidad'] is None:assert v['estado']=='SIN_DATOS' and v['causa']=='SALDO_DESCONOCIDO'
  elif d['calendario']['motivo']:assert v['estado']=='SIN_DATOS' and v['minutos'] is None
 assert not d['ventana']['completa'] and d['calendario']['motivo']=='FUERA_CALENDARIO_DOCUMENTADO'
 return d

def main(movimientos):
 dev=login('Desarrollo',5087);prueba=login('Pruebas',5088);resultados=[]
 if not movimientos:
  for s in [dev,prueba]:
   old=s.call('/inventario')[1];count=s.call('/historial?origen=MVP')[1]['total']
   for _ in range(4):
    d=estado(s);assert d['filas']==old
   assert s.call('/historial?origen=MVP')[1]['total']==count
   resultados.append({'servidor':s.url,'lecturas_sin_nuevos_movimientos':True,'ubicaciones':len(d['filas']),'corte':d['corte'],'motivo':d['calendario']['motivo']})
  consulta=login('Pruebas',5088,'consulta.prueba');assert consulta.call('/inventario?alarmas=true')[0]==200
  assert Sesion('http://127.0.0.1:5087').call('/inventario?alarmas=true')[0]==401
  resultados.append({'permiso_consulta':True,'sin_sesion':401});name='pruebas-alarmas-api.json'
 else:
  d=estado(prueba);r=next(r for r in d['filas'] if r['numero_parte']=='LAB-CONC-SAL');inicial=r['cantidad'];version=r['version_concurrencia'];n=prueba.call('/historial?origen=MVP')[1]['total']
  assert r['cajas_disponibles']>=1 and r['cantidad_por_caja']==24
  code,recibo=prueba.call('/central/entries',{'ubicacion':r['id_ubicacion'],'cajas':1,'version':version},str(uuid.uuid4()));assert code==200,(code,recibo)
  nuevo=estado(prueba);actual=next(x for x in nuevo['filas'] if x['id_ubicacion']==r['id_ubicacion']);assert actual['cantidad']==inicial+24 and actual['version_concurrencia']==version+1
  code,recibo=prueba.call('/movimientos/salidas',{'ubicacion':r['id_ubicacion'],'piezas':24,'version':actual['version_concurrencia']},str(uuid.uuid4()));assert code==200,(code,recibo)
  fin=estado(prueba);actual=next(x for x in fin['filas'] if x['id_ubicacion']==r['id_ubicacion']);assert actual['cantidad']==inicial and actual['version_concurrencia']==version+2
  assert prueba.call('/historial?origen=MVP')[1]['total']==n+2
  resultados.append({'base':'Pruebas','material':'LAB-CONC-SAL','saldo_inicial':inicial,'tras_entrada':inicial+24,'tras_salida':actual['cantidad'],'comprobantes_nuevos':2,'actualizacion_estado':True});name='pruebas-alarmas-movimientos.json'
 (ROOT/'evidencias'/name).write_text(json.dumps(resultados,ensure_ascii=False,indent=2));print(json.dumps(resultados,ensure_ascii=False))
if __name__=='__main__':
 p=argparse.ArgumentParser();p.add_argument('--movimientos',action='store_true');main(p.parse_args().movimientos)
