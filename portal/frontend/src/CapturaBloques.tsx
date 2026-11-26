// Entradas y salidas por bloques. Reglas para planta:
// - Solo se puede abrir una tarjeta si la operación es posible (espacio para entrada, saldo para salida).
// - Al registrar se muestra una confirmación clara con el saldo antes y después.
// - Una atención abierta se cierra al registrar (casilla marcada por defecto) o se pregunta al salir; las atenciones
//   abiertas de la cuenta se muestran arriba con su tiempo transcurrido para que no se queden contando.
import {ArrowDownToLine,ArrowUpFromLine,ImageOff,MapPin,ChevronRight,CircleAlert,Timer,SlidersHorizontal} from 'lucide-react';
import {confirmar as confirmarDialogo} from './dialogo';
import {useEffect,useRef,useState} from 'react';
import {date,nd,type User} from './api';
import type {EstadoAdmin,Ubicacion,Material,Atencion} from './adminTypes';
import {preverMovimiento} from './movimientoV2';
type Props={e:EstadoAdmin;user:User;run:(t:string,b:Record<string,unknown>)=>Promise<unknown>;dirty:(v:boolean)=>void;bloqueado:boolean;notificar:(titulo:string,detalle?:string)=>void};
type Disponibilidad={clase:'listo'|'aviso'|'bloqueado';texto:string};

/** ¿Se puede registrar este tipo de movimiento en la ubicación? Las tarjetas que no, no se abren. */
export function disponibilidad(tipo:'ENTRADA'|'SALIDA',m:Pick<Material,'activo'>,u:Pick<Ubicacion,'cantidad'|'libres'|'activa'>):Disponibilidad{
 if(tipo==='ENTRADA'){
  if(!m.activo)return {clase:'bloqueado',texto:'Material inhabilitado · solo retiro'};
  if(u.activa===false)return {clase:'bloqueado',texto:'Ubicación desactivada'};
  if(u.libres<=0)return {clase:'aviso',texto:'Sin espacio en la estantería'};
  return {clase:'listo',texto:'Registrar entrada'};
 }
 return u.cantidad>0?{clase:'listo',texto:'Registrar salida'}:{clase:'aviso',texto:'Sin existencias'};
}
/** Minutos transcurridos desde el inicio de una atención. */
export const minutosDesde=(inicio:string,ahora=Date.now())=>Math.max(0,Math.floor((ahora-new Date(inicio).getTime())/60000));
const cajasTexto=(n:number)=>nd(n)+(n===1?' caja':' cajas');

export function CapturaBloques({e,user,run,dirty,bloqueado,notificar}:Props){
 const [buscar,setBuscar]=useState(''),[estacion,setEstacion]=useState(''),[soloDisponibles,setSoloDisponibles]=useState(false),[tipo,setTipo]=useState<'ENTRADA'|'SALIDA'>('ENTRADA'),[elegida,setElegida]=useState(''),[cajas,setCajas]=useState(1),[consumo,setConsumo]=useState(true),[finalizarAl,setFinalizarAl]=useState(true),[confirmar,setConfirmar]=useState(false),[enviando,setEnviando]=useState(false),[error,setError]=useState(''),[ahora,setAhora]=useState(Date.now());
 const lock=useRef(false);const locked=bloqueado||enviando;
 useEffect(()=>{const t=setInterval(()=>setAhora(Date.now()),30000);return()=>clearInterval(t)},[]);
 const selected=e.ubicaciones.find(u=>u.id===elegida);const mat=selected&&e.materiales.find(m=>m.id===selected.material);const preview=selected&&mat?preverMovimiento(mat,selected,tipo,cajas):null;
 const allowed=user.permissions.includes(tipo==='ENTRADA'?'entry':'exit');
 const propia=(a:Atencion)=>a.usuario===user.id;
 const abiertas=e.atenciones;const mias=abiertas.filter(propia);
 function nombre(u:Ubicacion){const r=e.estanterias.find(r=>r.id===u.estanteria)!;return [e.estaciones.find(s=>s.id===r.estacion)?.nombre,r.nombre,r.nivel].join(' / ')}
 const parteDe=(a:Atencion)=>e.materiales.find(m=>m.id===a.material)?.numeroParte??'';
 const lugarDe=(a:Atencion)=>{const u=e.ubicaciones.find(x=>x.id===a.ubicacion);return u?nombre(u):''};
 function foto(m:Material){return e.imagenes[m.id]?<img className="captura-foto" alt={m.descripcion||m.numeroParte} src={'/api/v2/lineas/'+e.linea+'/imagen/'+m.id+'?v='+e.imagenes[m.id]}/>:<div className="captura-sin-foto"><ImageOff size={26} aria-hidden="true"/><span>Sin foto</span></div>}
 function elegir(u:Ubicacion){setElegida(u.id);setCajas(1);setConsumo(true);setFinalizarAl(true);setConfirmar(false);setError('');dirty(false)}
 async function ejecutar(t:string,b:Record<string,unknown>){if(lock.current||locked)return null;lock.current=true;setEnviando(true);setError('');try{const r=await run(t,b) as Record<string,unknown>;setConfirmar(false);dirty(false);return r}catch(ex){setError((ex as Error).message);setConfirmar(false);return null}finally{setEnviando(false);lock.current=false}}
 async function finalizar(a:Atencion){const r=await ejecutar('ATENCION_FINALIZAR',{id:a.id});if(r)notificar('Atención finalizada',parteDe(a)+' · '+lugarDe(a)+' · duró '+minutosDesde(a.inicio)+' min')}
 /** Al salir de la ubicación con una atención propia abierta, pregunta si se finaliza para que no siga contando. */
 async function salir(){
  if(confirmar&&!await confirmarDialogo({titulo:'¿Descartar esta confirmación?',mensaje:'El movimiento todavía no se ha registrado.',aceptar:'Descartar',cancelar:'Seguir aquí'}))return false;
  const a=selected&&abiertas.find(x=>x.ubicacion===selected.id&&propia(x));
  if(a&&await confirmarDialogo({titulo:'Tienes una atención abierta aquí',mensaje:'Si no la finalizas, el tiempo sigue contando para los KPIs. ¿La finalizas ahora?',aceptar:'Finalizar atención',cancelar:'Dejarla abierta'}))await finalizar(a);
  setElegida('');setConfirmar(false);dirty(false);return true;
 }
 async function registrar(){
  if(!selected||!mat||!preview||preview.error)return;
  const at=abiertas.find(a=>a.ubicacion===selected.id&&(e.rol==='ADMIN'||propia(a)));
  const cerrar=!!at&&finalizarAl;
  const r=await ejecutar(tipo,{ubicacion:selected.id,cajas,consumo:tipo==='SALIDA'&&consumo,atencion:at?.id??'',...(cerrar?{finalizarAtencion:true}:{})});
  if(!r)return;
  const piezas=Number(r.piezas??preview.piezas),antes=Number(r.saldoAnterior??selected.cantidad),despues=Number(r.saldoPosterior??preview.saldo);
  notificar((tipo==='ENTRADA'?'Entrada registrada':'Salida registrada')+' · '+nd(piezas)+' pz',mat.numeroParte+' · '+nombre(selected)+' · saldo '+nd(antes)+' → '+nd(despues)+' pz'+(cerrar?' · atención finalizada':''));
  setCajas(1);
 }
 const disponibles=e.ubicaciones.filter(u=>{if(u.activa===false)return false;const m=e.materiales.find(m=>m.id===u.material)!;const rack=e.estanterias.find(r=>r.id===u.estanteria)!;return (!estacion||rack.estacion===estacion)&&(!soloDisponibles||disponibilidad(tipo,m,u).clase==='listo')&&[m.numeroParte,m.descripcion,nombre(u)].join(' ').toLocaleLowerCase('es').includes(buscar.trim().toLocaleLowerCase('es'))});
 const atAbierta=selected&&abiertas.find(a=>a.ubicacion===selected.id);
 const cierraAt=!!atAbierta&&(e.rol==='ADMIN'||propia(atAbierta))&&finalizarAl;
 const resumenOpciones=[tipo==='SALIDA'?(consumo?'Consumo de producción':'No es consumo'):'',atAbierta&&(e.rol==='ADMIN'||propia(atAbierta))?(cierraAt?'finaliza la atención':'la atención sigue abierta'):''].filter(Boolean).join(' · ')+' · cambiar';
 return <section className="panel captura-bloques" aria-label="Captura por bloques" aria-busy={locked}>
 <div className="captura-encabezado"><h2>{tipo==='ENTRADA'?'Entrada de material':'Salida de material'}</h2>{selected&&mat&&<button className="secondary captura-volver" disabled={locked} onClick={()=>void salir()}>← Cambiar material</button>}<div className="segmentado" role="group" aria-label="Tipo de movimiento">{(['ENTRADA','SALIDA'] as const).map(t=><button key={t} className={'segmento'+(tipo===t?' activo':'')} aria-pressed={tipo===t} disabled={locked||!user.permissions.includes(t==='ENTRADA'?'entry':'exit')} onClick={()=>{if(tipo===t)return;void (async()=>{if(elegida&&!await salir())return;setTipo(t)})()}}>{t==='ENTRADA'?<ArrowDownToLine size={18} aria-hidden="true"/>:<ArrowUpFromLine size={18} aria-hidden="true"/>}{t==='ENTRADA'?'Entrada':'Salida'}</button>)}</div></div>
 {error&&<p className="error" role="alert">{error}</p>}
 {!selected||!mat?<>
  {mias.length>0&&<div className="atenciones-abiertas" role="region" aria-label="Tus atenciones abiertas"><p><Timer size={18} aria-hidden="true"/><strong>{mias.length===1?'Tienes 1 atención abierta':'Tienes '+mias.length+' atenciones abiertas'}</strong> · siguen contando tiempo hasta que las finalices.</p><ul>{mias.map(a=>{const min=minutosDesde(a.inicio,ahora);return <li key={a.id} className={min>=30?'larga':''}><span><strong className="parte">{parteDe(a)}</strong> · {lugarDe(a)} · desde hace {min} min</span><button className="secondary small" disabled={locked} onClick={()=>void finalizar(a)}>Finalizar</button></li>})}</ul></div>}
  <p className="help">Toca el material. Inicia la atención al comenzar a surtir; al registrar la entrada se finaliza sola.</p>
  <div className="form-grid"><label className="field">Buscar material o ubicación<input value={buscar} onChange={ev=>setBuscar(ev.target.value)} placeholder="Número de parte, descripción o estantería"/></label><label className="field">Estación<select className={estacion?'filtrando':undefined} value={estacion} onChange={ev=>setEstacion(ev.target.value)}><option value="">Todas</option>{e.estaciones.map(s=><option key={s.id} value={s.id}>{s.nombre}</option>)}</select></label></div>
  <label className="check"><input type="checkbox" checked={soloDisponibles} onChange={ev=>setSoloDisponibles(ev.target.checked)}/>Solo con {tipo==='ENTRADA'?'espacio disponible':'existencias'}</label>
  <p className="help">{disponibles.length} ubicaciones · {abiertas.length} atenciones en curso</p>
  <div className="captura-grid">{disponibles.map(u=>{const m=e.materiales.find(m=>m.id===u.material)!;const at=abiertas.find(a=>a.ubicacion===u.id);const estado=disponibilidad(tipo,m,u);const listo=estado.clase==='listo';const cajasSaldo=Math.ceil(u.cantidad/m.empaque);
   return <button className={'captura-tarjeta'+(listo?'':' no-disponible')} key={u.id} disabled={locked||!listo} aria-disabled={!listo} title={listo?undefined:estado.texto+': no se puede registrar '+(tipo==='ENTRADA'?'una entrada':'una salida')} onClick={()=>{if(listo)elegir(u)}} aria-label={m.numeroParte+', '+nombre(u)+', '+estado.texto}>
    <div className="tarjeta-foto">{foto(m)}{at&&<span className="tarjeta-atencion">Atención en curso · {at.responsable}</span>}</div>
    <div className="tarjeta-cuerpo"><strong className="tarjeta-parte">{m.numeroParte}</strong><span className="tarjeta-desc">{m.descripcion||'Sin descripción'}</span><span className="tarjeta-ubic"><MapPin size={14} aria-hidden="true"/>{nombre(u)}</span></div>
    <dl className="tarjeta-cifras"><div><dt>Saldo</dt><dd>{nd(u.cantidad)} pz</dd><small>{cajasTexto(cajasSaldo)}{u.cantidad%m.empaque?' (una abierta)':''}</small></div><div><dt>Espacio libre</dt><dd>{cajasTexto(u.libres)}</dd><small>{m.empaque} pz por caja</small></div></dl>
    <span className={'tarjeta-pie '+estado.clase}>{listo?<>{estado.texto}<ChevronRight size={18} aria-hidden="true"/></>:<><CircleAlert size={16} aria-hidden="true"/>{estado.texto}</>}</span>
   </button>})}</div>{!disponibles.length&&<p>No hay ubicaciones para estos filtros.</p>}</>:<div className="captura-detalle">
 <div className="captura-material">{foto(mat)}<div className="captura-datos"><span className="captura-etiqueta">Material seleccionado</span><h3>{mat.numeroParte}</h3><p>{mat.descripcion||'Sin descripción'}</p><p className="help">{nombre(selected)}</p></div>
  <dl className="captura-cifras"><div><dt>Saldo actual</dt><dd>{nd(selected.cantidad)} pz</dd><small>{cajasTexto(Math.ceil(selected.cantidad/mat.empaque))}</small></div><div><dt>Piezas por caja</dt><dd>{mat.empaque}</dd></div><div><dt>Cajas libres</dt><dd>{selected.libres}</dd></div></dl>
 </div>
 <div className="captura-pasos">
  <section className="paso" aria-labelledby="paso-1"><h4 id="paso-1"><span className="paso-num">1</span>Atención del surtido</h4>{atAbierta?<><p className="paso-estado en-curso">En curso · {minutosDesde(atAbierta.inicio,ahora)} min</p><p className="help">{atAbierta.responsable} · desde {date(atAbierta.inicio)}.</p>{(e.rol==='ADMIN'||propia(atAbierta))&&<button className="secondary paso-accion" disabled={locked} onClick={()=>void finalizar(atAbierta)}>Finalizar atención</button>}</>:<><p className="paso-estado">Sin iniciar</p><p className="help">Iníciala al comenzar a surtir para medir el tiempo.</p>{(e.rol==='ADMIN'||user.permissions.includes('atencion'))&&<button className="secondary paso-accion" disabled={locked||!mat.activo} onClick={()=>{void ejecutar('ATENCION_INICIAR',{ubicacion:selected.id}).then(r=>{if(r)notificar('Atención iniciada',mat.numeroParte+' · '+nombre(selected)+'. Se finaliza al registrar la entrada.')})}}>Iniciar atención</button>}</>}</section>
  <section className="paso" aria-labelledby="paso-2"><h4 id="paso-2"><span className="paso-num">2</span><label htmlFor="captura-cajas">Cajas a {tipo==='ENTRADA'?'ingresar':'retirar'}</label></h4><div className="captura-contador"><button className="secondary" aria-label="Restar una caja" disabled={locked||confirmar||cajas<=1} onClick={()=>{setCajas(cajas-1);dirty(true)}}>−</button><input id="captura-cajas" type="number" inputMode="numeric" min="1" max="1000000" step="1" disabled={locked||confirmar} value={cajas} onChange={ev=>{setCajas(Number(ev.target.value));dirty(true)}}/><button className="secondary" aria-label="Añadir una caja" disabled={locked||confirmar} onClick={()=>{setCajas(cajas+1);dirty(true)}}>+</button></div><p className="help">{preview&&!preview.error?nd(preview.piezas)+' piezas'+(preview.piezas!<cajas*mat.empaque?' (incluye el remanente de la caja abierta)':''):mat.empaque+' piezas por caja'}</p>
</section>
  <section className={'paso paso-final'+(confirmar?' confirmando':'')} aria-labelledby="paso-3"><h4 id="paso-3"><span className="paso-num">3</span>{confirmar?'Confirmar':'Revisar'}</h4>
   {preview?.error?<p role="alert" className="paso-error">{preview.error}</p>:preview&&<div className="saldo-previsto"><small>Saldo después de la {tipo==='ENTRADA'?'entrada':'salida'}</small><strong>{nd(selected.cantidad)} → {nd(preview.saldo)} <span>pz</span></strong></div>}
   {(tipo==='SALIDA'||atAbierta&&(e.rol==='ADMIN'||propia(atAbierta)))&&<details className="simple opciones-registro"><summary><SlidersHorizontal size={15} aria-hidden="true"/>{resumenOpciones}</summary>
    <p className="help">Normalmente no se cambia. Úsalo solo en casos especiales.</p>
    {tipo==='SALIDA'&&<label className="check"><input type="checkbox" checked={consumo} disabled={locked||confirmar} onChange={ev=>{setConsumo(ev.target.checked);dirty(true)}}/>Es consumo de producción<small className="block">Desmárcalo solo si es devolución, ajuste o retiro de remanente: no cuenta para la alarma.</small></label>}
    {atAbierta&&(e.rol==='ADMIN'||propia(atAbierta))&&<label className="check"><input type="checkbox" checked={finalizarAl} disabled={locked} onChange={ev=>setFinalizarAl(ev.target.checked)}/>Finalizar la atención al registrar<small className="block">Desmárcalo solo si vas a registrar otro movimiento dentro de la misma atención.</small></label>}
   </details>}
   {confirmar?<div role="group" aria-label="Confirmación del movimiento" className="paso-confirmar"><p>¿{tipo==='ENTRADA'?'Ingresar':'Retirar'} {nd(preview?.piezas)} piezas?</p><button disabled={locked||!allowed||!!preview?.error} onClick={()=>void registrar()}>{locked?'Registrando…':'Confirmar '+(tipo==='ENTRADA'?'entrada':'salida')}</button><button className="secondary" disabled={locked} onClick={()=>setConfirmar(false)}>Corregir cantidad</button></div>:<button className="paso-accion" disabled={locked||!allowed||!preview||!!preview.error} onClick={()=>{setConfirmar(true);dirty(true)}}>Revisar {tipo==='ENTRADA'?'entrada':'salida'}</button>}</section>
 </div>
</div>}
 </section>;
}
