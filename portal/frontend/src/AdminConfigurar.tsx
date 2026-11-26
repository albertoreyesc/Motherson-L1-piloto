import {useEffect,useState} from 'react';
import {UserRound,MapPinned,Warehouse,CalendarClock,Plus,Minus,X} from 'lucide-react';
import type {EstadoAdmin,Rack} from './adminTypes';
import {cajasOcupadasV2} from './ubicacionesV2';
import {vigenciaHorario} from './AvisoHorario';
type Props={e:EstadoAdmin;run:(tipo:string,body:Record<string,unknown>)=>Promise<unknown>;dirty:(v:boolean)=>void;seccionInicial?:'ESTANTERIA';alAbrir?:()=>void};
type Turno={inicio:string;fin:string;descansos?:{inicio:string;fin:string}[]};
type Seccion='PUESTO'|'ESTACION'|'ESTANTERIA'|'HORARIO';
const DIAS=['Domingo','Lunes','Martes','Miércoles','Jueves','Viernes','Sábado'];
const ORDEN_DIAS=[1,2,3,4,5,6,0];
const fechaLegible=(d:string)=>new Date(d+'T12:00:00').toLocaleDateString('es-MX',{weekday:'short',day:'numeric',month:'short',year:'numeric'});
const porNombre=<T extends {nombre:string}>(a:T,b:T)=>a.nombre.localeCompare(b.nombre,'es',{numeric:true});

/**
 * Configuración de la línea en pestañas: Puestos, Estaciones, Estanterías y Horario. Cada pestaña muestra lo que ya existe
 * en tarjetas (tocar una para editarla) y una tarjeta "Nuevo". Envía las mismas operaciones de siempre
 * (PUESTO, ESTACION, ESTANTERIA, CALENDARIO). Asignar materiales a estanterías se hace en Materiales o en Estaciones y ubicaciones.
 */
export function AdminConfigurar({e,run,dirty,seccionInicial,alAbrir}:Props){
 const [seccion,setSeccion]=useState<Seccion>(()=>seccionInicial??(vigenciaHorario(e.calendario).nivel==='ok'?'PUESTO':'HORARIO')),[sel,setSel]=useState(''),[nombre,setNombre]=useState(''),[estacion,setEstacion]=useState(''),[nivel,setNivel]=useState(''),[capacidad,setCapacidad]=useState(1),[buscar,setBuscar]=useState(''),[ok,setOk]=useState('');
 const cal=e.calendario;const [desde,setDesde]=useState(String(cal?.desde??'')),[hasta,setHasta]=useState(String(cal?.hasta??'')),[fuente,setFuente]=useState(String(cal?.fuente??'')),[dias,setDias]=useState<number[]>((cal?.dias as number[])??[]),[turnos,setTurnos]=useState<Turno[]>((cal?.turnos as Turno[])??[{inicio:'',fin:''}]),[excepciones,setExcepciones]=useState<Record<string,boolean>>((cal?.excepciones as Record<string,boolean>)??{}),[exDia,setExDia]=useState(''),[normal,setNormal]=useState(Number(cal?.normal??60)),[advertencia,setAdvertencia]=useState(Number(cal?.advertencia??30)),[critico,setCritico]=useState(Number(cal?.critico??15)),[busy,setBusy]=useState(false),[simulado,setSimulado]=useState(cal?.simulado===true);
 const execute=async(t:string,b:Record<string,unknown>)=>{setBusy(true);try{await run(t,b);dirty(false);return true}catch{/* error mostrado por el contenedor */return false}finally{setBusy(false)}};

 useEffect(()=>{if(seccionInicial)alAbrir?.()},[]);
 const setTurno=(i:number,t:Partial<Turno>)=>setTurnos(turnos.map((x,j)=>j===i?{...x,...t}:x));
 const semaforoValido=critico>0&&critico<advertencia&&advertencia<normal;
 const cambiarDias=(d:number[])=>{setDias(d);dirty(true)};
 const rackDe=(id:string)=>e.estanterias.find(r=>r.id===id);
 function limpiar(){setSel('');setNombre('');setNivel('');setEstacion('');setCapacidad(1);setOk('');dirty(false)}
 function cambiarSeccion(s:Seccion){setSeccion(s);setBuscar('');limpiar()}
 function elegir(id:string){setOk('');dirty(false);setSel(id);if(seccion==='PUESTO')setNombre(e.puestos.find(p=>p.id===id)?.nombre??'');if(seccion==='ESTACION')setNombre(e.estaciones.find(s=>s.id===id)?.nombre??'');if(seccion==='ESTANTERIA'){const r=rackDe(id);if(r){setNombre(r.nombre);setNivel(r.nivel);setEstacion(r.estacion);setCapacidad(r.capacidad)}}}
 async function guardar(){const editando=!!sel;const body=seccion==='ESTANTERIA'?{id:sel,nombre:nombre.trim(),estacion,nivel:nivel.trim(),capacidad}:{id:sel,nombre:nombre.trim()};
  if(await execute(seccion,body)){setOk((editando?'Cambios guardados: ':'Creado: ')+nombre.trim());if(!editando){setNombre('');setNivel('');setCapacidad(1)}}}
 const filtro=(t:string)=>t.toLocaleLowerCase('es').includes(buscar.trim().toLocaleLowerCase('es'));
 const tabs:[Seccion,string,typeof UserRound,number|null][]=[['PUESTO','Puestos',UserRound,e.puestos.length],['ESTACION','Estaciones',MapPinned,e.estaciones.length],['ESTANTERIA','Estanterías',Warehouse,e.estanterias.length],['HORARIO','Horario de trabajo',CalendarClock,null]];
 const ocupadas=(r:Rack)=>cajasOcupadasV2(e,r.id);
 const minimoCap=sel?Math.max(1,ocupadas(rackDe(sel)??{id:''} as Rack)):1;
 const tarjetaNueva=(texto:string)=><button type="button" role="option" aria-selected={!sel} className={'puesto-tarjeta nuevo'+(!sel?' elegido':'')} onClick={limpiar}><Plus size={20} aria-hidden="true"/><strong>{texto}</strong><small>Capturar manualmente</small></button>;
 const formulario=seccion!=='HORARIO'&&<form className="config-formulario" onChange={()=>dirty(true)} onSubmit={ev=>{ev.preventDefault();void guardar()}}>
  <h3>{sel?'Editar '+(seccion==='PUESTO'?'puesto':seccion==='ESTACION'?'estación':'estantería'):seccion==='PUESTO'?'Nuevo puesto':seccion==='ESTACION'?'Nueva estación':'Nueva estantería'}</h3>
  <div className="form-grid">
   {seccion==='ESTANTERIA'&&<label className="field">¿En qué estación está?<select required value={estacion} onChange={ev=>setEstacion(ev.target.value)}><option value="">Selecciona la estación</option>{[...e.estaciones].sort(porNombre).map(s=><option key={s.id} value={s.id}>{s.nombre}</option>)}</select></label>}
   <label className="field">{seccion==='PUESTO'?'Nombre del puesto':seccion==='ESTACION'?'Nombre de la estación':'Nombre de la estantería'}<input value={nombre} required maxLength={80} placeholder={seccion==='PUESTO'?'Ej. SZ01':seccion==='ESTACION'?'Ej. AL09':'Ej. SIM-DES-70'} onChange={ev=>setNombre(ev.target.value)}/></label>
   {seccion==='ESTANTERIA'&&<><label className="field">Nivel o repisa<input required maxLength={40} value={nivel} placeholder="Ej. A, B, 1" onChange={ev=>setNivel(ev.target.value)}/></label>
    <div className="field"><span>Espacios (cajas)</span><div className="capacidad-control"><button type="button" className="secondary" aria-label="Quitar un espacio" disabled={busy||capacidad<=minimoCap} onClick={()=>{setCapacidad(Math.max(minimoCap,capacidad-1));dirty(true)}}><Minus size={16} aria-hidden="true"/></button><input type="number" inputMode="numeric" min={minimoCap} max="1000000" required value={capacidad} aria-label="Espacios en cajas" onChange={ev=>setCapacidad(Math.max(1,Math.min(1000000,Math.trunc(Number(ev.target.value))||1)))}/><button type="button" className="secondary" aria-label="Agregar un espacio" disabled={busy} onClick={()=>{setCapacidad(Math.min(1000000,capacidad+1));dirty(true)}}><Plus size={16} aria-hidden="true"/></button></div>{sel&&<small>{minimoCap} ocupadas: no puede ser menor.</small>}</div></>}
  </div>
  {sel&&seccion==='PUESTO'&&<p className="help">El cambio de nombre conserva los materiales, las atenciones y el historial del puesto.</p>}
  <div className="button-row"><button disabled={busy||!nombre.trim()||seccion==='ESTANTERIA'&&(!estacion||!nivel.trim()||capacidad<minimoCap)}>{busy?'Guardando…':sel?'Guardar cambios':seccion==='PUESTO'?'Crear puesto':seccion==='ESTACION'?'Crear estación':'Crear estantería'}</button>{sel&&<button type="button" className="secondary" disabled={busy} onClick={limpiar}>Cancelar</button>}</div>
  {ok&&<p className="notice" role="status">{ok}</p>}
 </form>;
 return <>
 <section className="panel config-estructura"><h2>Configurar la Línea {e.linea}</h2>
  <p className="help">La línea se arma así: <strong>puesto</strong> → <strong>estación</strong> → <strong>estantería</strong>. Los materiales se asignan a una estantería en <em>Materiales y catálogos</em> o en <em>Estaciones y ubicaciones</em>.</p>
  <div className="pestanas" role="tablist" aria-label="Qué configurar">{tabs.map(([id,n,Icono,total])=><button key={id} type="button" role="tab" aria-selected={seccion===id} className={'pestana con-icono'+(seccion===id?' activa':'')} onClick={()=>cambiarSeccion(id)}><Icono size={17} aria-hidden="true"/>{n}{total!==null&&<small>{total}</small>}</button>)}</div>
  {seccion!=='HORARIO'&&<div className="config-dos-columnas"><div>
   {(seccion==='PUESTO'?e.puestos.length:seccion==='ESTACION'?e.estaciones.length:e.estanterias.length)>8&&<label className="field">Buscar<input value={buscar} onChange={ev=>setBuscar(ev.target.value)} placeholder="Nombre"/></label>}
   {seccion==='PUESTO'&&<div className="puestos-grid" role="listbox" aria-label="Puestos">{tarjetaNueva('Nuevo puesto')}{[...e.puestos].sort(porNombre).filter(p=>filtro(p.nombre)).map(p=>{const n=e.materiales.filter(m=>m.puesto===p.id).length;return <button type="button" role="option" aria-selected={sel===p.id} key={p.id} className={'puesto-tarjeta'+(sel===p.id?' elegido':'')} onClick={()=>elegir(p.id)}><UserRound size={20} aria-hidden="true"/><strong>{p.nombre}</strong><small>{n} {n===1?'material':'materiales'}</small></button>})}</div>}
   {seccion==='ESTACION'&&<div className="puestos-grid" role="listbox" aria-label="Estaciones">{tarjetaNueva('Nueva estación')}{[...e.estaciones].sort(porNombre).filter(s=>filtro(s.nombre)).map(s=>{const n=e.estanterias.filter(r=>r.estacion===s.id).length;return <button type="button" role="option" aria-selected={sel===s.id} key={s.id} className={'puesto-tarjeta'+(sel===s.id?' elegido':'')} onClick={()=>elegir(s.id)}><MapPinned size={20} aria-hidden="true"/><strong>{s.nombre}</strong><small>{n} {n===1?'estantería':'estanterías'}</small></button>})}</div>}
   {seccion==='ESTANTERIA'&&<><div className="puestos-grid" role="listbox" aria-label="Nueva estantería">{tarjetaNueva('Nueva estantería')}</div>{[...e.estaciones].sort(porNombre).map(s=>{const racks=e.estanterias.filter(r=>r.estacion===s.id&&filtro(s.nombre+' '+r.nombre+' '+r.nivel)).sort((a,b)=>porNombre(a,b)||a.nivel.localeCompare(b.nivel,'es'));if(!racks.length)return null;return <div key={s.id} className="grupo-estacion"><h4>{s.nombre}</h4><div className="puestos-grid" role="listbox" aria-label={'Estanterías de '+s.nombre}>{racks.map(r=>{const o=ocupadas(r);return <button type="button" role="option" aria-selected={sel===r.id} key={r.id} className={'puesto-tarjeta'+(sel===r.id?' elegido':'')+(o>=r.capacidad?' llena':'')} onClick={()=>elegir(r.id)}><Warehouse size={20} aria-hidden="true"/><strong>{r.nombre} · {r.nivel}</strong><small>{o} de {r.capacidad} cajas ocupadas</small></button>})}</div></div>})}</>}
  </div><div className="config-panel-form">{formulario}</div></div>}
 </section>
 {seccion==='HORARIO'&&<section className="panel config-horario"><h2>Horario de trabajo de la Línea {e.linea}</h2>
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
 </section>}</>;
}
