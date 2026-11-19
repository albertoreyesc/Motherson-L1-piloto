import React from 'react';
import type {Catalogo} from './MvpTypes';
export type Referencia={archivo:string;hoja?:string;celda?:string;fila?:number;sha256:string};
export type CampoDocumentado={estado:'DOCUMENTADO'|'NO_DOCUMENTADO'|'PENDIENTE_VALIDACION';valor?:string|null;motivo:string;fuente?:Referencia|null};
export type AtributosCatalogo={version:number;definicionUso:string;materiales:Record<string,{uso:CampoDocumentado}>;puestos:Record<string,{nombre:string;tipo:CampoDocumentado;fuente:Referencia}>;fuentes:Referencia[]};
export function campoCatalogo(catalogo:Catalogo|null,parte:string,campo:'uso'){return catalogo?.atributos?.materiales[parte]?.[campo]}
export function ValorCatalogo({valor,documento}:{valor:string|null|undefined;documento?:CampoDocumentado}){
 const existe=valor!=null&&valor.trim()!=='';
 const confirmado=existe&&documento?.estado==='DOCUMENTADO'&&valor===documento.valor;
 const etiqueta=existe?valor:documento?.estado==='NO_DOCUMENTADO'?'No documentado':'Pendiente de validación';
 return <span className="catalogo-valor"><span title={documento?.motivo}>{etiqueta}</span>{existe&&!confirmado&&<small>Pendiente de validación documental</small>}{confirmado&&documento?.fuente&&<details><summary>Fuente del dato</summary><small>{documento.fuente.archivo}<br/>{documento.fuente.hoja} · {documento.fuente.celda}<br/>Clasificación de uso del PFEP.</small></details>}</span>;
}
export function PuestoCatalogo({codigo,catalogo}:{codigo:string;catalogo:Catalogo|null}){
 const nombre=catalogo?.atributos?.puestos[codigo]?.nombre;
 return <span className="catalogo-puesto"><strong>{codigo}</strong><small>{nombre??'Nombre completo pendiente de validación'}</small></span>;
}
export function IntegridadCatalogo({catalogo,mostrarVacios,onMostrar}:{catalogo:Catalogo;mostrarVacios:boolean;onMostrar:(v:boolean)=>void}){
 const materiales=catalogo.materiales;const usos=materiales.filter(m=>m.uso&&m.uso===campoCatalogo(catalogo,m.numero_parte,'uso')?.valor).length;
 const tipos=catalogo.puestos.filter(p=>p.tipo?.trim()).length;
 return <section className="panel catalogo-integridad" aria-label="Integridad del catálogo"><h2>Datos del catálogo</h2><p>{catalogo.atributos?.definicionUso??'Falta la referencia documental de los atributos del catálogo.'}</p><ul><li><strong>{usos} de {materiales.length}</strong> materiales con uso documentado</li>{catalogo.modelo_puestos?<li><strong>{materiales.filter(m=>m.variante_revision).length} de {materiales.length}</strong> materiales con variante documentada</li>:<li><strong>{tipos} de {catalogo.puestos.length}</strong> puestos con tipo registrado</li>}</ul>{!catalogo.modelo_puestos&&<p>Los tipos de puesto sin respaldo se conservan vacíos. El código del puesto y su nombre completo se muestran por separado.</p>}{!catalogo.modelo_puestos&&<label className="catalogo-opcion"><input type="checkbox" checked={mostrarVacios} onChange={e=>onMostrar(e.target.checked)}/>Mostrar columnas sin datos</label>}{!catalogo.modelo_puestos&&!mostrarVacios&&<small>Se ocultan las columnas completamente vacías; puedes mostrarlas con este control.</small>}<small>La documentación de estos atributos corresponde al catálogo actual. Los eventos históricos originales se conservan sin cambios.</small></section>;
}
