import {test,expect} from '@playwright/test';
import fs from 'node:fs';
const runtime=JSON.parse(fs.readFileSync('../.local/pilot-tests/runtime.json','utf8'));
for(const size of [{name:'pc',width:1440,height:960},{name:'tableta',width:1024,height:768},{name:'telefono',width:390,height:844}]){
 test(size.name+' — acceso y navegación del piloto',async({page})=>{
  await page.setViewportSize(size);const errors:string[]=[];page.on('pageerror',e=>errors.push(e.message));
  await page.goto('/');await page.getByLabel('Usuario',{exact:true}).fill('persona0');await page.getByLabel('Contraseña',{exact:true}).fill(runtime.password);await page.getByRole('button',{name:'Iniciar sesión',exact:true}).click();
  await expect(page.getByRole('heading',{name:'Resumen',exact:true})).toBeVisible();
  for(const name of ['Inventario','Entradas y salidas','Historial de movimientos','Dashboard']){
   const mobile=page.getByLabel('Sección',{exact:true});
   if(await mobile.isVisible())await mobile.selectOption({label:name});else await page.getByRole('navigation').getByRole('button',{name,exact:true}).click();
   if(name==='Dashboard')await expect(page.getByRole('heading',{name:'Dashboard · Power BI',exact:true})).toBeVisible();
   else await expect(page.getByRole('heading',{name,exact:true,level:1})).toBeVisible();
   expect(await page.evaluate(()=>document.documentElement.scrollWidth<=window.innerWidth)).toBe(true);
  }
  await page.screenshot({path:'../evidencias/piloto-'+size.name+'.png',fullPage:true});expect(errors).toEqual([]);
 });
}
