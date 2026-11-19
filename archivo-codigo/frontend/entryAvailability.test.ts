import {describe,it,expect} from 'vitest';
import {entryAvailability,availableEntries,rackAvailability,rackAvailabilityLabel} from './entryFlow';
const blocked={Clave:'a',AsignacionId:1,Parte:'5G0 857 173',PiezasPorCaja:600,CapacidadCajas:1,CapacidadNominalPiezas:600,SaldoRegistradoPiezas:181,CajasDisponibles:0,CajasReservadas:0};
describe('disponibilidad antes de confirmar',()=>{
 it('una caja parcial no libera el único espacio',()=>{const s=entryAvailability(blocked);expect(s.canReceive).toBe(false);expect(s.label).toContain('Sin espacio');expect(rackAvailabilityLabel([blocked])).toContain('Sin espacio');expect(s.reason).toContain('181');expect(s.reason).toContain('600')});
 it('prioriza posiciones con entrada y no declara toda la estantería libre',()=>{const free={...blocked,Clave:'b',AsignacionId:2,SaldoRegistradoPiezas:0,CajasDisponibles:1};expect(availableEntries([blocked,free])[0]).toBe(free);expect(rackAvailability([blocked,free])).toMatchObject({available:1,total:2})});
 it('espacio reservado bloquea entrada libre y permite entrega vinculada',()=>{const r={...blocked,SaldoRegistradoPiezas:0,CajasReservadas:1};expect(entryAvailability(r).label).toBe('Espacio reservado');expect(rackAvailabilityLabel([r])).toBe('Espacio reservado');expect(entryAvailability({...r,SolicitudId:1,Cajas:1})).toMatchObject({canReceive:true,label:'Entrega reservada disponible'});expect(rackAvailability([r,{...r,SolicitudId:1,Cajas:1}])).toMatchObject({total:1,available:1,reservations:1})});
 it('no inventa disponibilidad cuando falta saldo, empaque o capacidad',()=>{for(const r of [{...blocked,SaldoRegistradoPiezas:null},{...blocked,CapacidadNominalPiezas:null},{...blocked,PiezasPorCaja:0}])expect(entryAvailability(r).canReceive).toBe(false)});
});
