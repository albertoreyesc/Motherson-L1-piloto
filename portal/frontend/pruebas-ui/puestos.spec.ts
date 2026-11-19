import {test,expect} from '@playwright/test';
import fs from 'node:fs';
import path from 'node:path';
test('Identidad individual y captura limitada al puesto; consulta sin captura',async({page})=>{
 const acceso=JSON.parse(fs.readFileSync(path.resolve('../.local/acceso-inicial-Motherson_L1_MVP_Pruebas.json'),'utf8'));
 const errores:string[]=[];const escrituras:string[]=[];
 page.on('pageerror',e=>errores.push(e.message));
 page.on('request',r=>{if(r.method()==='POST'&&!r.url().includes('/auth/'))escrituras.push(r.url())});
 async function login(nombre:string){
  await page.goto('http://127.0.0.1:5088/');
  await page.getByLabel('Usuario',{exact:true}).fill(nombre);await page.getByLabel('Contraseña',{exact:true}).fill(acceso.password);
  const r=page.waitForResponse(r=>r.url().includes('/inventario?alarmas=true'));
  await page.getByRole('button',{name:'Iniciar sesión'}).click();return await(await r).json();
 }
 const data=await login('entrada.prueba');
 const me=await page.request.get('http://127.0.0.1:5088/api/auth/me');const actor=await me.json();
 await page.getByRole('navigation').getByRole('button',{name:'Entradas y salidas',exact:true}).click();
 await expect(page.getByRole('button',{name:'Salida · cajas',exact:true})).toBeDisabled();
 await page.getByRole('checkbox',{name:'Solo ubicaciones disponibles'}).uncheck();
 const propios=data.filas.filter((r:{id_puesto:number})=>r.id_puesto===actor.puestoId);
 expect(propios.length).toBeGreaterThan(0);
 const grupos=new Set(propios.map((r:{id_estanteria:number})=>r.id_estanteria));
 await expect(page.locator('.mov-tarjeta')).toHaveCount(grupos.size);
 const ajeno=data.filas.find((r:{id_puesto:number})=>r.id_puesto!==actor.puestoId);
 await page.getByRole('searchbox').fill(ajeno.numero_parte);
 await expect(page.locator('.mov-tarjeta')).toHaveCount(0);
 await page.getByRole('searchbox').fill('');
 fs.mkdirSync(path.resolve('../evidencias/simplificacion/capturas'),{recursive:true});
 await page.screenshot({path:path.resolve('../evidencias/simplificacion/capturas/operador-puesto.png')});
 await page.getByRole('button',{name:'Cerrar sesión'}).click();await login('consulta.prueba');
 await page.getByRole('navigation').getByRole('button',{name:'Entradas y salidas',exact:true}).click();
 await expect(page.getByRole('heading',{name:'Consulta autorizada'})).toBeVisible();
 await expect(page.locator('.mov-confirmar')).toHaveCount(0);
 expect(errores).toEqual([]);expect(escrituras).toEqual([]);
});
