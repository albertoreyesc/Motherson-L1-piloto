import React,{useEffect,useState} from 'react';
import {createRoot} from 'react-dom/client';
import {ChevronRight} from 'lucide-react';
import {api,csrf,type User} from './api';
import {MvpApp} from './MvpApp';
import './styles.css';
function App(){
 const [user,setUser]=useState<User|null>(null),[initial,setInitial]=useState(true),[busy,setBusy]=useState(false),[error,setError]=useState('');
 useEffect(()=>{api<User>('/auth/me').then(async u=>{await csrf();setUser(u)}).catch(()=>{}).finally(()=>setInitial(false))},[]);
 async function login(e:React.FormEvent<HTMLFormElement>){e.preventDefault();const f=new FormData(e.currentTarget);setBusy(true);setError('');try{await csrf();await api('/auth/login',{username:f.get('username'),password:f.get('password')});await csrf();setUser(await api('/auth/me'))}catch(e){setError((e as Error).message)}finally{setBusy(false)}}
 useEffect(()=>{const expired=()=>{setUser(null);setError('La sesión cambió o venció. Inicia sesión con tu cuenta actual.');};window.addEventListener('motherson-session-expired',expired);return()=>window.removeEventListener('motherson-session-expired',expired)},[]);
 useEffect(()=>{if(!user)return;const timer=setInterval(()=>api<User>('/auth/me').then(setUser).catch(()=>{}),15000);return()=>clearInterval(timer)},[user?.id]);
 if(initial)return <p className="boot">Preparando acceso…</p>;
 if(user)return <MvpApp user={user} onLogout={async()=>{try{await api('/auth/logout',{})}finally{setUser(null);await csrf()}}}/>;
 return <div className="login"><section className="login-brand"><div className="brand"><img src="/brand/motherson-logo.svg" width="192" alt="Motherson"/></div><span className="eyebrow">SMART MATERIAL REPLENISHMENT</span><h1>Cada movimiento,<br/>con evidencia.</h1><p>Control de materiales de Línea 1, entradas y salidas por cajas y un histórico consultable.</p><div className="login-flow">Captura <ChevronRight size={16}/> Validación <ChevronRight size={16}/> Registro</div><small>Demostración · datos simulados</small></section><main className="login-main"><div className="login-form"><span className="pill">ACCESO AUTORIZADO</span><h2>Bienvenido al equipo</h2><p>Ingresa con tu cuenta asignada. Los permisos se validan en cada operación.</p><form onSubmit={login}><label className="field">Usuario<input name="username" autoComplete="username" required/></label><label className="field">Contraseña<input name="password" type="password" autoComplete="current-password" required/></label>{error&&<p className="error" role="alert">{error}</p>}<button disabled={busy}>{busy?'Verificando…':'Iniciar sesión'}<ChevronRight size={18}/></button></form><small>La cuenta autenticada identifica cada entrada. Los responsables ficticios del histórico no son cuentas de acceso.</small></div></main></div>;
}
createRoot(document.getElementById('root')!).render(<App/>);
