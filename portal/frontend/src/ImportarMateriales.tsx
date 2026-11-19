// Importar materiales desde Excel (17/11/2026): plantilla descargable, vista previa y aplicación todo o nada.
import React,{useRef,useState} from 'react';
import {Download,FileUp} from 'lucide-react';
import {api,nd} from './api';
import type {Catalogo} from './MvpTypes';
import {encabezadosPlantilla,etiquetasCampo,leerCsv,planificar,type Celda,type Plan} from './importacionMateriales';

type Vista={plan:Plan;archivo:string;hoja:string};
const rojo='FFDA2128';

/** Valor visible de una celda de ExcelJS (fórmulas, texto enriquecido, hipervínculos). */
function valorExcel(v:unknown):Celda{
 if(v==null||typeof v==='string'||typeof v==='number'||typeof v==='boolean'||v instanceof Date)return v as Celda;
 const o=v as {result?:unknown;richText?:{text:string}[];text?:string;error?:string};
 if('result' in o)return valorExcel(o.result);
 if(o.richText)return o.richText.map(t=>t.text).join('');
 if(typeof o.text==='string')return o.text;
 if(o.error)return o.error;
 return String(v);
}
function descargar(blob:Blob,nombre:string){const a=document.createElement('a');a.href=URL.createObjectURL(blob);a.download=nombre;a.click();setTimeout(()=>URL.revokeObjectURL(a.href),1000)}

export function ImportarMateriales({catalogo,onCambio}:{catalogo:Catalogo;onCambio:()=>Promise<void>|void}){
 const [vista,setVista]=useState<Vista|null>(null),[busy,setBusy]=useState(false),[error,setError]=useState(''),[aviso,setAviso]=useState('');
 const input=useRef<HTMLInputElement>(null);

 async function plantilla(){
  setBusy(true);setError('');
  try{
   const {default:ExcelJS}=await import('exceljs');
   const libro=new ExcelJS.Workbook();libro.creator='Motherson L1 · Portal';libro.created=new Date();
   const hoja=libro.addWorksheet('Materiales',{views:[{state:'frozen',ySplit:1}]});
   const anchos=[24,18,40,16,18,14,18];
   hoja.columns=encabezadosPlantilla.map((h,i)=>({header:h,key:String(i),width:anchos[i]}));
   hoja.getRow(1).eachCell(c=>{c.font={bold:true,color:{argb:'FFFFFFFF'}};c.fill={type:'pattern',pattern:'solid',fgColor:{argb:rojo}}});
   for(const m of catalogo.materiales)hoja.addRow([m.numero_parte,m.puesto,m.descripcion??'',m.uso??'',m.cantidad_por_caja??'',m.minimo??'',m.variante_revision??'']);
   const ultima=Math.max(2,catalogo.materiales.length+1);
   const colores=catalogo.colores??[];
   if(colores.length){
    const lista=libro.addWorksheet('Variantes válidas');lista.getColumn(1).width=24;
    lista.getCell(1,1).value='VARIANTE / COLOR';lista.getCell(1,1).font={bold:true};
    colores.forEach((c,i)=>{lista.getCell(i+2,1).value=c});
   }
   for(let r=2;r<=ultima;r++){
    hoja.getCell(r,4).dataValidation={type:'list',allowBlank:true,formulae:['"COMPARTIDO,EXCLUSIVO"']};
    if(colores.length)hoja.getCell(r,7).dataValidation={type:'list',allowBlank:true,showErrorMessage:true,errorTitle:'Variante no válida',error:'Usa una variante de la hoja "Variantes válidas" o deja la celda vacía.',formulae:[`'Variantes válidas'!$A$2:$A$${colores.length+1}`]};
    hoja.getCell(r,1).font={bold:true};hoja.getCell(r,2).font={color:{argb:'FF585858'}};
   }
   hoja.autoFilter={from:{row:1,column:1},to:{row:1,column:encabezadosPlantilla.length}};
   const ayuda=libro.addWorksheet('Instrucciones');ayuda.getColumn(1).width=110;
   ['Cómo usar esta plantilla',
    '1. Cambia solo las columnas DESCRIPCION, USO, CANTIDAD X CAJA, MINIMO (PZ) y VARIANTE / COLOR.',
    '2. PARTE identifica el material y no se cambia. PUESTO LOGÍSTICO es solo referencia: el Excel no cambia el puesto.',
    '3. Una celda vacía NO borra el dato: el portal la ignora.',
    '4. USO: COMPARTIDO o EXCLUSIVO. CANTIDAD X CAJA: entero mayor que 0. MINIMO (PZ): entero de 0 o más, en piezas.',
    '5. VARIANTE / COLOR debe existir ya en el catálogo de colores: consulta la hoja "Variantes válidas". Si no existe, déjala vacía y pide que la agreguen.',
    '6. Las existencias (piezas en estantería) no se importan: solo cambian con entradas y salidas.',
    '7. Los materiales que no estén en el portal se reportan como "no encontrados"; las altas nuevas van en otra etapa.',
    '8. En el portal: Materiales y catálogos → Importar Excel → revisa la vista previa → Aplicar.'].forEach((t,i)=>{const c=ayuda.getCell(i+1,1);c.value=t;if(i===0)c.font={bold:true,size:14}});
   const datos=await libro.xlsx.writeBuffer();
   descargar(new Blob([datos],{type:'application/vnd.openxmlformats-officedocument.spreadsheetml.sheet'}),'motherson-l1-plantilla-materiales.xlsx');
  }catch(e){setError(e instanceof Error?e.message:'No se pudo generar la plantilla.')}
  finally{setBusy(false)}
 }

 async function leer(archivo?:File){
  if(!archivo)return;
  setBusy(true);setError('');setAviso('');setVista(null);
  try{
   if(archivo.size>20*1024*1024)throw new Error('El archivo pesa más de 20 MB.');
   const nombre=archivo.name.toLowerCase();
   let plan:Plan|null=null;let hojaUsada='';
   if(nombre.endsWith('.csv')){plan=planificar(catalogo.materiales,leerCsv(await archivo.text()),catalogo.colores);hojaUsada='CSV'}
   else if(nombre.endsWith('.xlsx')){
    const {default:ExcelJS}=await import('exceljs');
    const libro=new ExcelJS.Workbook();await libro.xlsx.load(await archivo.arrayBuffer());
    const hojas=[...libro.worksheets].sort((a,b)=>(b.name==='Materiales'?1:0)-(a.name==='Materiales'?1:0));
    for(const hoja of hojas){
     const filas:Celda[][]=[];
     hoja.eachRow({includeEmpty:true},(fila,n)=>{const valores=fila.values as unknown[];filas[n-1]=valores.slice(1).map(valorExcel)});
     for(let i=0;i<filas.length;i++)filas[i]??=[];
     plan=planificar(catalogo.materiales,filas,catalogo.colores);
     if(plan){hojaUsada=hoja.name;break}
    }
   }else throw new Error('Solo se aceptan archivos .xlsx o .csv.');
   if(!plan)throw new Error('No se encontró una columna de número de parte (PARTE o NUMERO DE PARTE) en las primeras 10 filas de ninguna hoja.');
   const reconocidas=(Object.keys(etiquetasCampo) as (keyof typeof etiquetasCampo)[]).filter(c=>plan!.columnas[c]!==undefined);
   if(!reconocidas.length)throw new Error('La hoja tiene número de parte pero ninguna columna que se pueda importar (DESCRIPCION, USO, CANTIDAD X CAJA, MINIMO o VARIANTE).');
   setVista({plan,archivo:archivo.name,hoja:hojaUsada});
  }catch(e){setError(e instanceof Error?e.message:'No se pudo leer el archivo.')}
  finally{setBusy(false);if(input.current)input.current.value=''}
 }

 async function aplicar(){
  if(!vista?.plan.cambios.length)return;
  setBusy(true);setError('');
  try{
   const r=await api<{cambios:number;materiales:number}>('/catalogo/materiales',{cambios:vista.plan.cambios});
   setAviso(`Se aplicaron ${nd(r.cambios)} ${r.cambios===1?'cambio':'cambios'} en ${nd(r.materiales)} ${r.materiales===1?'material':'materiales'} desde ${vista.archivo}.`);
   setVista(null);await onCambio();
  }catch(e){setError(e instanceof Error?e.message:'No se aplicó la importación.')}
  finally{setBusy(false)}
 }

 const p=vista?.plan;
 const problemas=p?p.filas.filter(f=>f.estado!=='CAMBIA'&&f.estado!=='SIN_CAMBIOS'):[];
 return <section className="panel importar">
  <h2>Importar materiales desde Excel</h2>
  <p>Actualiza descripción, uso, piezas por caja, mínimo y variante de los materiales que ya están en el portal. No cambia existencias ni puestos, y las celdas vacías no borran datos. Antes de aplicar verás todo lo que va a cambiar.</p>
  <div className="button-row">
   <button type="button" className="secondary" disabled={busy} onClick={plantilla}><Download size={16} aria-hidden="true"/>Descargar plantilla</button>
   <input ref={input} type="file" accept=".xlsx,.csv,application/vnd.openxmlformats-officedocument.spreadsheetml.sheet,text/csv" hidden onChange={e=>leer(e.target.files?.[0])}/>
   <button type="button" disabled={busy} onClick={()=>input.current?.click()}><FileUp size={16} aria-hidden="true"/>{busy&&!vista?'Leyendo…':'Importar Excel'}</button>
  </div>
  {aviso&&<p className="success" role="status">{aviso}</p>}
  {error&&<p className="error" role="alert">{error}</p>}
  {vista&&p&&<div className="importar-vista">
   <h3>Vista previa · {vista.archivo} · hoja {vista.hoja} (encabezados en la fila {p.encabezado})</h3>
   <p>Columnas reconocidas: {(Object.keys(etiquetasCampo) as (keyof typeof etiquetasCampo)[]).filter(c=>p.columnas[c]!==undefined).map(c=>etiquetasCampo[c]).join(', ')}.</p>
   <ul className="importar-resumen">
    <li><strong>{nd(p.resumen.materialesConCambios)}</strong> materiales con cambios ({nd(p.resumen.cambios)} datos)</li>
    <li><strong>{nd(p.resumen.sinCambios)}</strong> ya coinciden</li>
    <li><strong>{nd(p.resumen.noEncontrados)}</strong> no encontrados</li>
    <li><strong>{nd(p.resumen.errores)}</strong> con errores</li>
    {p.resumen.repetidas>0&&<li><strong>{nd(p.resumen.repetidas)}</strong> filas repetidas</li>}
    {p.resumen.repetidasIguales>0&&<li><strong>{nd(p.resumen.repetidasIguales)}</strong> filas repetidas idénticas (se ignoran)</li>}
   </ul>
   {p.cambios.length>0&&<div className="table-wrap" role="region" aria-label="Cambios que se aplicarán" tabIndex={0}><table><thead><tr><th scope="col">Fila</th><th scope="col">Parte</th><th scope="col">Dato</th><th scope="col">Antes</th><th scope="col">Después</th></tr></thead>
    <tbody>{p.filas.filter(f=>f.estado==='CAMBIA').flatMap(f=>f.cambios.map(c=><tr key={f.fila+c.campo}><td>{f.fila}</td><td>{c.parte}</td><td>{etiquetasCampo[c.campo]}</td><td className="importar-antes">{c.anterior??'—'}</td><td className="importar-despues">{c.nuevo}</td></tr>))}</tbody></table></div>}
   {problemas.length>0&&<details className="importar-problemas" open={p.resumen.errores>0}><summary>{nd(problemas.length)} filas no se aplicarán (no encontradas, con errores o repetidas). Los demás cambios sí se pueden aplicar; estas filas se quedan como están.</summary>
    {problemas.some(f=>f.detalle.includes('la variante «'))&&<p className="importar-variantes">Variantes válidas en esta base: <strong>{catalogo.colores?.length?catalogo.colores.join(', '):'ninguna'}</strong>. Corrige la celda o déjala vacía.</p>}
    <ul>{problemas.map(f=><li key={f.fila}>Fila {f.fila} · <strong>{f.parte}</strong>: {f.detalle}</li>)}</ul></details>}
   <div className="button-row">
    <button type="button" disabled={busy||!p.cambios.length} onClick={aplicar}>{busy?'Aplicando…':p.cambios.length?`Aplicar ${nd(p.cambios.length)} ${p.cambios.length===1?'cambio':'cambios'}`:'No hay cambios que aplicar'}</button>
    <button type="button" className="secondary" disabled={busy} onClick={()=>{setVista(null);setError('')}}>Cancelar</button>
   </div>
  </div>}
 </section>;
}
