import {useEffect,useState} from 'react';
import {ImageOff} from 'lucide-react';

/** URL de la foto vigente de un material en una línea (null si no tiene). */
export function urlFoto(linea:number,imagenes:Record<string,number>,material:string){
 const v=imagenes[material];return v?'/api/v2/lineas/'+linea+'/imagen/'+material+'?v='+v:null;
}

/**
 * Miniatura de la pieza para identificarla de un vistazo en tablas y listas.
 * Al tocarla se abre en grande. Sin foto muestra un recuadro neutro del mismo tamaño para no desalinear filas.
 */
export function MiniFoto({linea,imagenes,material,numeroParte,descripcion,tam=44}:{linea:number;imagenes:Record<string,number>;material:string;numeroParte:string;descripcion?:string;tam?:number}){
 const [abierta,setAbierta]=useState(false);const url=urlFoto(linea,imagenes,material);
 useEffect(()=>{if(!abierta)return;const tecla=(ev:KeyboardEvent)=>{if(ev.key==='Escape')setAbierta(false)};window.addEventListener('keydown',tecla);return()=>window.removeEventListener('keydown',tecla)},[abierta]);
 if(!url)return <span className="mini-foto vacia" style={{width:tam,height:tam}} title="Sin foto" aria-hidden="true"><ImageOff size={Math.round(tam*.42)}/></span>;
 return <>
  <button type="button" className="mini-foto" style={{width:tam,height:tam}} onClick={()=>setAbierta(true)} aria-label={'Ver foto de '+numeroParte}><img src={url} alt="" loading="lazy" decoding="async"/></button>
  {abierta&&<div className="foto-grande-fondo" role="dialog" aria-modal="true" aria-label={'Foto de '+numeroParte} onClick={()=>setAbierta(false)}>
   <figure className="foto-grande" onClick={ev=>ev.stopPropagation()}><img src={url} alt={'Pieza '+numeroParte}/><figcaption><strong>{numeroParte}</strong>{descripcion&&<span>{descripcion}</span>}</figcaption><button onClick={()=>setAbierta(false)}>Cerrar</button></figure>
  </div>}
 </>;
}
