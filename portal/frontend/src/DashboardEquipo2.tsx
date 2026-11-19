// Pantalla incorporada del ZIP (3), adaptada a publicación propia y acceso privado.
import {useEffect,useRef,useState} from 'react';
import {ExternalLink,RefreshCw} from 'lucide-react';
import {api} from './api';
type Dashboard={configurado:boolean;url:string|null;modo:'privado'|'sesion_microsoft'};
type Acceso={reportId:string;embedUrl:string;accessToken:string;expiration:string};
export function DashboardEquipo2(){
 const [datos,setDatos]=useState<Dashboard|null>(null),[error,setError]=useState(''),[recarga,setRecarga]=useState(0);const container=useRef<HTMLDivElement>(null);
 useEffect(()=>{let active=true;api<Dashboard>('/dashboard-equipo2').then(d=>{if(active){setDatos(d);setError('')}}).catch(e=>{if(active)setError(e.message)});return()=>{active=false}},[recarga]);
 useEffect(()=>{
  if(!datos?.configurado||datos.modo!=='privado'||!container.current)return;
  const node=container.current;let active=true;let timer:ReturnType<typeof setTimeout>|undefined;let cleanup=()=>{};
  async function iniciar(){
   try{
    const pbi=await import('powerbi-client');if(!active)return;const client=new pbi.service.Service(pbi.factories.hpmFactory,pbi.factories.wpmpFactory,pbi.factories.routerFactory);
    cleanup=()=>client.reset(node);
    const acceso=await api<Acceso>('/dashboard/embed-token',{});if(!active)return;
    const report=client.embed(node,{type:'report',id:acceso.reportId,embedUrl:acceso.embedUrl,accessToken:acceso.accessToken,tokenType:pbi.models.TokenType.Embed,permissions:pbi.models.Permissions.Read,viewMode:pbi.models.ViewMode.View}) as import('powerbi-client').Report;
    report.on('error',()=>{if(active){if(timer)clearTimeout(timer);setError('No se pudo mostrar el informe. Revisa el acceso y vuelve a intentar.')}});
    const programar=(expiration:string)=>{timer=setTimeout(async()=>{try{const nuevo=await api<Acceso>('/dashboard/embed-token',{});if(!active)return;await report.setAccessToken(nuevo.accessToken);programar(nuevo.expiration)}catch{if(active)setError('No se pudo renovar el acceso. Inicia sesión nuevamente o vuelve a intentar.')}},Math.max(10000,Date.parse(expiration)-Date.now()-120000))};
    programar(acceso.expiration);
   }catch(e){if(active)setError((e as Error).message)}
  }
  void iniciar();return()=>{active=false;if(timer)clearTimeout(timer);cleanup()};
 },[datos,recarga]);
 const reintentar=()=>{setError('');setRecarga(n=>n+1)};
 if(error)return <section className="panel"><p className="error" role="alert">{error}</p><button onClick={reintentar}>Volver a intentar</button></section>;
 if(!datos)return <p>Consultando dashboard…</p>;
 if(!datos.configurado)return <section className="panel"><h2>Dashboard · Power BI</h2><p>La publicación en tu espacio propio está pendiente.</p><p>Necesitamos una cuenta de trabajo o escuela y un espacio de Power BI. El acceso privado sin otra sesión requiere habilitar la integración de la aplicación y disponer de capacidad.</p><p>Mientras se completa, puedes consultar el inventario, el historial y los KPIs del portal.</p></section>;
 return <section className="panel dashboard-equipo2"><div className="panel-head"><div><h2>Dashboard · Power BI</h2><p>{datos.modo==='privado'?'Informe privado disponible con tu sesión del portal.':'Este informe requiere una sesión de Microsoft con permiso para verlo.'}</p></div><div className="button-row"><button className="secondary" onClick={reintentar}><RefreshCw size={16}/> Recargar</button>{datos.url&&<a className="button-link" href={datos.url} target="_blank" rel="noopener noreferrer"><ExternalLink size={16}/> Abrir en otra pestaña</a>}</div></div>{datos.modo==='privado'?<div ref={container} className="dashboard-frame" aria-label="Informe privado de Power BI"/>:<iframe key={recarga} className="dashboard-frame" title="Dashboard · Power BI" src={datos.url??undefined} allowFullScreen/>}</section>;
}
