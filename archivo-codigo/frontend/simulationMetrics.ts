import type {Row} from './api';
const time=(s:unknown)=>{if(!s)return NaN;const text=String(s);return Date.parse(/(?:Z|[+-]\d\d:\d\d)$/i.test(text)?text:text+'Z')};
export function duration(start:unknown,end:unknown):number|null{const delta=(time(end)-time(start))/60_000;return Number.isFinite(delta)&&delta>=0?delta:null}
export function orderTimes(events:Row[],order:string,clock:string){
 const rows=events.filter(e=>e.OrdenId===order&&e.Estado==='APLICADO');
 const at=(type:string)=>rows.find(e=>e.Tipo===type)?.VirtualUtc;
 const request=at('SOLICITUD'),accepted=at('ACEPTACION'),transfer=at('TRASLADO'),delivery=at('ENTRADA_AUTO')??at('ENTRADA_MANUAL'),closed=at('CIERRE_ORDEN'),shortage=at('DESABASTO'),recovery=at('RECUPERACION');
 return {response:duration(request,accepted),preparation:duration(accepted,transfer),transport:duration(transfer,delivery),total:duration(request,closed),shortage:duration(shortage,recovery),recovery:duration(at('ALERTA'),recovery),open:closed?null:duration(request,clock)};
}
