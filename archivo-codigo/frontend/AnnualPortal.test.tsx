import React from 'react';
import {renderToStaticMarkup} from 'react-dom/server';
import {describe,it,expect} from 'vitest';
import {AnnualPortal,annualSections,annualUrl,selectAnnualRun,OrderJourney,ResponsibilityPanel} from './AnnualPortal';
import type {Catalog,User} from './api';
const cat:Catalog={materials:[],puestos:[],estaciones:[],ubicaciones:[],asignaciones:[],misPuestos:[]};
const user:User={id:1,name:'admin.local',roles:['administrador'],permissions:['view','admin','assign','entry','catalog'],simulado:true};
function render(u=user){return renderToStaticMarkup(<AnnualPortal user={u} onLogout={async()=>{}}/>);}
describe('recorrido histórico anual',()=>{
 it('conserva el filtro de responsable en la consulta de cada pantalla',()=>{
  for(const section of ['summary','inventory','supply','shifts','history']){
   const url=new URL('http://localhost'+annualUrl('run',{responsible:'SIM-KE-T1',post:'K.E.',date:'2026-10-06'},section));
   expect(url.searchParams.get('responsible')).toBe('SIM-KE-T1');
  }
 });
 it('distingue proceso generador, persona solicitante y confirmante del otro turno',()=>{
  const html=renderToStaticMarkup(<OrderJourney order={{id:1,part:'TEST',post:'K.E.',actor:'SIMULADOR_ANUAL',state:'CONFIRMADA',start:'2026-10-06T14:58:00-06:00',closed:'2026-10-06T15:30:00-06:00',requestedBy:{name:'Persona T1'},confirmedBy:{name:'Persona T2'}}} onClose={()=>{}} onHistory={()=>{}}/>);
  expect(html).toContain('Proceso generador');expect(html).toContain('SIMULADOR_ANUAL');
  expect(html).toContain('Persona T1');expect(html).toContain('Persona T2');expect(html).toContain('FICTICIO');
 });
 it('muestra materiales atendidos y continuidad para el responsable elegido',()=>{
  const html=renderToStaticMarkup(<ResponsibilityPanel data={{rows:[{id:'SIM-KE-T1',name:'Persona T1',post:'K.E.',shift:'PFEP T1',requests:1}],materials:[{key:'a',part:'MATERIAL-ATENDIDO'}],pending:[{id:1,part:'MATERIAL-PENDIENTE',requestedBy:{name:'Persona T1'},continuityBy:{name:'Persona T2'},state:'SOLICITADA'}],pendingTotal:1}} selected="SIM-KE-T1" onChoose={()=>{}} onSupply={()=>{}}/>);
  expect(html).toContain('MATERIAL-ATENDIDO');expect(html).toContain('MATERIAL-PENDIENTE');expect(html).toContain('Persona T2');
 });
 it('abre el nuevo histórico publicado sin quedar retenido en la ejecución anterior',()=>{
  const c={currentId:'six-posts',runs:[{runId:'previous'},{runId:'six-posts'}]};
  expect(selectAnnualRun(c,{run:'previous'}).runId).toBe('six-posts');
  expect(selectAnnualRun(c,{run:'previous',currentId:'previous'}).runId).toBe('six-posts');
  expect(selectAnnualRun(c,{run:'previous',currentId:'six-posts'}).runId).toBe('six-posts');
  expect(selectAnnualRun(c,{run:'removed',currentId:'six-posts'}).runId).toBe('six-posts');
 });
 it('agrupa la navegación en las ocho secciones unificadas',()=>{
  expect(annualSections).toEqual(['Resumen','Inventario y ubicaciones','Surtimientos','Turnos y continuidad','Historial y métricas','Administración','Entrada · cajas','Alarmas']);
  const nav=render().split('<nav')[1].split('</nav>')[0];
  expect((nav.match(/<button/g)||[]).length).toBe(8);
  expect(nav).not.toContain('Calidad ETL');
  expect(nav).not.toContain('Reservas de capacidad');
 });
 it('no ofrece administración al surtidor',()=>{
  const html=render({...user,permissions:['view','entry'],roles:['surtidor SZ01']});
  const nav=html.split('<nav')[1].split('</nav>')[0];
  expect((nav.match(/<button/g)||[]).length).toBe(5);
  expect(nav).not.toContain('Administración');
 });
 it('conserva el corte compartido entre pantallas y codifica cada filtro',()=>{
  const context={date:'2025-10-30',time:'15:00',shift:'PFEP T1',post:'SZ01',material:'key A&B'};
  for(const section of ['summary','inventory','supply','shifts','history']){
   const url=new URL('http://localhost'+annualUrl('618e1fdba783',context,section,{offset:25,limit:25}));
   for(const [k,v] of Object.entries(context))expect(url.searchParams.get(k)).toBe(v);
   expect(url.searchParams.get('section')).toBe(section);
   expect(url.searchParams.get('offset')).toBe('25');
  }
 });
 it('mantiene el periodo de métricas y paginación separados de la fecha de corte',()=>{
  const url=new URL('http://localhost'+annualUrl('618e1fdba783',{date:'2026-10-23',time:'23:30'},'history',{from:'2025-10-25',kind:'CONSUMO',offset:100}));
  expect(url.searchParams.get('date')).toBe('2026-10-23');
  expect(url.searchParams.get('from')).toBe('2025-10-25');
  expect(url.searchParams.get('kind')).toBe('CONSUMO');
  expect(url.searchParams.get('offset')).toBe('100');
 });
});
