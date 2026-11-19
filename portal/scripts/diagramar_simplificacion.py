"""Diagramas locales de la estructura auditada; no accede a filas de negocio."""
import copy
import csv
import html
import json
import uuid
from datetime import datetime, timezone
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
OUT = ROOT/'documentacion/diagramas/anteriores'
DATA = json.loads((ROOT/'portal/evidencias/simplificacion/modelo-fisico.json').read_text())
PLANTILLA = json.loads((OUT/'mvp-relaciones-listo.erd').read_text())
TECNICAS = {'version_concurrencia','hash_contrasena','id_operacion','huella_peticion'}
POSICIONES = {'ESTACION':(40,110),'ESTANTERIA':(40,390),'MATERIAL':(490,110),
              'UBICACION':(490,490),'PUESTO_LOGISTICO':(1030,110),
              'USUARIOS':(1030,480),'REGISTRO':(490,810),'TURNO':(40,1020)}
SIGNIFICADOS = {
 'id_estacion':'Identificador estable de la estación.', 'id_linea':'Línea de producción; el MVP admite únicamente L1.',
 'nombre_estacion':'Código o nombre documentado de la estación.', 'id_estanteria':'Identificador del nivel de estantería.',
 'estanteria':'Código de estantería dentro de la estación.', 'nivel':'Nivel o posición documentada.',
 'cajas_estanteria':'Capacidad del nivel en cajas; no representa existencias.',
 'id_puesto':'Puesto logístico responsable. En USUARIOS es la asignación operativa actual; en REGISTRO es el puesto del material al capturar.',
 'nombre':'Código del puesto (SZ01, SZ02, SG, K.E., MONTACARGAS o DESENGANCHE L1). El nombre completo documentado se presenta por separado.',
 'tipo':'Clasificación del puesto; permanece NULL cuando falta definición documental.',
 'numero_parte':'Referencia completa del material; conserva sufijos y ceros.',
 'descripcion':'Descripción documental del material.', 'uso':'Clasificación de uso del PFEP: COMPARTIDO o EXCLUSIVO; no es modelo de vehículo.',
 'cantidad_por_caja':'Piezas por caja del material.', 'minimo':'Umbral mínimo en piezas del material; distinto de la cobertura temporal por ubicación.',
 'id_ubicacion':'Asignación estable de material a nivel de estantería.', 'cantidad':'Saldo conocido de piezas; NULL significa apertura pendiente, no cero.',
 'version_concurrencia':'Versión técnica que impide confirmar desde un saldo desactualizado.',
 'id_turno':'Turno PFEP del movimiento; puede ser NULL fuera de jornada.', 'nombre_turno':'PFEP T1 o PFEP T2.',
 'hora_inicio':'Inicio local del turno; inclusivo.', 'hora_fin':'Fin local del turno; exclusivo.',
 'id_usuarios':'Identificador de la cuenta individual que captura; conserva la atribución antigua.',
 'nombre_usuario':'Nombre de acceso único.', 'nombre_visible':'Nombre mostrado de la cuenta cuando está documentado.',
 'hash_contrasena':'Verificador de contraseña del mecanismo existente. Nunca contraseña en texto plano ni campo de reporting.',
 'esta_activo':'Permite revocar el acceso sin borrar cuenta, puesto ni comprobantes.',
 'perfil':'Permisos del MVP: ADMINISTRADOR, ENTRADAS, MOVIMIENTOS o CONSULTA; solo ADMINISTRADOR puede registrar salidas.',
 'id_registro':'Identificador del comprobante inmutable.', 'tipo_movimiento':'APERTURA, ENTRADA o SALIDA.',
 'cantidad_movimiento':'Piezas del movimiento; el signo se interpreta según el tipo.', 'cantidad_cajas':'Cajas completas ingresadas; NULL en salidas y aperturas.',
 'cantidad_por_caja_aplicada':'Factor aplicado a esa entrada, conservado aunque cambie el catálogo.',
 'saldo_anterior':'Saldo previo a la operación.', 'saldo_posterior':'Saldo tras la operación.',
 'fecha_movimiento':'Instante UTC del movimiento; se presenta en hora de México.',
 'fecha_hora_inicio':'Inicio UTC de procesamiento de la captura.', 'fecha_hora_fin':'Fin UTC de procesamiento de la captura.',
 'id_operacion':'Identificador único de reintento: evita repetir el mismo movimiento.',
 'huella_peticion':'Huella SHA-256 de los datos de la petición para detectar reutilización incompatible del identificador.',
 'origen':'CAPTURA_SIMULADA, APERTURA_SIMULADA, CAPTURA_REAL o APERTURA_REAL. Esta entrega registra capturas simuladas.'
}

def uid(s): return str(uuid.uuid5(uuid.NAMESPACE_URL,'motherson-simplificado/'+s))
def tipo(c):
    t=c['tipo']
    if t in ('varchar','nvarchar','char','nchar'):
        n=c['max_length'];return f"{t}({'max' if n==-1 else n//2 if t.startswith('n') else n})"
    if t in ('datetime2','time'): return f"{t}({c['scale']})"
    return t

def generar(conceptual=False):
    columnas=[c for c in DATA['columnas'] if not conceptual or c['columna'] not in TECNICAS]
    d=copy.deepcopy(PLANTILLA)
    d.update(documentName='Motherson L1 — '+('modelo conceptual' if conceptual else 'modelo físico')+' — 8 tablas y 9 relaciones',lastUpdatedAt=datetime.now(timezone.utc).isoformat())
    for k in ('tableViewModels','columnModels','columnShareModels','relationViewModels','columnGroupModels','foregroundMemos','backgroundMemos'): d[k]=[]
    tipos=list(dict.fromkeys(tipo(c) for c in columnas));sid=d['schemaConfig']['defaultSchemaId']
    d['databaseSetting']['columnTypes']=[{'id':1000+i,'name':t,'description':'SQL Server','baseQuery':t,'category':'other',**({'withAutoIncrement':True} if t=='int' else {})} for i,t in enumerate(tipos)]
    d['erdSettingModel']['showRelationNames']=False
    d['erdSettingModel']['exportDdlSetting']['fileName']='mvp-simplificado'
    for t,(x,y) in POSICIONES.items():
        cs=[c for c in columnas if c['tabla']==t]
        d['tableViewModels'].append({'tableModel':{'tableModelId':uid(t),'physicalName':t,'logicalName':t,'schemaId':sid,'columnModelIds':[uid(t+'.'+c['columna']) for c in cs],'description':'Estructura auditada de Pruebas; documentación sin modificaciones SQL.'},'left':x,'top':y,'headerBackgroundColor':{'red':21,'green':94,'blue':117},'headerForegroundColor':{'red':255,'green':255,'blue':255},'createdAt':d['lastUpdatedAt']})
        for c in cs:
            name=t+'.'+c['columna'];d['columnModels'].append({'columnModelId':uid(name),'columnShareModelId':uid('share/'+name),'notNull':not c['is_nullable'],'primaryKey':c['es_pk'],'autoIncrement':c['is_identity']})
            d['columnShareModels'].append({'columnShareModelId':uid('share/'+name),'physicalName':c['columna'],'logicalName':c['columna'],'columnTypeId':1000+tipos.index(tipo(c)),'description':SIGNIFICADOS[c['columna']],'createdAt':d['lastUpdatedAt']})
    for r in DATA['relaciones']:
        d['relationViewModels'].append({'relationModel':{'relationModelId':uid(r['nombre']),'relationName':r['nombre'],'parentTableModelId':uid(r['padre']),'parentCardinality':'0..1' if r['is_nullable'] else '1','childTableModelId':uid(r['hija']),'childCardinality':'0..N','relationPairs':[{'parentColumnModelId':uid(r['padre']+'.'+r['columna_padre']),'childColumnModelId':uid(r['hija']+'.'+r['columna_hija'])}],'onUpdateAction':'NO ACTION','onDeleteAction':'NO ACTION'},'lineViewModel':{'strokeWidth':1.2,'edges':[],'orthogonalLines':[],'color':{'red':100,'green':116,'blue':139}},'createdAt':d['lastUpdatedAt']})
    nombre='mvp-simplificado-'+('conceptual' if conceptual else 'fisico')
    (OUT/(nombre+'.erd')).write_text(json.dumps(d,ensure_ascii=False,indent=2)+'\n')
    # SVG local legible; polilíneas por corredores exteriores, sin atravesar tablas.
    alturas={t:36+len([c for c in columnas if c['tabla']==t])*26 for t in POSICIONES}
    partes=['<svg xmlns="http://www.w3.org/2000/svg" width="1530" height="1430" viewBox="0 0 1530 1430"><rect width="1530" height="1430" fill="#f6f8fa"/>',
      '<defs><marker id="flecha" markerWidth="10" markerHeight="8" refX="9" refY="4" orient="auto"><path d="M0,0 L10,4 L0,8" fill="#64748b"/></marker></defs>',
      f'<text x="40" y="40" font-family="Arial" font-size="24" font-weight="bold">Motherson L1 · {"Modelo conceptual" if conceptual else "Modelo físico"} · 8 tablas / 9 FK</text>',
      '<text x="40" y="68" font-family="Arial" font-size="14" fill="#475569">SIMULADO · HISTÓRICO · Flecha: tabla hija → padre. PK = primaria; FK = foránea; opcional = admite NULL.</text>']
    # Los corredores se dimensionan sobre el modelo físico para conservar ambas vistas.
    rutas=[('ESTANTERIA','ESTACION',[(210,390),(210,110+alturas['ESTACION'])]),
      ('UBICACION','ESTANTERIA',[(490,550),(380,550),(380,450)]),
      ('UBICACION','MATERIAL',[(660,490),(660,110+alturas['MATERIAL'])]),
      ('MATERIAL','PUESTO_LOGISTICO',[(830,170),(1030,170)]),
      ('USUARIOS','PUESTO_LOGISTICO',[(1200,480),(1200,110+alturas['PUESTO_LOGISTICO'])]),
      ('REGISTRO','UBICACION',[(660,810),(660,490+alturas['UBICACION'])]),
      ('REGISTRO','USUARIOS',[(830,900),(930,900),(930,560),(1030,560)]),
      ('REGISTRO','TURNO',[(490,1090),(380,1090)]),
      ('REGISTRO','PUESTO_LOGISTICO',[(660,810+alturas['REGISTRO']),(660,1370),(1450,1370),(1450,170),(1370,170)])]
    for hija,padre,puntos in rutas:
        rr=next(r for r in DATA['relaciones'] if r['hija']==hija and r['padre']==padre)
        partes.append(f'<polyline points="{" ".join(str(x)+","+str(y) for x,y in puntos)}" fill="none" stroke="#64748b" stroke-width="1.8" marker-end="url(#flecha)"><title>{html.escape(hija+"."+rr["columna_hija"]+" → "+padre+"."+rr["columna_padre"])}</title></polyline>')
    fks={(r['hija'],r['columna_hija']) for r in DATA['relaciones']}
    for t,(x,y) in POSICIONES.items():
        cs=[c for c in columnas if c['tabla']==t]
        partes.extend([f'<rect x="{x}" y="{y}" width="340" height="{alturas[t]}" rx="8" fill="white" stroke="#155e75"/>',f'<path d="M{x+8},{y} H{x+332} Q{x+340},{y} {x+340},{y+8} V{y+34} H{x} V{y+8} Q{x},{y} {x+8},{y}" fill="#155e75"/>',f'<text x="{x+12}" y="{y+24}" font-family="Arial" font-size="16" fill="white" font-weight="bold">{t}</text>'])
        for i,c in enumerate(cs):
            yy=y+54+i*26;clave=('PK ' if c['es_pk'] else '')+('FK' if (t,c['columna']) in fks else '')
            partes.append(f'<text x="{x+10}" y="{yy}" font-family="Arial" font-size="10" fill="#155e75">{clave}</text><text x="{x+48}" y="{yy}" font-family="Arial" font-size="12">{c["columna"]}<title>{html.escape(SIGNIFICADOS[c["columna"]])}</title></text><text x="{x+242}" y="{yy}" font-family="Arial" font-size="11" fill="#475569">{tipo(c)}{" *" if c["is_nullable"] else ""}</text>')
    partes.append('<text x="40" y="1410" font-family="Arial" font-size="13" fill="#475569">* Atributo opcional. USUARIOS.id_puesto: varias cuentas por puesto; administración sin asignación forzada. Archivo histórico fuera del alcance operativo.</text></svg>')
    (OUT/(nombre+'.svg')).write_text(''.join(partes))

if __name__=='__main__':
    generar();generar(True)
    with (ROOT/'documentacion/DICCIONARIO_SIMPLIFICADO.csv').open('w',newline='') as f:
        w=csv.writer(f);w.writerow(['tabla','atributo','tipo_sql','admite_null','clave_primaria','descripcion'])
        for c in DATA['columnas']:w.writerow([c['tabla'],c['columna'],tipo(c),c['is_nullable'],c['es_pk'],SIGNIFICADOS[c['columna']]])
    print('Generados dos ERD, dos SVG y diccionario de 50 atributos desde metadatos SQL.')
