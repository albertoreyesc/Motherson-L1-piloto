import {describe,it,expect} from 'vitest';
import {duration,orderTimes} from './simulationMetrics';
describe('tiempos del escenario',()=>{
 it('conserva diferencias en zona horaria y rechaza secuencias invertidas',()=>{expect(duration('2026-10-23T15:00:00-06:00','2026-10-23T21:03:00Z')).toBe(3);expect(duration('2026-10-23T21:03:00','2026-10-23T21:00:00')).toBeNull();expect(duration(null,'bad')).toBeNull()});
 const event=(Tipo:string,minute:number,OrdenId='order')=>({Tipo,OrdenId,Estado:'APLICADO',VirtualUtc:`2026-10-23T21:${String(minute).padStart(2,'0')}:00Z`});
 it('distingue respuesta, preparación, traslado, desabasto y recuperación sin sumarlos dos veces',()=>{const t=orderTimes([event('SOLICITUD',0),event('ALERTA',0),event('ACEPTACION',2),event('TRASLADO',5),event('DESABASTO',6),event('ENTRADA_AUTO',8),event('RECUPERACION',8),event('CIERRE_ORDEN',8)],'order','2026-10-23T21:10:00Z');expect(t).toEqual({response:2,preparation:3,transport:3,total:8,shortage:2,recovery:8,open:null})});
 it('una solicitud abierta no recibe duración de cierre inventada',()=>{const t=orderTimes([event('SOLICITUD',0),event('DESABASTO',2),event('CIERRE_ORDEN',4,'other')],'order','2026-10-23T21:07:00Z');expect(t.total).toBeNull();expect(t.shortage).toBeNull();expect(t.open).toBe(7)});
});
