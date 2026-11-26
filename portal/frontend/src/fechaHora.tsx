import {CalendarDays,Clock} from 'lucide-react';

/** Fecha y hora actuales de Ciudad de México como 'YYYY-MM-DDTHH:MM'. */
export function ahoraCdmx(desplazamientoMin=0){
 const d=new Date(Date.now()+desplazamientoMin*60000);
 const p=new Intl.DateTimeFormat('en-CA',{timeZone:'America/Mexico_City',year:'numeric',month:'2-digit',day:'2-digit',hour:'2-digit',minute:'2-digit',hourCycle:'h23'}).formatToParts(d);
 const v=(t:string)=>p.find(x=>x.type===t)?.value??'00';
 return `${v('year')}-${v('month')}-${v('day')}T${v('hour')}:${v('minute')}`;
}
const diaCdmx=(dias:number)=>ahoraCdmx(dias*1440).slice(0,10);

/** Último tipo de puntero usado (mouse, dedo o lápiz). */
let ultimoPuntero='mouse';
if(typeof document!=='undefined')document.addEventListener('pointerdown',ev=>{ultimoPuntero=ev.pointerType||'mouse'},{capture:true,passive:true});
/**
 * Con el dedo (tableta) abre el calendario o el reloj al tocar el campo. Con mouse y teclado no lo abre: así se puede
 * escribir la fecha directamente; el calendario sigue disponible con el ícono del propio campo.
 */
export function abrirSelector(ev:{currentTarget:HTMLInputElement}){if(ultimoPuntero!=='touch'&&ultimoPuntero!=='pen')return;try{ev.currentTarget.showPicker?.()}catch{/* algunos navegadores no lo permiten; el campo sigue editable */}}

/**
 * Selector de fecha y hora pensado para tableta: fecha y hora en campos separados (sin segundos),
 * que se abren al tocarlos, y atajos frecuentes. Valor: 'YYYY-MM-DDTHH:MM' (hora de Ciudad de México).
 */
export function FechaHora({valor,cambiar,min,max,etiqueta='Fecha y hora (Ciudad de México)',atajos=true,deshabilitado=false}:{valor:string;cambiar:(v:string)=>void;min?:string;max?:string;etiqueta?:string;atajos?:boolean;deshabilitado?:boolean}){
 const fecha=valor.slice(0,10),hora=valor.slice(11,16);
 const opciones=[{n:'Ahora',v:()=>ahoraCdmx()},{n:'Hace 1 hora',v:()=>ahoraCdmx(-60)},{n:'Hoy 6:00',v:()=>diaCdmx(0)+'T06:00'},{n:'Ayer 15:00',v:()=>diaCdmx(-1)+'T15:00'}];
 return <fieldset className="fecha-hora" disabled={deshabilitado}>
  <legend>{etiqueta}</legend>
  <div className="fecha-hora-campos">
   <label className="campo-icono"><CalendarDays size={18} aria-hidden="true"/><span className="sr-only">Fecha</span><input type="date" value={fecha} min={min?.slice(0,10)} max={max?.slice(0,10)} required onClick={abrirSelector} onChange={ev=>cambiar(ev.target.value+'T'+(hora||'06:00'))}/></label>
   <label className="campo-icono"><Clock size={18} aria-hidden="true"/><span className="sr-only">Hora</span><input type="time" step="60" value={hora} required onClick={abrirSelector} onChange={ev=>cambiar((fecha||diaCdmx(0))+'T'+ev.target.value)}/></label>
  </div>
  {atajos&&<div className="fecha-atajos" role="group" aria-label="Atajos de fecha y hora">{opciones.map(o=>{const v=o.v();const fuera=(min&&v<min.slice(0,16))||(max&&v>max.slice(0,16));return <button type="button" key={o.n} className={'secondary'+(valor.slice(0,16)===v?' elegido':'')} disabled={!!fuera} onClick={()=>cambiar(v)}>{o.n}</button>})}</div>}
 </fieldset>;
}
