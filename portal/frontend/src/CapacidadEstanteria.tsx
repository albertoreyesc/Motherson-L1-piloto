// Capacidad en cajas de una estantería con botones + / − (17/11/2026).
// Se edita en pantalla y solo se guarda al confirmar; el servidor rechaza si cambió en el inter.
import React,{useEffect,useState} from 'react';
import {Minus,Plus} from 'lucide-react';
import {api,nd} from './api';
import type {Inventario} from './MvpTypes';

/** Cajas ocupadas en la estantería: capacidad menos lo libre que calcula el servidor. */
export function cajasOcupadas(r:Pick<Inventario,'cajas_estanteria'|'cajas_disponibles'>){
 return r.cajas_estanteria!=null&&r.cajas_disponibles!=null?Math.max(0,r.cajas_estanteria-r.cajas_disponibles):null;
}
/** Límites del + / −: nunca menos que lo ocupado ni menos de 1; máximo 10,000. */
export function limitesCapacidad(r:Pick<Inventario,'cajas_estanteria'|'cajas_disponibles'>){
 return {minimo:Math.max(1,cajasOcupadas(r)??1),maximo:10000};
}

export function CapacidadEstanteria({row,editable,onCambio}:{row:Inventario;editable:boolean;onCambio:()=>Promise<void>|void}){
 const actual=row.cajas_estanteria;
 const [valor,setValor]=useState(actual??1),[busy,setBusy]=useState(false),[error,setError]=useState('');
 useEffect(()=>{setValor(actual??1);setError('')},[actual,row.id_estanteria]);
 const ocupadas=cajasOcupadas(row);
 const {minimo,maximo}=limitesCapacidad(row);
 const cambiado=actual!=null&&valor!==actual;
 const resumen=<small className="capacidad-resumen">{ocupadas==null?'Ocupación sin determinar':`${nd(ocupadas)} ocupadas · ${nd(row.cajas_disponibles)} libres`}</small>;
 if(!editable||actual==null)return <div className="capacidad"><strong>{nd(actual)}</strong>{resumen}</div>;

 async function guardar(){
  setBusy(true);setError('');
  try{await api('/catalogo/capacidad',{estanteria:row.id_estanteria,anterior:actual,nueva:valor});await onCambio()}
  catch(e){setError(e instanceof Error?e.message:'No se guardó la capacidad.')}
  finally{setBusy(false)}
 }
 return <div className="capacidad">
  <div className="capacidad-control" role="group" aria-label={'Capacidad en cajas de '+row.estanteria+' / '+row.nivel}>
   <button type="button" className="secondary" aria-label="Quitar una caja de capacidad" disabled={busy||valor<=minimo} onClick={()=>setValor(v=>Math.max(minimo,v-1))}><Minus size={14} aria-hidden="true"/></button>
   <output aria-live="polite">{nd(valor)}</output>
   <button type="button" className="secondary" aria-label="Agregar una caja de capacidad" disabled={busy||valor>=maximo} onClick={()=>setValor(v=>Math.min(maximo,v+1))}><Plus size={14} aria-hidden="true"/></button>
  </div>
  {cambiado?<div className="capacidad-acciones"><button type="button" disabled={busy} onClick={guardar}>{busy?'Guardando…':'Guardar'}</button><button type="button" className="secondary" disabled={busy} onClick={()=>{setValor(actual);setError('')}}>Cancelar</button></div>:resumen}
  {error&&<p className="error" role="alert">{error}</p>}
 </div>;
}
