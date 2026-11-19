// Indicadores acordados con el Equipo 2 para la entrega del 05/11/2026 (KPIhoja1.pdf y KPIhoja2.pdf).
// Hoja 1 · Estado actual: [04] inventario, [13] cajas equivalentes, [10] advertencia, [11] crítico y agotados.
// Hoja 2 · Actividad logística: [03] completados, [21] en curso, [18] repetidos en un día, [12] incompletos,
// [17] intervalo promedio. Los valores los calcula el backend (/api/kpis); esta pantalla solo los presenta y exporta.
import {useEffect,useRef,useState} from 'react';
import {Download,RefreshCw} from 'lucide-react';
import {api,date,nd} from './api';

export type Indicador={cantidad:number;porcentaje:number|null};
export type MaterialKpi={parte:string;saldo:number|null;minimo:number|null;estado:string;porcentajeMinimo:number|null;posiciones:number};
export type PosicionKpi={id_ubicacion:number;numero_parte:string;descripcion:string|null;puesto:string;estacion:string;estanteria:string;nivel:string;cantidad:number|null;cantidad_por_caja:number|null;minimo:number|null;cajas_estanteria:number|null};
export type OperacionKpi={id:number;parte:string;estacion:string;posicion:number;puesto:number;turnoInicio:number|null;inicio:string|null;fin:string|null;responsable:string|null;nombre:string|null;proceso:string;completada:boolean;abierta:boolean;ubicacionValida:boolean};
export type Kpis={
 contexto:{corteLocal:string;desde:string;hasta:string;modo:string;fechaStock:string;denominador:number;estacion:string;parte:string};
 estadoActual:{inventario:number|null;cajasEquivalentes:number|null;posiciones:number;posicionesConSaldo:number;sinSaldo:number;sinEmpaque:number;sinEvaluar:number;materialesEvaluados:number;advertencia:Indicador;critico:Indicador;agotado:Indicador;materiales:MaterialKpi[];inventarioDetalle:PosicionKpi[]};
 actividad:{completados:number;enCurso:number;repetidos:number;incompletos:number;intervaloPromedioHoras:number|null;intervalosEvaluados:number;operacionesIniciadas:number;operacionesEvaluadasCalidad:number;porDia:{dia:string;cantidad:number}[];materiales:{parte:string;cantidad:number}[];repeticiones:{parte:string;estacion:string;dia:string;operaciones:number;adicionales:number}[];ultimas:{parte:string;estacion:string;fin:string;horas:number}[];operaciones:OperacionKpi[]};
};
type Opciones={estaciones:string[];materiales:string[]};

export const estadoTexto:Record<string,string>={NORMAL:'Normal',ADVERTENCIA:'Advertencia',CRITICO:'Crítico',AGOTADO:'Agotado',SIN_DATOS:'Sin datos'};
const ordenEstado:Record<string,number>={AGOTADO:0,CRITICO:1,ADVERTENCIA:2,NORMAL:3,SIN_DATOS:4};
export const pct=(v:number|null|undefined)=>v==null?'N/D':new Intl.NumberFormat('es-MX',{maximumFractionDigits:1}).format(v)+' %';
export const dec=(v:number|null|undefined,d=1)=>v==null?'N/D':new Intl.NumberFormat('es-MX',{maximumFractionDigits:d}).format(v);
// Prioridad del diseño acordado: agotados -> críticos -> advertencia; dentro de cada estado, menor cobertura primero.
export const atencion=(m:MaterialKpi[])=>m.filter(x=>x.estado==='AGOTADO'||x.estado==='CRITICO'||x.estado==='ADVERTENCIA')
 .sort((a,b)=>ordenEstado[a.estado]-ordenEstado[b.estado]||(a.porcentajeMinimo??0)-(b.porcentajeMinimo??0)||a.parte.localeCompare(b.parte));
// Fecha y hora local de México en el formato que recibe el backend.
export function ahoraMexico(desfaseDias=0){const d=new Date(Date.now()+desfaseDias*864e5);const p=Object.fromEntries(new Intl.DateTimeFormat('en-CA',{timeZone:'America/Mexico_City',year:'numeric',month:'2-digit',day:'2-digit',hour:'2-digit',minute:'2-digit',second:'2-digit',hourCycle:'h23'}).formatToParts(d).map(x=>[x.type,x.value]));return {fecha:`${p.year}-${p.month}-${p.day}`,corte:`${p.year}-${p.month}-${p.day}T${p.hour}:${p.minute}:${p.second}.${String(d.getUTCMilliseconds()).padStart(3,'0')}`};}

function Tarjeta({titulo,valor,nota,tono}:{titulo:string;valor:string;nota:string;tono?:'advertencia'|'critico'|'agotado'}){
 return <section className={'kpi-card'+(tono?' kpi-'+tono:'')}><small>{titulo}</small><strong>{valor}</strong><p>{nota}</p></section>;
}

export function Indicadores({sql=false}:{sql?:boolean}={}){
 const hoy=ahoraMexico(),semana=ahoraMexico(-6);
 const [corte,setCorte]=useState(hoy.corte),[desde,setDesde]=useState(semana.fecha),[hasta,setHasta]=useState(hoy.fecha),[estacion,setEstacion]=useState(''),[parte,setParte]=useState('');
 const [opciones,setOpciones]=useState<Opciones>(),[datos,setDatos]=useState<Kpis>(),[busy,setBusy]=useState(false),[exportando,setExportando]=useState(false),[error,setError]=useState('');
 const secuencia=useRef(0);
 async function consultar(f={corte,desde,hasta,estacion,parte}){
  const id=++secuencia.current;setBusy(true);setError('');
  const q=new URLSearchParams({corte:f.corte,desde:f.desde,hasta:f.hasta});if(f.estacion)q.set('estacion',f.estacion);if(f.parte)q.set('parte',f.parte);
  try{const r=await api<Kpis>('/kpis?'+q);if(id===secuencia.current)setDatos(r)}catch(e){if(id===secuencia.current)setError((e as Error).message)}finally{if(id===secuencia.current)setBusy(false)}
 }
 useEffect(()=>{api<Opciones>('/kpis/opciones').then(setOpciones).catch(()=>{});void consultar();return()=>{secuencia.current++}},[]);
 function semanaHistorico(){const f={corte:'2026-10-25T23:30',desde:'2026-10-19',hasta:'2026-10-25',estacion,parte};setCorte(f.corte);setDesde(f.desde);setHasta(f.hasta);void consultar(f)}
 async function descargar(){if(!datos)return;setExportando(true);setError('');
  try{const {libroKpis}=await import('./ExcelKpis');const blob=await libroKpis(datos);const a=document.createElement('a');a.href=URL.createObjectURL(blob);a.download=`motherson-l1-kpis-${datos.contexto.corteLocal.replace(/[:T]/g,'-')}.xlsx`;document.body.appendChild(a);a.click();a.remove();setTimeout(()=>URL.revokeObjectURL(a.href),1000)}
  catch(e){setError('No se pudo generar el Excel: '+(e as Error).message)}finally{setExportando(false)}}
 const e=datos?.estadoActual,a=datos?.actividad,c=datos?.contexto;
 const lista=e?atencion(e.materiales):[];
 return <>
  <section className="panel" aria-busy={busy}><h2>KPIs · Motherson L1</h2><p>Los 10 KPIs de la entrega: cinco de Estado actual y cinco de Actividad logística. El inventario se reconstruye desde su apertura acreditada; la actividad cuenta las atenciones registradas. Los movimientos de material se contabilizan por separado.</p>
   <form className="kpi-filtros" onSubmit={ev=>{ev.preventDefault();void consultar()}}>
    <label className="field">Corte (hora de México)<input type="datetime-local" step="0.001" value={corte} max={hoy.corte} onChange={ev=>setCorte(ev.target.value)} required/></label>
    <label className="field">Actividad desde<input type="date" value={desde} onChange={ev=>setDesde(ev.target.value)} required/></label>
    <label className="field">Actividad hasta<input type="date" value={hasta} onChange={ev=>setHasta(ev.target.value)} required/></label>
    <label className="field">Estación<select value={estacion} onChange={ev=>setEstacion(ev.target.value)}><option value="">Todas</option>{opciones?.estaciones.map(s=><option key={s}>{s}</option>)}</select></label>
    <label className="field">Material<select value={parte} onChange={ev=>setParte(ev.target.value)}><option value="">Todos</option>{opciones?.materiales.map(s=><option key={s}>{s}</option>)}</select></label>
   </form>
   <div className="kpi-acciones"><button disabled={busy} onClick={()=>void consultar()}><RefreshCw size={16} aria-hidden="true"/>{busy?'Calculando…':'Calcular indicadores'}</button>{!sql&&<button className="secondary" disabled={busy} onClick={semanaHistorico}>Semana del histórico (19–25 oct)</button>}<button className="secondary" disabled={!datos||busy||exportando} onClick={()=>void descargar()}><Download size={16} aria-hidden="true"/>{exportando?'Generando Excel…':'Descargar Excel'}</button></div>
   {error&&<p className="error" role="alert">{error}</p>}
   {c&&<p className="kpi-contexto">Corte {c.corteLocal.replace('T',' ')} · actividad del {c.desde} al {c.hasta}{c.estacion?' · estación '+c.estacion:''}{c.parte?' · '+c.parte:''} · datos SIMULADOS</p>}
  </section>
  {e&&a&&<>
   <h2 className="kpi-hoja">Hoja 1 · Estado actual</h2>
   <div className="kpi-grid">
    <Tarjeta titulo="Inventario actual en punto de uso" valor={nd(e.inventario)+' pz'} nota={`Suma de existencias en ${nd(e.posicionesConSaldo)} de ${nd(e.posiciones)} ubicaciones.`}/>
    <Tarjeta titulo="Cajas equivalentes disponibles" valor={dec(e.cajasEquivalentes)} nota="Piezas ÷ piezas por caja, sumado por material. Puede ser fraccionario."/>
    <Tarjeta tono="advertencia" titulo="Materiales en advertencia" valor={nd(e.advertencia.cantidad)} nota={`${pct(e.advertencia.porcentaje)} de ${nd(e.materialesEvaluados)} evaluados · mínimo < existencia ≤ 1.20 × mínimo.`}/>
    <Tarjeta tono="critico" titulo="Materiales en nivel crítico" valor={nd(e.critico.cantidad)} nota={`${pct(e.critico.porcentaje)} · 0 < existencia ≤ mínimo.`}/>
    <Tarjeta tono="agotado" titulo="Materiales agotados" valor={nd(e.agotado.cantidad)} nota={`${pct(e.agotado.porcentaje)} · existencia = 0.`}/>
   </div>
   <section className="panel"><h3>Materiales que requieren atención</h3><p>Orden: agotados, críticos y advertencia; dentro de cada estado, menor cobertura (existencia ÷ mínimo) primero.{e.sinEvaluar?` ${nd(e.sinEvaluar)} materiales sin mínimo o sin saldo quedan fuera del cálculo.`:''}</p>
    <div className="table-wrap" role="region" aria-label="Materiales que requieren atención" tabIndex={0}><table><thead><tr><th scope="col">Material</th><th scope="col">Estado</th><th scope="col">Existencia pz</th><th scope="col">Mínimo pz</th><th scope="col">Cobertura</th><th scope="col">Ubicaciones</th></tr></thead>
     <tbody>{lista.map(m=><tr key={m.parte}><td>{m.parte}</td><td><span className={'kpi-estado kpi-'+m.estado.toLowerCase()}>{estadoTexto[m.estado]}</span></td><td>{nd(m.saldo)}</td><td>{nd(m.minimo)}</td><td>{pct(m.porcentajeMinimo)}</td><td>{nd(m.posiciones)}</td></tr>)}</tbody></table>{!lista.length&&<p className="empty">Ningún material en advertencia, crítico o agotado.</p>}</div></section>
   <h2 className="kpi-hoja">Hoja 2 · Actividad logística</h2>
   <div className="kpi-grid">
    <Tarjeta titulo="Reabastecimientos completados" valor={nd(a.completados)} nota="Con fin registrado dentro del periodo y fin ≥ inicio."/>
    <Tarjeta titulo="Reabastecimientos en curso" valor={nd(a.enCurso)} nota="Iniciados y sin fin al corte."/>
    <Tarjeta tono="advertencia" titulo="Reabastecimientos repetidos en un día" valor={nd(a.repetidos)} nota="Operaciones adicionales a la primera del mismo material y estación en el día."/>
    <Tarjeta tono="critico" titulo="Registros incompletos" valor={nd(a.incompletos)} nota={`Sin responsable o sin ubicación, de ${nd(a.operacionesEvaluadasCalidad)} evaluados.`}/>
    <Tarjeta titulo="Intervalo promedio entre reabastecimientos" valor={a.intervaloPromedioHoras==null?'N/D':dec(a.intervaloPromedioHoras)+' h'} nota={`Entre inicios consecutivos del mismo material y estación · ${nd(a.intervalosEvaluados)} intervalos.`}/>
   </div>
   <div className="kpi-dos">
    <section className="panel"><h3>Completados por día</h3><div className="table-wrap" tabIndex={0} role="region" aria-label="Completados por día"><table><thead><tr><th scope="col">Día</th><th scope="col">Completados</th></tr></thead><tbody>{a.porDia.map(d=><tr key={d.dia}><td>{d.dia}</td><td>{nd(d.cantidad)}</td></tr>)}</tbody></table>{!a.porDia.length&&<p className="empty">Sin reabastecimientos completados en el periodo.</p>}</div></section>
    <section className="panel"><h3>Repeticiones por material y estación</h3><div className="table-wrap" tabIndex={0} role="region" aria-label="Repeticiones"><table><thead><tr><th scope="col">Día</th><th scope="col">Material</th><th scope="col">Estación</th><th scope="col">Surtimientos</th></tr></thead><tbody>{a.repeticiones.map(r=><tr key={r.dia+r.parte+r.estacion}><td>{r.dia}</td><td>{r.parte}</td><td>{r.estacion}</td><td>{nd(r.operaciones)}</td></tr>)}</tbody></table>{!a.repeticiones.length&&<p className="empty">Sin repeticiones en el periodo.</p>}</div></section>
   </div>
   <p className="kpi-contexto">Último cálculo con inventario al corte {date(c?.fechaStock)!=='N/D'?'(saldo inicial '+date(c?.fechaStock)+')':''}. La descarga en Excel incluye estos valores y sus tablas de detalle.</p>
  </>}
 </>;
}
