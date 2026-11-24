import type {Material,Item} from './adminTypes';
export type CeldaV2=string|number|null;
export const camposV2=['numeroParte','descripcion','variante','revision','modelo','uso','puesto','empaque','minimo'] as const;
export type CampoV2=typeof camposV2[number];
export const titulosV2:Record<CampoV2,string>={numeroParte:'Número de parte',descripcion:'Descripción',variante:'Variante / color',revision:'Revisión',modelo:'Modelo / vehículo',uso:'Uso PFEP',puesto:'Puesto',empaque:'Piezas por caja',minimo:'Mínimo en piezas'};
const normal=(s:string)=>s.normalize('NFD').replace(/[\u0300-\u036f]/g,'').toUpperCase().replace(/[^A-Z0-9]/g,'');
const alias:Record<CampoV2,string[]>={numeroParte:['NUMEROPARTE','NUMERODEPARTE','NPARTE','PARTE','PARTNUMBER','MATERIAL','CODIGO'],descripcion:['DESCRIPCION','DESCRIPTION','DENOMINACION'],variante:['VARIANTE','COLOR','VARIANTECOLOR'],revision:['REVISION','REV'],modelo:['MODELO','VEHICULO','AUTO','MODELOVEHICULO'],uso:['USO','USOPFEP','UTILIZACION'],puesto:['PUESTO','PUESTOLOGISTICO','SURTIDOR'],empaque:['PIEZASPORCAJA','PZCAJA','CANTIDADXCAJA','CANTIDADPORCAJA','EMPAQUE'],minimo:['MINIMO','MINIMOPZ','MINIMOENPIEZAS','MINIMOPIEZAS']};
export function sugerirColumnas(row:CeldaV2[]){const m:Partial<Record<CampoV2,number>>={};for(const campo of camposV2){const matches=row.map((v,i)=>alias[campo].includes(normal(String(v??'')))?i:-1).filter(i=>i>=0);if(matches.length===1)m[campo]=matches[0]}return m}
export type FilaV2={fila:number;parte:string;estado:'ALTA'|'CAMBIO'|'SIN_CAMBIOS'|'ERROR';errores:string[];avisos:string[];datos:Record<string,unknown>;cambios:string[]};
export function planV2(rows:CeldaV2[][],header:number,map:Partial<Record<CampoV2,number>>,materiales:Material[],puestos:Item[],crearPuestos=false):FilaV2[]{
 if(map.numeroParte===undefined)throw new Error('Mapea la columna de número de parte.');
 const indices=Object.values(map);if(new Set(indices).size!==indices.length)throw new Error('Una columna no puede representar dos campos.');
 const out:FilaV2[]=[];const seen=new Map<string,FilaV2>();
 rows.slice(header+1).forEach((r,i)=>{
  if(!r.some(c=>c!==null&&String(c).trim()!==''))return;
  const raw=r[map.numeroParte!];const parte=String(raw??'').trim().normalize();const old=materiales.find(m=>m.numeroParte===parte);
  const f:FilaV2={fila:header+i+2,parte,estado:old?'CAMBIO':'ALTA',errores:[],avisos:[],datos:{numeroParte:parte,...(old?{id:old.id}:{})},cambios:[]};
  if(parte.startsWith('[FORMULA'))f.errores.push('Número de parte: reemplaza la fórmula por un valor de texto.');
  if(!parte||parte.length>80)f.errores.push('Número de parte requerido, hasta 80 caracteres.');
  if(typeof raw==='number'&&(!Number.isSafeInteger(raw)||String(raw).length>15))f.errores.push('Número de parte numérico inseguro: guárdalo como texto en el archivo.');
  const cercana=materiales.find(m=>m.numeroParte!==parte&&normal(m.numeroParte)===normal(parte));if(cercana)f.avisos.push('Código similar a '+cercana.numeroParte+'. Se tratará como un código distinto; no se fusionará.');
  for(const campo of camposV2.filter(x=>x!=='numeroParte')){
   const col=map[campo];if(col===undefined)continue;const raw=r[col];if(raw===null||raw===undefined||String(raw).trim()==='')continue;
   if(String(raw).startsWith('[FORMULA')){f.errores.push(titulosV2[campo]+': sustituye la fórmula por un valor (columna '+(col+1)+').');continue}
   let value:unknown=String(raw).trim();
   if(value==='[BORRAR]'){
    if(['descripcion','variante','revision','modelo','uso'].includes(campo))value='';else{f.errores.push(titulosV2[campo]+': no se puede borrar.');continue}
   }
   if(campo==='empaque'||campo==='minimo'){const n=typeof raw==='number'?raw:Number(value);if(!Number.isInteger(n)||n<(campo==='empaque'?1:0)||n>(campo==='empaque'?1000000:100000000)){f.errores.push(titulosV2[campo]+': entero fuera de rango (columna '+(col+1)+').');continue}value=n;}
   else if(String(value).length>(campo==='descripcion'?250:80))f.errores.push(titulosV2[campo]+': texto demasiado largo (columna '+(col+1)+').');
   if(campo==='puesto'){
    const porId=puestos.find(p=>p.id===value);const candidates=porId?[porId]:puestos.filter(p=>p.nombre===value);
    if(candidates.length>1){f.errores.push('Puesto ambiguo: '+value+'. Selecciona su identificador exacto.');continue}
    if(!candidates.length){
     if(!crearPuestos){f.errores.push('El puesto '+value+' no existe en esta línea. Activa Crear puestos faltantes o créalo en Configurar línea.');continue}
     f.datos.puestoNuevo=value;f.cambios.push('Crear puesto en esta línea: '+value);continue;
    }value=candidates[0].id;
   }
   f.datos[campo]=value;if(!old||old[campo]!==value)f.cambios.push(titulosV2[campo]+': '+(old?String(old[campo]):'nuevo')+' → '+String(value));
   if(['variante','revision','modelo','uso'].includes(campo)&&value!==''&&!materiales.some(m=>m[campo]===value))f.avisos.push('Nuevo valor de '+titulosV2[campo]+': '+value);
  }
  if(!old&&!f.datos.empaque)f.errores.push('Material nuevo: indica las piezas por caja.');
  if(!old&&!f.datos.puesto&&!f.datos.puestoNuevo)f.errores.push('Material nuevo: indica el puesto en la columna Puesto.');
  if(old&&!f.cambios.length)f.estado='SIN_CAMBIOS';
  const duplicate=seen.get(parte);if(duplicate){duplicate.errores.push('Código repetido en fila '+f.fila);duplicate.estado='ERROR';f.errores.push('Código repetido en fila '+duplicate.fila);}else seen.set(parte,f);
  if(f.errores.length)f.estado='ERROR';out.push(f);
 });
 if(out.length>500)throw new Error('Máximo 500 filas por importación transaccional.');return out;
}
export function leerCsvV2(texto:string):CeldaV2[][]{
 const limpio=texto.replace(/^\uFEFF/,'');
 function parse(sep:string){const rows:string[][]=[];let row:string[]=[],field='',quoted=false,closed=false;
  const cell=()=>{row.push(field);field='';closed=false;if(row.length>100)throw new Error('CSV: máximo 100 columnas.');};
  const line=()=>{cell();rows.push(row);row=[];if(rows.length>2001)throw new Error('CSV: máximo 2001 filas de origen; divide el archivo.');};
  for(let i=0;i<limpio.length;i++){const c=limpio[i];
   if(quoted){if(c==='"'){if(limpio[i+1]==='"'){field+='"';i++}else{quoted=false;closed=true}}else field+=c;continue}
   if(c===sep){cell();continue}if(c==='\r'||c==='\n'){if(c==='\r'&&limpio[i+1]==='\n')i++;line();continue}
   if(c==='"'){if(field!==''||closed)throw new Error('CSV: comillas inválidas en fila '+(rows.length+1)+', columna '+(row.length+1)+'.');quoted=true;continue}
   if(closed&&c.trim()!=='')throw new Error('CSV: texto fuera de comillas en fila '+(rows.length+1)+'.');if(!closed)field+=c;
  }
  if(quoted)throw new Error('CSV: comillas sin cerrar en fila '+(rows.length+1)+', columna '+(row.length+1)+'.');if(field||row.length||closed)line();return rows;
 }
 const candidates:{rows:string[][];score:number;width:number}[]=[];let error='CSV inválido.';
 for(const sep of [',',';','\t'])try{const rows=parse(sep);candidates.push({rows,score:Math.max(0,...rows.slice(0,30).map(r=>Object.keys(sugerirColumnas(r)).length)),width:Math.max(0,...rows.slice(0,30).map(r=>r.length))})}catch(ex){const mensaje=(ex as Error).message;if(error==='CSV inválido.'||mensaje.includes('sin cerrar'))error=mensaje}
 if(!candidates.length)throw new Error(error);return candidates.sort((a,b)=>b.score-a.score||b.width-a.width)[0].rows;
}
