// Libro de Excel de los 10 KPIs (resumen) y sus tablas de detalle, generado en el navegador a partir de /api/kpis.
// Se carga bajo demanda para no aumentar el tamaño inicial del portal.
import ExcelJS from 'exceljs';
import {type Kpis,atencion,estadoTexto} from './Indicadores';

const rojo='FFDA2128';
type Col={header:string;key:string;width:number;fmt?:string};

function hoja(libro:ExcelJS.Workbook,nombre:string,columnas:Col[],filas:Record<string,unknown>[]){
 const h=libro.addWorksheet(nombre,{views:[{state:'frozen',ySplit:1}]});
 h.columns=columnas.map(c=>({header:c.header,key:c.key,width:c.width,style:c.fmt?{numFmt:c.fmt}:{}}));
 h.getRow(1).eachCell(celda=>{celda.font={bold:true,color:{argb:'FFFFFFFF'}};celda.fill={type:'pattern',pattern:'solid',fgColor:{argb:rojo}};celda.alignment={vertical:'middle',wrapText:true}});
 h.addRows(filas);
 if(filas.length)h.autoFilter={from:{row:1,column:1},to:{row:1,column:columnas.length}};
 return h;
}
// Fechas del backend en UTC -> texto local de México (Excel no guarda zona horaria).
const local=(s:string|null|undefined)=>s?new Date(/Z$|[+-]\d{2}:\d{2}$/.test(s)?s:s+'Z').toLocaleString('es-MX',{timeZone:'America/Mexico_City',hour12:false}):'';
const fraccion=(v:number|null|undefined)=>v==null?null:v/100;

export async function libroKpis(d:Kpis):Promise<Blob>{
 const libro=new ExcelJS.Workbook();libro.creator='Motherson L1 · Portal';libro.created=new Date();
 const e=d.estadoActual,a=d.actividad,c=d.contexto;
 const resumen=hoja(libro,'Resumen KPIs',[
  {header:'Hoja',key:'hoja',width:24},{header:'ID',key:'id',width:7},{header:'Indicador',key:'kpi',width:42},{header:'Valor',key:'valor',width:14,fmt:'#,##0.0'},
  {header:'Unidad',key:'unidad',width:14},{header:'Porcentaje',key:'pct',width:12,fmt:'0.0%'},{header:'Cálculo',key:'calculo',width:70}],[
  {hoja:'1 · Estado actual',id:'[04]',kpi:'Inventario actual en punto de uso',valor:e.inventario,unidad:'piezas',calculo:'SUM(UBICACION.cantidad) al corte'},
  {hoja:'1 · Estado actual',id:'[13]',kpi:'Cajas equivalentes disponibles',valor:e.cajasEquivalentes,unidad:'cajas',calculo:'Σ existencia ÷ MATERIAL.cantidad_por_caja'},
  {hoja:'1 · Estado actual',id:'[10]',kpi:'Materiales en advertencia',valor:e.advertencia.cantidad,unidad:'materiales',pct:fraccion(e.advertencia.porcentaje),calculo:'mínimo < existencia ≤ 1.20 × mínimo; % sobre materiales evaluados'},
  {hoja:'1 · Estado actual',id:'[11]',kpi:'Materiales en nivel crítico',valor:e.critico.cantidad,unidad:'materiales',pct:fraccion(e.critico.porcentaje),calculo:'0 < existencia ≤ mínimo'},
  {hoja:'1 · Estado actual',id:'[01]',kpi:'Materiales agotados',valor:e.agotado.cantidad,unidad:'materiales',pct:fraccion(e.agotado.porcentaje),calculo:'existencia = 0'},
  {hoja:'2 · Actividad logística',id:'[03]',kpi:'Reabastecimientos completados',valor:a.completados,unidad:'operaciones',calculo:'fecha_hora_fin informada, dentro del periodo y ≥ fecha_hora_inicio'},
  {hoja:'2 · Actividad logística',id:'[21]',kpi:'Reabastecimientos en curso',valor:a.enCurso,unidad:'operaciones',calculo:'fecha_hora_inicio informada y fecha_hora_fin vacía al corte'},
  {hoja:'2 · Actividad logística',id:'[18]',kpi:'Reabastecimientos repetidos en un día',valor:a.repetidos,unidad:'operaciones',calculo:'Σ MAX(registros − 1, 0) por día, material y estación'},
  {hoja:'2 · Actividad logística',id:'[12]',kpi:'Registros incompletos',valor:a.incompletos,unidad:'registros',calculo:'Sin responsable o sin ubicación identificada'},
  {hoja:'2 · Actividad logística',id:'[17]',kpi:'Intervalo promedio entre reabastecimientos',valor:a.intervaloPromedioHoras,unidad:'horas',calculo:'Promedio entre inicios consecutivos del mismo material y estación'}]);
 resumen.addRow([]);
 for(const [k,v] of [['Corte (hora de México)',c.corteLocal.replace('T',' ')],['Periodo de actividad',`${c.desde} a ${c.hasta}`],['Estación',c.estacion||'Todas'],['Material',c.parte||'Todos'],['Materiales evaluados',e.materialesEvaluados],['Fuente',c.modo],['Naturaleza de los datos','SIMULADO']] as [string,unknown][]){const r=resumen.addRow([k,'',v]);r.getCell(1).font={bold:true}}

 hoja(libro,'Atención',[{header:'Material',key:'parte',width:26},{header:'Estado',key:'estado',width:14},{header:'Existencia pz',key:'saldo',width:14,fmt:'#,##0'},{header:'Mínimo pz',key:'minimo',width:12,fmt:'#,##0'},{header:'Cobertura',key:'cobertura',width:12,fmt:'0.0%'},{header:'Ubicaciones',key:'posiciones',width:12}],
  atencion(e.materiales).map(m=>({parte:m.parte,estado:estadoTexto[m.estado],saldo:m.saldo,minimo:m.minimo,cobertura:fraccion(m.porcentajeMinimo),posiciones:m.posiciones})));
 hoja(libro,'Materiales',[{header:'Material',key:'parte',width:26},{header:'Estado',key:'estado',width:14},{header:'Existencia pz',key:'saldo',width:14,fmt:'#,##0'},{header:'Mínimo pz',key:'minimo',width:12,fmt:'#,##0'},{header:'Cobertura',key:'cobertura',width:12,fmt:'0.0%'},{header:'Ubicaciones',key:'posiciones',width:12}],
  e.materiales.map(m=>({parte:m.parte,estado:estadoTexto[m.estado]??m.estado,saldo:m.saldo,minimo:m.minimo,cobertura:fraccion(m.porcentajeMinimo),posiciones:m.posiciones})));
 hoja(libro,'Inventario',[{header:'ID ubicación',key:'id',width:12},{header:'Material',key:'parte',width:26},{header:'Descripción',key:'descripcion',width:30},{header:'Puesto',key:'puesto',width:14},{header:'Estación',key:'estacion',width:12},{header:'Estantería',key:'estanteria',width:12},{header:'Nivel',key:'nivel',width:8},{header:'Existencia pz',key:'cantidad',width:14,fmt:'#,##0'},{header:'Pz por caja',key:'factor',width:12},{header:'Cajas equivalentes',key:'cajas',width:16,fmt:'#,##0.00'},{header:'Capacidad cajas',key:'capacidad',width:15},{header:'Mínimo pz (material)',key:'minimo',width:18}],
  e.inventarioDetalle.map(p=>({id:p.id_ubicacion,parte:p.numero_parte,descripcion:p.descripcion,puesto:p.puesto,estacion:p.estacion,estanteria:p.estanteria,nivel:p.nivel,cantidad:p.cantidad,factor:p.cantidad_por_caja,cajas:p.cantidad!=null&&p.cantidad_por_caja?p.cantidad/p.cantidad_por_caja:null,capacidad:p.cajas_estanteria,minimo:p.minimo})));
 hoja(libro,'Registros',[{header:'ID',key:'id',width:10},{header:'Origen',key:'origen',width:18},{header:'Material',key:'parte',width:26},{header:'Estación',key:'estacion',width:12},{header:'ID ubicación',key:'posicion',width:12},{header:'Puesto',key:'puesto',width:8},{header:'Responsable',key:'responsable',width:24},{header:'Turno inicio',key:'turno',width:12},{header:'Inicio',key:'inicio',width:20},{header:'Fin',key:'fin',width:20},{header:'Completado',key:'completada',width:12},{header:'En curso',key:'abierta',width:10},{header:'Ubicación válida',key:'valida',width:15}],
  a.operaciones.map(o=>({id:o.id<0?-o.id:o.id,origen:o.proceso==='PORTAL_REGISTRO'?'Portal (REGISTRO)':'Histórico maestro',parte:o.parte,estacion:o.estacion,posicion:o.posicion,puesto:o.puesto,responsable:o.nombre??o.responsable??'',turno:o.turnoInicio,inicio:local(o.inicio),fin:local(o.fin),completada:o.completada?'Sí':'No',abierta:o.abierta?'Sí':'No',valida:o.ubicacionValida?'Sí':'No'})));
 hoja(libro,'Por día',[{header:'Día',key:'dia',width:14},{header:'Completados',key:'cantidad',width:14}],a.porDia);
 hoja(libro,'Repetidos',[{header:'Día',key:'dia',width:14},{header:'Material',key:'parte',width:26},{header:'Estación',key:'estacion',width:12},{header:'Surtimientos',key:'operaciones',width:14},{header:'Adicionales',key:'adicionales',width:12}],a.repeticiones);
 hoja(libro,'Más reabastecidos',[{header:'Material',key:'parte',width:26},{header:'Completados',key:'cantidad',width:14}],a.materiales);
 hoja(libro,'Último reabasto',[{header:'Material',key:'parte',width:26},{header:'Estación',key:'estacion',width:12},{header:'Último fin',key:'fin',width:20},{header:'Horas desde entonces',key:'horas',width:20,fmt:'#,##0.0'}],a.ultimas.map(u=>({...u,fin:local(u.fin)})));
 const buffer=await libro.xlsx.writeBuffer();
 return new Blob([buffer],{type:'application/vnd.openxmlformats-officedocument.spreadsheetml.sheet'});
}
