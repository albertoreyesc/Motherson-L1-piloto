import type {Row} from './api';

export function validAnnual(value:Row):boolean{
 return value?.nature==='SIMULADO'&&value?.engine==='annual-lab-v1'&&typeof value.runId==='string'&&
  typeof value.config?.start==='string'&&typeof value.config?.endExclusive==='string'&&
  ['catalog','months','balances','dailyBalances','materialMetrics','materialMonths','postShift','postShiftMonths','eventSample','caseSchedule','sourceQuarantine','calendar','sources'].every(k=>Array.isArray(value[k]))&&
  typeof value.total?.events==='number'&&value.catalog.every((m:Row)=>typeof m.key==='string'&&typeof m.parte==='string');
}

/** Matching period/material scope, not a sum of percentages or ending balances. */
export function annualScope(report:Row,month:string,key:string):Row{
 const metrics=key?(month?report.materialMonths.find((r:Row)=>r.label===month+'|'+key):report.materialMetrics.find((r:Row)=>r.label===key)):
  month?report.months.find((r:Row)=>r.label===month):report.total;
 const endings:Row[]=report.dailyBalances.filter((r:Row)=>(!month||r.date.startsWith(month))&&(!key||r.key===key));
 const lastDate=endings.reduce((d,r)=>r.date>d?r.date:d,'');
 const ending=lastDate?endings.filter(r=>r.date===lastDate).reduce((n,r)=>n+r.stock,0):null;
 const consumed=metrics?.consumed??0,entries=metrics?.entries??0;
 return {...metrics,finalStock:ending,initialStock:ending==null?null:ending-entries+consumed};
}

export function dailyTrend(report:Row,month:string,key:string):Row[]{
 const days=new Map<string,number>();
 for(const r of report.dailyBalances as Row[]){if((!month||r.date.startsWith(month))&&(!key||r.key===key))days.set(r.date,(days.get(r.date)??0)+r.stock)}
 return [...days].sort(([a],[b])=>a.localeCompare(b)).map(([date,stock])=>({date,stock}));
}
