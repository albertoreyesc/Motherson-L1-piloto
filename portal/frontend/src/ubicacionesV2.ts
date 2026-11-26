import type {EstadoAdmin,Material,Ubicacion} from './adminTypes';

/** Las ubicaciones anteriores a esta versión no traen el campo: se consideran activas. */
export const ubicacionActiva=(u:Pick<Ubicacion,'activa'>)=>u.activa!==false;
/** Puestos, estaciones y estanterías anteriores a esta versión no traen el campo: se consideran activos. */
export const estaActivo=(x:{activo?:boolean}|undefined)=>!!x&&x.activo!==false;

type Estructura=Pick<EstadoAdmin,'puestos'|'estaciones'|'estanterias'|'materiales'|'ubicaciones'|'atenciones'>;
/**
 * ¿Se puede desactivar este puesto, estación o estantería? Replica las reglas del servidor (PUESTO_ESTADO,
 * ESTACION_ESTADO, ESTANTERIA_ESTADO) para explicar antes de enviar. Devuelve '' si se puede. Las cuentas de
 * usuario ligadas a un puesto solo las revisa el servidor.
 */
export function motivoNoDesactivar(e:Estructura,tipo:'PUESTO'|'ESTACION'|'ESTANTERIA',id:string):string{
 if(tipo==='PUESTO'){
  const n=e.materiales.filter(m=>m.puesto===id&&m.activo).length;
  if(n)return 'Tiene '+n+(n===1?' material activo. Inhabilítalo':' materiales activos. Inhabilítalos')+' en Materiales y catálogos antes de desactivar el puesto.';
  if(e.atenciones.some(a=>a.puesto===id&&!a.fin))return 'Tiene una atención abierta. Finalízala antes de desactivar el puesto.';
  return '';
 }
 if(tipo==='ESTACION'){
  const n=e.estanterias.filter(r=>r.estacion===id&&estaActivo(r)).length;
  return n?'Tiene '+n+(n===1?' estantería activa. Desactívala':' estanterías activas. Desactívalas')+' primero en la pestaña Estanterías.':'';
 }
 const us=e.ubicaciones.filter(u=>u.estanteria===id);const piezas=us.reduce((n,u)=>n+u.cantidad,0);
 if(piezas)return 'Tiene '+piezas+' piezas. Muévelas en Estaciones y ubicaciones o retíralas con una salida antes de desactivarla.';
 if(e.atenciones.some(a=>!a.fin&&us.some(u=>u.id===a.ubicacion)))return 'Hay una atención abierta en esta estantería. Finalízala antes de desactivarla.';
 return '';
}

export type PlanDesactivar={error:string}|{error?:undefined;piezas:number;cajas:number;destino:'NINGUNO'|'NUEVA'|'EXISTENTE'|'REACTIVAR'};

/**
 * Vista previa de UBICACION_ESTADO (desactivar). Replica las reglas del servidor para avisar antes de enviar;
 * el servidor vuelve a validar todo.
 */
export function planDesactivar(e:Pick<EstadoAdmin,'ubicaciones'|'materiales'|'estanterias'|'atenciones'>,ubicacion:string,destino:string):PlanDesactivar{
 const u=e.ubicaciones.find(x=>x.id===ubicacion);if(!u)return {error:'Ubicación no encontrada.'};
 const m=e.materiales.find(x=>x.id===u.material);if(!m)return {error:'Material no encontrado.'};
 if(!ubicacionActiva(u))return {error:'La ubicación ya está desactivada.'};
 if(e.atenciones.some(a=>a.ubicacion===u.id&&!a.fin))return {error:'Hay una atención abierta en esta ubicación. Finalízala antes de desactivarla.'};
 if(!destino)return u.cantidad>0?{error:'La ubicación tiene '+u.cantidad+' piezas: elige la estantería destino para trasladarlas o retíralas antes con una salida.'}:{piezas:0,cajas:0,destino:'NINGUNO'};
 if(destino===u.estanteria)return {error:'La estantería destino debe ser distinta de la actual.'};
 const rack=e.estanterias.find(r=>r.id===destino);if(!rack)return {error:'Estantería destino no encontrada.'};
 if(!estaActivo(rack))return {error:'La estantería destino está desactivada.'};
 const existente=e.ubicaciones.find(x=>x.material===m.id&&x.estanteria===destino);
 if(!existente&&!m.activo)return {error:'El material está inhabilitado: no se crean ubicaciones nuevas; retira su remanente con una salida.'};
 // Ocupación en cajas como el servidor: cada ubicación ocupa ceil(saldo/empaque) cajas.
 const cajasDe=(x:Ubicacion)=>{const mm=e.materiales.find(y=>y.id===x.material);return mm?Math.ceil(x.cantidad/mm.empaque):0};
 const ocupadas=e.ubicaciones.filter(x=>x.estanteria===destino&&x.id!==existente?.id).reduce((n,x)=>n+cajasDe(x),0);
 const cajasDestino=Math.ceil(((existente?.cantidad??0)+u.cantidad)/m.empaque);
 if(ocupadas+cajasDestino>rack.capacidad)return {error:'La estantería destino no tiene espacio para '+u.cantidad+' piezas.'};
 return {piezas:u.cantidad,cajas:Math.ceil(u.cantidad/m.empaque),destino:!existente?'NUEVA':ubicacionActiva(existente)?'EXISTENTE':'REACTIVAR'};
}

/** Sugiere la siguiente revisión: B→C, Z→AA, 2→3, REV2→REV3, A1→A2. Vacía si no hay un patrón claro. */
export function siguienteRevision(rev:string){
 const r=rev.trim();if(!r)return '';
 const num=r.match(/^(.*?)(\d+)$/);if(num)return num[1]+String(Number(num[2])+1).padStart(num[2].length,'0');
 if(/^[A-Z]+$/.test(r)){const chars=r.split('');let i=chars.length-1;while(i>=0&&chars[i]==='Z'){chars[i]='A';i--}if(i<0)return 'A'+chars.join('');chars[i]=String.fromCharCode(chars[i].charCodeAt(0)+1);return chars.join('')}
 return '';
}

export type Duplicado={numeroParte:string;descripcion:string;variante:string;revision:string;modelo:string;uso:string;empaque:number;minimo:number};
export const camposDuplicado:(keyof Duplicado)[]=['numeroParte','descripcion','variante','revision','modelo','uso','empaque','minimo'];

/** Valida la nueva revisión en el cliente y devuelve los campos que cambian respecto al original. */
export function revisarDuplicado(origen:Material,d:Duplicado,materiales:Material[]){
 const errores:string[]=[];const numero=d.numeroParte.trim();
 if(!numero)errores.push('Indica el número de parte nuevo.');
 else if(numero.length>80)errores.push('El número de parte admite hasta 80 caracteres.');
 else if(materiales.some(m=>m.numeroParte===numero))errores.push('Ese número de parte ya existe.');
 if(!Number.isSafeInteger(d.empaque)||d.empaque<1||d.empaque>1000000)errores.push('Piezas por caja: entero entre 1 y 1,000,000.');
 if(!Number.isSafeInteger(d.minimo)||d.minimo<0||d.minimo>100000000)errores.push('Mínimo: entero mayor o igual a cero.');
 if(d.descripcion.length>250)errores.push('La descripción admite hasta 250 caracteres.');
 for(const c of ['variante','revision','modelo','uso'] as const)if(d[c].length>80)errores.push('Revisa '+c+' (máximo 80 caracteres).');
 const cambios=camposDuplicado.filter(c=>String(d[c]).trim()!==String(origen[c]??'').trim()).map(c=>({campo:c,antes:String(origen[c]??''),despues:String(d[c]).trim()}));
 return {errores,cambios};
}

/** Cajas ocupadas en una estantería: cada ubicación ocupa ceil(saldo/empaque) cajas, igual que el servidor. */
export function cajasOcupadasV2(e:Pick<EstadoAdmin,'ubicaciones'|'materiales'>,estanteria:string){
 return e.ubicaciones.filter(u=>u.estanteria===estanteria).reduce((n,u)=>{const m=e.materiales.find(x=>x.id===u.material);return n+(m?Math.ceil(u.cantidad/m.empaque):0)},0);
}

/** Límites del + / − de capacidad: nunca menos que lo ocupado ni menos de 1; máximo el del servidor. */
export function limitesCapacidadV2(e:Pick<EstadoAdmin,'ubicaciones'|'materiales'>,estanteria:string){
 return {minimo:Math.max(1,cajasOcupadasV2(e,estanteria)),maximo:1000000};
}

/** Materiales distintos que comparten la estantería (para avisar que la capacidad es común). */
export function materialesEnEstanteria(e:Pick<EstadoAdmin,'ubicaciones'>,estanteria:string){
 return new Set(e.ubicaciones.filter(u=>u.estanteria===estanteria&&ubicacionActiva(u)).map(u=>u.material)).size;
}
