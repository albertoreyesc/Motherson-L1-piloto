import {describe,it,expect} from 'vitest';
import {importe,disponibilidad,cajasEnUbicacion,type Inventario} from './MvpTypes';
const row={cantidad:72,cantidad_por_caja:24,cajas_disponibles:3} as Inventario;
describe('Captura del MVP',()=>{
 it('convierte 2 cajas a 48 piezas y prevé 120',()=>expect(importe(row,'ENTRADA',2)).toEqual({piezas:48,saldo:120,valido:true}));
 it('sale por cajas: 2 cajas de 24 desde 120 prevén 72',()=>expect(importe({...row,cantidad:120},'SALIDA',2)).toEqual({piezas:48,saldo:72,valido:true}));
 it('una caja abierta cuenta como caja: 50 piezas de 24 son 3 cajas',()=>expect(cajasEnUbicacion({...row,cantidad:50})).toBe(3));
 it('la última caja abierta sale con sus piezas sueltas sin saldo negativo',()=>expect(importe({...row,cantidad:50},'SALIDA',3)).toEqual({piezas:50,saldo:0,valido:true}));
 it('no permite sacar más cajas de las que hay',()=>expect(importe({...row,cantidad:50},'SALIDA',4).valido).toBe(false));
 it('no permite salida sin empaque documentado',()=>expect(importe({...row,cantidad_por_caja:null},'SALIDA',1).valido).toBe(false));
 it.each([0,-1,1.5,1000001])('rechaza cantidad %s',n=>expect(importe(row,'ENTRADA',n).valido).toBe(false));
 it('no convierte un saldo desconocido en cero',()=>expect(importe({...row,cantidad:null},'ENTRADA',1).valido).toBe(false));
 it('respeta cajas disponibles calculadas por servidor',()=>expect(importe({...row,cajas_disponibles:0},'ENTRADA',1).valido).toBe(false));
 it('no permite más salidas que existencias',()=>expect(importe(row,'SALIDA',4).valido).toBe(false));
 it('explica capacidad pendiente y falta de espacio',()=>{expect(disponibilidad({...row,cajas_disponibles:null})).toContain('pendiente');expect(disponibilidad({...row,cajas_disponibles:0})).toContain('Sin espacio')});
});
import {revisarImagen} from './ImagenMaterial';
describe('Imagen de la pieza',()=>{
 it('acepta PNG y JPG de hasta 5 MB',()=>{expect(revisarImagen({type:'image/png',size:1000})).toBeNull();expect(revisarImagen({type:'image/jpeg',size:5*1024*1024})).toBeNull()});
 it.each(['image/gif','image/webp','application/pdf',''])('rechaza el tipo %s',t=>expect(revisarImagen({type:t,size:1000})).toContain('PNG o JPG'));
 it('rechaza archivos vacíos o de más de 5 MB',()=>{expect(revisarImagen({type:'image/png',size:0})).toContain('vacío');expect(revisarImagen({type:'image/png',size:5*1024*1024+1})).toContain('5 MB')});
});
import {cajasOcupadas,limitesCapacidad} from './CapacidadEstanteria';
describe('Capacidad de estantería (+ / −)',()=>{
 it('calcula ocupadas como capacidad menos libres',()=>expect(cajasOcupadas({cajas_estanteria:6,cajas_disponibles:2})).toBe(4));
 it('no deja bajar de lo ocupado',()=>expect(limitesCapacidad({cajas_estanteria:6,cajas_disponibles:2}).minimo).toBe(4));
 it('nunca baja de 1 caja aunque esté vacía',()=>expect(limitesCapacidad({cajas_estanteria:3,cajas_disponibles:3}).minimo).toBe(1));
 it('sin datos de ocupación no inventa un número',()=>expect(cajasOcupadas({cajas_estanteria:3,cajas_disponibles:null})).toBeNull());
});
import {planificar,normalizarParte,leerCsv,valorArchivo} from './importacionMateriales';
describe('Importar materiales desde Excel',()=>{
 const catalogo=[
  {numero_parte:'5G0 857 173',puesto:'SZ01',descripcion:'ELEMENTO DE AJUSTE',uso:'COMPARTIDO',cantidad_por_caja:600,minimo:50,variante_revision:null},
  {numero_parte:'17B 857 005 B  82V',puesto:'SG',descripcion:'TAPA',uso:null,cantidad_por_caja:20,minimo:null,variante_revision:'82V'},
 ];
 it('toma la última línea y colapsa espacios del número de parte',()=>expect(normalizarParte('SZ01\n17B 857 005 B   82V')).toBe('17B 857 005 B 82V'));
 it('plantilla: solo cambia lo distinto y las celdas vacías no borran',()=>{
  const p=planificar(catalogo,[['PARTE','PUESTO LOGÍSTICO','DESCRIPCION','USO','CANTIDAD X CAJA','MINIMO (PZ)','VARIANTE / COLOR'],
   ['5G0 857 173','SZ01','ELEMENTO DE AJUSTE','compartido',500,'',''],['17B 857 005 B  82V','SG','','EXCLUSIVO',20,30,'82V']])!;
  expect(p.cambios).toEqual([{parte:'5G0 857 173',campo:'cantidad_por_caja',anterior:'600',nuevo:'500'},
   {parte:'17B 857 005 B  82V',campo:'uso',anterior:null,nuevo:'EXCLUSIVO'},{parte:'17B 857 005 B  82V',campo:'minimo',anterior:null,nuevo:'30'}]);
 });
 it('reconoce la hoja Partes de UPAEP Update (encabezado en la fila 2)',()=>{
  const p=planificar(catalogo,[[],[null,'PUESTO LOGÍSTICO + MATERIAL ','PARTE','DESCRIPCION','ESTACION','ESTANTERIA ','NIVEL','CodigoEstanteria','CAJAS X ESTANTERIA ','CANTIDAD X CAJA'],
   [null,'SZ01','5G0 857 173','ELEMENTO DE AJUSTE','A03',1,'B','A03-1-B',1,600],[null,'5G0 857 173'],[null,'SG','7B 857 005 B  82V','TAPA','AL19',1,'C','',4,20]])!;
  expect(p.encabezado).toBe(2);expect(p.resumen).toMatchObject({sinCambios:1,noEncontrados:1,cambios:0});
 });
 it('reconoce NumerosDeParte del PFEP (NUMERO DE PARTE con prefijo, NOMBRE, USO, PIEZAS X CAJA)',()=>{
  const p=planificar(catalogo,[['NUMERO ','Column1','NUMERO DE PARTE ','Logistica','NOMBRE ','IMAGEN PIEZA','PROVEEDOR ','ORIGEN','AUTO','USO ','PIEZAS X CAJA '],
   [5,'5G0 857 173','SZ01\n5G0 857 173','SZ01','ELEMENTO DE AJUSTE','#VALUE!','X','EUROPA','JETTA','EXCLUSIVO ',600]])!;
  expect(p.cambios).toEqual([{parte:'5G0 857 173',campo:'uso',anterior:'COMPARTIDO',nuevo:'EXCLUSIVO'}]);
 });
 it('una fila con error no aplica ninguno de sus cambios',()=>{
  const p=planificar(catalogo,[['PARTE','USO','CANTIDAD X CAJA'],['5G0 857 173','EXCLUSIVO','TRASPALEO']])!;
  expect(p.cambios).toEqual([]);expect(p.filas[0]).toMatchObject({estado:'ERROR',fila:2});
 });
 it('un material repetido con datos distintos no se aplica',()=>{
  const p=planificar(catalogo,[['PARTE','MINIMO'],['5G0 857 173',10],['5G0 857 173',20]])!;
  expect(p.cambios).toEqual([]);expect(p.resumen.errores).toBe(1);expect(p.resumen.repetidas).toBe(1);
 });
 it('sin columna de número de parte no hay plan',()=>expect(planificar(catalogo,[['A','B'],[1,2]])).toBeNull());
 it('valida enteros y uso',()=>{expect(valorArchivo('minimo','-3')).toHaveProperty('error');expect(valorArchivo('cantidad_por_caja','1,200')).toEqual({valor:'1200'});expect(valorArchivo('uso','auto')).toHaveProperty('error')});
 it('lee CSV con punto y coma y comillas',()=>expect(leerCsv('PARTE;DESCRIPCION\r\n"5G0 857 173";"TAPA; GRANDE"\r\n')).toEqual([['PARTE','DESCRIPCION'],['5G0 857 173','TAPA; GRANDE']]));
});
describe('Variantes en la importación',()=>{
 const cat=[{numero_parte:'17A 800 375 CE',puesto:'SZ01',descripcion:'CERRADURA',uso:null,cantidad_por_caja:20,minimo:null,variante_revision:null}];
 it('una variante que no existe marca solo esa fila y deja aplicar las demás',()=>{
  const p=planificar([...cat,{...cat[0],numero_parte:'5G0 857 173'}],[['PARTE','MINIMO','VARIANTE / COLOR'],['17A 800 375 CE',10,'CE'],['5G0 857 173',12,'']],['82V','RM5'])!;
  expect(p.filas[0]).toMatchObject({estado:'ERROR'});expect(p.filas[0].detalle).toContain('«CE» no existe');
  expect(p.cambios).toEqual([{parte:'5G0 857 173',campo:'minimo',anterior:null,nuevo:'12'}]);
 });
 it('una variante válida se acepta',()=>expect(planificar(cat,[['PARTE','VARIANTE'],['17A 800 375 CE','82v']],['82V'])!.cambios).toEqual([{parte:'17A 800 375 CE',campo:'variante',anterior:null,nuevo:'82V'}]));
});
