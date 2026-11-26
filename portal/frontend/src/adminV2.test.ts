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
 it('ambas hojas coinciden con la consulta, ceros y N/D',async()=>{const datos={contexto:{linea:2,corte:'2026-11-22T18:00:00Z',desde:'2026-11-20',hasta:'2026-11-22',estacion:'',material:'',turno:''},estadoActual:{inventario:10,cajasEquivalentes:1,agotados:0,criticos:0,advertencia:0,sinDatos:0,ubicaciones:1,materiales:[{id:'m1',numeroParte:'000123-CE',saldo:10,minimo:5,estado:'NORMAL',activo:true,ubicaciones:1}]},actividad:{completados:1,enCurso:0,repetidos:0,incompletos:0,intervaloPromedioHoras:null,intervalosEvaluados:0,porDia:[{dia:'2026-11-22',cantidad:1}],repeticiones:[],operaciones:[{id:'a1',material:'m1',ubicacion:'u1',responsable:'Prueba',inicio:'2026-11-22T17:00:00Z',fin:'2026-11-22T17:02:00Z',turno:1}],nota:'Datos controlados'}} as unknown as KpisAdmin;const b=await libroAdminV2(datos,[material]);const buffer=await b.xlsx.writeBuffer();const {default:ExcelJS}=await import('exceljs');const reloaded=new ExcelJS.Workbook();await reloaded.xlsx.load(buffer);expect(reloaded.worksheets.map(s=>s.name)).toEqual(['Hoja 1 - Estado actual','Hoja 2 - Actividad logística','Hoja 3 - Diagnóstico','Hoja 4 - Histórico atenciones']);const hist=JSON.stringify(reloaded.worksheets[3].getSheetValues());expect(hist).toContain('2026-11-22 11:00:00');expect(hist).toContain('000123-CE');expect(reloaded.worksheets[3].getCell('C9').value).toBe(2);expect(reloaded.worksheets[0].getCell('B5').value).toBe(10);expect(reloaded.worksheets[1].getCell('B5').value).toBe(1);expect(reloaded.worksheets[1].getCell('B9').value).toBe('N/D');expect(JSON.stringify(reloaded.worksheets[0].getSheetValues())).toContain('000123-CE')});
});

import {preverMovimiento} from './movimientoV2';
describe('Captura por bloques: saldo y capacidad',()=>{
 const u={id:'u1',material:'m1',estanteria:'r1',cantidad:15,libres:2,apertura:'2026-11-22T00:00:00Z'};
 it('retira remanente de caja abierta sin saldo negativo',()=>expect(preverMovimiento(material,u,'SALIDA',2)).toEqual({piezas:15,saldo:0}));
 it('respeta capacidad y limita salidas a las cajas existentes',()=>{expect(preverMovimiento(material,u,'ENTRADA',3).error).toContain('espacio');expect(preverMovimiento(material,u,'SALIDA',3).error).toContain('suficientes')});
 it('material inhabilitado permite retiro y rechaza entrada',()=>{expect(preverMovimiento({...material,activo:false},u,'ENTRADA',1).error).toContain('inhabilitado');expect(preverMovimiento({...material,activo:false},u,'SALIDA',1)).toEqual({piezas:10,saldo:5})});
 it.each([0,-1,1.5,NaN,Infinity,1000001])('rechaza cantidad inválida %s',c=>expect(preverMovimiento(material,u,'ENTRADA',c).error).toBeTruthy());
});

import {planDesactivar,siguienteRevision,revisarDuplicado,ubicacionActiva} from './ubicacionesV2';
describe('Ubicaciones: desactivar y mover (observación Motherson)',()=>{
 const base=()=>({materiales:[material,{...material,id:'m2',numeroParte:'000999',activo:false}],estanterias:[{id:'r1',nombre:'A',estacion:'s1',nivel:'1',capacidad:5},{id:'r2',nombre:'B',estacion:'s1',nivel:'2',capacidad:2}],atenciones:[] as never[],
  ubicaciones:[{id:'u1',material:'m1',estanteria:'r1',cantidad:15,libres:3,apertura:''},{id:'u2',material:'m1',estanteria:'r2',cantidad:0,libres:2,apertura:'',activa:false},{id:'u3',material:'m2',estanteria:'r1',cantidad:0,libres:3,apertura:''}]});
 it('ubicaciones sin el campo se consideran activas',()=>{expect(ubicacionActiva({})).toBe(true);expect(ubicacionActiva({activa:false})).toBe(false)});
 it('con saldo exige destino',()=>expect(planDesactivar(base(),'u1','').error).toContain('estantería destino'));
 it('sin saldo se puede desactivar sin destino',()=>expect(planDesactivar(base(),'u3','')).toEqual({piezas:0,cajas:0,destino:'NINGUNO'}));
 it('reactiva la asignación previa en el destino y respeta capacidad',()=>{expect(planDesactivar(base(),'u1','r2')).toEqual({piezas:15,cajas:2,destino:'REACTIVAR'});const e=base();e.estanterias[1].capacidad=1;expect(planDesactivar(e,'u1','r2').error).toContain('espacio')});
 it('no permite el mismo destino ni desactivar con atención abierta',()=>{expect(planDesactivar(base(),'u1','r1').error).toContain('distinta');const e={...base(),atenciones:[{id:'a1',material:'m1',ubicacion:'u1',puesto:'p1',responsable:'x',usuario:1,inicio:'',fin:null,turno:1}]};expect(planDesactivar(e,'u1','r2').error).toContain('atención abierta')});
 it('material inhabilitado no crea ubicación nueva',()=>{const e=base();e.ubicaciones[2].cantidad=3;expect(planDesactivar(e,'u3','r2').error).toContain('inhabilitado')});
 it('entrada bloqueada en ubicación desactivada',()=>expect(preverMovimiento(material,{id:'u2',material:'m1',estanteria:'r2',cantidad:0,libres:2,apertura:'',activa:false},'ENTRADA',1).error).toContain('desactivada'));
});
describe('Nueva revisión de una parte (duplicar)',()=>{
 it('sugiere la siguiente revisión',()=>{expect(siguienteRevision('B')).toBe('C');expect(siguienteRevision('AZ')).toBe('BA');expect(siguienteRevision('Z')).toBe('AA');expect(siguienteRevision('REV09')).toBe('REV10');expect(siguienteRevision('')).toBe('');expect(siguienteRevision('b-x')).toBe('')});
 it('exige número de parte distinto y lista solo los cambios',()=>{const d={numeroParte:'000123-CE',descripcion:'Viejo',variante:'A',revision:'',modelo:'',uso:'COMPARTIDO',empaque:10,minimo:5};expect(revisarDuplicado(material,d,[material]).errores.join()).toContain('ya existe');const r=revisarDuplicado(material,{...d,numeroParte:'000123-CF',revision:'B',empaque:12},[material]);expect(r.errores).toEqual([]);expect(r.cambios.map(c=>c.campo)).toEqual(['numeroParte','revision','empaque'])});
 it('valida empaque y mínimo',()=>expect(revisarDuplicado(material,{numeroParte:'N',descripcion:'',variante:'',revision:'',modelo:'',uso:'',empaque:0,minimo:-1},[material]).errores.length).toBe(2));
});

describe('Importar catálogo antes de preparar almacenamiento',()=>{
 const rows=[['Número de parte','Puesto','Piezas por caja','Modelo / vehículo'],['L2-0001','L2-P01',24,'Plataforma A'],['L2-0002','L2-P01',30,'Plataforma A']];
 it('reconoce todos los encabezados de la plantilla, incluido Modelo / vehículo',()=>expect(sugerirColumnas(rows[0])).toEqual({numeroParte:0,puesto:1,empaque:2,modelo:3}));
 it('explica la autorización que falta para un puesto inexistente',()=>expect(planV2(rows,0,sugerirColumnas(rows[0]),[],[]).every(f=>f.estado==='ERROR'&&f.errores.join().includes('Crear puestos faltantes'))).toBe(true));
 it('prepara altas distintas por un dígito con el puesto nuevo explícito',()=>{const fs=planV2(rows,0,sugerirColumnas(rows[0]),[],[],true);expect(fs.map(f=>f.estado)).toEqual(['ALTA','ALTA']);expect(fs.map(f=>f.parte)).toEqual(['L2-0001','L2-0002']);expect(fs[0].datos).toMatchObject({puestoNuevo:'L2-P01',empaque:24,modelo:'Plataforma A'});expect(fs[0].datos).not.toHaveProperty('puesto')});
 it('reutiliza un puesto existente por nombre',()=>expect(planV2(rows,0,sugerirColumnas(rows[0]),[],[{id:'p2',nombre:'L2-P01'}],true)[0].datos).toMatchObject({puesto:'p2'}));
 it('un identificador exacto tiene prioridad sobre un nombre coincidente',()=>{const f=planV2([['Parte','Puesto','Empaque'],['ABC','p2',10]],0,{numeroParte:0,puesto:1,empaque:2},[],[{id:'p2',nombre:'L2-P01'},{id:'p3',nombre:'p2'}],true)[0];expect(f.estado).toBe('ALTA');expect(f.datos.puesto).toBe('p2')});
 it('no crea puestos a partir de nombres ambiguos',()=>expect(planV2(rows,0,sugerirColumnas(rows[0]),[],[{id:'p2',nombre:'L2-P01'},{id:'p3',nombre:'L2-P01'}],true)[0].estado).toBe('ERROR'));
 it('sigue rechazando filas sin puesto, empaque o con partes duplicadas',()=>{expect(planV2([['Parte'],['ABC']],0,{numeroParte:0},[],[],true)[0].estado).toBe('ERROR');expect(planV2([...rows,rows[1]],0,sugerirColumnas(rows[0]),[],[],true).filter(f=>f.estado==='ERROR')).toHaveLength(2)});
});

it('exporta diagnóstico con el mismo corte y cifras consultadas',async()=>{const datos={contexto:{linea:2,corte:'2026-11-24T23:00:00Z',desde:'2026-11-24',hasta:'2026-11-24'},estadoActual:{inventario:0,cajasEquivalentes:0,agotados:0,criticos:0,advertencia:0,sinDatos:0,materiales:[]},actividad:{completados:2,enCurso:0,repetidos:0,incompletos:0,intervaloPromedioHoras:null,intervalosEvaluados:0,porDia:[],repeticiones:[],operaciones:[]},diagnostico:{objetivoMinutos:4.5,completadosValidos:2,duracionesInvalidas:0,promedioMinutos:3,p90Minutos:4.6,fueraObjetivo:1,porcentajeFueraObjetivo:50,porPuesto:[],porMaterial:[],porHora:[],porTurno:[],recomendaciones:['Muestra controlada']}} as unknown as KpisAdmin;const b=await libroAdminV2(datos,[]);expect(b.worksheets[2].getCell('B1').value).toBe(2);expect(b.worksheets[2].getCell('B7').value).toBe(3);expect(b.worksheets[2].getCell('B8').value).toBe(4.6);expect(b.worksheets[2].getCell('B9').value).toBe(50)});

import {cajasOcupadasV2,limitesCapacidadV2,materialesEnEstanteria} from './ubicacionesV2';
import {fechaLocalExcel,duracionMinutos} from './excelAdminV2';
describe('espacios en estanterías (+ / −)',()=>{
 const est={materiales:[{id:'a',empaque:10},{id:'b',empaque:4}],ubicaciones:[{id:'u1',material:'a',estanteria:'r1',cantidad:25},{id:'u2',material:'b',estanteria:'r1',cantidad:4},{id:'u3',material:'a',estanteria:'r2',cantidad:0,activa:false}]} as never;
 it('cuenta cajas ocupadas como el servidor (caja abierta cuenta completa)',()=>{expect(cajasOcupadasV2(est,'r1')).toBe(4);expect(cajasOcupadasV2(est,'r2')).toBe(0)});
 it('no deja bajar la capacidad por debajo de lo ocupado ni de 1',()=>{expect(limitesCapacidadV2(est,'r1')).toEqual({minimo:4,maximo:1000000});expect(limitesCapacidadV2(est,'r2').minimo).toBe(1)});
 it('avisa cuando la estantería es compartida (solo ubicaciones activas)',()=>{expect(materialesEnEstanteria(est,'r1')).toBe(2);expect(materialesEnEstanteria(est,'r2')).toBe(0)});
});
describe('histórico de KPIs en Excel',()=>{
 it('convierte a hora de Ciudad de México y calcula la duración',()=>{expect(fechaLocalExcel('2026-11-22T17:00:00Z')).toBe('2026-11-22 11:00:00');expect(fechaLocalExcel(null)).toBe('');expect(duracionMinutos('2026-11-22T17:00:00Z','2026-11-22T17:02:30Z')).toBe(2.5);expect(duracionMinutos('2026-11-22T17:00:00Z',null)).toBeNull()});
});
import {rolPortal,SECCIONES_POR_ROL} from './AdminApp';
describe('secciones por rol',()=>{
 it('cada rol ve solo lo suyo',()=>{expect(rolPortal(['view','historico','administrar_accesos','entry','exit','imagenes','ajustar_catalogo'])).toBe('ADMIN');expect(rolPortal(['view','atencion','entry'])).toBe('PUESTO');expect(rolPortal(['view','historico'])).toBe('LECTOR');expect(SECCIONES_POR_ROL.ADMIN).toHaveLength(9);expect(SECCIONES_POR_ROL.PUESTO).toEqual([4,3]);expect(SECCIONES_POR_ROL.LECTOR).not.toContain(1);expect(SECCIONES_POR_ROL.LECTOR).not.toContain(8);expect(SECCIONES_POR_ROL.LECTOR).not.toContain(4)});
});
