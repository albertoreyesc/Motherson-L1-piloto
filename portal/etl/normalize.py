"""Offline, read-only source extraction. No stock, movements, event dates, or silent promotions.
Run: python normalize.py --sources ../sources/originals --out ../sources/staging
"""
import argparse, collections, datetime, hashlib, json, re, unicodedata, zipfile
from pathlib import Path
from xml.etree import ElementTree as ET
from openpyxl import load_workbook

CODES = ('DESENGANCHE L1', 'K.E.', 'MONTACARGAS', 'SG', 'SZ01', 'SZ02')
NS = {'s': 'http://schemas.openxmlformats.org/spreadsheetml/2006/main'}
def text(v):
    return re.sub(r'\s+', ' ', unicodedata.normalize('NFKC', str(v))).strip() if v is not None else None
def positive(v):
    return int(v) if isinstance(v, (float, int)) and not isinstance(v, bool) and 0 < v <= 1000000 and int(v) == v else None
def serial(v):
    return v.isoformat() if isinstance(v,(datetime.datetime,datetime.date,datetime.time)) else v
def bounds(path):
    # Google exports may omit dimensions or style enormous blank ranges. Inspect only actual XML values/formulas.
    result = {}
    with zipfile.ZipFile(path) as z:
        for name in z.namelist():
            if not re.fullmatch(r'xl/worksheets/sheet\d+\.xml', name): continue
            maxrow, maxcol = 0, 0
            for _, el in ET.iterparse(z.open(name),events=('end',)):
                if el.tag.endswith('}c'):
                    if any(el.find('s:'+t,NS) is not None for t in ('v','is','f')):
                        ref=el.attrib['r'];letters=re.match(r'[A-Z]+',ref)[0];col=0
                        for ch in letters: col=col*26+ord(ch)-64
                        maxrow=max(maxrow,int(re.search(r'\d+',ref)[0]));maxcol=max(maxcol,col)
                    el.clear()
            result[name]=(maxrow,maxcol)
    return result
def candidate(kind,sheet,values):
    def v(col): return values.get(col,{}).get('cached',values.get(col,{}).get('value'))
    if kind=='UPAEP' and sheet=='Partes' and v('C'):
        return dict(parte=text(v('C')),descripcion=text(v('D')),puesto=text(v('B')),estacion=text(v('E')),estanteria=text(v('F')),nivel=text(v('G')),capacidadCajas=positive(v('I')),piezasPorCaja=positive(v('J')))
    if kind=='PFEP' and sheet=='NumerosDeParte' and v('B'):
        return dict(parte=text(v('B')),descripcion=text(v('E')),puesto=text(v('D')),claveHistorica=text(v('C')),piezasPorCaja=positive(v('W')))
    if kind=='PFEP' and sheet=='Estanterias' and v('F'):
        return dict(parte=text(v('F')),estacion=text(v('C')),estanteria=text(v('D')),nivel=text(v('E')),capacidadCajas=positive(v('G')),piezasPorCaja=None)
    return None
def extract(path,kind,origin):
    limits=bounds(path); w=load_workbook(path,read_only=True,data_only=False);cached=load_workbook(path,read_only=True,data_only=True)
    rows=[];stats=collections.Counter();seen=collections.Counter()
    for idx,sheet in enumerate(w):
        maxrow,maxcol=limits.get(f'xl/worksheets/sheet{idx+1}.xml',(0,0))
        if not maxrow: continue
        s2=cached[sheet.title]
        raw_rows=sheet.iter_rows(min_row=1,max_row=maxrow,max_col=maxcol)
        cache_rows=s2.iter_rows(min_row=1,max_row=maxrow,max_col=maxcol)
        for cells,cache in zip(raw_rows,cache_rows):
            raw={}
            for cell,cc in zip(cells,cache):
                if cell.value is None: continue
                col=re.match(r'[A-Z]+',cell.coordinate)[0]
                raw[col]={'value':serial(cell.value),'type':cell.data_type}
                if cell.data_type=='f':raw[col]['cached']=serial(cc.value)
            if not raw: continue
            num=next(c.row for c in cells if c.value is not None);issues=[]
            data=candidate(kind,sheet.title,raw) if num>(2 if sheet.title in ('Partes','Estanterias') else 1) else None
            if data:
                stats['candidateRows']+=1
                if data.get('puesto') not in CODES:
                    issues.append({'rule':'PUESTO_PENDIENTE','detail':'Código sin coincidencia exacta; no se infiere de la clave histórica.'})
                if not data.get('piezasPorCaja'):
                    issues.append({'rule':'EMPAQUE_PENDIENTE','detail':'Factor caja no validado o ausente. Pallet no se convierte en caja.'})
                key=tuple(data.get(x) for x in ('parte','puesto','estacion','estanteria','nivel'));seen[key]+=1
                if seen[key]>1:issues.append({'rule':'REFERENCIA_REPETIDA_REVISAR','detail':'Se conserva: repetición puede ser válida por puesto, estación o ubicación. No implica movimiento duplicado.'})
            if any(c.get('type')=='f' or c.get('type')=='e' for c in raw.values()):
                issues.append({'rule':'FORMULA_O_ERROR_ORIGEN','detail':'Se conserva fórmula, valor cacheado y error. No se evalúa ni corrige automáticamente.'})
            if kind=='UPAEP' and sheet.title=='Partes' and num>2 and 'B' in raw and 'C' not in raw:
                issues.append({'rule':'FILA_SOLO_B','detail':'Fila de clave histórica; conservar para auditoría sin crear otro material.'})
            for issue in issues:stats[issue['rule']]+=1
            rows.append(dict(sheet=sheet.title,row=num,raw=raw,candidate=data,issues=issues))
    w.close();cached.close()
    return dict(file=path.name,sha256=hashlib.sha256(path.read_bytes()).hexdigest(),origin=origin,type=kind,rows=rows,statistics=dict(stats))
def main():
    p=argparse.ArgumentParser();p.add_argument('--sources',type=Path,required=True);p.add_argument('--out',type=Path,required=True);args=p.parse_args()
    sources=[]
    for path in sorted(args.sources.glob('*.xlsx')):
        kind='PFEP' if 'PFEP' in path.name else 'UPAEP'
        origin='https://drive.google.com/file/d/1lJmWG-tzXAxQ1RktjrMw28za-I21St0c' if kind=='PFEP' else 'https://docs.google.com/spreadsheets/d/1ql_5sLNvov8aU85IZgLUkVcp5fmy4v37eRE7YQUd6gE/edit (EXPORTACIÓN de copia nativa; original binario UPAEP pendiente)'
        sources.append(extract(path,kind,origin))
    args.out.mkdir(parents=True,exist_ok=True)
    output={'schemaVersion':1,'sources':sources,'rules':{'noStock':True,'noEventDates':True,'noHistoricalConsumption':True,'noAutomaticPromotion':True,'exactCodes':CODES}}
    (args.out/'staging.json').write_text(json.dumps(output,ensure_ascii=False,indent=2),encoding='utf8')
    report={'sources':[{k:v for k,v in s.items() if k!='rows'}|{'preservedRows':len(s['rows'])} for s in sources]}
    (args.out/'quality-report.json').write_text(json.dumps(report,ensure_ascii=False,indent=2),encoding='utf8')
    print(json.dumps(report,ensure_ascii=False))
if __name__=='__main__':main()
