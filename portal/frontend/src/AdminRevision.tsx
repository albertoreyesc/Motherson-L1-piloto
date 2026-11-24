import {useState} from 'react';
import type {EstadoAdmin,Material} from './adminTypes';
import {revisarDuplicado,siguienteRevision,ubicacionActiva,type Duplicado} from './ubicacionesV2';
type Props={e:EstadoAdmin;origen:Material;run:(tipo:string,body:Record<string,unknown>)=>Promise<unknown>;dirty:(v:boolean)=>void;cerrar:()=>void};
const etiquetas:Record<keyof Duplicado,string>={numeroParte:'Número de parte',descripcion:'Descripción',variante:'Variante / color',revision:'Revisión',modelo:'Modelo / vehículo',uso:'Uso PFEP',empaque:'Piezas por caja',minimo:'Mínimo (pz)'};

/** Nueva revisión de una parte: duplica el material, se editan solo los datos que cambian y se inhabilita el anterior. */
export function AdminRevision({e,origen,run,dirty,cerrar}:Props){
 const [d,setD]=useState<Duplicado>({numeroParte:origen.numeroParte,descripcion:origen.descripcion,variante:origen.variante,revision:siguienteRevision(origen.revision),modelo:origen.modelo,uso:origen.uso,empaque:origen.empaque,minimo:origen.minimo});
 const [copiar,setCopiar]=useState(true),[inhabilitar,setInhabilitar]=useState(true),[motivo,setMotivo]=useState(''),[busy,setBusy]=useState(false),[error,setError]=useState('');
 const {errores,cambios}=revisarDuplicado(origen,d,e.materiales);
 const sitios=e.ubicaciones.filter(u=>u.material===origen.id&&ubicacionActiva(u));const saldo=sitios.reduce((n,u)=>n+u.cantidad,0);
 const set=<K extends keyof Duplicado>(k:K,v:Duplicado[K])=>{setD({...d,[k]:v});dirty(true)};
 async function guardar(){setBusy(true);setError('');try{await run('MATERIAL_DUPLICAR',{id:origen.id,...d,numeroParte:d.numeroParte.trim(),motivo:motivo.trim(),copiarUbicaciones:copiar,inhabilitarOrigen:inhabilitar});dirty(false);cerrar()}catch(ex){setError((ex as Error).message)}finally{setBusy(false)}}
 return <section className="panel" aria-label={'Nueva revisión de '+origen.numeroParte}><h2>Nueva revisión de {origen.numeroParte}</h2>
 <p>Se crea un material nuevo con los datos de {origen.numeroParte}. Cambia solo lo que es distinto (normalmente el número de parte y la revisión). El historial del material anterior se conserva.</p>
 {error&&<p className="error" role="alert">{error}</p>}
 <form onSubmit={ev=>{ev.preventDefault();if(!errores.length&&motivo.trim())void guardar()}}>
 <div className="form-grid">{(Object.keys(etiquetas) as (keyof Duplicado)[]).map(k=><label className="field" key={k}>{etiquetas[k]}{k==='empaque'||k==='minimo'?<input type="number" min={k==='empaque'?1:0} required value={d[k]} onChange={ev=>set(k,Number(ev.target.value))}/>:<input value={d[k]} required={k==='numeroParte'} maxLength={k==='descripcion'?250:80} onChange={ev=>set(k,ev.target.value)}/>}</label>)}</div>
 <label className="check"><input type="checkbox" checked={copiar} onChange={ev=>{setCopiar(ev.target.checked);dirty(true)}}/>Asignar la nueva revisión a las mismas estanterías ({sitios.length}), con saldo cero</label>
 <label className="check"><input type="checkbox" checked={inhabilitar} onChange={ev=>{setInhabilitar(ev.target.checked);dirty(true)}}/>Inhabilitar {origen.numeroParte} (ya no admite entradas; su remanente se retira con salidas)</label>
 {inhabilitar&&saldo>0&&<p className="notice">{origen.numeroParte} todavía tiene {saldo} pz en {sitios.length} ubicaciones. Seguirán visibles para retirarlas con una salida.</p>}
 <label className="field">Motivo<input required maxLength={250} value={motivo} onChange={ev=>{setMotivo(ev.target.value);dirty(true)}} placeholder="Ej. cambio de revisión notificado por ingeniería"/></label>
 <div className="notice"><strong>Cambios respecto a {origen.numeroParte}</strong>{cambios.length===0?<p>Sin cambios todavía: el número de parte debe ser distinto.</p>:<ul>{cambios.map(c=><li key={c.campo}>{etiquetas[c.campo]}: {c.antes||'(vacío)'} → {c.despues||'(vacío)'}</li>)}</ul>}</div>
 {errores.length>0&&<ul role="alert">{errores.map(x=><li key={x}>{x}</li>)}</ul>}
 <div className="button-row"><button disabled={busy||errores.length>0||!motivo.trim()}>Crear nueva revisión</button><button type="button" className="secondary" disabled={busy} onClick={()=>{dirty(false);cerrar()}}>Cancelar</button></div>
 </form></section>;
}
