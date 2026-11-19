import type {Row,User} from './api';
export type EntryAttempt={row:Row;boxes:number;version:string;key:string;rack:string;selected:string;uncertain?:boolean};
export const entryKey=(r:Row)=>String(r.entryKey??r.Clave??r.AsignacionId);
export const entryShelf=(r:Row)=>[r.Estacion??'Línea 1',r.Estanteria??r.Ubicacion,r.Puesto].filter(Boolean).join(' · ');
export function entrySelection(rows:Row[],rack:string,selected:string,pending:EntryAttempt|null){
 const racks=[...new Set(rows.map(entryShelf))];
 const activeRack=pending?.rack??(racks.includes(rack)?rack:racks.length===1?racks[0]:'');
 const materials=rows.filter(r=>entryShelf(r)===activeRack);
 const row=pending?.row??materials.find(r=>entryKey(r)===selected)??(materials.length===1?materials[0]:undefined);
 return {racks,activeRack,materials,row};
}
export function restoreEntry(key?:string){
 try{const v=key?JSON.parse(sessionStorage.getItem(key)??'null'):null;const p=v?.schema===2?v.pending:v?.key?v:null;
 return {rack:String(v?.rack??v?.context?.rack??p?.rack??''),selected:String(v?.selected??v?.context?.selected??p?.selected??''),pending:p?.row&&typeof p.key==='string'&&typeof p.version==='string'&&Number.isSafeInteger(p.boxes)&&p.boxes>0?p as EntryAttempt:null};
 }catch{return {rack:'',selected:'',pending:null};}
}
export function persistEntry(key:string|undefined,rack:string,selected:string,pending:EntryAttempt|null){
 try{if(key)sessionStorage.setItem(key,JSON.stringify({schema:2,context:{rack,selected},pending}));return true;}catch{return false;}
}
export function entryAmounts(row:Row|undefined,boxes:number,simulation=false){
 const factor=Number(row?.Factor??row?.PiezasPorCaja??0),stock=row?(simulation?row.Piezas:row.SaldoRegistradoPiezas):null;
 const capacity=row?(simulation?row.CapacidadCajas*factor:row.CapacidadNominalPiezas):null;
 const count=row?.SolicitudId?Number(row.Cajas):boxes,pieces=count*factor,after=stock==null?null:Number(stock)+pieces;
 const freeBoxes=row?.CajasDisponibles==null?null:Number(row.CajasDisponibles)+(row.SolicitudId?count:0);
 const fits=after!=null&&capacity!=null&&after<=capacity&&(freeBoxes==null||count<=freeBoxes);
 const valid=!!row&&Number.isSafeInteger(count)&&count>0&&count<=1000000&&Number.isSafeInteger(factor)&&factor>0&&Number.isSafeInteger(pieces)&&Number.isSafeInteger(after)&&fits;
 return {factor,stock,capacity,count,pieces,after,freeBoxes,fits,valid};
}
export const annualLanding=(user:User|null)=>!!user?.simulado&&user.permissions.some(p=>['admin','supervise','catalog','assign'].includes(p));
export const retryMustKeepIdentity=(status?:number,wasUncertain=false)=>!status||status>=500||status===408||status===429||(wasUncertain&&(status===401||status===403));

export function entryAvailability(row:Row,simulation=false){
 const a=entryAmounts(row,1,simulation),factor=a.factor;
 const physical=factor>0&&a.stock!=null&&a.capacity!=null?Math.max(0,Math.floor((a.capacity-Number(a.stock))/factor)):0;
 const free=Math.max(0,Math.min(a.freeBoxes??physical,physical));
 const canReceive=row.SolicitudId?a.valid:free>=1&&a.valid;
 const reserved=Number(row.CajasReservadas??0);
 const label=canReceive?(row.SolicitudId?'Entrega reservada disponible':`Admite ${free} ${free===1?'caja':'cajas'}`):reserved>0?'Espacio reservado':'Sin espacio para otra caja completa';
 const reason=a.stock==null?'Falta un conteo validado para esta posición.':a.capacity==null||factor<=0?'Falta validar el empaque o la capacidad.':reserved>0&&!canReceive?`${reserved} ${reserved===1?'caja está reservada':'cajas están reservadas'} para solicitudes pendientes. Confirma la entrega vinculada a su reserva.`:!canReceive?`Esta posición admite ${Number(row.CapacidadCajas??a.capacity/factor)} ${Number(row.CapacidadCajas??a.capacity/factor)===1?'caja':'cajas'} de ${factor} piezas. Conserva ${a.stock} piezas y todavía no puede recibir otra caja completa.`:`${free} ${free===1?'caja disponible':'cajas disponibles'} considerando ocupación y reservas.`;
 return {free,canReceive,reserved,label,reason};
}
export function availableEntries(rows:Row[],simulation=false){
 return [...rows].sort((a,b)=>{const x=entryAvailability(a,simulation),y=entryAvailability(b,simulation);return Number(y.canReceive)-Number(x.canReceive)||y.free-x.free||String(a.Parte).localeCompare(String(b.Parte))||entryKey(a).localeCompare(entryKey(b))});
}
export function rackAvailability(rows:Row[],simulation=false){
 const positions=new Map<string,boolean>(),reservedPositions=new Set<string>();let reservations=0;
 for(const row of rows){const key=String(row.Clave??row.AsignacionId);positions.set(key,(positions.get(key)??false)||entryAvailability(row,simulation).canReceive);if(Number(row.CajasReservadas??0)>0)reservedPositions.add(key);if(row.SolicitudId&&entryAvailability(row,simulation).canReceive)reservations++}
 return {total:positions.size,available:[...positions.values()].filter(Boolean).length,reservations,reservedPositions:reservedPositions.size};
}

export function rackAvailabilityLabel(rows:Row[],simulation=false){
 const r=rackAvailability(rows,simulation);
 if(r.total===1){const selected=availableEntries(rows,simulation).find(row=>entryAvailability(row,simulation).canReceive)??rows[0];return selected?entryAvailability(selected,simulation).label:'Sin espacio para otra caja completa'}
 return r.available>0?`${r.available} materiales con entrada disponible`:r.reservedPositions>0?'Espacio reservado':'Sin espacio para otra caja completa';
}
