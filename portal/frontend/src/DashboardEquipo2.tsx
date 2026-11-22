// Pantalla incorporada del ZIP (3), adaptada a publicación propia y acceso privado.
import {useEffect,useRef,useState} from 'react';
import {ExternalLink,RefreshCw} from 'lucide-react';
import {api} from './api';
type Dashboard={configurado:boolean;url:string|null;modo:'privado'|'sesion_microsoft';motivo?:string|null;avisoDatos?:string|null};
type Acceso={reportId:string;embedUrl:string;accessToken:string;expiration:string};
export function DashboardEquipo2({estadoPath='/dashboard-equipo2',tokenPath='/dashboard/embed-token'}:{estadoPath?:string;tokenPath?:string}={}){
 const [datos,setDatos]=useState<Dashboard|null>(null),[error,setError]=useState(''),[recarga,setRecarga]=useState(0);const container=useRef<HTMLDivElement>(null);
 useEffect(()=>{let active=true;api<Dashboard>(estadoPath).then(d=>{if(active){setDatos(d);setError('')}}).catch(e=>{if(active)setError(e.message)});return()=>{active=false}},[recarga,estadoPath]);
 useEffect(()=>{
  if(!datos?.configurado||datos.modo!=='privado'||!container.current)return;
  const node=container.current;let active=true;let timer:ReturnType<typeof setTimeout>|undefined;let cleanup=()=>{};
  async function iniciar(){
   try{
    const pbi=await import('powerbi-client');if(!active)return;const client=new pbi.service.Service(pbi.factories.hpmFactory,pbi.factories.wpmpFactory,pbi.factories.routerFactory);
    cleanup=()=>client.reset(node);
    const acceso=await api<Acceso>(tokenPath,{});if(!active)return;
    const report=client.embed(node,{type:'report',id:acceso.reportId,embedUrl:acceso.embedUrl,accessToken:acceso.accessToken,tokenType:pbi.models.TokenType.Embed,permissions:pbi.models.Permissions.Read,viewMode:pbi.models.ViewMode.View}) as import('powerbi-client').Report;
    report.on('error',()=>{if(active){if(timer)clearTimeout(timer);setError('No se pudo mostrar el informe. Revisa el acceso y vuelve a intentar.')}});
    const programar=(expiration:string)=>{timer=setTimeout(async()=>{try{const nuevo=await api<Acceso>(tokenPath,{});if(!active)return;await report.setAccessToken(nuevo.accessToken);programar(nuevo.expiration)}catch{if(active)setError('No se pudo renovar el acceso. Inicia sesión nuevamente o vuelve a intentar.')}},Math.max(10000,Date.parse(expiration)-Date.now()-120000))};
    programar(acceso.expiration);
   }catch(e){if(active)setError((e as Error).message)}
  }
  void iniciar();return()=>{active=false;if(timer)clearTimeout(timer);cleanup()};
 },[datos,recarga,tokenPath]);
 const reintentar=()=>{setError('');setRecarga(n=>n+1)};
 if(error)return <section className="panel"><p className="error" role="alert">{error}</p><button onClick={reintentar}>Volver a intentar</button></section>;
 if(!datos)return <p>Consultando dashboard…</p>;
 if(!datos.configurado)return <section className="panel"><h2>Dashboard · Power BI</h2><p>{datos.motivo??"El Dashboard está pendiente de configuración de Power BI."}</p><p>Mientras se completa, puedes consultar el inventario, el historial y los KPIs del portal.</p></section>;
 return <section className="panel dashboard-equipo2"><div className="panel-head"><div><h2>Dashboard · Power BI</h2><p>{datos.modo==='privado'?'Informe privado disponible con tu sesión del portal.':'Informe original de Power BI. Accede con tu cuenta Microsoft autorizada.'}</p></div><div className="button-row"><button className="secondary" onClick={reintentar}><RefreshCw size={16}/> Recargar informe</button>{datos.url&&<a className="button-link" href={datos.url} target="_blank" rel="noopener noreferrer"><ExternalLink size={16}/> Abrir en otra pestaña</a>}</div></div>{datos.avisoDatos&&<p role="note">{datos.avisoDatos}</p>}{datos.modo==='sesion_microsoft'&&<p>Power BI comprueba los permisos y la licencia de cada cuenta. Si no aparece el inicio de sesión, abre el informe en otra pestaña. Recargar muestra el último modelo actualizado; no importa datos nuevos desde SQL. Al terminar en un equipo compartido, cierra también tu sesión de Microsoft.</p>}{datos.modo==='privado'?<div ref={container} className="dashboard-frame" aria-label="Informe privado de Power BI"/>:<iframe key={recarga} className="dashboard-frame" title="Dashboard · Power BI" src={datos.url??undefined} allowFullScreen/>}</section>;
}
