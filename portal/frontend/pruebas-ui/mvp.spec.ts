import {test,expect} from '@playwright/test';
import fs from 'node:fs';import path from 'node:path';
test('Recorrido de seis secciones, captura visual e histórico; escritorio y móvil',async({page})=>{
 const acceso=JSON.parse(fs.readFileSync(path.resolve('../.local/acceso-inicial-Motherson_L1_MVP_Desarrollo.json'),'utf8'));
 const errors:string[]=[];page.on('pageerror',e=>errors.push(e.message));
 page.on('response',r=>{if(r.url().includes('/api/'))console.log(r.status(),r.url())});page.on('pageerror',e=>console.log('Error de página:',e.message));await page.goto('/');await page.getByLabel('Usuario',{exact:true}).fill(acceso.usuario);await page.getByLabel('Contraseña',{exact:true}).fill(acceso.password);const inventario=page.waitForResponse(r=>r.url().includes('/inventario?alarmas=true'));await page.getByRole('button',{name:'Iniciar sesión'}).click();const actual=await(await inventario).json();await expect(page.getByRole('heading',{name:'Resumen',exact:true})).toBeVisible({timeout:15000});const saldo=actual.filas.every((r:{cantidad:number|null})=>r.cantidad!=null)?new Intl.NumberFormat('es-MX').format(actual.filas.reduce((n:number,r:{cantidad:number})=>n+r.cantidad,0)):'N/D';await expect(page.getByText(saldo+' pz',{exact:true})).toBeVisible();
 const dir=path.resolve('../evidencias/capturas');fs.mkdirSync(dir,{recursive:true});await page.screenshot({path:path.join(dir,'01-resumen.png')});
 const tabs=['Materiales y catálogos','Estaciones y ubicaciones','Inventario','Entradas y salidas','Historial de movimientos'];
 for(const [i,name] of tabs.entries()){
  await page.getByRole('navigation').getByRole('button',{name,exact:true}).click();await expect(page.getByRole('heading',{name,exact:true})).toBeVisible();
  if(name==='Entradas y salidas'){await expect(page.locator('.mvp-location').first()).toBeVisible();await page.locator('.mvp-location').first().click();await expect(page.getByRole('button',{name:'Confirmar entrada',exact:true})).toBeEnabled();}
  if(name==='Historial de movimientos'){
   await expect(page.locator('tbody tr').first()).toBeVisible();await page.getByRole('combobox',{name:'Origen',exact:true}).selectOption('HISTORICO_21');await page.getByRole('button',{name:'Consultar movimientos',exact:true}).click();await expect(page.locator('tbody tr').first()).toContainText('H21:');
  }
  await page.screenshot({path:path.join(dir,String(i+2).padStart(2,'0')+'-pantalla.png')});
 }
 await page.setViewportSize({width:390,height:844});await page.getByLabel('Sección',{exact:true}).selectOption('0');await expect(page.getByRole('heading',{name:'Resumen',exact:true})).toBeVisible();expect(await page.evaluate(()=>document.documentElement.scrollWidth<=window.innerWidth)).toBe(true);await page.screenshot({path:path.join(dir,'07-movil.png')});expect(errors).toEqual([]);
});
