import {test,expect,type Page} from '@playwright/test';
import fs from 'node:fs';
import path from 'node:path';
import type {EstadoInventario} from '../src/Alarmas';
const capturas=path.resolve('../evidencias/capturas-movimientos');
fs.mkdirSync(capturas,{recursive:true});
async function iniciar(page:Page,base='Desarrollo',usuario?:string){
 const acceso=JSON.parse(fs.readFileSync(path.resolve('../.local/acceso-inicial-Motherson_L1_MVP_'+base+'.json'),'utf8'));
 await page.goto(base==='Pruebas'?'http://127.0.0.1:5088/':'http://127.0.0.1:5087/');
 await page.getByLabel('Usuario',{exact:true}).fill(usuario??acceso.usuario);await page.getByLabel('Contraseña',{exact:true}).fill(acceso.password);
 const lectura=page.waitForResponse(r=>r.url().includes('/inventario?alarmas=true'));
 await page.getByRole('button',{name:'Iniciar sesión'}).click();return await(await lectura).json() as EstadoInventario;
}
async function entrar(page:Page){
 if((await page.viewportSize())!.width<=760)await page.getByLabel('Sección',{exact:true}).selectOption('4');
 else await page.getByRole('navigation').getByRole('button',{name:'Entradas y salidas',exact:true}).click();
 await expect(page.locator('.mov-cabecera')).toBeVisible();
}
async function sinDesbordamiento(page:Page){expect(await page.evaluate(()=>document.documentElement.scrollWidth<=window.innerWidth)).toBe(true)}

test('Captura real de Desarrollo: búsqueda, confirmación y tamaños, sin enviar movimientos',async({page})=>{
 const posts:string[]=[];const errores:string[]=[];page.on('pageerror',e=>errores.push(e.message));page.on('request',r=>{if(r.method()==='POST'&&!r.url().includes('/auth/'))posts.push(r.url())});
 const data=await iniciar(page);await page.setViewportSize({width:1600,height:1050});await entrar(page);
 const filas=data.filas.filter(r=>r.cajas_disponibles!=null&&r.cajas_disponibles>0);const grupos=new Set(filas.map(r=>r.id_estanteria));
 await expect(page.locator('.mov-tarjeta')).toHaveCount(grupos.size);
 expect(await page.locator('.mov-cuadricula').evaluate(el=>getComputedStyle(el).gridTemplateColumns.split(' ').length)).toBe(4);
 await expect(page.getByRole('button',{name:'Entrada · cajas',exact:true})).toHaveAttribute('aria-pressed','true');
 await expect(page.locator('.mov-cabecera')).toHaveCSS('background-color','rgb(218, 33, 40)');
 await page.screenshot({path:path.join(capturas,'01-ubicaciones-escritorio.png')});
 await page.getByRole('searchbox').fill('no-existe-una-ubicacion-asi');await expect(page.getByRole('heading',{name:'Sin coincidencias'})).toBeVisible();
 await page.getByRole('searchbox').fill(filas[0].numero_parte);await expect(page.locator('.mov-tarjeta').first()).toBeVisible();
 await page.locator('.mov-tarjeta').first().focus();await page.keyboard.press('Enter');
 if(await page.getByRole('heading',{name:'Selecciona el material',exact:true}).isVisible())await page.locator('.mov-tarjeta').first().click();
 await expect(page.getByRole('button',{name:'Confirmar entrada',exact:true})).toBeEnabled();
 await expect(page.locator('.mov-pasos [aria-current=step]')).toContainText('Confirmar');
 await expect(page.locator('.mov-pasos .completo')).toHaveCount(2);
 await expect(page.locator('.mov-confirmacion')).toContainText('Ubicación seleccionada');
 const cantidad=page.getByRole('spinbutton',{name:'Cajas completas'});await cantidad.fill('0');await expect(page.getByRole('button',{name:'Confirmar entrada',exact:true})).toBeDisabled();await expect(page.locator('#mov-validacion')).toContainText('cantidad entera');await cantidad.fill('1');
 await page.screenshot({path:path.join(capturas,'02-confirmacion-entrada-escritorio.png')});
 await page.getByRole('button',{name:'Salida · cajas',exact:true}).click();await expect(page.getByRole('button',{name:'Salida · cajas',exact:true})).toHaveAttribute('aria-pressed','true');
 await page.getByRole('searchbox').fill(filas[0].numero_parte);await page.locator('.mov-tarjeta').first().click();
 if(await page.getByRole('heading',{name:'Selecciona el material',exact:true}).isVisible())await page.locator('.mov-tarjeta').first().click();
 await expect(page.getByRole('button',{name:'Confirmar salida',exact:true})).toBeEnabled();await page.getByRole('spinbutton',{name:'Cantidad de piezas'}).fill('1000000');await expect(page.getByRole('button',{name:'Confirmar salida',exact:true})).toBeDisabled();await expect(page.locator('#mov-validacion')).toContainText('supera las piezas');await page.getByRole('spinbutton',{name:'Cantidad de piezas'}).fill('1');
 await page.screenshot({path:path.join(capturas,'03-confirmacion-salida-escritorio.png')});
 for(const [ancho,columnas,nombre] of [[900,2,'tablet'],[390,1,'movil']] as const){
  await page.setViewportSize({width:ancho,height:900});await sinDesbordamiento(page);await page.screenshot({path:path.join(capturas,'04-confirmacion-'+nombre+'.png'),fullPage:true});
  await page.getByRole('button',{name:'Cambiar ubicación',exact:true}).click();await page.getByRole('searchbox').fill('');
  expect(await page.locator('.mov-cuadricula').evaluate(el=>getComputedStyle(el).gridTemplateColumns.split(' ').length)).toBe(columnas);await sinDesbordamiento(page);await page.screenshot({path:path.join(capturas,'05-ubicaciones-'+nombre+'.png')});
  await page.locator('.mov-tarjeta').first().click();if(await page.getByRole('heading',{name:'Selecciona el material',exact:true}).isVisible())await page.locator('.mov-tarjeta').first().click();
 }
 expect(posts).toEqual([]);expect(errores).toEqual([]);
});

test('Estados aislados en navegador: materiales filtrados, sin espacio y saldo desconocido',async({page})=>{
 const data=await iniciar(page,'Pruebas');const r=data.filas[0];
 // Casos controlados SOLO en la respuesta del navegador; no se escriben en SQL.
 const filas=[{...r,id_ubicacion:900001,id_estanteria:900001,numero_parte:'PRUEBA-VISUAL-A',cantidad:24,cajas_disponibles:2},{...r,id_ubicacion:900002,id_estanteria:900001,numero_parte:'PRUEBA-VISUAL-B',cantidad:0,cajas_disponibles:0},{...r,id_ubicacion:900003,id_estanteria:900003,numero_parte:'PRUEBA-VISUAL-C',cantidad:null,cajas_disponibles:null}];
 await page.route('**/api/inventario?alarmas=true',route=>route.fulfill({json:{...data,filas}}));
 await page.getByRole('button',{name:'Actualizar',exact:true}).click();await entrar(page);
 await expect(page.locator('.mov-tarjeta')).toHaveCount(1);await page.getByRole('checkbox',{name:'Solo ubicaciones disponibles'}).uncheck();await expect(page.locator('.mov-tarjeta')).toHaveCount(2);
 await expect(page.getByText('Existencias desconocidas',{exact:true})).toBeVisible();await page.locator('.mov-tarjeta').first().click();await expect(page.getByRole('heading',{name:'Selecciona el material',exact:true})).toBeVisible();await expect(page.locator('.mov-tarjeta')).toHaveCount(2);
 await page.locator('.mov-tarjeta').filter({hasText:'PRUEBA-VISUAL-B'}).click();await expect(page.getByRole('button',{name:'Confirmar entrada',exact:true})).toBeDisabled();await expect(page.locator('#mov-validacion')).toContainText('espacio disponible');
 await page.getByRole('button',{name:'Cambiar ubicación',exact:true}).click();await page.locator('.mov-tarjeta').filter({hasText:'Existencias desconocidas'}).click();await expect(page.getByRole('button',{name:'Confirmar entrada',exact:true})).toBeDisabled();await expect(page.locator('#mov-validacion')).toContainText('existencias desconocidas');
 await page.getByRole('button',{name:'Salida · cajas',exact:true}).click();await expect(page.getByRole('checkbox',{name:'Solo ubicaciones disponibles'})).not.toBeChecked();await expect(page.locator('.mov-tarjeta')).toHaveCount(2);await page.getByRole('checkbox',{name:'Solo ubicaciones disponibles'}).check();await expect(page.locator('.mov-tarjeta')).toHaveCount(1);
});

test('Respuesta incierta: misma clave y datos tras recargar; controles bloqueados',async({page})=>{
 await iniciar(page,'Pruebas');await entrar(page);await page.getByRole('searchbox').fill('LAB-CONC-SAL');await page.locator('.mov-tarjeta').first().click();
 const intentos:{clave:string|undefined;datos:string|null}[]=[];
 await page.route('**/api/central/entries',async route=>{const r=route.request();intentos.push({clave:r.headers()['idempotency-key'],datos:r.postData()});await route.abort('failed')});
 await page.getByRole('button',{name:'Confirmar entrada',exact:true}).click();await expect(page.getByRole('alert')).toContainText('Respuesta pendiente');
 await expect(page.getByRole('button',{name:'Salida · cajas',exact:true})).toBeDisabled();await expect(page.getByRole('button',{name:'Cambiar ubicación',exact:true})).toBeDisabled();await expect(page.getByRole('spinbutton')).toBeDisabled();
 const conservado=await page.evaluate(()=>Object.keys(sessionStorage).filter(k=>k.startsWith('motherson-mvp-operacion:')).map(k=>JSON.parse(sessionStorage.getItem(k)!)));
 expect(conservado).toHaveLength(1);expect(conservado[0].id).toBe(intentos[0].clave);
 await page.reload();await entrar(page);await expect(page.getByRole('spinbutton')).toBeDisabled();await expect(page.getByRole('button',{name:'Comprobar la misma operación',exact:true})).toBeEnabled();await page.getByRole('button',{name:'Comprobar la misma operación',exact:true}).click();await expect(page.getByRole('alert')).toContainText('Respuesta pendiente');expect(intentos).toHaveLength(2);expect(intentos[1]).toEqual(intentos[0]);
 // Tampoco se vuelve a abrir la selección si la consulta deja de incluir esa ubicación.
 await page.route('**/api/inventario?alarmas=true',async route=>{const d=await(await route.fetch()).json();await route.fulfill({json:{...d,filas:[]}})});
 await page.getByRole('button',{name:'Actualizar',exact:true}).click();await expect(page.getByRole('heading',{name:'Comprobar la operación pendiente'})).toBeVisible();await expect(page.locator('.mov-tarjeta')).toHaveCount(0);
 await page.getByRole('button',{name:'Comprobar la misma operación',exact:true}).click();await expect(page.getByRole('alert')).toContainText('Respuesta pendiente');expect(intentos[2]).toEqual(intentos[0]);
});

test('Conflicto de concurrencia: informa el error y vuelve a consultar, sin registro de prueba',async({page})=>{
 await iniciar(page,'Pruebas');await entrar(page);await page.getByRole('searchbox').fill('LAB-CONC-SAL');await page.locator('.mov-tarjeta').first().click();
 await page.route('**/api/central/entries',route=>route.fulfill({status:409,json:{error:'El saldo cambió. Revisa la disponibilidad actual.'}}));
 const actualiza=page.waitForResponse(r=>r.url().includes('/inventario?alarmas=true'));await page.getByRole('button',{name:'Confirmar entrada',exact:true}).click();await actualiza;await expect(page.getByRole('alert')).toContainText('El saldo cambió');await expect(page.getByRole('spinbutton')).toBeEnabled();
 expect(await page.evaluate(()=>Object.keys(sessionStorage).filter(k=>k.startsWith('motherson-mvp-operacion:')))).toEqual([]);
});

test('Cuenta de entradas conserva su permiso y consulta no obtiene controles de captura',async({page})=>{
 await iniciar(page,'Pruebas','entrada.prueba');await entrar(page);await expect(page.getByRole('button',{name:'Entrada · cajas',exact:true})).toBeEnabled();await expect(page.getByRole('button',{name:'Salida · cajas',exact:true})).toBeDisabled();
 await page.getByRole('button',{name:'Cerrar sesión'}).click();await iniciar(page,'Pruebas','consulta.prueba');
 await page.getByRole('navigation').getByRole('button',{name:'Entradas y salidas',exact:true}).click();await expect(page.getByRole('heading',{name:'Consulta autorizada'})).toBeVisible();await expect(page.locator('.mov-confirmar')).toHaveCount(0);
});
