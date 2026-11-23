import {describe,it,expect} from 'vitest';
import {planV2,sugerirColumnas,leerCsvV2} from './importacionV2';
import {libroAdminV2} from './excelAdminV2';
import type {Material,KpisAdmin} from './adminTypes';
const puestos=[{id:'p1',nombre:'SZ01'}];
const material:Material={id:'m1',numeroParte:'000123-CE',descripcion:'Viejo',variante:'A',revision:'',modelo:'',uso:'COMPARTIDO',puesto:'p1',empaque:10,minimo:5,activo:true};
describe('Importación flexible v2',()=>{
 it('encabezados con espacios, acentos y alias',()=>expect(sugerirColumnas([' NÚMERO de PARTE ','Descripción','PZ / CAJA','AUTO','REV'])).toEqual({numeroParte:0,descripcion:1,empaque:2,modelo:3,revision:4}));
 it('no adivina encabezados duplicados',()=>expect(sugerirColumnas(['Parte','Part number']).numeroParte).toBeUndefined());
 it('fila seleccionada, ceros/sufijos y variante nueva',()=>{const rows=[['Título'],['Código','Puesto','Empaque','Color'],['000123-CF','SZ01',12,'NUEVA']];const f=planV2(rows,1,sugerirColumnas(rows[1]),[material],puestos)[0];expect(f.estado).toBe('ALTA');expect(f.datos.numeroParte).toBe('000123-CF');expect(f.datos.variante).toBe('NUEVA');expect(f.fila).toBe(3);expect(f.avisos.length).toBeGreaterThan(0)});
 it('vacío conserva y BORRAR es explícito',()=>{const f=planV2([['Parte','Descripción','Color'],['000123-CE','','[BORRAR]']],0,{numeroParte:0,descripcion:1,variante:2},[material],puestos)[0];expect(f.datos).not.toHaveProperty('descripcion');expect(f.datos.variante).toBe('')});
 it('marca ambos duplicados',()=>{const fs=planV2([['Parte'],['000123-CE'],['000123-CE']],0,{numeroParte:0},[material],puestos);expect(fs.every(f=>f.estado==='ERROR')).toBe(true)});
 it('no fusiona códigos similares',()=>{const f=planV2([['Parte','Puesto','Empaque'],['000123 CE','SZ01',10]],0,{numeroParte:0,puesto:1,empaque:2},[material],puestos)[0];expect(f.estado).toBe('ALTA');expect(f.datos).not.toHaveProperty('id');expect(f.avisos.join('')).toContain('no se fusionará')});
 it('rechaza fórmulas, números inseguros y altas sin empaque',()=>{for(const value of ['[FORMULA: convertir a valor]',1234567890123456,'NUEVO']){const f=planV2([['Parte'],[value]],0,{numeroParte:0},[],puestos)[0];expect(f.estado).toBe('ERROR')}expect(()=>planV2([['Parte']],0,{numeroParte:0,uso:0},[],puestos)).toThrow()});
 it('uso, variante, revisión y modelo separados',()=>{const f=planV2([['Parte','Uso','Color','Rev','Auto'],['000123-CE','EXCLUSIVO','AZUL','C','JETTA']],0,{numeroParte:0,uso:1,variante:2,revision:3,modelo:4},[material],puestos)[0];expect(f.datos).toMatchObject({uso:'EXCLUSIVO',variante:'AZUL',revision:'C',modelo:'JETTA'})});
});
describe('CSV estructurado',()=>{
 it('detecta punto y coma con título antes del encabezado',()=>{const rows=leerCsvV2('Título\nParte;Puesto;Empaque\n000123-CF;SZ01;10');expect(rows[2]).toEqual(['000123-CF','SZ01','10'])});
 it('conserva comas y saltos dentro de comillas',()=>expect(leerCsvV2('Parte,Descripción\n0001,"Pieza, con\nsalto"')[1]).toEqual(['0001','Pieza, con\nsalto']));
 it('rechaza comillas sin cerrar',()=>expect(()=>leerCsvV2('Parte,Descripción\n0001,"Pieza')).toThrow('comillas sin cerrar'));
});
describe('Exportación KPIs' ,()=>{
 it('ambas hojas coinciden con la consulta, ceros y N/D',async()=>{const datos={contexto:{linea:2,corte:'2026-11-22T18:00:00Z',desde:'2026-11-20',hasta:'2026-11-22',estacion:'',material:'',turno:''},estadoActual:{inventario:10,cajasEquivalentes:1,agotados:0,criticos:0,advertencia:0,sinDatos:0,ubicaciones:1,materiales:[{id:'m1',numeroParte:'000123-CE',saldo:10,minimo:5,estado:'NORMAL',activo:true,ubicaciones:1}]},actividad:{completados:1,enCurso:0,repetidos:0,incompletos:0,intervaloPromedioHoras:null,intervalosEvaluados:0,porDia:[{dia:'2026-11-22',cantidad:1}],repeticiones:[],operaciones:[{id:'a1',material:'m1',ubicacion:'u1',responsable:'Prueba',inicio:'2026-11-22T17:00:00Z',fin:'2026-11-22T17:02:00Z',turno:1}],nota:'Datos controlados'}} as unknown as KpisAdmin;const b=await libroAdminV2(datos,[material]);const buffer=await b.xlsx.writeBuffer();const {default:ExcelJS}=await import('exceljs');const reloaded=new ExcelJS.Workbook();await reloaded.xlsx.load(buffer);expect(reloaded.worksheets.map(s=>s.name)).toEqual(['Hoja 1 - Estado actual','Hoja 2 - Actividad logística']);expect(reloaded.worksheets[0].getCell('B5').value).toBe(10);expect(reloaded.worksheets[1].getCell('B5').value).toBe(1);expect(reloaded.worksheets[1].getCell('B9').value).toBe('N/D');expect(JSON.stringify(reloaded.worksheets[0].getSheetValues())).toContain('000123-CE')});
});

import {preverMovimiento} from './movimientoV2';
describe('Captura por bloques: saldo y capacidad',()=>{
 const u={id:'u1',material:'m1',estanteria:'r1',cantidad:15,libres:2,apertura:'2026-11-22T00:00:00Z'};
 it('retira remanente de caja abierta sin saldo negativo',()=>expect(preverMovimiento(material,u,'SALIDA',2)).toEqual({piezas:15,saldo:0}));
 it('respeta capacidad y limita salidas a las cajas existentes',()=>{expect(preverMovimiento(material,u,'ENTRADA',3).error).toContain('espacio');expect(preverMovimiento(material,u,'SALIDA',3).error).toContain('suficientes')});
 it('material inhabilitado permite retiro y rechaza entrada',()=>{expect(preverMovimiento({...material,activo:false},u,'ENTRADA',1).error).toContain('inhabilitado');expect(preverMovimiento({...material,activo:false},u,'SALIDA',1)).toEqual({piezas:10,saldo:5})});
 it.each([0,-1,1.5,NaN,Infinity,1000001])('rechaza cantidad inválida %s',c=>expect(preverMovimiento(material,u,'ENTRADA',c).error).toBeTruthy());
});
