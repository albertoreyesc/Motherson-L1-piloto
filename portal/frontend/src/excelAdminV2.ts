import type {KpisAdmin,Material} from './adminTypes';
/** Fecha y hora de Ciudad de México legible en Excel (vacía si no hay dato). */
export function fechaLocalExcel(iso:string|null|undefined){
 if(!iso)return '';const d=new Date(iso);if(Number.isNaN(d.getTime()))return iso;
 const p=new Intl.DateTimeFormat('en-CA',{timeZone:'America/Mexico_City',year:'numeric',month:'2-digit',day:'2-digit',hour:'2-digit',minute:'2-digit',second:'2-digit',hourCycle:'h23'}).formatToParts(d);
 const v=(t:string)=>p.find(x=>x.type===t)?.value??'';return v('year')+'-'+v('month')+'-'+v('day')+' '+v('hour')+':'+v('minute')+':'+v('second');
}
/** Minutos entre inicio y fin (null si sigue abierta o no es válida). */
export function duracionMinutos(inicio:string,fin:string|null){
 if(!fin)return null;const m=(new Date(fin).getTime()-new Date(inicio).getTime())/60000;return Number.isFinite(m)&&m>=0?Math.round(m*10)/10:null;
}
/**
 * Libro de KPIs. La Hoja 4 lleva el histórico completo de atenciones del periodo consultado:
 * se consulta en Excel en lugar de mostrarse completo dentro del portal.
 */
export const ESTADOS_KPI:Record<string,string>={AGOTADO:'Agotado',CRITICO:'Crítico',ADVERTENCIA:'Advertencia',NORMAL:'Normal',SIN_DATOS:'Sin saldo acreditado'};
/**
 * Estado actual respetando el filtro de estación: con una estación elegida solo cuentan los materiales que tienen
 * ubicación en ella (los demás no pertenecen a esa estación). Sin filtro de estación no cambia nada.
 */
export function estadoFiltrado(datos:KpisAdmin){
 const st=datos.estadoActual;
 // Los conteos salen siempre de la misma lista que se muestra, para que tarjetas, tabla y Excel coincidan.
 const materiales=datos.contexto.estacion?st.materiales.filter(m=>m.ubicaciones>0):st.materiales;const n=(e:string)=>materiales.filter(m=>m.estado===e).length;
 return {...st,materiales,agotados:n('AGOTADO'),criticos:n('CRITICO'),advertencia:n('ADVERTENCIA'),sinDatos:n('SIN_DATOS')};
}
type Nombres={estaciones?:{id:string;nombre:string}[]};
export async function libroAdminV2(datos:KpisAdmin,materiales:Material[],lugar:(ubicacion:string)=>string=u=>u,nombres:Nombres={}){
 const {default:ExcelJS}=await import('exceljs');const b=new ExcelJS.Workbook();const a=b.addWorksheet('Hoja 1 - Estado actual'),c=b.addWorksheet('Hoja 2 - Actividad logística'),d=b.addWorksheet('Hoja 3 - Diagnóstico'),h=b.addWorksheet('Hoja 4 - Histórico atenciones');
  const cx=datos.contexto as Record<string,unknown>;const est=String(cx.estacion??''),mat=String(cx.material??''),tur=String(cx.turno??'');
  const filtros=['Estación: '+(est?nombres.estaciones?.find(x=>x.id===est)?.nombre??est:'Todas'),'Material: '+(mat?materiales.find(x=>x.id===mat)?.numeroParte??mat:'Todos'),'Turno: '+(tur==='1'?'06:00 a 14:59':tur==='2'?'15:00 a 05:59':'Todos')];
  for(const s of [a,c,d,h]){s.addRow(['Línea',cx.linea]);s.addRow(['Corte (hora de Ciudad de México)',fechaLocalExcel(String(cx.corte??''))]);s.addRow(['Periodo',cx.desde+' al '+cx.hasta]);s.addRow(['Filtros',filtros.join(' · ')]);}
  const st=estadoFiltrado(datos);for(const r of [['Inventario pz',st.inventario],['Cajas equivalentes',Math.round(st.cajasEquivalentes*10)/10],['Materiales',st.materiales.length],['Agotados',st.agotados],['Críticos',st.criticos],['Advertencia',st.advertencia],['Sin saldo acreditado',st.sinDatos]])a.addRow(r);a.addRow([]);a.addRow(['Parte','Descripción','Saldo pz','Mínimo','Estado','Activo','Ubicaciones']).font={bold:true};st.materiales.forEach(m=>a.addRow([m.numeroParte,materiales.find(x=>x.id===m.id)?.descripcion??'',m.saldo??'',m.minimo,ESTADOS_KPI[m.estado]??m.estado,m.activo?'Sí':'No',m.ubicaciones]));if(!st.materiales.length)a.addRow(['Sin materiales con ubicación para estos filtros.']);
  const ac=datos.actividad;for(const r of [['Completados',ac.completados],['En curso',ac.enCurso],['Repetidos',ac.repetidos],['Incompletos',ac.incompletos],['Intervalo promedio h',ac.intervaloPromedioHoras??'N/D'],['Intervalos evaluados',ac.intervalosEvaluados]])c.addRow(r);c.addRow([]);c.addRow(['Día','Completados']);ac.porDia.forEach(r=>c.addRow([r.dia,r.cantidad]));c.addRow([]);c.addRow(['Día','Material','Estación','Atenciones','Adicionales']);ac.repeticiones.forEach(r=>c.addRow([r.dia,materiales.find(m=>m.id===r.material)?.numeroParte??r.material,r.estacion,r.cantidad,r.adicionales]));c.addRow([]);c.addRow(['El detalle de cada atención está en la Hoja 4 - Histórico atenciones.']);
  h.addRow(['Atenciones',ac.operaciones.length]);h.addRow(['Horas en hora de Ciudad de México. Una atención sin fin sigue en curso al corte.']);h.addRow([]);
  const encabezado=h.addRow(['Inicio','Fin','Duración (min)','Número de parte','Descripción','Ubicación','Responsable','Turno','Estado','Origen','Atención']);encabezado.font={bold:true};
  [...ac.operaciones].sort((x,y)=>String(y.inicio).localeCompare(String(x.inicio))).forEach(r=>{const m=materiales.find(x=>x.id===r.material);const o=r as typeof r&{origen?:string};h.addRow([fechaLocalExcel(r.inicio),fechaLocalExcel(r.fin),duracionMinutos(r.inicio,r.fin)??'',m?.numeroParte??r.material,m?.descripcion??'',lugar(r.ubicacion),r.responsable,r.turno,r.fin?'Completada':'En curso',o.origen==='REGISTRO_ORIGINAL'?'Histórico original':'Portal',r.id])});
  if(!ac.operaciones.length)h.addRow(['Sin atenciones en este periodo con estos filtros. Prueba un periodo más amplio o “Exportar histórico completo a Excel”.']);
  h.views=[{state:'frozen',ySplit:8}];if(ac.operaciones.length)h.autoFilter={from:{row:8,column:1},to:{row:8,column:11}};const dg=datos.diagnostico;if(dg){for(const r of [['Objetivo minutos',dg.objetivoMinutos],['Completados válidos',dg.completadosValidos],['Tiempo promedio min',dg.promedioMinutos??'N/D'],['Tiempo P90 min',dg.p90Minutos??'N/D'],['Fuera objetivo %',dg.porcentajeFueraObjetivo??'N/D'],['Duraciones inválidas',dg.duracionesInvalidas]])d.addRow(r);d.addRow([]);d.addRow(['Puesto','Completados','Promedio min','Horas acumuladas']);dg.porPuesto.forEach(p=>d.addRow([p.nombre,p.completados,p.promedioMinutos,p.horas]));d.addRow([]);d.addRow(['Parte','Completados','Promedio min','Horas','Muestra >=10']);dg.porMaterial.forEach(m=>d.addRow([m.numeroParte,m.completados,m.promedioMinutos,m.horas,m.rankingConfiable?'Sí':'No']));d.addRow([]);d.addRow(['Hora local','Inicios']);dg.porHora.forEach(h=>d.addRow([h.hora,h.inicios]));d.addRow([]);d.addRow(['Turno','Completados','Promedio min','Horas']);dg.porTurno.forEach(t=>d.addRow([t.turno,t.completados,t.promedioMinutos,t.horas]));d.addRow([]);d.addRow(['Lectura del período']);dg.recomendaciones.forEach(n=>d.addRow([n]));}else d.addRow(['Vuelve a calcular para obtener diagnóstico.']);for(const s of [a,c,d,h])s.columns.forEach(col=>col.width=27);
 return b;
}
