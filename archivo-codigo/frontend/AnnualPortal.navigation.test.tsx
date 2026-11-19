import React from 'react';
import {renderToStaticMarkup} from 'react-dom/server';
import {afterEach,describe,expect,it,vi} from 'vitest';
import {AnnualPortal,OrderJourney} from './AnnualPortal';
import type {Row,Catalog,User} from './api';

const hooks=vi.hoisted(()=>({values:[] as unknown[]}));
vi.mock('react',async()=>{
 const real=await vi.importActual<typeof import('react')>('react');
 return {...real,useState:(initial:unknown)=>[hooks.values.length?hooks.values.shift():initial,()=>{}]};
});
afterEach(()=>{hooks.values=[]});
const cat:Catalog={materials:[],puestos:[],estaciones:[],ubicaciones:[],asignaciones:[],misPuestos:[]};
const user:User={id:1,name:'admin.local',roles:['administrador'],permissions:['view','admin'],simulado:true};
const context={date:'2026-10-05',time:'23:30',shift:'',material:'',post:''};
const run='618e1fdba783',from='2025-10-25';
const meta={runs:[{runId:run,start:from,endInclusive:'2026-10-24',endExclusive:'2026-10-25',catalog:[],zone:'America/Mexico_City'}]};
function query(page:number,c=context){return JSON.stringify({run,context:c,page,offset:0,status:'pending',kind:'',from,refresh:0})}
const order={id:123,key:'material-a',part:'TEST-PART',station:'AL22',rack:'E3',level:'B',post:'SZ02',state:'ACEPTADA',start_shift:'PFEP T1',actor:'SIMULADOR_ANUAL',start:'2026-10-05T14:57:00-06:00',accepted:'2026-10-05T14:58:00-06:00',transfer:null,delivered:null,closed:null,boxes:1,reservedBoxes:1,reservedPieces:66,responseMinutes:1,preparationMinutes:null,transportMinutes:null,attentionMinutes:null,openMinutes:3};
function result():Row{return {
 context:{...context,cutLocal:'2026-10-05T23:30:00-06:00',activeShift:'FUERA',workingDay:true},
 summary:{included:1,excluded:0,stock:66,pendingOrders:1,entries:66,unfilled:0},inventory:[],
 orders:{rows:[order],total:1,offset:0,limit:25},entries:{rows:[],total:0,offset:0,limit:25},
 shifts:{definitions:[{code:'PFEP T1',start:'06:00',end:'15:00'},{code:'PFEP T2',start:'15:00',end:'23:30'}],cuts:[{id:1,local:'2026-10-05T15:00:00-06:00',shift:'PFEP T1',part:'TEST-PART',stock:66,minimum:70,passed:0,pending_order:123}],openStops:[]},
 metrics:{daily:[],load:[],initialStock:100,entries:66,consumed:100,finalStock:66,reconciliation:0},
 events:{rows:[],total:482997,offset:0,limit:25}
}}
function render(page:number,payload:{query:string;value:Row},c=context){
 hooks.values=[meta,run,c,page,payload,'',false,0,0,'pending','',from,'',0,null];
 return renderToStaticMarkup(<AnnualPortal user={user} onLogout={async()=>{}}/>);
}
describe('navegación con respuestas de distintas pantallas',()=>{
 for(const [page,name] of [[2,'Solicitudes y etapas'],[3,'Existencias al cierre de turno'],[4,'Bitácora completa']] as const){
  it('renderiza '+name+' con su respuesta completa',()=>{
   const html=render(page,{query:query(page),value:result()});
   expect(html).toContain(name);
   expect(html).not.toContain('Consultando el histórico completo');
  });
  it('no usa una respuesta de Resumen al abrir '+name,()=>{
   const html=render(page,{query:query(0),value:{summary:{stock:9007},inventory:[]}});
   expect(html).toContain('Consultando el histórico completo');
   expect(html).not.toContain('9,007');
  });
 }
 it('oculta una respuesta anterior al cambiar la fecha del mismo Surtimientos',()=>{
  const html=render(2,{query:query(2),value:result()},{...context,date:'2025-10-30'});
  expect(html).toContain('Consultando el histórico completo');
  expect(html).not.toContain('TEST-PART');
 });
 it('oculta una respuesta anterior al cambiar la posición de Historial',()=>{
  const html=render(4,{query:query(4),value:result()},{...context,material:'another-key'});
  expect(html).toContain('Consultando el histórico completo');
  expect(html).not.toContain('482,997');
 });
 it('el recorrido de una solicitud abierta mantiene pendientes sus etapas futuras',()=>{
  const html=renderToStaticMarkup(<OrderJourney order={order} onClose={()=>{}} onHistory={()=>{}}/>);
  expect(html).toContain('SIMULADOR_ANUAL');
  expect(html).toContain('PFEP T1');
  expect((html.match(/Pendiente al corte/g)||[]).length).toBe(3);
  expect(html).toContain('Ver historial de este material');
 });
 it('el recorrido confirmado muestra todas las etapas y su reserva liberada',()=>{
  const html=renderToStaticMarkup(<OrderJourney order={{...order,state:'CONFIRMADA',transfer:'2026-10-05T15:01:00-06:00',delivered:'2026-10-05T15:06:00-06:00',closed:'2026-10-05T15:06:00-06:00',reservedBoxes:0,reservedPieces:0}} onClose={()=>{}} onHistory={()=>{}}/>);
  expect(html).not.toContain('Pendiente al corte');
  expect(html).toContain('Confirmada');
 });
});
