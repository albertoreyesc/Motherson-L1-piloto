import {useState} from 'react';
import {UserRound,MapPinned,Warehouse,PackagePlus,Plus,X} from 'lucide-react';
import type {EstadoAdmin} from './adminTypes';
type Props={e:EstadoAdmin;run:(tipo:string,body:Record<string,unknown>)=>Promise<unknown>;dirty:(v:boolean)=>void};
type Turno={inicio:string;fin:string;descansos?:{inicio:string;fin:string}[]};
const DIAS=['Domingo','Lunes','Martes','Miércoles','Jueves','Viernes','Sábado'];
const ORDEN_DIAS=[1,2,3,4,5,6,0];
const fechaLegible=(d:string)=>new Date(d+'T12:00:00').toLocaleDateString('es-MX',{weekday:'short',day:'numeric',month:'short',year:'numeric'});

/**
 * Configuración de la línea en lenguaje de planta. Envía exactamente las mismas operaciones que la versión
 * anterior (PUESTO, ESTACION, ESTANTERIA, UBICACION, CALENDARIO), solo cambia cómo se capturan.
 */
export function AdminConfigurar({e,run,dirty}:Props){
 const [tipo,setTipo]=useState('PUESTO'),[nombre,setNombre]=useState(''),[estacion,setEstacion]=useState(''),[nivel,setNivel]=useState(''),[capacidad,setCapacidad]=useState(1),[material,setMaterial]=useState(''),[rack,setRack]=useState(''),[editRack,setEditRack]=useState('');
 const cal=e.calendario;const [desde,setDesde]=useState(String(cal?.desde??'')),[hasta,setHasta]=useState(String(cal?.hasta??'')),[fuente,setFuente]=useState(String(cal?.fuente??'')),[dias,setDias]=useState<number[]>((cal?.dias as number[])??[]),[turnos,setTurnos]=useState<Turno[]>((cal?.turnos as Turno[])??[{inicio:'',fin:''}]),[excepciones,setExcepciones]=useState<Record<string,boolean>>((cal?.excepciones as Record<string,boolean>)??{}),[exDia,setExDia]=useState(''),[normal,setNormal]=useState(Number(cal?.normal??60)),[advertencia,setAdvertencia]=useState(Number(cal?.advertencia??30)),[critico,setCritico]=useState(Number(cal?.critico??15)),[busy,setBusy]=useState(false),[simulado,setSimulado]=useState(cal?.simulado===true);
 const execute=async(t:string,b:Record<string,unknown>)=>{setBusy(true);try{await run(t,b);dirty(false)}catch{/* error mostrado por el contenedor */}finally{setBusy(false)}};
 const opciones=[
  {id:'PUESTO',icono:UserRound,titulo:'Puesto logístico',texto:'Quién surte: el puesto del materialista.',total:e.puestos.length,boton:'Crear puesto'},
  {id:'ESTACION',icono:MapPinned,titulo:'Estación',texto:'Zona de la línea donde están las estanterías.',total:e.estaciones.length,boton:'Crear estación'},
  {id:'ESTANTERIA',icono:Warehouse,titulo:'Estantería',texto:'Rack dentro de una estación, con su capacidad en cajas.',total:e.estanterias.length,boton:editRack?'Guardar cambios':'Crear estantería'},
  {id:'UBICACION',icono:PackagePlus,titulo:'Asignar material',texto:'Indica en qué estantería se guarda un material.',total:e.ubicaciones.filter(u=>u.activa!==false).length,boton:'Asignar material'},
 ] as const;
 const actual=opciones.find(o=>o.id===tipo)!;
 const setTurno=(i:number,t:Partial<Turno>)=>setTurnos(turnos.map((x,j)=>j===i?{...x,...t}:x));
 const semaforoValido=critico>0&&critico<advertencia&&advertencia<normal;
 const cambiarDias=(d:number[])=>{setDias(d);dirty(true)};
 return <>
 <section className="panel config-estructura"><h2>Estructura de la Línea {e.linea}</h2>
  <p className="help">La línea se arma así: <strong>puesto</strong> → <strong>estación</strong> → <strong>estantería</strong> → <strong>material asignado</strong>. Elige qué quieres agregar.</p>
  <div className="config-opciones" role="radiogroup" aria-label="¿Qué quieres agregar?">{opciones.map(o=>{const Icono=o.icono;return <button type="button" role="radio" aria-checked={tipo===o.id} key={o.id} className={'config-opcion'+(tipo===o.id?' elegida':'')} onClick={()=>{setTipo(o.id);setEditRack('');setNombre('');setNivel('');setEstacion('');setCapacidad(1)}}><Icono size={22} aria-hidden="true"/><strong>{o.titulo}</strong><span>{o.texto}</span><small>{o.total} registrados</small></button>})}</div>
  <form className="config-formulario" onChange={()=>dirty(true)} onSubmit={ev=>{ev.preventDefault();void execute(tipo,tipo==='UBICACION'?{material,estanteria:rack}:tipo==='ESTANTERIA'?{id:editRack,nombre,estacion,nivel,capacidad}:{nombre})}}>
   <div className="form-grid">
   {tipo==='ESTANTERIA'&&<label className="field">¿Nueva o una que ya existe?<select value={editRack} onChange={ev=>{setEditRack(ev.target.value);const r=e.estanterias.find(r=>r.id===ev.target.value);if(r){setNombre(r.nombre);setNivel(r.nivel);setEstacion(r.estacion);setCapacidad(r.capacidad)}else{setNombre('');setNivel('');setEstacion('');setCapacidad(1)}}}><option value="">Crear una estantería nueva</option>{e.estanterias.map(r=><option value={r.id} key={r.id}>Modificar {r.nombre} / {r.nivel}</option>)}</select></label>}
   {tipo!=='UBICACION'&&<label className="field">{tipo==='PUESTO'?'Nombre del puesto':tipo==='ESTACION'?'Nombre de la estación':'Nombre de la estantería'}<input value={nombre} required maxLength={80} placeholder={tipo==='PUESTO'?'Ej. SZ01':tipo==='ESTACION'?'Ej. AL09':'Ej. SIM-DES-70'} onChange={ev=>setNombre(ev.target.value)}/></label>}
   {tipo==='ESTANTERIA'&&<><label className="field">¿En qué estación está?<select required value={estacion} onChange={ev=>setEstacion(ev.target.value)}><option value="">Selecciona la estación</option>{e.estaciones.map(s=><option key={s.id} value={s.id}>{s.nombre}</option>)}</select></label><label className="field">Nivel o repisa<input required value={nivel} placeholder="Ej. A, B, 1, 2" onChange={ev=>setNivel(ev.target.value)}/></label><label className="field">¿Cuántas cajas caben?<input type="number" inputMode="numeric" min="1" max="1000000" required value={capacidad} onChange={ev=>setCapacidad(Number(ev.target.value))}/></label></>}
   {tipo==='UBICACION'&&<><label className="field">Material<select required value={material} onChange={ev=>setMaterial(ev.target.value)}><option value="">Selecciona el material</option>{e.materiales.filter(m=>m.activo).map(m=><option key={m.id} value={m.id}>{m.numeroParte}{m.descripcion?' · '+m.descripcion:''}</option>)}</select></label><label className="field">¿En qué estantería se guarda?<select required value={rack} onChange={ev=>setRack(ev.target.value)}><option value="">Selecciona la estantería</option>{e.estanterias.map(r=><option key={r.id} value={r.id}>{e.estaciones.find(s=>s.id===r.estacion)?.nombre} / {r.nombre} / {r.nivel}</option>)}</select></label></>}
   </div>
   {tipo==='UBICACION'&&<p className="help">La asignación empieza con 0 piezas; las existencias se registran con una entrada.</p>}
   {tipo==='ESTANTERIA'&&editRack&&<p className="help">No se puede reducir la capacidad por debajo de las cajas que ya tiene.</p>}
   <button disabled={busy}>{actual.boton}</button>
  </form>
 </section>
 <section className="panel config-horario"><h2>Horario de trabajo de la Línea {e.linea}</h2>
  <p className="help">Indica qué días y en qué horario se trabaja. Con esto el sistema calcula cuántos minutos de material quedan en cada ubicación.</p>
  <form onChange={()=>dirty(true)} onSubmit={ev=>{ev.preventDefault();if(semaforoValido)void execute('CALENDARIO',{desde,hasta,fuente,dias,turnos,excepciones,normal,advertencia,critico,simulado})}}>
   <section className="paso-config"><h3><span className="paso-num">1</span>¿Cuándo aplica este horario?</h3>
    <div className="form-grid"><label className="field">Desde<input type="date" required value={desde} onChange={ev=>setDesde(ev.target.value)}/></label><label className="field">Hasta (incluido)<input type="date" required min={desde||undefined} value={hasta} onChange={ev=>setHasta(ev.target.value)}/></label><label className="field">¿Quién lo autorizó?<input required maxLength={250} value={fuente} onChange={ev=>setFuente(ev.target.value)} placeholder="Ej. Gerencia de planta, oficio del 20/11"/></label></div>
    <label className="check"><input type="checkbox" checked={simulado} onChange={ev=>setSimulado(ev.target.checked)}/>Es un horario de prueba (falta validarlo con planta)</label>
   </section>
   <section className="paso-config"><h3><span className="paso-num">2</span>Días que se trabaja</h3>
    <div className="chips-dias" role="group" aria-label="Días que se trabaja">{ORDEN_DIAS.map(i=><button type="button" key={i} aria-pressed={dias.includes(i)} className={'chip-dia'+(dias.includes(i)?' activo':'')} onClick={()=>cambiarDias(dias.includes(i)?dias.filter(x=>x!==i):[...dias,i].sort())}>{DIAS[i]}</button>)}</div>
    <div className="fecha-atajos" role="group" aria-label="Atajos de días">{[['Lunes a viernes',[1,2,3,4,5]],['Lunes a sábado',[1,2,3,4,5,6]],['Todos los días',[0,1,2,3,4,5,6]]].map(([n,d])=><button type="button" key={String(n)} className={'secondary'+(JSON.stringify([...dias].sort())===JSON.stringify(d)?' elegido':'')} onClick={()=>cambiarDias([...d as number[]])}>{n as string}</button>)}</div>
   </section>
   <section className="paso-config"><h3><span className="paso-num">3</span>Turnos <small>(hora de Ciudad de México)</small></h3>
    {turnos.map((t,i)=><div className="turno-tarjeta" key={i}>
     <div className="turno-cabecera"><strong>Turno {i+1}</strong>{turnos.length>1&&<button type="button" className="secondary small" onClick={()=>{setTurnos(turnos.filter((_,j)=>j!==i));dirty(true)}}><X size={16} aria-hidden="true"/>Quitar turno</button>}</div>
     <div className="turno-horas"><label className="field">Entra a las<input type="time" required value={t.inicio} onChange={ev=>setTurno(i,{inicio:ev.target.value})}/></label><label className="field">Sale a las<input type="time" required value={t.fin} onChange={ev=>setTurno(i,{fin:ev.target.value})}/></label>{t.inicio&&t.fin&&t.fin<t.inicio&&<p className="help turno-nota">Termina al día siguiente (turno nocturno).</p>}</div>
     <div className="turno-descansos"><p className="help">Descansos de este turno (comida, pausas). No cuentan como tiempo de trabajo.</p>
      {(t.descansos??[]).map((d,k)=><div className="descanso-fila" key={k}><label className="field">De<input type="time" required value={d.inicio} onChange={ev=>setTurno(i,{descansos:t.descansos!.map((b,n)=>n===k?{...b,inicio:ev.target.value}:b)})}/></label><label className="field">A<input type="time" required value={d.fin} onChange={ev=>setTurno(i,{descansos:t.descansos!.map((b,n)=>n===k?{...b,fin:ev.target.value}:b)})}/></label><button type="button" className="secondary small" aria-label={'Quitar descanso '+(k+1)} onClick={()=>{setTurno(i,{descansos:t.descansos!.filter((_,n)=>n!==k)});dirty(true)}}><X size={16} aria-hidden="true"/>Quitar</button></div>)}
      <button type="button" className="secondary small" disabled={(t.descansos?.length??0)>=8} onClick={()=>{setTurno(i,{descansos:[...t.descansos??[],{inicio:'',fin:''}]});dirty(true)}}><Plus size={16} aria-hidden="true"/>Agregar descanso</button>
     </div>
    </div>)}
    <button type="button" className="secondary" disabled={turnos.length>=3} onClick={()=>{setTurnos([...turnos,{inicio:'',fin:''}]);dirty(true)}}><Plus size={18} aria-hidden="true"/>Agregar otro turno</button>
   </section>
   <section className="paso-config"><h3><span className="paso-num">4</span>Días festivos o especiales <small>(opcional)</small></h3>
    <p className="help">Fechas que no siguen el horario normal: un festivo que no se trabaja o un sábado que sí.</p>
    <div className="button-row fila-alineada"><label className="field">Fecha<input type="date" value={exDia} onChange={ev=>setExDia(ev.target.value)}/></label><button type="button" className="secondary" disabled={!exDia} onClick={()=>{setExcepciones({...excepciones,[exDia]:false});setExDia('');dirty(true)}}>No se trabaja</button><button type="button" className="secondary" disabled={!exDia} onClick={()=>{setExcepciones({...excepciones,[exDia]:true});setExDia('');dirty(true)}}>Sí se trabaja</button></div>
    {Object.keys(excepciones).length>0&&<ul className="lista-excepciones">{Object.entries(excepciones).sort().map(([d,b])=><li key={d}><span><strong>{fechaLegible(d)}</strong> · {b?'Sí se trabaja':'No se trabaja'}</span><button type="button" className="secondary small" aria-label={'Quitar '+fechaLegible(d)} onClick={()=>{setExcepciones(Object.fromEntries(Object.entries(excepciones).filter(([k])=>k!==d)));dirty(true)}}><X size={16} aria-hidden="true"/></button></li>)}</ul>}
   </section>
   <section className="paso-config"><h3><span className="paso-num">5</span>Semáforo de cobertura</h3>
    <p className="help">¿Con cuántos minutos de material restante cambia de color cada ubicación?</p>
    <div className="semaforo-filas">
     <label className="semaforo-fila verde"><span>Normal (verde) desde</span><input type="number" inputMode="numeric" min="3" max="1440" required value={normal} onChange={ev=>setNormal(Number(ev.target.value))}/><span>minutos</span></label>
     <label className="semaforo-fila amarillo"><span>Advertencia (amarillo) desde</span><input type="number" inputMode="numeric" min="2" required value={advertencia} onChange={ev=>setAdvertencia(Number(ev.target.value))}/><span>minutos</span></label>
     <label className="semaforo-fila rojo"><span>Crítico (rojo) desde</span><input type="number" inputMode="numeric" min="1" required value={critico} onChange={ev=>setCritico(Number(ev.target.value))}/><span>minutos</span></label>
     <p className="semaforo-fila paro"><span>Menos de {critico||'…'} minutos: <strong>riesgo de paro</strong>. Sin piezas: <strong>agotado</strong>.</span></p>
    </div>
    {!semaforoValido&&<p className="paso-error" role="alert">Revisa los minutos: el verde debe ser mayor que el amarillo, y el amarillo mayor que el rojo.</p>}
    <details><summary><span className="resumen-titulo">¿Cómo se calcula la cobertura?</span><small className="resumen-ayuda">Detalle técnico del cálculo.</small></summary><p className="help">Se usan los últimos 60 minutos de trabajo y se necesitan al menos dos salidas marcadas como consumo en la ubicación. Si faltan datos se muestra “Sin estimación”. El día de un turno nocturno es el día en que empieza. Los cambios quedan registrados y no modifican los cálculos de fechas anteriores.</p></details>
   </section>
   <section className="paso-config vista-semana"><h3>Así queda tu semana</h3>
    <p className="help">{desde&&hasta?`Del ${fechaLegible(desde)} al ${fechaLegible(hasta)}`:'Falta indicar desde y hasta cuándo aplica'}{simulado?' · horario de prueba':''}</p>
    <div className="semana-grid">{ORDEN_DIAS.map(i=><div key={i} className={dias.includes(i)?'dia laboral':'dia'}><strong>{DIAS[i]}</strong>{dias.includes(i)?turnos.map((t,n)=><span key={n} className="block">{t.inicio||'--:--'}–{t.fin||'--:--'}{t.fin&&t.inicio&&t.fin<t.inicio?' (+1)':''}{t.descansos?.length?` · ${t.descansos.length} desc.`:''}</span>):<span className="block">No se trabaja</span>}</div>)}</div>
   </section>
   <button disabled={busy||!semaforoValido}>Guardar horario</button>
  </form>
 </section></>;
}
