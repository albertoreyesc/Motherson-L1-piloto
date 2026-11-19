// Captura visual: conserva validaciones, comprobantes y la operación idempotente.
import React,{useEffect,useRef,useState} from 'react';
import {ArrowDownToLine,ArrowUpFromLine,ArrowLeft,ArrowRight,ArrowRightLeft,Check,CheckCircle2,ChevronRight,Clock3,Info,MapPin,Minus,Package,Plus,Search,TriangleAlert} from 'lucide-react';
import {api,ApiError,date,nd,type User} from './api';
import {cajasEnUbicacion,importe,sitio,type Inventario,type Comprobante} from './MvpTypes';

type Tipo='ENTRADA'|'SALIDA';
type Pendiente={id:string;tipo:Tipo;ubicacion:number;cantidad:number;piezas?:number;version:number;esConsumo?:boolean;incierta?:boolean};
const pasos=['Ubicación','Material','Confirmar'];
const admite=(r:Inventario,tipo:Tipo)=>tipo==='ENTRADA'?(r.cajas_disponibles!=null&&r.cajas_disponibles>0):(cajasEnUbicacion(r)??0)>0;
function disponibilidadVisual(r:Inventario,tipo:Tipo){
 if(r.cantidad==null)return 'Disponibilidad sin determinar';
 if(tipo==='SALIDA'){const cajas=cajasEnUbicacion(r);return cajas==null?'Falta el empaque (piezas por caja)':cajas>0?nd(cajas)+(cajas===1?' caja':' cajas')+' · '+nd(r.cantidad)+' pz':'Sin cajas disponibles';}
 if(r.cajas_disponibles==null)return 'Disponibilidad sin determinar';
 return r.cajas_disponibles>0?'Admite '+nd(r.cajas_disponibles)+(r.cajas_disponibles===1?' caja':' cajas'):'Sin espacio para otra caja completa';
}
const coincide=(r:Inventario,busqueda:string)=>[r.estacion,r.estanteria,r.nivel,r.numero_parte,r.descripcion,r.puesto].join(' ').toLocaleLowerCase('es').includes(busqueda.trim().toLocaleLowerCase('es'));

export function QuickEntryPanel({user,rows,onRefresh,refreshing=false,continuacion=false}:{user:User;rows:Inventario[];onRefresh:()=>Promise<void>;refreshing?:boolean;continuacion?:boolean}){
 const storage='motherson-mvp-operacion:'+user.tipoCuenta+':'+user.id+':'+(continuacion?'continuacion-v2':'sql');
 const [pending,setPending]=useState<Pendiente|null>(()=>{try{const v=JSON.parse(sessionStorage.getItem(storage)??'null');return v&&typeof v.id==='string'&&['ENTRADA','SALIDA'].includes(v.tipo)&&Number.isSafeInteger(v.ubicacion)&&Number.isSafeInteger(v.cantidad)&&Number.isSafeInteger(v.version)?v:null}catch{return null}});
 const [tipo,setTipo]=useState<Tipo>(pending?.tipo??(user.permissions.includes('entry')?'ENTRADA':'SALIDA'));
 const [nivel,setNivel]=useState<number|null>(null),[selected,setSelected]=useState<number|null>(pending?.ubicacion??null);
 const [cantidad,setCantidad]=useState(pending?.cantidad??1),[disponibles,setDisponibles]=useState(true),[busqueda,setBusqueda]=useState('');
 const [esConsumo,setEsConsumo]=useState(pending?.esConsumo??false);
 const [busy,setBusy]=useState(false),[error,setError]=useState(''),[receipt,setReceipt]=useState<Comprobante|null>(null);
 const title=useRef<HTMLHeadingElement>(null);
 const allowed=user.permissions.includes(tipo==='ENTRADA'?'entry':'exit');
 const locked=!!pending||busy;
 const selectedRow=rows.find(r=>r.id_ubicacion===(pending?.ubicacion??selected));
 const cantidadActual=pending?.cantidad??cantidad;
 const amount=selectedRow?importe(selectedRow,tipo,cantidadActual):null;
 const step=selectedRow||pending?2:nivel!=null?1:0;
 useEffect(()=>{title.current?.focus({preventScroll:true})},[step]);
 const orden=(a:Inventario,b:Inventario)=>tipo==='ENTRADA'?(b.cajas_disponibles??-1)-(a.cajas_disponibles??-1):(b.cantidad??-1)-(a.cantidad??-1);
 const visible=rows.filter(r=>(!disponibles||admite(r,tipo))&&coincide(r,busqueda)).sort(orden);
 const groups=Array.from(new Map(visible.map(r=>[r.id_estanteria,r])).values());
 const materials=visible.filter(r=>r.id_estanteria===nivel);
 const visibles=step===0?groups.length:materials.length;
 const total=step===0?new Set(rows.map(r=>r.id_estanteria)).size:rows.filter(r=>r.id_estanteria===nivel).length;
 const ubicacionActual=rows.find(r=>r.id_estanteria===nivel);
 const motivo=!Number.isSafeInteger(cantidadActual)||cantidadActual<1||cantidadActual>1000000?'Elige una cantidad entera entre 1 y 1,000,000.':selectedRow?.cantidad==null?'No se puede registrar con existencias desconocidas.':tipo==='ENTRADA'&&selectedRow.cajas_disponibles==null?'Falta validar la capacidad o las piezas por caja.':tipo==='ENTRADA'&&cantidadActual>selectedRow.cajas_disponibles!?'La cantidad supera el espacio disponible para cajas completas.':tipo==='SALIDA'&&cajasEnUbicacion(selectedRow)==null?'Falta el empaque (piezas por caja) de este material.':tipo==='SALIDA'&&cantidadActual>cajasEnUbicacion(selectedRow)!?'Solo hay '+nd(cajasEnUbicacion(selectedRow))+(cajasEnUbicacion(selectedRow)===1?' caja':' cajas')+' en esta ubicación.':'La cantidad excede el límite permitido para este movimiento.';

 function remember(p:Pendiente|null){if(p)sessionStorage.setItem(storage,JSON.stringify(p));else sessionStorage.removeItem(storage);setPending(p)}
 function cambiarTipo(nuevo:Tipo){if(locked)return;setTipo(nuevo);setEsConsumo(false);setSelected(null);setNivel(null);setCantidad(1);setReceipt(null);setError('')}
 function elegirUbicacion(r:Inventario){if(locked)return;setNivel(r.id_estanteria);setError('');const one=visible.filter(x=>x.id_estanteria===r.id_estanteria);if(one.length===1)setSelected(one[0].id_ubicacion)}
 function volver(){if(locked)return;setSelected(null);setNivel(null);setError('')}
 async function save(){if(busy||refreshing||!allowed||(!pending&&(!amount?.valido||!selectedRow)))return;setBusy(true);setError('');const p=pending??{id:crypto.randomUUID(),tipo,ubicacion:selectedRow!.id_ubicacion,cantidad:cantidadActual,version:selectedRow!.version_concurrencia,piezas:amount?.piezas,esConsumo:tipo==='SALIDA'&&esConsumo};
  try{remember(p)}catch{setError('No se pudo conservar el identificador. Habilita almacenamiento antes de confirmar.');setBusy(false);return;}
  try{const value=await api<Comprobante>(continuacion?'/continuacion/movimientos':p.tipo==='ENTRADA'?'/central/entries':'/movimientos/salidas',continuacion?{ubicacion:p.ubicacion,tipo:p.tipo,cantidad:p.cantidad,version:p.version,esConsumo:p.esConsumo??false}:p.tipo==='ENTRADA'?{ubicacion:p.ubicacion,cajas:p.cantidad,version:p.version}:{ubicacion:p.ubicacion,piezas:p.piezas??p.cantidad,version:p.version},p.id);remember(null);setReceipt(value);setCantidad(1);try{await onRefresh()}catch{setError('Movimiento confirmado. Actualiza el inventario antes de otro registro.');setSelected(null)}}
  catch(e){const status=e instanceof ApiError?e.status:0;setError((e as Error).message);if(!status||status>=500||status===408||status===429||(p.incierta&&(status===401||status===403))){remember({...p,incierta:true});setError('Respuesta pendiente de comprobación. Reintenta la misma operación; se conserva su identificador.')}else{remember(null);try{await onRefresh()}catch{setSelected(null)}}}finally{setBusy(false)}
 }

 return <section className="movimientos-ui" aria-label="Registro de movimientos" aria-busy={busy||refreshing}>
  <div className="mov-cabecera">
   <div className="mov-titulo"><span className="mov-icono"><ArrowRightLeft size={24} aria-hidden="true"/></span><div><small>CAPTURA DE MATERIAL · LÍNEA 1</small><h2>{tipo==='ENTRADA'?'Entrada de material':'Salida de material'}</h2><p>{tipo==='ENTRADA'?'Selecciona dónde ingresar las cajas':'Selecciona de dónde retirar las cajas'}</p></div></div>
   <div className="mov-operaciones" role="group" aria-label="Tipo de movimiento">
    <button aria-pressed={tipo==='ENTRADA'} disabled={locked||!user.permissions.includes('entry')} onClick={()=>cambiarTipo('ENTRADA')}><ArrowDownToLine size={18} aria-hidden="true"/>Entrada · cajas</button>
    <button aria-pressed={tipo==='SALIDA'} disabled={locked||!user.permissions.includes('exit')} onClick={()=>cambiarTipo('SALIDA')}><ArrowUpFromLine size={18} aria-hidden="true"/>Salida · cajas</button>
   </div>
  </div>
  <ol className="mov-pasos" aria-label="Pasos del registro">{pasos.map((p,i)=><li key={p} aria-current={i===step?'step':undefined} className={i<step?'completo':''}><span className="mov-numero">{i<step?<Check size={16} aria-label="Completado"/>:i+1}</span><span>{p}{i<step&&<small>Completado</small>}</span>{i<2&&<ChevronRight size={16} className="mov-paso-flecha" aria-hidden="true"/>}</li>)}</ol>
  {receipt&&<div className="mov-comprobante" role="status"><CheckCircle2 size={24} aria-hidden="true"/><div><strong>Operación {receipt.id_movimiento??receipt.id_registro} confirmada</strong><p>{receipt.tipo_movimiento==='ENTRADA'?'Entrada':'Salida'} de <b>{nd(receipt.cantidad_movimiento)} piezas</b> · saldo resultante <b>{nd(receipt.saldo_posterior)} piezas</b>.</p><small>{date(receipt.fecha_movimiento)} · Comprobante conservado hasta cambiar de operación o salir de esta pantalla.</small></div></div>}
  {pending&&<div className="mov-pendiente"><Clock3 size={22} aria-hidden="true"/><div><strong>{busy?'Comprobando operación…':'Operación pendiente de comprobación'}</strong><p>La selección y la cantidad permanecen bloqueadas. Se comprobará el mismo registro, sin crear otro identificador.</p><small>Identificador: {pending.id}</small></div></div>}
  {error&&<div className="mov-error" role="alert"><TriangleAlert size={20} aria-hidden="true"/><p>{error}</p></div>}
  {refreshing&&<p className="mov-cargando" aria-live="polite">Actualizando disponibilidad…</p>}
  {step<2?<>
   <div className="mov-seccion"><div>{step===1&&ubicacionActual&&<p className="mov-ruta"><MapPin size={16} aria-hidden="true"/>{sitio(ubicacionActual)}</p>}<h3 ref={title} tabIndex={-1}>{step===0?'Selecciona tu ubicación':'Selecciona el material'}</h3><p>{step===0?'Ubicaciones autorizadas para tu cuenta.':'Solo materiales asignados a esta ubicación.'}</p></div>{step===1&&<button className="mov-secundario" disabled={locked} onClick={volver}><ArrowLeft size={17} aria-hidden="true"/>Cambiar ubicación</button>}</div>
   <div className="mov-herramientas">
    <label className="mov-busqueda"><span>Buscar ubicación o material</span><span className="mov-busqueda-input"><Search size={18} aria-hidden="true"/><input type="search" value={busqueda} disabled={locked} onChange={e=>setBusqueda(e.target.value)} placeholder="Estación, estantería, nivel o número de parte"/></span></label>
    <label className="mov-filtro"><input type="checkbox" checked={disponibles} disabled={locked} onChange={e=>setDisponibles(e.target.checked)}/><span>{step===0?'Solo ubicaciones disponibles':'Solo materiales disponibles'}</span></label>
    <span className="mov-conteo" aria-live="polite"><b>{nd(visibles)}</b> de {nd(total)} {step===0?'ubicaciones':'materiales'}</span>
   </div>
   <div className="mvp-blocks mov-cuadricula">{step===0?groups.map(r=>{
    const todas=rows.filter(x=>x.id_estanteria===r.id_estanteria);const libres=todas.filter(x=>admite(x,tipo)).length;
    const texto=todas.length===1?disponibilidadVisual(r,tipo):`${libres} de ${todas.length} materiales ${tipo==='ENTRADA'?'admiten entrada':'con cajas disponibles'}`;
    return <button className="mvp-location mov-tarjeta" key={r.id_estanteria} disabled={locked||refreshing} onClick={()=>elegirUbicacion(r)}>
     <span className="mov-tarjeta-arriba"><MapPin size={20} aria-hidden="true"/><ArrowRight size={18} aria-hidden="true"/></span><strong className="mov-estacion">{r.estacion}</strong>
     <span className="mov-datos-ubicacion"><span>Estantería <b>{r.estanteria}</b></span><span>Nivel <b>{r.nivel}</b></span></span><span className="mov-puesto">{Array.from(new Set(todas.map(x=>x.puesto))).join(' · ')}</span>
     <span className="mov-disponibilidad">{texto}</span>
    </button>
   }):materials.map(r=><button className="mvp-location mov-tarjeta" key={r.id_ubicacion} disabled={locked||refreshing} onClick={()=>{setSelected(r.id_ubicacion);setError('')}}><span className="mov-tarjeta-arriba"><Package size={20} aria-hidden="true"/><ArrowRight size={18} aria-hidden="true"/></span><strong>{r.numero_parte}</strong><span className="mov-descripcion">{r.descripcion??'Sin descripción registrada'}</span><span className="mov-puesto">{r.puesto}</span><span className="mov-disponibilidad">{disponibilidadVisual(r,tipo)}</span></button>)}</div>
   {!visibles&&<div className="mov-vacio"><Search size={28} aria-hidden="true"/><h3>{busqueda?'Sin coincidencias':'Sin disponibilidad para esta operación'}</h3><p>{busqueda?'Prueba con otra estación, ubicación o número de parte.':'Puedes mostrar todas las ubicaciones para consultar su disponibilidad.'}</p><button className="mov-secundario" onClick={()=>{setBusqueda('');setDisponibles(false)}}>Mostrar todas {step===0?'las ubicaciones':'las opciones'}</button></div>}
  </>:selectedRow?<section className="mov-confirmacion" aria-label="Confirmación del movimiento">
   <div className="mov-seccion"><div><p className="mov-seleccion"><CheckCircle2 size={17} aria-hidden="true"/>Ubicación seleccionada</p><h3 ref={title} tabIndex={-1}>{selectedRow.numero_parte}</h3><p className="mov-ruta"><MapPin size={16} aria-hidden="true"/>{sitio(selectedRow)} · {selectedRow.puesto}</p><p>{selectedRow.descripcion??'Sin descripción registrada'}</p></div><button className="mov-secundario" disabled={locked} onClick={volver}><ArrowLeft size={17} aria-hidden="true"/>Cambiar ubicación</button></div>
   <div className="mov-confirmar-grid"><fieldset className="mov-cantidad"><legend>{tipo==='ENTRADA'?'Entrada · cajas completas':'Salida · cajas'}</legend>
    <label htmlFor="mov-cantidad">{tipo==='ENTRADA'?'Cajas completas':'Cajas a retirar'}</label>
    <div className="mov-contador"><button className="mov-secundario" aria-label="Restar una caja" disabled={locked||cantidadActual<=1} onClick={()=>setCantidad(Math.max(1,cantidadActual-1))}><Minus size={22} aria-hidden="true"/></button><input id="mov-cantidad" type="number" inputMode="numeric" min="1" max="1000000" step="1" value={cantidadActual} aria-invalid={!amount?.valido} aria-describedby={!amount?.valido&&!pending?'mov-validacion':undefined} disabled={locked} onChange={e=>setCantidad(Number(e.target.value))}/><button className="mov-secundario" aria-label="Añadir una caja" disabled={locked||cantidadActual>=1000000} onClick={()=>setCantidad(cantidadActual+1)}><Plus size={22} aria-hidden="true"/></button></div>
    {continuacion&&tipo==='SALIDA'&&<label className="mov-filtro"><input type="checkbox" checked={pending?.esConsumo??esConsumo} disabled={locked} onChange={e=>setEsConsumo(e.target.checked)}/><span>Representa consumo simulado de producción</span></label>}
    <div className="mov-rapidos" role="group" aria-label="Cantidades rápidas">{[1,2,3].map(n=><button className="mov-secundario" aria-pressed={cantidadActual===n} key={n} disabled={locked} onClick={()=>setCantidad(n)}>{n} {n===1?'caja':'cajas'}</button>)}</div>
    <p className="mov-disponibilidad"><Package size={16} aria-hidden="true"/>{disponibilidadVisual(selectedRow,tipo)}</p>
   </fieldset><div className="mov-prevision"><p className="mov-conversion">{tipo==='ENTRADA'?<>{nd(cantidadActual)} {cantidadActual===1?'caja':'cajas'} × {nd(selectedRow.cantidad_por_caja)} pz/caja = <b>{nd(amount?.piezas)} piezas</b></>:<>{nd(cantidadActual)} {cantidadActual===1?'caja':'cajas'} × {nd(selectedRow.cantidad_por_caja)} pz/caja = <b>{nd(amount?.piezas)} piezas</b>{amount&&amount.piezas<cantidadActual*(selectedRow.cantidad_por_caja??0)?' · incluye la caja abierta con sus piezas sueltas':''}</>}</p><dl className="mov-saldos"><div><dt>Saldo anterior</dt><dd>{nd(selectedRow.cantidad)} <small>pz</small></dd></div><div><dt>{tipo==='ENTRADA'?'Piezas ingresadas':'Piezas descontadas'}</dt><dd>{tipo==='ENTRADA'?'+':'−'}{nd(amount?.piezas)} <small>pz</small></dd></div><div className="mov-saldo-final"><dt>Saldo previsto</dt><dd>{nd(amount?.saldo)} <small>pz</small></dd></div></dl><p className="mov-ayuda"><Info size={16} aria-hidden="true"/>Disponibilidad de la consulta del servidor. El saldo previsto se confirma al registrar.</p></div></div>
   {!amount?.valido&&!pending&&<p className="mov-validacion" id="mov-validacion"><Info size={18} aria-hidden="true"/>{motivo}</p>}
   <button className="mov-confirmar" disabled={busy||refreshing||!allowed||(!pending&&!amount?.valido)} onClick={save}>{busy?<><Clock3 size={20} aria-hidden="true"/>Comprobando…</>:pending?<><Clock3 size={20} aria-hidden="true"/>Comprobar la misma operación</>:<><Check size={20} aria-hidden="true"/>{tipo==='ENTRADA'?'Confirmar entrada':'Confirmar salida'}</>}</button>
   <p className="mov-ayuda mov-pie">El servidor valida saldo, capacidad y permiso. La operación se conserva en la continuación; SQL mantiene su saldo al corte.</p>
  </section>:<section className="mov-confirmacion"><h3 ref={title} tabIndex={-1}>Comprobar la operación pendiente</h3><p>La ubicación no aparece en esta consulta. Se conserva la operación original para comprobar su resultado.</p><p>{pending?.tipo==='ENTRADA'?'Entrada':'Salida'} · ubicación {pending?.ubicacion} · {nd(pending?.cantidad)} cajas</p><button className="mov-confirmar" disabled={busy||refreshing||!allowed} onClick={save}>{busy?'Comprobando…':'Comprobar la misma operación'}</button></section>}
 </section>;
}
