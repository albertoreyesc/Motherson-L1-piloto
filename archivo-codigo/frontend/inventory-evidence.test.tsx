import React from 'react';
import {renderToStaticMarkup} from 'react-dom/server';
import {describe,it,expect} from 'vitest';
import {InventoryEvidence,AdjustmentPanel,PeriodClosePanel,evidenceLabel,observedHours,alertEvaluationLabel} from './InventoryEvidence';
describe('evidencia del inventario en la interfaz',()=>{
 it('separa un cero conocido de un saldo ausente y muestra la naturaleza separada',()=>{
  const html=renderToStaticMarkup(<InventoryEvidence rows={[{MaterialId:1,UbicacionId:1,Parte:'PRUEBA-CERO',Ubicacion:'RACK',SaldoRegistradoPiezas:0,EntradasConfirmadasPiezas:0,EstadoEvidencia:'CONCILIADO_AL_CORTE',Simulado:true},{MaterialId:2,UbicacionId:2,Parte:'PRUEBA-ND',Ubicacion:'RACK-2',SaldoRegistradoPiezas:null,EstadoEvidencia:'SIN_CONTEO',Simulado:false}]}/>);
  expect(html).toContain('<strong>0</strong>');expect(html).toContain('<strong>N/D</strong>');expect(html).toContain('SIMULADO');expect(html).toContain('OPERATIVO');expect(html).toContain('Sin conteo');
 });
 it('muestra el recuento real con fecha y persona y conserva el motivo de cobertura N/D',()=>{
  const html=renderToStaticMarkup(<InventoryEvidence rows={[{MaterialId:1,UbicacionId:2,Parte:'PRUEBA',Ubicacion:'R',SaldoRegistradoPiezas:129,EstadoEvidencia:'REGISTRO_PARCIAL',UltimoConteoPiezas:100,UltimoConteoUtc:'2026-10-22T12:00:00Z',UltimoConteoUsuario:'admin.prueba',MotivoCobertura:'N/D: captura incompleta'}]}/>);
  expect(html).toContain('Registro parcial');expect(html).toContain('100 pz');expect(html).toContain('admin.prueba');expect(html).toContain('N/D: captura incompleta');expect(html).toContain('horas transcurridas');expect(html).not.toContain('Stock (piezas)');
 });
 it('las pantallas administrativas no permiten confirmar sin revisar y capturar evidencia',()=>{
  const action=async()=>({});const adjust=renderToStaticMarkup(<AdjustmentPanel rows={[]} action={action}/>),close=renderToStaticMarkup(<PeriodClosePanel periods={[]} rows={[]} action={action}/>);
  expect(adjust).toContain('disabled=""');expect(adjust).toContain('Revisar ajuste');expect(close).toContain('capturaron y validaron manualmente');expect(close).toContain('incompleto');expect(close).toContain('Revisar cierre de periodo');
 });
 it('una alerta persistida pierde vigencia visual si el saldo actual es parcial',()=>{const a={MaterialId:1,UbicacionId:2,PeriodoId:'P',Estado:'ACTIVA'};expect(alertEvaluationLabel(a,[{MaterialId:1,UbicacionId:2,PeriodoId:'P',Critica:false}])).toContain('sin vigencia');expect(alertEvaluationLabel(a,[{MaterialId:1,UbicacionId:2,PeriodoId:'P',Critica:true}])).toContain('al corte');});
 it('una ventana corta no redondea horas positivas a cero',()=>{expect(observedHours(0.000342777)).not.toBe('0');expect(observedHours(null)).toBe('N/D')});
 it('no traduce una evidencia parcial a verificado',()=>{expect(evidenceLabel('REGISTRO_PARCIAL')).toBe('Registro parcial');expect(evidenceLabel('CONCILIADO_AL_CORTE')).toBe('Conciliado al corte')});
});
