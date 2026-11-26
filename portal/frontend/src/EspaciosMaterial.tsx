// Espacios en estanterías desde "Materiales y catálogos" (recupera el + / − del sistema anterior, 17/11/2026).
// Todo se guarda con las mismas operaciones de administración v2: ESTANTERIA, ESTACION y UBICACION.
import {useEffect,useState} from 'react';
import {Minus,Plus,MapPin} from 'lucide-react';
import {nd} from './api';
import type {EstadoAdmin,Material,Rack} from './adminTypes';
import {cajasOcupadasV2,limitesCapacidadV2,materialesEnEstanteria,ubicacionActiva} from './ubicacionesV2';
type Run=(tipo:string,body:Record<string,unknown>)=>Promise<unknown>;

export const nombreEstanteria=(e:EstadoAdmin,r:Rack)=>[e.estaciones.find(s=>s.id===r.estacion)?.nombre,r.nombre,r.nivel].filter(Boolean).join(' / ');

/** Capacidad en cajas de una estantería con + / −. Se edita en pantalla y solo se guarda al confirmar. */
export function CapacidadV2({e,rack,run,editable,bloqueado}:{e:EstadoAdmin;rack:Rack;run:Run;editable:boolean;bloqueado:boolean}){
 const actual=rack.capacidad;
 const [valor,setValor]=useState(actual),[busy,setBusy]=useState(false),[error,setError]=useState('');
 useEffect(()=>{setValor(actual);setError('')},[actual,rack.id]);
 const ocupadas=cajasOcupadasV2(e,rack.id);
 const {minimo,maximo}=limitesCapacidadV2(e,rack.id);
 const compartida=materialesEnEstanteria(e,rack.id);
 const cambiado=valor!==actual;
 const resumen=<small className="capacidad-resumen">{nd(ocupadas)} ocupadas · {nd(Math.max(0,actual-ocupadas))} libres{compartida>1?' · compartida con '+(compartida-1)+(compartida===2?' material más':' materiales más'):''}</small>;
 if(!editable)return <div className="capacidad"><strong>{nd(actual)} cajas</strong>{resumen}</div>;
 async function guardar(){
  setBusy(true);setError('');
  try{await run('ESTANTERIA',{id:rack.id,nombre:rack.nombre,estacion:rack.estacion,nivel:rack.nivel,capacidad:valor})}
  catch(ex){setError((ex as Error).message||'No se guardó la capacidad.')}
  finally{setBusy(false)}
 }
 const locked=busy||bloqueado;
 return <div className="capacidad">
  <div className="capacidad-control" role="group" aria-label={'Espacios en cajas de '+nombreEstanteria(e,rack)}>
   <button type="button" className="secondary" aria-label="Quitar un espacio" title={valor<=minimo?'No puede ser menor que las cajas ocupadas':'Quitar un espacio'} disabled={locked||valor<=minimo} onClick={()=>setValor(v=>Math.max(minimo,v-1))}><Minus size={16} aria-hidden="true"/></button>
   <input type="number" inputMode="numeric" min={minimo} max={maximo} value={valor} disabled={locked} aria-label="Espacios en cajas" onChange={ev=>{const n=Math.trunc(Number(ev.target.value));if(Number.isFinite(n))setValor(Math.min(maximo,Math.max(0,n)))}} onBlur={()=>setValor(v=>Math.min(maximo,Math.max(minimo,v)))}/>
   <button type="button" className="secondary" aria-label="Agregar un espacio" disabled={locked||valor>=maximo} onClick={()=>setValor(v=>Math.min(maximo,v+1))}><Plus size={16} aria-hidden="true"/></button>
  </div>
  {cambiado?<div className="capacidad-acciones"><button type="button" className="small" disabled={locked||valor<minimo} onClick={()=>void guardar()}>{busy?'Guardando…':'Guardar '+nd(valor)+' cajas'}</button><button type="button" className="secondary small" disabled={locked} onClick={()=>{setValor(actual);setError('')}}>Cancelar</button></div>:resumen}
  {error&&<p className="error" role="alert">{error}</p>}
 </div>;
}

/** Ubicaciones de un material con su espacio, y alta de una ubicación nueva en otra estantería. */
export function UbicacionesMaterial({e,m,run,admin,bloqueado}:{e:EstadoAdmin;m:Material;run:Run;admin:boolean;bloqueado:boolean}){
 const [abierto,setAbierto]=useState(false),[rack,setRack]=useState(''),[busy,setBusy]=useState(false),[error,setError]=useState('');
 const propias=e.ubicaciones.filter(u=>u.material===m.id&&ubicacionActiva(u));
 const ocupadas=new Set(e.ubicaciones.filter(u=>u.material===m.id).map(u=>u.estanteria));
 const libres=e.estanterias.filter(r=>!ocupadas.has(r.id));
 async function asignar(){setBusy(true);setError('');try{await run('UBICACION',{material:m.id,estanteria:rack});setRack('');setAbierto(false)}catch(ex){setError((ex as Error).message)}finally{setBusy(false)}}
 return <div className="ubicaciones-material">
  {propias.length===0&&<small className="block">Sin ubicación asignada</small>}
  {propias.map(u=>{const r=e.estanterias.find(x=>x.id===u.estanteria);if(!r)return null;return <div className="ubicacion-espacio" key={u.id}>
   <span className="ubicacion-nombre"><MapPin size={14} aria-hidden="true"/>{nombreEstanteria(e,r)}</span>
   <small className="block">Saldo {nd(u.cantidad)} pz · {nd(Math.ceil(u.cantidad/m.empaque))} {Math.ceil(u.cantidad/m.empaque)===1?'caja':'cajas'}</small>
   <CapacidadV2 e={e} rack={r} run={run} editable={admin} bloqueado={bloqueado}/>
  </div>})}
  {admin&&m.activo&&(abierto?<div className="asignar-ubicacion">
   <label className="field">¿En qué estantería se guarda?<select value={rack} disabled={busy||bloqueado} onChange={ev=>setRack(ev.target.value)}><option value="">Selecciona la estantería</option>{libres.map(r=><option key={r.id} value={r.id}>{nombreEstanteria(e,r)} · {nd(Math.max(0,r.capacidad-cajasOcupadasV2(e,r.id)))} libres</option>)}</select></label>
   {libres.length===0&&<p className="help">No hay estanterías disponibles. Crea una con “Agregar estación o estantería”.</p>}
   <div className="button-row"><button type="button" className="small" disabled={busy||bloqueado||!rack} onClick={()=>void asignar()}>{busy?'Asignando…':'Asignar'}</button><button type="button" className="secondary small" disabled={busy} onClick={()=>{setAbierto(false);setRack('');setError('')}}>Cancelar</button></div>
   <p className="help">Empieza con 0 piezas; las existencias se registran con una entrada.</p>
  </div>:<button type="button" className="enlace" disabled={bloqueado} onClick={()=>setAbierto(true)}>+ Asignar a estantería</button>)}
  {error&&<p className="error" role="alert">{error}</p>}
 </div>;
}

/** Alta rápida de estaciones y estanterías sin salir del catálogo. */
export function AltaEstructura({e,run,bloqueado}:{e:EstadoAdmin;run:Run;bloqueado:boolean}){
 const [estacion,setEstacion]=useState(''),[rack,setRack]=useState({nombre:'',estacion:'',nivel:'',capacidad:1}),[busy,setBusy]=useState(false),[error,setError]=useState(''),[ok,setOk]=useState('');
 async function ejecutar(tipo:string,body:Record<string,unknown>,listo:string){setBusy(true);setError('');setOk('');try{await run(tipo,body);setOk(listo);return true}catch(ex){setError((ex as Error).message);return false}finally{setBusy(false)}}
 const locked=busy||bloqueado;
 return <details className="panel alta-estructura"><summary><span className="resumen-titulo">+ Agregar estación o estantería</span><small className="resumen-ayuda">Para asignar materiales a un lugar nuevo sin salir del catálogo.</small></summary>
  {error&&<p className="error" role="alert">{error}</p>}{ok&&<p className="notice" role="status">{ok}</p>}
  <div className="alta-estructura-grid">
   <form onSubmit={ev=>{ev.preventDefault();const n=estacion.trim();void ejecutar('ESTACION',{nombre:n},'Estación '+n+' creada.').then(b=>{if(b)setEstacion('')})}}>
    <h3>Nueva estación</h3>
    <label className="field">Nombre de la estación<input required maxLength={80} value={estacion} placeholder="Ej. AL09" onChange={ev=>setEstacion(ev.target.value)}/></label>
    <button className="small" disabled={locked||!estacion.trim()}>Crear estación</button>
   </form>
   <form onSubmit={ev=>{ev.preventDefault();void ejecutar('ESTANTERIA',{id:'',nombre:rack.nombre.trim(),estacion:rack.estacion,nivel:rack.nivel.trim(),capacidad:rack.capacidad},'Estantería '+rack.nombre.trim()+' creada.').then(b=>{if(b)setRack({nombre:'',estacion:rack.estacion,nivel:'',capacidad:1})})}}>
    <h3>Nueva estantería</h3>
    <div className="form-grid">
     <label className="field">¿En qué estación está?<select required value={rack.estacion} onChange={ev=>setRack({...rack,estacion:ev.target.value})}><option value="">Selecciona la estación</option>{e.estaciones.map(s=><option key={s.id} value={s.id}>{s.nombre}</option>)}</select></label>
     <label className="field">Nombre<input required maxLength={80} value={rack.nombre} placeholder="Ej. SIM-DES-70" onChange={ev=>setRack({...rack,nombre:ev.target.value})}/></label>
     <label className="field">Nivel o repisa<input required maxLength={40} value={rack.nivel} placeholder="Ej. A, B, 1" onChange={ev=>setRack({...rack,nivel:ev.target.value})}/></label>
     <div className="field"><span>Espacios (cajas)</span><div className="capacidad-control"><button type="button" className="secondary" aria-label="Quitar un espacio" disabled={locked||rack.capacidad<=1} onClick={()=>setRack({...rack,capacidad:Math.max(1,rack.capacidad-1)})}><Minus size={16} aria-hidden="true"/></button><input type="number" inputMode="numeric" min="1" max="1000000" required value={rack.capacidad} aria-label="Espacios en cajas" onChange={ev=>setRack({...rack,capacidad:Math.max(1,Math.min(1000000,Math.trunc(Number(ev.target.value))||1))})}/><button type="button" className="secondary" aria-label="Agregar un espacio" disabled={locked} onClick={()=>setRack({...rack,capacidad:Math.min(1000000,rack.capacidad+1)})}><Plus size={16} aria-hidden="true"/></button></div></div>
    </div>
    {e.estaciones.length===0&&<p className="help">Primero crea una estación.</p>}
    <button className="small" disabled={locked||!rack.estacion||!rack.nombre.trim()||!rack.nivel.trim()}>Crear estantería</button>
   </form>
  </div>
 </details>;
}
