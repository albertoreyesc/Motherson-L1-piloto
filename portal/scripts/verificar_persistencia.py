"""Comprobación no destructiva. --reiniciar requiere autorización si SQL es compartido."""
import argparse,hashlib,json,subprocess,time
from pathlib import Path
from test_integracion import Sesion
ROOT=Path(__file__).resolve().parents[1]
def capturar():
 a=json.loads((ROOT/'.local/acceso-inicial-Motherson_L1_MVP_Pruebas.json').read_text());s=Sesion('http://127.0.0.1:5088').login(a['usuario'],a['password']);actor=s.call('/auth/me');assert actor[0]==200 and isinstance(actor[1].get('permissions'),list),'Respuesta de sesión inválida';status,rows=s.call('/inventario');assert status==200
 lab=next(r for r in rows if r['numero_parte']=='LAB-MAT-001');assert lab['cantidad']==105
 status,h=s.call('/historial?origen=MVP&parte=LAB-MAT-001');assert status==200 and h['total']==3
 return dict(saldo_lab=lab['cantidad'],comprobantes_lab=sorted(r['id_movimiento'] for r in h['filas']),filas_inventario=len(rows),huella_inventario=hashlib.sha256(json.dumps(rows,sort_keys=True,separators=(',',':')).encode()).hexdigest(),movimientos_historicos=s.call('/historial?origen=HISTORICO_21')[1]['total'],sesion_con_permisos=True)
def main(args):
 target=ROOT/'evidencias/persistencia.json';before=capturar()
 if args.reiniciar:
  if args.contenedor!='motherson-mvp-sql-1':raise SystemExit('Contenedor no acordado para esta instalación')
  subprocess.run(['docker','restart',args.contenedor],check=True,capture_output=True)
  for _ in range(25):
   try:
    if Sesion('http://127.0.0.1:5088').call('/health')[0]==200:break
   except Exception:pass
   time.sleep(2)
  after=capturar();assert before==after,'Persistencia distinta después del reinicio';result=dict(tipo='REINICIO_CONTENEDOR',contenedor=args.contenedor,antes=before,despues=after,resultado='APROBADO')
 else:
  target=ROOT/'evidencias/estado-actual.json'
  result=dict(tipo='COMPROBACION_ESTADO_ACTUAL',estado_actual=before,nota='No reinicia SQL ni sustituye la evidencia del reinicio autorizado.')
 target.write_text(json.dumps(result,ensure_ascii=False,indent=2));print(json.dumps(result,ensure_ascii=False))
if __name__=='__main__':
 p=argparse.ArgumentParser();p.add_argument('--reiniciar',action='store_true');p.add_argument('--contenedor',default='motherson-mvp-sql-1');main(p.parse_args())
