import {date,nd} from './api';
import type {KpisAdmin} from './adminTypes';

/** Hoja 3 · Diagnóstico. Sin actividad en el periodo muestra un solo aviso en lugar de tablas vacías. */
export function AdminDiagnostico({datos,incrustado=false}:{datos:KpisAdmin;incrustado?:boolean}){
 const d=datos.diagnostico;
 if(!d)return <section className="panel"><h2>Hoja 3 · Diagnóstico</h2><p>Vuelve a calcular los indicadores para cargar el diagnóstico.</p></section>;
 const encabezado=<p className="help">Del {String(datos.contexto.desde)} al {String(datos.contexto.hasta)} · corte {date(datos.contexto.corte)} · mismos filtros de línea, estación, material y turno que las otras hojas.</p>;
 const lectura=<><h3>Lectura del período</h3><ul>{d.recomendaciones.map((n,i)=><li key={i}>{n}</li>)}</ul></>;
 const sinActividad=d.completadosValidos===0&&!d.porPuesto.length&&!d.porMaterial.length;
 if(sinActividad)return <section className={incrustado?'diagnostico':'panel diagnostico'}>{!incrustado&&<><h2>Hoja 3 · Diagnóstico</h2>{encabezado}</>}
  <p className="notice">No hay atenciones completadas en este periodo con estos filtros, así que no hay tiempos que diagnosticar. Prueba un periodo más amplio o usa “Exportar histórico completo a Excel”.</p>{lectura}</section>;
 const horas=d.porHora.filter(h=>h.inicios>0);
 return <section className={incrustado?'diagnostico':'panel diagnostico'}>{!incrustado&&<><h2>Hoja 3 · Diagnóstico</h2>{encabezado}</>}
  {!incrustado&&<div className="kpis">{[['Completados válidos',d.completadosValidos],['Tiempo promedio (min)',d.promedioMinutos],['Tiempo P90 (min)',d.p90Minutos],['Fuera del objetivo (%)',d.porcentajeFueraObjetivo]].map(([n,v])=><section key={String(n)}><small>{n}</small><strong>{nd(v)}</strong></section>)}</div>}
  <p className="help">Objetivo: {d.objetivoMinutos} minutos por atención. P90: el 90 % de las atenciones tardó eso o menos. Se excluyeron {d.duracionesInvalidas} duraciones inválidas.</p>
  {lectura}
  <h3>Actividad por puesto</h3>
  {d.porPuesto.length?<div className="table-wrap"><table><thead><tr><th>Puesto</th><th>Completados</th><th>Promedio min</th><th>Horas acumuladas</th></tr></thead><tbody>{d.porPuesto.map(p=><tr key={p.puesto}><td>{p.nombre}</td><td>{nd(p.completados)}</td><td>{nd(p.promedioMinutos)}</td><td>{nd(p.horas)}</td></tr>)}</tbody></table></div>:<p className="empty">Sin actividad completada por puesto.</p>}
  <h3>Tiempos por material</h3>
  {d.porMaterial.length?<div className="table-wrap"><table><thead><tr><th>Parte</th><th>Completados</th><th>Promedio min</th><th>Horas</th><th>Muestra para ranking</th></tr></thead><tbody>{d.porMaterial.map(m=><tr key={m.material}><td><strong className="parte">{m.numeroParte}</strong></td><td>{nd(m.completados)}</td><td>{nd(m.promedioMinutos)}</td><td>{nd(m.horas)}</td><td>{m.rankingConfiable?'Suficiente (10 o más)':'Insuficiente (menos de 10)'}</td></tr>)}</tbody></table></div>:<p className="empty">Sin tiempos por material.</p>}
  <div className="diagnostico-columnas">
   <div><h3>Inicios por hora</h3>{horas.length?<div className="table-wrap"><table><thead><tr><th>Hora</th><th>Inicios</th></tr></thead><tbody>{horas.map(h=><tr key={h.hora}><td>{String(h.hora).padStart(2,'0')}:00</td><td>{nd(h.inicios)}</td></tr>)}</tbody></table></div>:<p className="empty">Sin inicios en el periodo.</p>}</div>
   <div><h3>Comparación por turno</h3>{d.porTurno.length?<div className="table-wrap"><table><thead><tr><th>Turno</th><th>Completados</th><th>Promedio min</th><th>Horas</th></tr></thead><tbody>{d.porTurno.map(t=><tr key={t.turno}><td>{t.turno===1?'1 · 06:00 a 14:59':t.turno===2?'2 · 15:00 a 05:59':String(t.turno)}</td><td>{nd(t.completados)}</td><td>{nd(t.promedioMinutos)}</td><td>{nd(t.horas)}</td></tr>)}</tbody></table></div>:<p className="empty">Sin atenciones por turno.</p>}</div>
  </div>
 </section>;
}
