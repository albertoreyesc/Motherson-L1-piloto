// KPIs de la línea: barra de filtros compacta, 4 indicadores clave y detalle en pestañas (Inventario, Actividad, Tiempos).
// Los datos y el Excel son los mismos de antes; solo cambia cómo se acomodan en pantalla.
import {AdminDiagnostico} from './AdminDiagnostico';
import {MiniFoto} from './fotoMaterial';
import {libroAdminV2,estadoFiltrado,ESTADOS_KPI} from './excelAdminV2';
import {useEffect,useRef,useState} from 'react';
import {Download,Info,SlidersHorizontal} from 'lucide-react';
import {api,date,nd} from './api';
import {descargarV2} from './AdminImportar';
import type {EstadoAdmin,KpisAdmin} from './adminTypes';
export function fechaMexico(d=new Date()){const p=new Intl.DateTimeFormat('en-CA',{timeZone:'America/Mexico_City',year:'numeric',month:'2-digit',day:'2-digit'}).formatToParts(d);return ['year','month','day'].map(t=>p.find(x=>x.type===t)!.value).join('-')}
const haceDias=(n:number)=>fechaMexico(new Date(Date.now()-n*86400000));
type Periodo='hoy'|'7'|'30'|'personalizado';
const PERIODOS:[Periodo,string,number][]=[['hoy','Hoy',0],['7','7 días',6],['30','30 días',29]];
/** Orden de gravedad para "a atender primero". */
const GRAVEDAD:Record<string,number>={AGOTADO:0,CRITICO:1,ADVERTENCIA:2};
const LIMITE_PANTALLA=14;

/** Materiales a atender primero: agotados, críticos y en advertencia, del más grave al menos grave. */
export function porAtender(materiales:KpisAdmin['estadoActual']['materiales']){
 return materiales.filter(m=>m.estado in GRAVEDAD).sort((a,b)=>GRAVEDAD[a.estado]-GRAVEDAD[b.estado]||(a.saldo??0)/Math.max(1,a.minimo)-(b.saldo??0)/Math.max(1,b.minimo));
}

export function AdminKpis({e}:{e:EstadoAdmin}){
 const [periodo,setPeriodo]=useState<Periodo>('7'),[desde,setDesde]=useState(haceDias(6)),[hasta,setHasta]=useState(fechaMexico()),[estacion,setEstacion]=useState(''),[turno,setTurno]=useState(''),[material,setMaterial]=useState(''),[corte,setCorte]=useState('');
 const [datos,setDatos]=useState<KpisAdmin|null>(null),[error,setError]=useState(''),[busy,setBusy]=useState(false),[cargando,setCargando]=useState(false),[pestana,setPestana]=useState<'inventario'|'actividad'|'tiempos'>('inventario');
 const [disponible,setDisponible]=useState<{total:number;legado:number;nuevas:number;desde:string|null;hasta:string|null}|null>(null),[exportando,setExportando]=useState(''),[avisoExport,setAvisoExport]=useState('');
 const consulta=useRef(0),menuExcel=useRef<HTMLDetailsElement>(null);
 useEffect(()=>{let ok=true;api<NonNullable<typeof disponible>>('/v2/lineas/'+e.linea+'/actividad-disponible').then(d=>{if(ok)setDisponible(d)}).catch(()=>{});return()=>{ok=false}},[e.linea]);
 // Se calcula solo al cambiar un filtro (con una pequeña espera para no consultar en cada tecla).
 useEffect(()=>{if(!desde||!hasta||desde>hasta||(corte&&corte.length<16))return;const n=++consulta.current;const t=setTimeout(()=>{setCargando(true);setError('');const q=new URLSearchParams({desde,hasta,estacion,material,turno});if(corte)q.set('corte',corte);api<KpisAdmin>('/v2/lineas/'+e.linea+'/kpis?'+q).then(d=>{if(n===consulta.current)setDatos(d)}).catch(ex=>{if(n===consulta.current){setError((ex as Error).message);setDatos(null)}}).finally(()=>{if(n===consulta.current)setCargando(false)})},300);return()=>clearTimeout(t)},[e.linea,desde,hasta,estacion,material,turno,corte]);
 useEffect(()=>{const cerrar=(ev:MouseEvent)=>{const m=menuExcel.current;if(m?.open&&!m.contains(ev.target as Node))m.open=false};document.addEventListener('click',cerrar);return()=>document.removeEventListener('click',cerrar)},[]);
 const lugar=(id:string)=>{const u=e.ubicaciones.find(x=>x.id===id);const r=u&&e.estanterias.find(x=>x.id===u.estanteria);return r?[e.estaciones.find(s=>s.id===r.estacion)?.nombre,r.nombre,r.nivel].filter(Boolean).join(' / '):id};
 const elegirPeriodo=(p:Periodo,dias?:number)=>{setPeriodo(p);if(dias!==undefined){setDesde(haceDias(dias));setHasta(fechaMexico())}};
 async function descargar(libro:KpisAdmin,nombre:string){const b=await libroAdminV2(libro,e.materiales,lugar,{estaciones:e.estaciones});descargarV2(new Blob([await b.xlsx.writeBuffer()],{type:'application/vnd.openxmlformats-officedocument.spreadsheetml.sheet'}),nombre)}
 async function exportarConsulta(){if(menuExcel.current)menuExcel.current.open=false;if(!datos||busy)return;setBusy(true);setError('');setAvisoExport('');try{await descargar(datos,'kpis-linea-'+e.linea+'-'+desde+'-a-'+hasta+'.xlsx')}catch(ex){setError('No se pudo generar el Excel: '+(ex as Error).message)}finally{setBusy(false)}}
 /** Histórico completo directo a Excel, sin cargarlo en pantalla. Respeta estación, material y turno; máximo dos años por archivo. */
 async function exportarHistorico(){if(menuExcel.current)menuExcel.current.open=false;if(!disponible?.desde||!disponible.hasta||busy)return;setBusy(true);setError('');setAvisoExport('');setExportando('Preparando el histórico…');try{
  const fin=disponible.hasta;const limite=fechaMexico(new Date(new Date(fin+'T12:00:00-06:00').getTime()-730*86400000));const ini=disponible.desde<limite?limite:disponible.desde;
  const completo=await api<KpisAdmin>('/v2/lineas/'+e.linea+'/kpis?'+new URLSearchParams({desde:ini,hasta:fin,estacion,material,turno}));
  await descargar(completo,'historico-kpis-linea-'+e.linea+'-'+ini+'-a-'+fin+'.xlsx');
  setAvisoExport('Se descargó el histórico del '+ini+' al '+fin+' ('+nd(completo.actividad.operaciones.length)+' atenciones)'+(ini!==disponible.desde?'. Máximo dos años por archivo.':'.'));
 }catch(ex){setError('No se pudo exportar el histórico: '+(ex as Error).message)}finally{setBusy(false);setExportando('')}}

 const st=datos?estadoFiltrado(datos):null;const urgentes=st?porAtender(st.materiales):[];const d=datos?.diagnostico;const ac=datos?.actividad;
 const masFiltros=!!material||!!corte;
 return <div className="kpis-v3" aria-busy={cargando||busy}>
 <section className="panel kpis-barra">
  <div className="kpis-filtros">
   <div className="fecha-atajos periodo-atajos" role="group" aria-label="Periodo">{PERIODOS.map(([id,n,dias])=><button type="button" key={id} className={'secondary'+(periodo===id?' elegido':'')} aria-pressed={periodo===id} onClick={()=>elegirPeriodo(id,dias)}>{n}</button>)}<button type="button" className={'secondary'+(periodo==='personalizado'?' elegido':'')} aria-pressed={periodo==='personalizado'} onClick={()=>elegirPeriodo('personalizado')}>Personalizado</button></div>
   <label className="field compacto">Estación<select value={estacion} onChange={ev=>setEstacion(ev.target.value)}><option value="">Todas</option>{e.estaciones.map(s=><option value={s.id} key={s.id}>{s.nombre}</option>)}</select></label>
   <label className="field compacto">Turno<select value={turno} onChange={ev=>setTurno(ev.target.value)}><option value="">Todos</option><option value="1">1 · 06:00 a 14:59</option><option value="2">2 · 15:00 a 05:59</option></select></label>
   <details className="simple menu-excel" ref={menuExcel}><summary className="boton-excel"><Download size={17} aria-hidden="true"/>{exportando||'Excel'} ▾</summary><div role="menu">
    <button role="menuitem" disabled={!datos||busy} onClick={()=>void exportarConsulta()}>Esta consulta<small>{desde} al {hasta}</small></button>
    <button role="menuitem" disabled={!disponible?.desde||busy} onClick={()=>void exportarHistorico()}>Histórico completo<small>{disponible?.desde?nd(disponible.total)+' atenciones · '+disponible.desde+' al '+disponible.hasta:'Sin histórico disponible'}</small></button>
   </div></details>
  </div>
  {periodo==='personalizado'&&<div className="kpis-filtros"><label className="field compacto">Desde<input type="date" value={desde} max={hasta} onChange={ev=>setDesde(ev.target.value)}/></label><label className="field compacto">Hasta<input type="date" value={hasta} min={desde} max={fechaMexico()} onChange={ev=>setHasta(ev.target.value)}/></label></div>}
  <details className="simple mas-filtros" open={masFiltros||undefined}><summary><SlidersHorizontal size={15} aria-hidden="true"/>Más filtros{masFiltros?' (activos)':''}</summary><div className="kpis-filtros">
   <label className="field compacto">Material<select value={material} onChange={ev=>setMaterial(ev.target.value)}><option value="">Todos</option>{e.materiales.map(m=><option value={m.id} key={m.id}>{m.numeroParte}</option>)}</select></label>
   <label className="field compacto">Inventario al corte de (opcional)<input type="datetime-local" step="1" value={corte} max={fechaMexico()+'T23:59'} onChange={ev=>setCorte(ev.target.value)}/></label>
   {corte&&<button type="button" className="secondary small" onClick={()=>setCorte('')}>Usar el corte actual</button>}
  </div></details>
  {avisoExport&&<p className="help" role="status">{avisoExport}</p>}
  {error&&<p className="error" role="alert">{error}</p>}
 </section>

 {!datos?<p className="help">{cargando?'Calculando indicadores…':error?'':'Elige un periodo para ver los indicadores.'}</p>:<>
 <div className="kpis-clave">
  <section className={urgentes.length?'kpi-tarjeta alerta':'kpi-tarjeta'}><small>Por atender</small><strong>{nd(st!.agotados+st!.criticos)}</strong><span>{st!.agotados} agotados · {st!.criticos} críticos{st!.advertencia?' · '+st!.advertencia+' en advertencia':''}</span></section>
  <section className="kpi-tarjeta"><small>Reabastecimientos</small><strong>{nd(ac!.completados)}</strong><span>completados · {ac!.enCurso} en curso</span></section>
  <section className="kpi-tarjeta"><small>Tiempo promedio</small><strong>{d?.promedioMinutos==null?'N/D':nd(d.promedioMinutos)+' min'}</strong><span className={d?.promedioMinutos!=null&&d.promedioMinutos>d.objetivoMinutos?'fuera':''}>objetivo {d?nd(d.objetivoMinutos):'N/D'} min</span></section>
  <section className="kpi-tarjeta"><small>Fuera de objetivo</small><strong>{d?.porcentajeFueraObjetivo==null?'N/D':nd(d.porcentajeFueraObjetivo)+' %'}</strong><span>de las atenciones</span></section>
 </div>
 <p className="help kpis-contexto">Del {String(datos.contexto.desde)} al {String(datos.contexto.hasta)} · inventario al {date(datos.contexto.corte)}{cargando?' · actualizando…':''}</p>

 <section className="panel">
  <div className="pestanas" role="tablist" aria-label="Detalle de indicadores">{([['inventario','Existencias'],['actividad','Actividad'],['tiempos','Tiempos']] as const).map(([id,n])=><button key={id} role="tab" type="button" aria-selected={pestana===id} className={'pestana'+(pestana===id?' activa':'')} onClick={()=>setPestana(id)}>{n}</button>)}</div>

  {pestana==='inventario'&&<div role="tabpanel">
   <h3>Materiales a atender primero</h3>
   {urgentes.length?<ul className="lista-urgentes">{urgentes.slice(0,LIMITE_PANTALLA).map(m=>{const mat=e.materiales.find(x=>x.id===m.id);return <li key={m.id}><div className="celda-pieza"><MiniFoto linea={e.linea} imagenes={e.imagenes} material={m.id} numeroParte={m.numeroParte} descripcion={mat?.descripcion} tam={36}/><div><strong className="parte">{m.numeroParte}</strong><small className="block">{mat?.descripcion}</small></div></div><span className="urgente-saldo">{nd(m.saldo)} pz / mín {m.minimo}</span><span className={'estado-pill kpi-'+m.estado.toLowerCase()}>{ESTADOS_KPI[m.estado]}</span></li>})}</ul>:<p className="notice">Ningún material está agotado, crítico ni en advertencia.</p>}
   {urgentes.length>LIMITE_PANTALLA&&<p className="help">Se muestran {LIMITE_PANTALLA} de {urgentes.length}; los demás están en la lista completa y en el Excel.</p>}
   <details className="ver-todos"><summary><span className="resumen-titulo">Ver los {st!.materiales.length} materiales</span><small className="resumen-ayuda">{nd(st!.inventario)} pz en total · {nd(Math.round(st!.cajasEquivalentes*10)/10)} cajas equivalentes</small></summary>
    <div className="table-wrap"><table><thead><tr><th>Parte</th><th>Saldo</th><th>Mínimo</th><th>Estado</th><th>Ubicaciones</th></tr></thead><tbody>{st!.materiales.map(m=><tr key={m.id}><td><strong className="parte">{m.numeroParte}</strong><small className="block">{e.materiales.find(x=>x.id===m.id)?.descripcion}</small></td><td>{nd(m.saldo)}</td><td>{m.minimo}</td><td><span className={'estado-pill kpi-'+m.estado.toLowerCase()}>{ESTADOS_KPI[m.estado]??m.estado}</span></td><td>{m.ubicaciones}</td></tr>)}</tbody></table></div>
   </details>
   <details className="simple como-se-calcula"><summary><Info size={15} aria-hidden="true"/>¿Cómo se calcula?</summary><p className="help">Compara el saldo total del material al corte con su mínimo: Agotado = 0 piezas; Crítico = saldo igual o menor al mínimo; Advertencia = hasta 1.2 veces el mínimo. No es la alarma de minutos por ubicación (esa está en Inventario y Resumen). Con una estación elegida solo cuentan los materiales que tienen ubicación en ella.</p></details>
  </div>}

  {pestana==='actividad'&&<div role="tabpanel">
   <div className="kpis-mini">{[['Completados',ac!.completados],['En curso al corte',ac!.enCurso],['Repetidos en un día',ac!.repetidos],['Registros incompletos',ac!.incompletos],['Intervalo promedio',ac!.intervaloPromedioHoras==null?null:nd(ac!.intervaloPromedioHoras)+' h']].map(([n,v])=><div key={String(n)}><small>{n}</small><strong>{nd(v)}</strong></div>)}</div>
   <div className="diagnostico-columnas">
    <div><h3>Completados por día</h3>{ac!.porDia.length?<ul className="barras-dia">{(()=>{const dias=ac!.porDia.slice(-LIMITE_PANTALLA);const max=Math.max(...dias.map(x=>x.cantidad),1);return dias.map(x=><li key={x.dia}><span>{x.dia.slice(5)}</span><span className="barra"><span style={{width:(x.cantidad/max*100)+'%'}}/></span><strong>{x.cantidad}</strong></li>)})()}</ul>:<p className="empty">Sin atenciones completadas en el periodo.</p>}{ac!.porDia.length>LIMITE_PANTALLA&&<p className="help">Últimos {LIMITE_PANTALLA} días; el resto en el Excel.</p>}</div>
    <div><h3>Repeticiones el mismo día</h3>{ac!.repeticiones.length?<ul className="lista-simple">{ac!.repeticiones.slice(0,10).map((r,i)=><li key={i}><strong className="parte">{e.materiales.find(m=>m.id===r.material)?.numeroParte??r.material}</strong> · {r.estacion} · {r.dia}<span>+{r.adicionales}</span></li>)}</ul>:<p className="empty">Sin repeticiones en el periodo.</p>}{ac!.repeticiones.length>10&&<p className="help">Se muestran 10 de {ac!.repeticiones.length}; el detalle está en el Excel.</p>}</div>
   </div>
   <details className="simple como-se-calcula"><summary><Info size={15} aria-hidden="true"/>¿Cómo se calcula?</summary><p className="help">{ac!.nota} Completados se cuenta por fecha de fin; las atenciones abiertas pueden venir de días anteriores. El intervalo promedio compara inicios consecutivos del mismo material y estación ({ac!.intervalosEvaluados} evaluados); N/D si no hay dos comparables.</p></details>
  </div>}

  {pestana==='tiempos'&&<div role="tabpanel"><AdminDiagnostico datos={datos} incrustado/></div>}
 </section></>}
 </div>;
}
