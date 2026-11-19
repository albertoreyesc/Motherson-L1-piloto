// Foto de la pieza en el catálogo: miniatura, vista ampliada y, para administración, subir, cambiar o quitar.
import React,{useEffect,useRef,useState} from 'react';
import {ImagePlus,Trash2,X} from 'lucide-react';
import {api,enviarArchivo} from './api';

export const tiposImagen=['image/png','image/jpeg'];
export const maximoImagen=5*1024*1024;
/** Devuelve el motivo de rechazo o null si el archivo se puede subir. */
export function revisarImagen(archivo:{type:string;size:number}){
 if(!tiposImagen.includes(archivo.type))return 'Solo se aceptan imágenes PNG o JPG de la pieza.';
 if(archivo.size<=0)return 'El archivo está vacío.';
 if(archivo.size>maximoImagen)return 'La imagen pesa más de 5 MB.';
 return null;
}
const ruta=(parte:string,version:number)=>'/api/materiales/imagen?parte='+encodeURIComponent(parte)+'&v='+version;

export function ImagenMaterial({parte,version,editable,onCambio}:{parte:string;version?:number;editable:boolean;onCambio:()=>Promise<void>|void}){
 const [ampliada,setAmpliada]=useState(false),[busy,setBusy]=useState(false),[error,setError]=useState('');
 const input=useRef<HTMLInputElement>(null);
 const tiene=version!=null;
 useEffect(()=>{if(!ampliada)return;const cerrar=(e:KeyboardEvent)=>{if(e.key==='Escape')setAmpliada(false)};window.addEventListener('keydown',cerrar);return()=>window.removeEventListener('keydown',cerrar)},[ampliada]);

 async function subir(archivo?:File){
  if(!archivo)return;setError('');
  const motivo=revisarImagen(archivo);
  if(motivo){setError(motivo);if(input.current)input.current.value='';return}
  setBusy(true);
  try{await enviarArchivo('/materiales/imagen?parte='+encodeURIComponent(parte),archivo);await onCambio()}
  catch(e){setError(e instanceof Error?e.message:'No se pudo subir la imagen.')}
  finally{setBusy(false);if(input.current)input.current.value=''}
 }
 async function quitar(){
  if(!window.confirm('¿Quitar la imagen de '+parte+'?'))return;
  setBusy(true);setError('');
  try{await api('/materiales/imagen/eliminar?parte='+encodeURIComponent(parte),{});await onCambio()}
  catch(e){setError(e instanceof Error?e.message:'No se pudo quitar la imagen.')}
  finally{setBusy(false)}
 }

 return <div className="imagen-material">
  {tiene
   ?<button type="button" className="imagen-miniatura" onClick={()=>setAmpliada(true)} aria-label={'Ver imagen de la pieza '+parte}><img src={ruta(parte,version)} alt={'Pieza '+parte} loading="lazy"/></button>
   :<span className="imagen-vacia">Sin imagen</span>}
  {editable&&<div className="imagen-acciones">
   <input ref={input} type="file" accept="image/png,image/jpeg" hidden onChange={e=>subir(e.target.files?.[0])}/>
   <button type="button" className="secondary" disabled={busy} onClick={()=>input.current?.click()}><ImagePlus size={14} aria-hidden="true"/>{busy?'Guardando…':tiene?'Cambiar':'Subir'}</button>
   {tiene&&<button type="button" className="secondary" disabled={busy} onClick={quitar} aria-label={'Quitar imagen de '+parte} title="Quitar imagen"><Trash2 size={14} aria-hidden="true"/></button>}
  </div>}
  {error&&<p className="error" role="alert">{error}</p>}
  {ampliada&&tiene&&<div className="imagen-modal" role="dialog" aria-modal="true" aria-label={'Imagen de la pieza '+parte} onClick={()=>setAmpliada(false)}>
   <figure onClick={e=>e.stopPropagation()}>
    <button type="button" className="secondary imagen-cerrar" onClick={()=>setAmpliada(false)} aria-label="Cerrar" autoFocus><X size={18} aria-hidden="true"/></button>
    <img src={ruta(parte,version)} alt={'Pieza '+parte}/>
    <figcaption>{parte}</figcaption>
   </figure>
  </div>}
 </div>;
}
