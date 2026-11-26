// Traduce los registros del historial (eventos v2 y movimientos anteriores) a lenguaje de planta:
// una operación con nombre claro, una frase de detalle y una lista de datos con etiquetas, sin JSON.
import type {EstadoAdmin} from './adminTypes';
export type FilaHistorial=Record<string,unknown>;
export type EventoLegible={operacion:string;detalle:string;campos:[string,string][];comprobante:string};
type Ctx=Pick<EstadoAdmin,'materiales'|'ubicaciones'|'estanterias'|'estaciones'|'puestos'>;

const OPERACIONES:Record<string,string>={ENTRADA:'Entrada',SALIDA:'Salida',ATENCION_INICIAR:'Inicio de atención',ATENCION_FINALIZAR:'Fin de atención',
 MATERIAL_ESTADO:'Estado de material',MATERIAL_RENOMBRAR:'Cambio de número de parte',MATERIAL_ELIMINAR:'Material eliminado',MATERIAL_DUPLICAR:'Nueva revisión de material',
 PUESTO:'Puesto logístico',ESTACION:'Estación',ESTANTERIA:'Estantería',UBICACION:'Material asignado a estantería',UBICACION_ESTADO:'Estado de ubicación',
 IMPORTAR:'Importación de catálogo',CALENDARIO:'Horario de trabajo',IMAGEN:'Foto de material',USUARIO_CREAR:'Alta de usuario',USUARIO_ACTUALIZAR:'Cambio de usuario',APERTURA:'Apertura del sistema'};
const ETIQUETAS:Record<string,string>={numeroParte:'Número de parte',numero_parte:'Número de parte',piezas:'Piezas',cajas:'Cajas',saldoAnterior:'Saldo anterior',saldo_anterior:'Saldo anterior',
 saldoPosterior:'Saldo después',saldo_posterior:'Saldo después',consumo:'Consumo de producción',motivo:'Motivo',nombre:'Nombre',nivel:'Nivel',capacidad:'Espacios (cajas)',
 material:'Material',ubicacion:'Ubicación',estanteria:'Estantería',estacion:'Estación',puesto:'Puesto',activo:'Activo',activa:'Activa',altas:'Materiales nuevos',
 actualizados:'Materiales actualizados',puestosCreados:'Puestos creados',desde:'Desde',hasta:'Hasta',fuente:'Autorizado por',usuario:'Usuario',rol:'Rol',
 tipo_movimiento:'Movimiento',cantidad_movimiento:'Cantidad',usuario_captura:'Capturó',responsable:'Responsable',piezasTrasladadas:'Piezas trasladadas',
 atencionFinalizada:'Atención finalizada',origen:'Origen',eliminada:'Foto quitada',cambioContrasena:'Cambió contraseña',revision:'Revisión',descripcion:'Descripción'};
const OMITIR=new Set(['id','version','operacion','tipo','fecha','linea','huella','filas','dias','turnos','excepciones','atencion','resultado','solicitud','cambios','secuencia','accesos','password','comprobante','normal','advertencia','critico','simulado','confirmar','fila','destino','estanteriaDestino','ubicaciones','origenInhabilitado','numeroParteOrigen','espacio_identificadores','huella_peticion','id_operacion']);

const obj=(v:unknown):Record<string,unknown>=>{if(v&&typeof v==='object')return v as Record<string,unknown>;if(typeof v==='string'){try{const o=JSON.parse(v);return o&&typeof o==='object'?o:{}}catch{return {}}}return {}};
const num=(v:unknown)=>typeof v==='number'?new Intl.NumberFormat('es-MX').format(v):String(v??'');

/** Nombre legible de un identificador según el campo (material → número de parte, ubicación → estación / estantería / nivel). */
function nombrar(ctx:Ctx,campo:string,v:unknown){
 const id=String(v??'');if(!id)return '';
 const rack=(rid:string)=>{const r=ctx.estanterias.find(x=>x.id===rid);return r?[ctx.estaciones.find(s=>s.id===r.estacion)?.nombre,r.nombre,r.nivel].filter(Boolean).join(' / '):rid};
 if(campo==='material')return ctx.materiales.find(m=>m.id===id)?.numeroParte??id;
 if(campo==='ubicacion'){const u=ctx.ubicaciones.find(x=>x.id===id);return u?rack(u.estanteria):id}
 if(campo==='estanteria'||campo==='estanteriaDestino')return rack(id);
 if(campo==='estacion')return ctx.estaciones.find(s=>s.id===id)?.nombre??id;
 if(campo==='puesto')return ctx.puestos.find(p=>p.id===id)?.nombre??id;
 return id;
}
function valor(ctx:Ctx,campo:string,v:unknown){
 if(typeof v==='boolean')return v?'Sí':'No';
 if(['material','ubicacion','estanteria','estacion','puesto'].includes(campo))return nombrar(ctx,campo,v);
 if(campo==='rol')return ({ADMIN:'Administración',PUESTO:'Puesto logístico',LECTOR:'Consulta'} as Record<string,string>)[String(v)]??String(v);
 return num(v);
}

export function describirEvento(f:FilaHistorial,ctx:Ctx):EventoLegible{
 const tipo=String(f.tipo??f.tipo_movimiento??'');const s=obj(f.solicitud),r=obj(f.resultado);const d={...s,...r};
 const parte=String(r.numeroParte??nombrar(ctx,'material',d.material)??'');const lugar=nombrar(ctx,'ubicacion',d.ubicacion);
 let operacion=OPERACIONES[tipo]??(tipo?tipo.charAt(0)+tipo.slice(1).toLowerCase().replace(/_/g,' '):'Movimiento');let detalle='';
 switch(tipo){
  case 'ENTRADA':case 'SALIDA':
   if(tipo==='SALIDA'&&r.consumo===true)operacion='Salida (consumo)';
   detalle=num(r.piezas)+' pz de '+parte+(lugar?' en '+lugar:'')+' · saldo '+num(r.saldoAnterior)+' → '+num(r.saldoPosterior)+' pz'+(r.atencionFinalizada?' · atención finalizada':'');break;
  case 'ATENCION_INICIAR':{const u=ctx.ubicaciones.find(x=>x.id===String(s.ubicacion));detalle='Surtido de '+(u?nombrar(ctx,'material',u.material)+' en ':'')+lugar;break}
  case 'ATENCION_FINALIZAR':detalle='Atención terminada';break;
  case 'MATERIAL_ESTADO':operacion=s.activo===false?'Material inhabilitado':'Material reactivado';detalle=parte+(r.motivo?' · motivo: '+r.motivo:'');break;
  case 'MATERIAL_RENOMBRAR':detalle=parte+' → '+String(s.numeroParte??'')+(r.motivo?' · motivo: '+r.motivo:'');break;
  case 'MATERIAL_ELIMINAR':detalle=parte+(r.motivo?' · motivo: '+r.motivo:'');break;
  case 'MATERIAL_DUPLICAR':detalle=String(r.numeroParteOrigen??'')+' → '+parte;break;
  case 'PUESTO':case 'ESTACION':detalle=(s.id?'Cambio a ':'Alta de ')+String(s.nombre??'');break;
  case 'ESTANTERIA':detalle=(s.id?'Cambio de ':'Alta de ')+[nombrar(ctx,'estacion',s.estacion),s.nombre,s.nivel].filter(Boolean).join(' / ')+' · '+num(s.capacidad)+' espacios';break;
  case 'UBICACION':detalle=nombrar(ctx,'material',s.material)+' en '+nombrar(ctx,'estanteria',s.estanteria);break;
  case 'UBICACION_ESTADO':operacion=s.activa===false?'Ubicación desactivada':'Ubicación reactivada';detalle=(parte?parte+' · ':'')+lugar+(r.piezasTrasladadas?' · '+num(r.piezasTrasladadas)+' pz trasladadas':'')+(s.motivo?' · motivo: '+s.motivo:'');break;
  case 'IMPORTAR':detalle=num(r.altas??0)+' nuevos · '+num(r.actualizados??0)+' actualizados'+(r.puestosCreados?' · '+num(r.puestosCreados)+' puestos creados':'');break;
  case 'CALENDARIO':detalle='Del '+String(s.desde??'')+' al '+String(s.hasta??'')+(s.fuente?' · '+s.fuente:'')+(s.simulado?' · de prueba':'');break;
  case 'IMAGEN':operacion=r.eliminada?'Foto quitada':'Foto de material';detalle=nombrar(ctx,'material',d.material);break;
  case 'USUARIO_CREAR':case 'USUARIO_ACTUALIZAR':detalle=String(d.usuario??'')+' · '+valor(ctx,'rol',d.rol)+(d.activo===false?' · desactivado':'');break;
  case 'APERTURA':detalle='Inicio de la administración por línea';break;
  default:
   detalle=[f.numero_parte,f.cantidad_movimiento!=null?num(f.cantidad_movimiento)+' pz':'',f.saldo_anterior!=null?'saldo '+num(f.saldo_anterior)+' → '+num(f.saldo_posterior):'',f.estanteria].filter(Boolean).map(String).join(' · ');
 }
 const fuente=Object.keys(s).length||Object.keys(r).length?d:f;
 const campos=Object.entries(fuente).filter(([k,v])=>!OMITIR.has(k)&&!(k==='material'&&fuente.numeroParte)&&v!==null&&v!==''&&typeof v!=='object').map(([k,v])=>[ETIQUETAS[k]??k.replace(/_/g,' '),valor(ctx,k,v)] as [string,string]);
 return {operacion,detalle,campos,comprobante:String(f.id??f.id_operacion??f.id_movimiento??'')};
}
