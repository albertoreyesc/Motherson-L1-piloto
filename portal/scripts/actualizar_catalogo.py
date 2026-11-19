"""Actualización limitada de USO, con respaldo previo y reversión selectiva."""
import argparse,hashlib,json,os
from datetime import datetime,timezone
from pathlib import Path
from catalogo_sql import BASES,datos,sql

def literal(s):return 'NULL' if s is None else "N'"+str(s).replace("'","''")+"'"
def ejecutar(base,plan_path,revertir=None):
 plan=json.loads(Path(plan_path).read_text());cambios=plan['cambios'];plan_sha=hashlib.sha256(Path(plan_path).read_bytes()).hexdigest()
 if len({c['numero_parte'] for c in cambios})!=len(cambios):raise ValueError('Parte repetida en el plan')
 for c in cambios:
  if c['uso'] not in ('COMPARTIDO','EXCLUSIVO'):raise ValueError('USO sin categoría confirmada')
  f=c['fuente'];p=Path(f['ruta'])
  if not p.exists() or hashlib.sha256(p.read_bytes()).hexdigest()!=f['sha256']:raise ValueError('Fuente ausente o modificada; no se aplica el plan')
 antes=datos(base);porparte={m['numero_parte']:m for m in antes['materiales']}
 if any(c['numero_parte'] not in porparte for c in cambios):raise ValueError('El plan contiene partes ausentes del catálogo destino')
 if revertir:
  respaldo=json.loads(Path(revertir).read_text())
  if respaldo['base']!=base:raise ValueError('Respaldo de otra base')
  if respaldo['plan_sha256']!=plan_sha:raise ValueError('El respaldo no corresponde a este plan de conciliación')
  guardados={m['numero_parte']:m for m in respaldo['catalogo']['materiales']};updates=[]
  for c in cambios:
   parte=c['numero_parte'];anterior=guardados[parte]['uso'];actual=porparte[parte]['uso']
   if actual==anterior:continue
   if actual!=c['uso']:raise ValueError('Se conserva un valor modificado después de la actualización: '+parte)
   updates.append((parte,actual,anterior))
 else:
  updates=[]
  for c in cambios:
   parte=c['numero_parte'];actual=porparte[parte]['uso']
   if actual==c['uso']:continue
   if actual is not None and str(actual).strip():raise ValueError('No se sobrescribe el valor existente: '+parte)
   updates.append((parte,actual,c['uso']))
 # Respaldo lógico completo de los tres catálogos y referencia al estado protegido.
 carpeta=Path(__file__).resolve().parents[1]/'.local'/'respaldos-catalogo';carpeta.mkdir(parents=True,exist_ok=True)
 ruta=carpeta/(base+'-'+datetime.now(timezone.utc).strftime('%Y%m%dT%H%M%S%fZ')+'.json')
 fd=os.open(ruta,os.O_WRONLY|os.O_CREAT|os.O_EXCL,0o600)
 with os.fdopen(fd,'w') as f:json.dump({'base':base,'plan_sha256':plan_sha,'catalogo':antes},f,ensure_ascii=False,indent=2)
 statements=[]
 for parte,anterior,nuevo in updates:
  condicion='uso IS NULL' if anterior is None else 'uso='+literal(anterior)
  statements.append(f"UPDATE dbo.MATERIAL SET uso={literal(nuevo)} WHERE numero_parte={literal(parte)} AND ({condicion}); IF @@ROWCOUNT<>1 THROW 51000,'El catálogo cambió durante la conciliación',1;")
 if statements:
  consulta="""SET XACT_ABORT ON; BEGIN TRY BEGIN TRANSACTION;
 DECLARE @lock int; EXEC @lock=sys.sp_getapplock @Resource=N'MVP_INVENTARIO',@LockMode='Exclusive',@LockOwner='Transaction',@LockTimeout=10000;
 IF @lock<0 THROW 51000,'No se pudo bloquear la conciliación',1;
 """+'\n'.join(statements)+"\nCOMMIT; END TRY BEGIN CATCH IF @@TRANCOUNT>0 ROLLBACK; THROW; END CATCH;"
  sql(base,consulta)
 despues=datos(base)
 for k in ('inventario','corte','huella_registros','huella_eventos','colores_huerfanos','partes_duplicadas','puestos','colores'):
  if antes[k]!=despues[k]:raise RuntimeError('Cambió un dato protegido: '+k)
 if len(antes['materiales'])!=len(despues['materiales']):raise RuntimeError('Cambió la cantidad de materiales')
 for a,b in zip(antes['materiales'],despues['materiales']):
  if {k:v for k,v in a.items() if k!='uso'}!={k:v for k,v in b.items() if k!='uso'}:raise RuntimeError('Cambió otro atributo del material')
 destino={parte:nuevo for parte,_,nuevo in updates}
 for m in despues['materiales']:
  if m['numero_parte'] in destino and m['uso']!=destino[m['numero_parte']]:raise RuntimeError('USO aplicado no coincide con el plan')
 return {'base':base,'actualizados':len(updates),'respaldo':str(ruta),'reversion':bool(revertir),'protegidos_sin_cambios':True}
if __name__=='__main__':
 p=argparse.ArgumentParser();p.add_argument('--base',choices=BASES,required=True);p.add_argument('--plan',required=True);p.add_argument('--revertir');p.add_argument('--salida',required=True);a=p.parse_args();r=ejecutar(a.base,a.plan,a.revertir);Path(a.salida).write_text(json.dumps(r,ensure_ascii=False,indent=2));print(json.dumps(r))
