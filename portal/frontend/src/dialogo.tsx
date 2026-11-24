import {useEffect,useState} from 'react';

/** Diálogo propio del portal en lugar de window.prompt/confirm/alert: legible y táctil en tabletas. */
type Opciones={titulo:string;mensaje?:string;campo?:{etiqueta:string;valor?:string;placeholder?:string;sugerencias?:string[]};aceptar?:string;cancelar?:string|null;peligro?:boolean};
type Pendiente=Opciones&{resolver:(valor:string|null)=>void};
let abrir:((p:Pendiente)=>void)|null=null;

function mostrar(op:Opciones){
 return new Promise<string|null>(resolver=>{
  if(!abrir){ // Respaldo si el diálogo no está montado.
   if(op.campo){resolver(window.prompt(op.titulo,op.campo.valor??''));return}
   if(op.cancelar===null){window.alert(op.titulo+(op.mensaje?'\n\n'+op.mensaje:''));resolver('');return}
   resolver(window.confirm(op.titulo+(op.mensaje?'\n\n'+op.mensaje:''))?'':null);return;
  }
  abrir({...op,resolver});
 });
}
/** Pide un texto obligatorio. Devuelve null si se cancela. */
export const pedirTexto=(op:Opciones&{campo:NonNullable<Opciones['campo']>})=>mostrar(op);
/** Pide confirmación. Devuelve true si se acepta. */
export const confirmar=async(op:Omit<Opciones,'campo'>)=>(await mostrar(op))!==null;
/** Muestra un aviso con un solo botón. */
export const avisar=async(titulo:string,mensaje?:string)=>{await mostrar({titulo,mensaje,cancelar:null,aceptar:'Entendido'})};

export function DialogoHost(){
 const [p,setP]=useState<Pendiente|null>(null),[valor,setValor]=useState('');
 useEffect(()=>{abrir=x=>{setValor(x.campo?.valor??'');setP(x)};return()=>{abrir=null}},[]);
 useEffect(()=>{if(!p)return;const tecla=(ev:KeyboardEvent)=>{if(ev.key==='Escape'&&p.cancelar!==null){p.resolver(null);setP(null)}};window.addEventListener('keydown',tecla);return()=>window.removeEventListener('keydown',tecla)},[p]);
 if(!p)return null;
 const cerrar=(r:string|null)=>{p.resolver(r);setP(null)};
 const listo=!p.campo||valor.trim().length>0;
 const aceptar=()=>{if(listo)cerrar(p.campo?valor.trim():'')};
 return <div className="dialogo-fondo" role="presentation" onClick={ev=>{if(ev.target===ev.currentTarget&&p.cancelar!==null)cerrar(null)}}>
  <div className="dialogo" role={p.cancelar===null?'alertdialog':'dialog'} aria-modal="true" aria-labelledby="dialogo-titulo">
   <h3 id="dialogo-titulo">{p.titulo}</h3>
   {p.mensaje&&<p>{p.mensaje}</p>}
   {p.campo&&<form onSubmit={ev=>{ev.preventDefault();aceptar()}}>
    <label className="field">{p.campo.etiqueta}<input autoFocus value={valor} maxLength={250} placeholder={p.campo.placeholder} onChange={ev=>setValor(ev.target.value)}/></label>
    {p.campo.sugerencias&&p.campo.sugerencias.length>0&&<div className="dialogo-sugerencias" role="group" aria-label="Motivos frecuentes">{p.campo.sugerencias.map(s=><button type="button" key={s} className={'secondary'+(valor===s?' elegido':'')} aria-pressed={valor===s} onClick={()=>setValor(s)}>{s}</button>)}</div>}
   </form>}
   <div className="dialogo-botones">{p.cancelar!==null&&<button className="secondary" onClick={()=>cerrar(null)}>{p.cancelar??'Cancelar'}</button>}<button className={p.peligro?'peligro':''} disabled={!listo} onClick={aceptar} autoFocus={!p.campo}>{p.aceptar??'Aceptar'}</button></div>
  </div>
 </div>;
}
