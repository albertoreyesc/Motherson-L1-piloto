"""Conciliación de atributos: lee fuentes, no modifica Excel ni SQL."""
import argparse,ast,collections,csv,gzip,hashlib,json,re
from pathlib import Path

def normalizar(v):return re.sub(r'[\s-]+','',str(v or '')).upper()
def texto(v):return ' '.join(str(v).split()) if v is not None else ''
def huella(p):return hashlib.sha256(p.read_bytes()).hexdigest()
def preparar(origen,actual,salida):
 import openpyxl
 origen=Path(origen);actual=Path(actual);salida=Path(salida);salida.mkdir(parents=True,exist_ok=True)
 before=json.loads(actual.read_text());materiales=before['Motherson_L1_MVP_Desarrollo']['materiales']
 destino=collections.Counter(normalizar(m['numero_parte']) for m in materiales)
 if any(n>1 for n in destino.values()):raise ValueError('Colisión de normalización en los materiales destino')
 fuentes=[];indices=[];revisiones=[]
 for carpeta,nombre in [('reviewed-originals','PFEP LOGISTICA - UPAEP (4).xlsx'),('originals','PFEP LOGISTICA - UPAEP (1).xlsx')]:
  f=origen/'portal/sources'/carpeta/nombre;w=openpyxl.load_workbook(f,read_only=True,data_only=True);idx=collections.defaultdict(list)
  for n,row in enumerate(w['NumerosDeParte'].values,1):
   if n>1 and row[1]:idx[normalizar(row[1])].append({'fila':n,'parte':str(row[1]),'uso':texto(row[9]),'linea':row[27],'celda':f'J{n}'})
  fuentes.append({'archivo':nombre,'ruta':str(f.resolve()),'sha256':huella(f),'hoja':'NumerosDeParte','columnas':{'parte':'B','auto':'I','uso':'J','linea':'AB'}});indices.append(idx)
  cabeceras=[]
  for sheet in w:
   for n,row in enumerate(sheet.iter_rows(min_row=1,max_row=3,values_only=True),1):
    for col,value in enumerate(row,1):
     if isinstance(value,str) and re.fullmatch(r'COLOR|TIPO|USO|TIPO DE SURTIMIENTO',texto(value).upper()):cabeceras.append({'hoja':sheet.title,'fila':n,'columna':col,'texto':value})
  revisiones.append({'archivo':nombre,'cabeceras_relevantes':cabeceras,'filas_parte':sum(len(v) for v in idx.values()),'claves_repetidas':[{'clave':k,'filas':v} for k,v in idx.items() if len(v)>1]});w.close()
 # Código/nombre procede del catálogo de seis puestos ya aprobado, no de una categoría inventada.
 post_file=origen/'portal/simulation/logistic_catalog.py';arbol=ast.parse(post_file.read_text());puestos=next(ast.literal_eval(x.value) for x in arbol.body if isinstance(x,ast.Assign) and any(isinstance(t,ast.Name) and t.id=='POSTS' for t in x.targets))
 post_sha=huella(post_file);items={};conciliacion=[];cambios=[]
 for m in materiales:
  parte=m['numero_parte'];refs=indices[0].get(normalizar(parte),[]);anteriores=indices[1].get(normalizar(parte),[])
  estado='NO_DOCUMENTADO';motivo='Sin coincidencia exacta del número completo en PFEP.';valor=None;fuente=None
  if len(refs)>1:estado='PENDIENTE_VALIDACION';motivo='Coincidencias múltiples: no se selecciona la primera.'
  elif len(refs)==1:
   ref=refs[0];valor=ref['uso'];fuente={**fuentes[0],'fila':ref['fila'],'celda':ref['celda'],'parte_original':ref['parte']}
   if ref['linea']!=1:estado='PENDIENTE_VALIDACION';motivo='La referencia no está confirmada para Línea 1.'
   elif not valor:motivo='USO vacío en el PFEP.'
   elif valor not in ('COMPARTIDO','EXCLUSIVO'):estado='PENDIENTE_VALIDACION';motivo='Valor de USO fuera de las categorías documentadas esperadas.'
   elif len(anteriores)!=1 or anteriores[0]['uso']!=valor:estado='PENDIENTE_VALIDACION';motivo='Diferencia o cruce ambiguo entre las dos versiones de PFEP.'
   else:estado='DOCUMENTADO';motivo='USO explícito, único y consistente entre PFEP (4) y PFEP (1).'
  if parte=='57N 858 065 A MEC':estado='PENDIENTE_VALIDACION';motivo='PFEP contiene 57N 858 065 MEC sin A. No se equiparan las referencias.'
  items[parte]={'uso':{'estado':estado,'valor':valor if estado=='DOCUMENTADO' else None,'motivo':motivo,'fuente':fuente},'color':{'estado':'NO_DOCUMENTADO','motivo':'No se localizó una columna de color ni una equivalencia de códigos validada en las fuentes revisadas.'}}
  if estado=='DOCUMENTADO':cambios.append({'numero_parte':parte,'uso':valor,'fuente':fuente})
  for campo in ('uso','color'):
   v=items[parte][campo];conciliacion.append({'entidad':'MATERIAL','identificador':parte,'campo':campo,'valor_anterior':m.get(campo),'valor_propuesto':v.get('valor'),'archivo':(v.get('fuente') or {}).get('archivo'),'hoja':(v.get('fuente') or {}).get('hoja'),'fila':(v.get('fuente') or {}).get('fila'),'celda':(v.get('fuente') or {}).get('celda'),'resultado':v['estado'],'motivo':v['motivo']})
 postmap={p['code']:{'nombre':p['name'],'tipo':{'estado':'NO_DOCUMENTADO','motivo':'No hay clasificación de tipo por puesto confirmada.'},'fuente':{'archivo':'logistic_catalog.py','sha256':post_sha,'definicion':'POSTS'}} for p in puestos}
 for p in before['Motherson_L1_MVP_Desarrollo']['puestos']:conciliacion.append({'entidad':'PUESTO_LOGISTICO','identificador':p['nombre'],'campo':'tipo','valor_anterior':p['tipo'],'valor_propuesto':None,'resultado':'NO_DOCUMENTADO','motivo':'TIPO DE SURTIMIENTO describe el manejo del material; no clasifica al puesto.'})
 reporte={'materiales':len(materiales),'uso_documentado':sum(v['uso']['estado']=='DOCUMENTADO' for v in items.values()),'uso_pendiente':sum(v['uso']['estado']=='PENDIENTE_VALIDACION' for v in items.values()),'color_documentado':0,'puestos':len(postmap),'tipo_documentado':0,'fuentes':fuentes,'revision_excel':revisiones,'definicion_uso':'Clasificación de utilización del PFEP: COMPARTIDO o EXCLUSIVO. AUTO es una columna distinta y no se copia dentro de uso.','color_csv':'El exportador anterior usa cargar_datos.color_de_parte; sus códigos y SIN COLOR son deducciones, no confirmaciones de color físico.','migracion':'preparar_historico.py y MvpMigration.cs no incluyen uso, id_color ni tipo. prepare_sources.py tampoco extrae USO del PFEP. La consulta Catalog sí recupera los tres campos.'}
 import copy
 doc={'version':1,'definicionUso':reporte['definicion_uso'],'materiales':copy.deepcopy(items),'puestos':postmap,'fuentes':[{k:v for k,v in f.items() if k!='ruta'} for f in fuentes]}
 for item in doc['materiales'].values():
  # La auditoría conserva el diagnóstico de color; el contrato vigente ya no lo ofrece.
  item.pop('color',None)
  if item['uso'].get('fuente'):item['uso']['fuente']={k:v for k,v in item['uso']['fuente'].items() if k!='ruta'}
 plan={'version':1,'cambios':cambios,'huella_fuentes':[f['sha256'] for f in fuentes]}
 for n,d in [('catalogo-documentado.json',doc),('plan-catalogo.json',plan),('diagnostico-catalogo.json',reporte),('conciliacion-catalogo.json',conciliacion)]: (salida/n).write_text(json.dumps(d,ensure_ascii=False,indent=2)+'\n')
 # CSV de intercambio como resultado de auditoría, sin modificar ningún libro.
 campos=sorted({k for r in conciliacion for k in r})
 with (salida/'conciliacion-catalogo.csv').open('w',newline='',encoding='utf-8-sig') as f:w=csv.DictWriter(f,fieldnames=campos);w.writeheader();w.writerows(conciliacion)
 print(json.dumps({k:v for k,v in reporte.items() if k not in ('fuentes','revision_excel')},ensure_ascii=False))
if __name__=='__main__':
 p=argparse.ArgumentParser();p.add_argument('--origen',required=True);p.add_argument('--actual',required=True);p.add_argument('--salida',required=True);a=p.parse_args();preparar(a.origen,a.actual,a.salida)
