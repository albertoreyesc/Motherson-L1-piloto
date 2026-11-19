import React,{useEffect,useState} from 'react';
import {api,type Row,type User} from './api';
export function SessionAccess({user,onAccess}:{user:User;onAccess:(access:Row)=>void}){
 const [access,setAccess]=useState<Row|null>(null),[error,setError]=useState('');
 useEffect(()=>{let active=true;async function load(){try{const a=await api<Row>('/central/access');if(active){setAccess(a);onAccess(a);setError('')}}catch(e){if(active)setError((e as Error).message)}}load();const timer=setInterval(load,15000);return()=>{active=false;clearInterval(timer)}},[user.id]);
 return <section className="panel session-access" aria-label="Autorización operativa actual"><div><strong>Turno real del servidor: {access?.activeShift??'Consultando…'}</strong><p>Turnos autorizados para operar: {!access?'Consultando autorizaciones…':access.authorizedShifts?.length?[...new Set(access.authorizedShifts.map((s:Row)=>s.Codigo))].join(' · '):'Sin autorización vigente'}.</p></div>{user.permissions.includes('entry')&&<p role="status">{access?.canEnter?'Captura habilitada en tus posiciones autorizadas.':access?'Puedes consultar. Para capturar se requieren turno y asignación vigentes'+(user.roles.includes('lider surtidores')?', además de cobertura temporal.':'.'):'Comprobando acceso de captura…'}</p>}{error&&<p role="alert">{error}</p>}</section>
}
