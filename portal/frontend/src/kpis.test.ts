import {describe,it,expect} from 'vitest';
import ExcelJS from 'exceljs';
import {atencion,ahoraMexico,pct,type Kpis,type MaterialKpi} from './Indicadores';
import {libroKpis} from './ExcelKpis';
const m=(parte:string,estado:string,porcentajeMinimo:number):MaterialKpi=>({parte,saldo:1,minimo:1,estado,porcentajeMinimo,posiciones:1});
const base:Kpis={contexto:{corteLocal:'2026-11-05T23:30',desde:'2026-10-30',hasta:'2026-11-05',modo:'MAESTRO_Y_CONTINUACION',fechaStock:'2026-10-27T23:30:00Z',denominador:3,estacion:'',parte:''},
 estadoActual:{inventario:885,cajasEquivalentes:36.875,posiciones:3,posicionesConSaldo:3,sinSaldo:0,sinEmpaque:0,sinEvaluar:0,materialesEvaluados:3,advertencia:{cantidad:1,porcentaje:100/3},critico:{cantidad:1,porcentaje:100/3},agotado:{cantidad:1,porcentaje:100/3},materiales:[m('C','CRITICO',80),m('A','AGOTADO',0),m('W','ADVERTENCIA',115)],inventarioDetalle:[]},
 actividad:{completados:2,enCurso:1,repetidos:1,incompletos:0,intervaloPromedioHoras:3,intervalosEvaluados:1,operacionesIniciadas:3,operacionesEvaluadasCalidad:3,porDia:[],materiales:[],repeticiones:[],ultimas:[],operaciones:[]}};
describe('Indicadores del Equipo 2',()=>{
 it('ordena agotados, críticos y advertencia; dentro del estado, menor cobertura primero',()=>{
  expect(atencion([m('N','NORMAL',300),m('C2','CRITICO',90),m('W','ADVERTENCIA',110),m('C1','CRITICO',40),m('A','AGOTADO',0)]).map(x=>x.parte)).toEqual(['A','C1','C2','W']);
 });
 it('arma el corte en hora de México con el formato del backend',()=>{
  const r=ahoraMexico();expect(r.corte).toMatch(/^\d{4}-\d{2}-\d{2}T\d{2}:\d{2}$/);expect(r.fecha).toBe(r.corte.slice(0,10));
 });
 it('formatea porcentajes y valores faltantes',()=>{expect(pct(20)).toBe('20 %');expect(pct(null)).toBe('N/D')});
 it('genera el Excel con el resumen de los 10 KPIs y sus tablas',async()=>{
  const blob=await libroKpis(base);const libro=new ExcelJS.Workbook();await libro.xlsx.load(await blob.arrayBuffer());
  expect(libro.worksheets.map(h=>h.name)).toEqual(['Resumen KPIs','Atención','Materiales','Inventario','Registros','Por día','Repetidos','Más reabastecidos','Último reabasto']);
  const r=libro.getWorksheet('Resumen KPIs')!;
  const kpis=[];for(let i=2;i<=11;i++)kpis.push([r.getCell(i,3).value,r.getCell(i,4).value]);
  expect(kpis).toEqual([['Inventario actual en punto de uso',885],['Cajas equivalentes disponibles',36.875],['Materiales en advertencia',1],['Materiales en nivel crítico',1],['Materiales agotados',1],['Reabastecimientos completados',2],['Reabastecimientos en curso',1],['Reabastecimientos repetidos en un día',1],['Registros incompletos',0],['Intervalo promedio entre reabastecimientos',3]]);
  expect(libro.getWorksheet('Atención')!.getColumn(1).values.slice(2)).toEqual(['A','C','W']);
 });
});
