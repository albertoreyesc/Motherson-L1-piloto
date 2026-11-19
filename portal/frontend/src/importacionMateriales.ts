// Lógica de la importación de materiales desde Excel/CSV (17/11/2026), sin dependencias de Excel:
// reconoce encabezados, cruza números de parte con el catálogo y arma la vista previa de cambios.
import type {Catalogo} from './MvpTypes';

export type Campo='descripcion'|'uso'|'cantidad_por_caja'|'minimo'|'variante';
export type Celda=string|number|boolean|Date|null|undefined;
export type CambioMaterial={parte:string;campo:Campo;anterior:string|null;nuevo:string};
export type EstadoFila='CAMBIA'|'SIN_CAMBIOS'|'NO_ENCONTRADO'|'ERROR'|'REPETIDA';
export type FilaPlan={fila:number;parte:string;estado:EstadoFila;detalle:string;cambios:CambioMaterial[]};
export type Plan={encabezado:number;columnas:Partial<Record<Campo|'parte',number>>;filas:FilaPlan[];cambios:CambioMaterial[];
 resumen:{materialesConCambios:number;cambios:number;sinCambios:number;noEncontrados:number;errores:number;repetidas:number;repetidasIguales:number}};
type Material=Catalogo['materiales'][number];

export const etiquetasCampo:Record<Campo,string>={descripcion:'Descripción',uso:'Uso PFEP',cantidad_por_caja:'Pz/caja',minimo:'Mínimo pz',variante:'Variante / color'};
/** Encabezados de la plantilla, con los nombres que ya usan los Excel de Motherson. */
export const encabezadosPlantilla=['PARTE','PUESTO LOGÍSTICO','DESCRIPCION','USO','CANTIDAD X CAJA','MINIMO (PZ)','VARIANTE / COLOR'] as const;

const clave=(t:string)=>t.normalize('NFD').replace(/[̀-ͯ]/g,'').toUpperCase().replace(/[^A-Z0-9]/g,'');
// Sinónimos tomados de la plantilla y de las hojas Partes (UPAEP Update), NumerosDeParte (PFEP) y TAOS/JETTA/TIGUAN.
const sinonimos:Record<Campo|'parte',string[]>={
 parte:['PARTE','NUMERO DE PARTE','NO. PARTE','NO PARTE','NUM. PARTE','PART NUMBER','MATERIAL NUMBER'],
 descripcion:['DESCRIPCION','NOMBRE','DESCRIPCION DE MATERIAL','MATERIAL DESCRIPTION'],
 uso:['USO','USO PFEP'],
 cantidad_por_caja:['CANTIDAD X CAJA','PIEZAS X CAJA','CANTIDAD POR CAJA','PIEZAS POR CAJA','PZ/CAJA','PZ X CAJA','CANTIDA DE PIEZAS POR CAJA','CANTIDAD DE PIEZAS POR CAJA'],
 minimo:['MINIMO','MINIMO (PZ)','MINIMO PZ','MIN'],
 variante:['VARIANTE','COLOR','VARIANTE / COLOR','VARIANTE / REVISION'],
};
const indiceSinonimos=new Map(Object.entries(sinonimos).flatMap(([campo,lista])=>lista.map(s=>[clave(s),campo as Campo|'parte'] as const)));

/** Texto de una celda tal como se ve; las fechas no aplican a estos campos. */
export function textoCelda(v:Celda){
 if(v==null)return '';
 if(v instanceof Date)return v.toISOString();
 return String(v).trim();
}
/** Número de parte comparable: última línea (en PFEP vienen como "SZ01\n2G5 035 282"), espacios colapsados, mayúsculas. */
export function normalizarParte(v:Celda){
 const lineas=textoCelda(v).split(/\r?\n/).map(l=>l.trim()).filter(Boolean);
 return (lineas.at(-1)??'').replace(/\s+/g,' ').toUpperCase();
}
/** Busca en las primeras 10 filas la que tenga una columna de número de parte. */
export function encontrarEncabezado(filas:Celda[][]){
 for(let i=0;i<Math.min(10,filas.length);i++){
  const columnas:Partial<Record<Campo|'parte',number>>={};
  filas[i].forEach((v,c)=>{const campo=indiceSinonimos.get(clave(textoCelda(v)));if(campo&&columnas[campo]===undefined)columnas[campo]=c});
  if(columnas.parte!==undefined)return {fila:i,columnas};
 }
 return null;
}

const valorActual=(m:Material,campo:Campo)=>{
 const v=campo==='variante'?m.variante_revision:m[campo];
 return v==null?null:String(v);
};
/** Valida y normaliza un valor del archivo. Devuelve el texto final o un mensaje de error. */
export function valorArchivo(campo:Campo,v:Celda,colores?:readonly string[]):{valor:string}|{error:string}{
 const t=textoCelda(v).replace(/\s+/g,' ');
 if(campo==='descripcion')return t.length<=250?{valor:t}:{error:'la descripción pasa de 250 caracteres'};
 if(campo==='uso'){const u=t.toUpperCase();return u==='COMPARTIDO'||u==='EXCLUSIVO'?{valor:u}:{error:`uso «${t}» no es COMPARTIDO ni EXCLUSIVO`}}
 if(campo==='variante'){const u=t.toUpperCase();if(u.length>10)return {error:'la variante pasa de 10 caracteres'};
  if(colores&&!colores.includes(u))return {error:`la variante «${u}» no existe en esta base`};
  return {valor:u}}
 const n=typeof v==='number'?v:Number(t.replace(/,/g,''));
 if(campo==='cantidad_por_caja')return Number.isInteger(n)&&n>=1&&n<=1000000?{valor:String(n)}:{error:`piezas por caja «${t}» no es un entero mayor que 0`};
 return Number.isInteger(n)&&n>=0&&n<=100000000?{valor:String(n)}:{error:`mínimo «${t}» no es un entero de 0 o más`};
}

/** Cruza el archivo con el catálogo. Las celdas vacías no cambian nada. Una fila con error no aplica ninguno de sus cambios. */
export function planificar(materiales:Material[],filas:Celda[][],colores?:readonly string[]):Plan|null{
 const encabezado=encontrarEncabezado(filas);
 if(!encabezado)return null;
 const porParte=new Map<string,Material[]>();
 for(const m of materiales){const k=normalizarParte(m.numero_parte);porParte.set(k,[...(porParte.get(k)??[]),m])}
 const campos=(Object.keys(etiquetasCampo) as Campo[]).filter(c=>encabezado.columnas[c]!==undefined);
 const resultado:FilaPlan[]=[];const vistas=new Map<string,FilaPlan>();let repetidasIguales=0;
 filas.slice(encabezado.fila+1).forEach((celdas,i)=>{
  const fila=encabezado.fila+i+2;
  const parte=normalizarParte(celdas[encabezado.columnas.parte!]);
  if(!parte)return;
  const candidatos=porParte.get(parte)??[];
  if(candidatos.length!==1){resultado.push({fila,parte,estado:candidatos.length?'ERROR':'NO_ENCONTRADO',detalle:candidatos.length?'el número de parte coincide con más de un material':'no existe en el catálogo del portal',cambios:[]});return}
  const material=candidatos[0];const errores:string[]=[];const cambios:CambioMaterial[]=[];
  for(const campo of campos){
   const celda=celdas[encabezado.columnas[campo]!];
   if(!textoCelda(celda))continue;
   const r=valorArchivo(campo,celda,colores);
   if('error' in r){errores.push(r.error);continue}
   const anterior=valorActual(material,campo);
   if(r.valor!==anterior)cambios.push({parte:material.numero_parte,campo,anterior,nuevo:r.valor});
  }
  const previa=vistas.get(material.numero_parte);
  if(previa){
   // Celdas combinadas o filas copiadas: si repite exactamente lo mismo, se ignora en silencio.
   const igual=JSON.stringify(previa.cambios)===JSON.stringify(cambios)&&!errores.length;
   if(igual){repetidasIguales++;return}
   if(previa.estado!=='ERROR'){previa.estado='ERROR';previa.detalle='el material aparece repetido con datos distintos';previa.cambios=[]}
   resultado.push({fila,parte,estado:'REPETIDA',detalle:`repite la fila ${previa.fila} con datos distintos; no se aplica ninguna`,cambios:[]});
   return;
  }
  const plan:FilaPlan=errores.length?{fila,parte,estado:'ERROR',detalle:errores.join('; '),cambios:[]}
   :{fila,parte,estado:cambios.length?'CAMBIA':'SIN_CAMBIOS',detalle:cambios.length?`${cambios.length} ${cambios.length===1?'cambio':'cambios'}`:'ya coincide con el portal',cambios};
  vistas.set(material.numero_parte,plan);resultado.push(plan);
 });
 const cambios=resultado.flatMap(f=>f.estado==='CAMBIA'?f.cambios:[]);
 const cuenta=(e:EstadoFila)=>resultado.filter(f=>f.estado===e).length;
 return {encabezado:encabezado.fila+1,columnas:encabezado.columnas,filas:resultado,cambios,resumen:{
  materialesConCambios:cuenta('CAMBIA'),cambios:cambios.length,sinCambios:cuenta('SIN_CAMBIOS'),
  noEncontrados:cuenta('NO_ENCONTRADO'),errores:cuenta('ERROR'),repetidas:cuenta('REPETIDA'),repetidasIguales}};
}

/** CSV simple (por ejemplo, exportado de SAP): detecta coma o punto y coma y respeta comillas. */
export function leerCsv(texto:string):string[][]{
 const limpio=texto.replace(/^﻿/,'');
 const primera=limpio.split(/\r?\n/,1)[0]??'';
 const sep=(primera.match(/;/g)?.length??0)>(primera.match(/,/g)?.length??0)?';':(primera.includes('\t')&&!primera.includes(',')?'\t':',');
 const filas:string[][]=[];let fila:string[]=[];let campo='';let comillas=false;
 for(let i=0;i<limpio.length;i++){
  const c=limpio[i];
  if(comillas){if(c==='"'){if(limpio[i+1]==='"'){campo+='"';i++}else comillas=false}else campo+=c;continue}
  if(c==='"')comillas=true;
  else if(c===sep){fila.push(campo);campo=''}
  else if(c==='\n'||c==='\r'){if(c==='\r'&&limpio[i+1]==='\n')i++;fila.push(campo);filas.push(fila);fila=[];campo=''}
  else campo+=c;
 }
 if(campo||fila.length){fila.push(campo);filas.push(fila)}
 return filas;
}
