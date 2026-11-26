// Aviso de vigencia del horario de trabajo de la línea. Sin horario vigente las alarmas de cobertura pasan a
// "Sin estimación" sin que nada truene, así que se avisa antes de que venza y cuando ya venció.
import {CalendarClock} from 'lucide-react';
import {fechaMexico} from './AdminKpis';

export type VigenciaHorario={nivel:'ok'|'pronto'|'vencido'|'sin'|'futuro';dias:number;desde:string;hasta:string};
/** Días de anticipación con los que se avisa que el horario está por vencer. */
export const DIAS_AVISO_HORARIO=15;
const dia=(s:string)=>Date.parse(s+'T12:00:00Z');

/** Vigencia del horario a la fecha de hoy en Ciudad de México. "Hasta" es inclusivo. */
export function vigenciaHorario(cal:Record<string,unknown>|null|undefined,hoy=fechaMexico()):VigenciaHorario{
 const desde=String(cal?.desde??''),hasta=String(cal?.hasta??'');
 if(!cal||!desde||!hasta)return {nivel:'sin',dias:0,desde,hasta};
 const dias=Math.round((dia(hasta)-dia(hoy))/86400000);
 if(hoy<desde)return {nivel:'futuro',dias,desde,hasta};
 if(dias<0)return {nivel:'vencido',dias,desde,hasta};
 return {nivel:dias<=DIAS_AVISO_HORARIO?'pronto':'ok',dias,desde,hasta};
}
const legible=(d:string)=>new Date(d+'T12:00:00').toLocaleDateString('es-MX',{day:'numeric',month:'long',year:'numeric'});

export function AvisoHorario({cal,linea,admin,configurar}:{cal:Record<string,unknown>|null;linea:number;admin:boolean;configurar?:()=>void}){
 const v=vigenciaHorario(cal);if(v.nivel==='ok')return null;
 const titulo=v.nivel==='sin'?'La Línea '+linea+' no tiene horario de trabajo':v.nivel==='vencido'?'El horario de la Línea '+linea+' venció el '+legible(v.hasta):v.nivel==='futuro'?'El horario de la Línea '+linea+' empieza hasta el '+legible(v.desde):v.dias===0?'El horario de la Línea '+linea+' vence hoy':'El horario de la Línea '+linea+' vence en '+v.dias+(v.dias===1?' día':' días')+' ('+legible(v.hasta)+')';
 const texto=v.nivel==='pronto'?'Renuévalo para que las alarmas de cobertura sigan calculando.':'Mientras tanto las alarmas de cobertura muestran “Sin estimación” (el saldo en cero sí se marca como Agotado). Entradas, salidas e inventario funcionan normal.';
 return <div className={'aviso-horario '+(v.nivel==='pronto'?'amarillo':'rojo')} role={v.nivel==='pronto'?'status':'alert'}>
  <CalendarClock size={24} aria-hidden="true"/>
  <div><strong>{titulo}</strong><span>{texto} {!admin&&'Avisa a administración.'}</span></div>
  {admin&&configurar&&<button className="secondary small" onClick={configurar}>{v.nivel==='sin'?'Capturar horario':'Renovar horario'}</button>}
 </div>;
}
