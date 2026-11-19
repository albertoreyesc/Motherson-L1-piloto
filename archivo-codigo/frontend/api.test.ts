import {describe,it,expect} from 'vitest';
import {date,nd,verifiedStock,selection,type Catalog} from './api';
describe('presentación y selección',()=>{
 it('mantiene N/D separado de cero',()=>{expect(nd(null)).toBe('N/D');expect(nd(0)).toBe('0')});
 it('sin conteo no fabrica un saldo inicial cero',()=>{expect(verifiedStock([])).toBeNull();expect(verifiedStock([{StockPiezas:20},{StockPiezas:null}])).toBeNull();expect(verifiedStock([{StockPiezas:0}])).toBe(0)});
 it('presenta el mismo instante con Z u offset y conserva inválidos como N/D',()=>{expect(date('2026-10-22T06:00:00Z')).toBe(date('2026-10-22T00:00:00-06:00'));expect(date('2026-10-22T06:00:00')).toBe(date('2026-10-22T06:00:00Z'));expect(date('invalido')).toBe('N/D')});
 it('no cruza ubicaciones ni elimina asignaciones válidas',()=>{const c:Catalog={materials:[],puestos:[],estaciones:[],ubicaciones:[],misPuestos:[],asignaciones:[{MaterialId:1,UbicacionId:2},{MaterialId:1,UbicacionId:3},{MaterialId:2,UbicacionId:2}]};expect(selection(c,1,2)).toHaveLength(1);expect(selection(c,1,0)).toHaveLength(2)});
});
