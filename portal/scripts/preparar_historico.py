"""Preparación reproducible de exportaciones 21; no modifica fuentes ni SQL."""
import argparse,collections,datetime,gzip,hashlib,json
from pathlib import Path

def leer(path):
    with gzip.open(path,'rt',encoding='utf-8') as f:
        for s in f: yield json.loads(s)

def preparar(origen,salida):
    origen=Path(origen).resolve();salida=Path(salida).resolve();salida.mkdir(parents=True,exist_ok=True)
    m=json.loads((origen/'manifest-database.json').read_text());carpeta=origen/'json'
    if m['masterId']!='4d577245a22d' or m['formato']!=21:raise ValueError('Fuente no corresponde al histórico aprobado')
    fuentes=[]
    for p in sorted(carpeta.glob('*.jsonl.gz')):
        fuentes.append(dict(nombre=p.name,ruta=str(p),sha256=hashlib.file_digest(p.open('rb'),'sha256').hexdigest(),filas=sum(1 for _ in leer(p))))
    if len(fuentes)!=10:raise ValueError('Se requieren las diez exportaciones completas')
    posiciones={x['id_posicion']:x for x in leer(carpeta/'posiciones.jsonl.gz')};saldos={x['id_posicion']:x['saldo_piezas'] for x in leer(carpeta/'saldos_actuales.jsonl.gz')};config={}
    for c in leer(carpeta/'configuraciones_posicion.jsonl.gz'):
        if c['id_posicion'] not in config or c['id_version']>config[c['id_posicion']]['id_version']:config[c['id_posicion']]=c
    f=origen/'portal/simulation/fixtures/dataset.json';datos=json.loads(f.read_text());descripciones={r['parte']:r['descripcion'] for r in datos['catalog']}
    ids=set();versions=set();tipos=collections.Counter();clases=collections.Counter();ultimo={};fechas=[];incidencias=[];referencias=0
    campos=['id_registro_origen','id_version_origen','id_posicion_origen','fecha_movimiento','tipo_evento','tipo_movimiento','cantidad_piezas','cantidad_delta','numero_parte','id_puesto','id_estacion','id_estanteria','id_ubicacion','estacion','estanteria','nivel','id_turno','id_usuario','responsable_simulado','proceso_generador','origen_declarado','saldo_anterior','saldo_posterior','es_base']
    with gzip.open(salida/'eventos-normalizados.jsonl.gz','wt',encoding='utf-8') as out:
        for x in leer(carpeta/'registros.jsonl.gz'):
            i=x['id_registro'];v=x['id_version'];p=posiciones.get(x['id_posicion']);t=x['fecha_evento_utc'];q=x['cantidad_movimiento'];tipo=x['tipo_registro'];tipos[tipo]+=1
            if i in ids or v in versions:raise ValueError('Identificador de evento o versión repetido')
            ids.add(i);versions.add(v)
            if x['id_posicion'] is not None and not p:referencias+=1
            if t:fechas.append(t)
            movimiento='SALIDA' if tipo=='CONSUMO' and q is not None and q<0 else 'ENTRADA' if tipo=='ENTRADA_CONFIRMADA' and q is not None and q>0 else 'APERTURA' if tipo=='INVENTARIO_INICIAL' and q is not None and q>=0 else 'AJUSTE' if tipo=='AJUSTE_AUDITADO' and q is not None and q!=0 else None
            if movimiento in ('ENTRADA','SALIDA') and x['saldo_anterior'] is not None and x['saldo_posterior'] is not None and x['saldo_anterior']+q!=x['saldo_posterior']:
                incidencias.append(dict(id=i,regla='DELTA_NO_CONCILIA',detalle='No se incluye como movimiento KPI'));movimiento=None
            clases[movimiento or 'EVENTO_NO_MOVIMIENTO']+=1
            if tipo in ('ENTRADA_CONFIRMADA','CONSUMO','INVENTARIO_INICIAL','AJUSTE_AUDITADO','PRUEBA_AGOTAMIENTO','PRUEBA_CRITICO') and x['saldo_posterior'] is not None:
                k=(t or '',i)
                if x['id_posicion'] not in ultimo or k>ultimo[x['id_posicion']][0]:ultimo[x['id_posicion']]=(k,x['saldo_posterior'])
            n=dict(id_registro_origen=i,id_version_origen=v,id_posicion_origen=x['id_posicion'],fecha_movimiento=t,tipo_evento=tipo,tipo_movimiento=movimiento,cantidad_piezas=abs(q) if movimiento else None,cantidad_delta=q,numero_parte=p['numero_parte'] if p else None,id_puesto=p['id_puesto'] if p else None,id_estacion=p['id_estacion'] if p else None,id_estanteria=p['id_estanteria'] if p else None,id_ubicacion=p['id_ubicacion'] if p else None,estacion=p['codigo_estacion'] if p else None,estanteria=p['codigo_estanteria'] if p else None,nivel=p['nivel'] if p else None,id_turno=x['id_turno'],id_usuario=x['id_usuario'],responsable_simulado=x['nombre_responsable'],proceso_generador=x['proceso_generador'],origen_declarado=x['origen'],saldo_anterior=x['saldo_anterior'],saldo_posterior=x['saldo_posterior'],es_base=x['es_base'])
            out.write(json.dumps(n,ensure_ascii=False,separators=(',',':'))+'\n')
    diferencias=[dict(id_posicion=i,saldo=s,ultimo_evento=ultimo.get(i)) for i,s in saldos.items() if s is not None and (i not in ultimo or ultimo[i][1]!=s)]
    if len(ids)!=m['events'] or referencias or diferencias:raise ValueError('No concilia la exportación: '+str((len(ids),referencias,diferencias)))
    elegibles=[]
    for i,p in posiciones.items():
        c=config[i];razon=''
        if not p['es_operable'] or p['fecha_retiro_utc']:razon='POSICION_NO_OPERABLE'
        elif c['naturaleza_posicion']!='DOCUMENTAL':razon='UBICACION_LOGICA_SIMULADA_NO_VALIDADA'
        elif saldos.get(i) is None:razon='SALDO_PENDIENTE'
        elif not c['piezas_por_caja'] or not c['capacidad_cajas']:razon='EMPAQUE_CAPACIDAD_PENDIENTE'
        if razon:incidencias.append(dict(id_posicion=i,regla=razon));continue
        elegibles.append(dict(id=i,parte=p['numero_parte'],descripcion=descripciones.get(p['numero_parte']),id_puesto=p['id_puesto'],puesto=p['codigo_puesto'],id_estacion=p['id_estacion'],estacion=p['codigo_estacion'],estanteria=p['codigo_estanteria'],nivel=p['nivel'],factor=c['piezas_por_caja'],capacidad=c['capacidad_cajas'],minimo=c['minimo_piezas'],saldo=saldos[i]))
    porparte=collections.defaultdict(set);niveles=collections.Counter()
    for e in elegibles:porparte[e['parte']].add((e['id_puesto'],e['factor'],e['minimo']));niveles[(e['id_estacion'],e['estanteria'],e['nivel'])]+=1
    if any(len(v)>1 for v in porparte.values()) or any(v>1 for v in niveles.values()):raise ValueError('Carga necesita conciliar atributos por parte o capacidad compartida')
    huella=hashlib.sha256(json.dumps([(f['nombre'],f['sha256']) for f in fuentes],separators=(',',':')).encode()).hexdigest()
    plan=dict(maestro=m['masterId'],revision=m['revision'],corte=max(fechas),inicio=min(fechas),huella=huella,fuentes=fuentes,eventos=len(ids),campos_evento=campos,archivo_eventos=str(salida/'eventos-normalizados.jsonl.gz'),posiciones=elegibles,puestos=sorted({(p['id_puesto'],p['codigo_puesto']) for p in posiciones.values()}),incidencias=incidencias)
    (salida/'plan.json').write_text(json.dumps(plan,ensure_ascii=False,indent=2))
    reporte=dict(maestro=m['masterId'],revision=m['revision'],inicio=min(fechas),corte=max(fechas),eventos=len(ids),ids_duplicados=0,versiones_duplicadas=0,referencias_inexistentes=referencias,saldos_discrepantes=diferencias,tipos=dict(tipos),clases_kpi=dict(clases),ubicaciones_apertura=len(elegibles),piezas_apertura=sum(e['saldo'] for e in elegibles),incidencias=incidencias,huella_fuentes=huella,fuentes=[{k:v for k,v in x.items() if k!='ruta'} for x in fuentes],nota='Fechas originales de fecha_evento_utc conservadas sin desplazar; no son mediciones reales. Nombres/IDs del catálogo inmutable del esquema 21, sin cruce con catálogos actuales del MVP.')
    (salida/'validacion-historico.json').write_text(json.dumps(reporte,ensure_ascii=False,indent=2));return reporte
if __name__=='__main__':
    a=argparse.ArgumentParser();a.add_argument('--origen',type=Path,required=True);a.add_argument('--salida',type=Path,required=True);args=a.parse_args();r=preparar(args.origen,args.salida);print(json.dumps({k:v for k,v in r.items() if k not in ['tipos','incidencias','fuentes']},ensure_ascii=False))
