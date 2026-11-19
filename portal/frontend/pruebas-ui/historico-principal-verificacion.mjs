// Prueba aislada: SQL solo lectura, contraseña administrativa temporal en memoria,
// diario temporal distinto del de demostración. No ejecuta migraciones ni atenciones.
import {chromium, expect} from '@playwright/test';
import {spawn, execFileSync} from 'node:child_process';
import {once} from 'node:events';
import fs from 'node:fs';
import os from 'node:os';
import path from 'node:path';
import net from 'node:net';
import {randomUUID} from 'node:crypto';
import {fileURLToPath} from 'node:url';

const portal=path.resolve(path.dirname(fileURLToPath(import.meta.url)), '../..');
const backend=path.join(portal,'backend/Motherson.Api');
const evidence=path.join(portal,'evidencias/historico-principal');
const captures=path.join(evidence,'capturas');
const diary=fs.mkdtempSync(path.join(os.tmpdir(),'motherson-continuacion-api-'));
const baseURL='http://127.0.0.1:5091';
const password=randomUUID()+randomUUID();
const hash=execFileSync('dotnet',[path.join(portal,'pruebas-continuacion/bin/Debug/net10.0/Continuacion.Pruebas.dll'),'--hash-temporal',password],{encoding:'utf8'}).trim();
const env={...process.env,MVP_ROOT:portal,MVP_DIARIO_PATH:diary,MVP_ADMIN_PUESTOS_HASH:hash,
 ConnectionStrings__Sql:JSON.parse(fs.readFileSync(path.join(portal,'.local/conexiones-Motherson_L1_MVP_Demo.json'),'utf8')).app,
 ASPNETCORE_URLS:baseURL,ASPNETCORE_ENVIRONMENT:'Development'};
const passed=[];
let server,browser,page,log='';
const check=(name,value)=>{if(!value)throw new Error(name);passed.push(name)};
async function freePort(){const socket=net.createServer();socket.listen(5091,'127.0.0.1');await once(socket,'listening');await new Promise(r=>socket.close(r));}
async function start(){
 log='';server=spawn('dotnet',['bin/Debug/net10.0/Motherson.Api.dll'],{cwd:backend,env,stdio:['ignore','pipe','pipe']});
 server.stdout.on('data',b=>log+=b);server.stderr.on('data',b=>log+=b);
 for(let n=0;n<100;n++){
  if(server.exitCode!==null)throw new Error('Servidor temporal terminó: '+log);
  try{const r=await fetch(baseURL+'/api/health');if(r.ok){const data=await r.json();check('API temporal conecta exclusivamente a Demo',data.baseDatos==='Motherson_L1_MVP_Demo');return}}catch{}
  await new Promise(r=>setTimeout(r,250));
 }
 throw new Error('Servidor temporal no respondió: '+log);
}
async function stop(){if(server&&server.exitCode===null){const closed=once(server,'exit');server.kill('SIGINT');await closed;}}
async function request(url,body,key=randomUUID(),csrf=true){
 const headers={};if(body!==undefined){headers['Content-Type']='application/json';headers['Idempotency-Key']=key;
  if(csrf)headers['X-XSRF-TOKEN']=(await(await page.request.get(baseURL+'/api/auth/csrf')).json()).token;
 }
 const r=body===undefined?await page.request.get(baseURL+url):await page.request.post(baseURL+url,{headers,data:body});
 return {status:r.status(),data:await r.json().catch(()=>null)};
}
const inventory=async()=>{const r=await request('/api/inventario?alarmas=true');check('Inventario disponible',r.status===200);return r.data};
const total=data=>[...new Map(data.filas.map(r=>[r.numero_parte,r.stock_total_material])).values()].filter(n=>n!=null).reduce((a,b)=>a+b,0);
async function section(name){await page.getByRole('navigation',{name:'Navegación principal'}).getByRole('button',{name,exact:true}).click();}
async function selectMaterial(part){
 await page.getByRole('searchbox').fill(part);
 await page.locator('.mov-tarjeta').first().click();
 if(await page.locator('.mov-tarjeta').count())await page.locator('.mov-tarjeta').filter({hasText:part}).first().click();
 await expect(page.getByRole('button',{name:/^Confirmar (entrada|salida)$/})).toBeVisible();
}
try{
 fs.mkdirSync(captures,{recursive:true});await freePort();await start();
 browser=await chromium.launch({executablePath:process.env.MVP_BROWSER??'/Applications/Brave Browser.app/Contents/MacOS/Brave Browser',headless:true});
 const context=await browser.newContext({viewport:{width:1440,height:1000}});page=await context.newPage();
 const errors=[];page.on('pageerror',e=>errors.push(e.message));
 check('Inventario exige autenticación',(await request('/api/inventario')).status===401);
 await page.goto(baseURL);
 await page.getByLabel('Usuario',{exact:true}).fill('admin.local');await page.getByLabel('Contraseña',{exact:true}).fill(password);
 await page.getByRole('button',{name:'Iniciar sesión'}).click();
 await expect(page.getByRole('heading',{name:'Resumen',exact:true})).toBeVisible({timeout:30000});
 const before=await inventory();check('Saldo inicial sin volver a sumar el histórico',total(before)===10435);
 await expect(page.getByTestId('existencias-conocidas')).toContainText('10,435');
 await expect(page.getByText('67 materiales con saldo conocido · 1 pendientes de saldo',{exact:true})).toBeVisible();
 check('Resumen separa existencias conocidas de saldo desconocido',before.filas.filter(r=>r.stock_total_material==null).length===1);
 check('La fecha inicial corresponde al saldo SQL',before.fuente.fechaStockInicial.startsWith('2026-11-04T18:27:48'));
 const h0=(await request('/api/historial')).data;check('Proyección original sin duplicar los 75 movimientos MVP',h0.total===230976);
 const journal=path.join(diary,'eventos.jsonl');const baseline=fs.readFileSync(journal,'utf8');
 await inventory();await request('/api/catalogo');await request('/api/historial');
 check('Consultar no crea movimientos',fs.readFileSync(journal,'utf8')===baseline);
 check('Fuera de calendario se explica ausencia de tasa',before.calendario.motivo!==null&&before.alarmas.every(a=>a.resultado.estado==='SIN_DATOS'||a.resultado.estado==='AGOTADO'));
 await page.evaluate(()=>{document.activeElement?.blur();window.scrollTo(0,0)});await page.screenshot({path:path.join(captures,'01-resumen.png'),fullPage:true});
 const target=before.filas.find(r=>r.cajas_disponibles>0&&r.cantidad!=null&&before.filas.filter(x=>x.id_estanteria===r.id_estanteria).length===1);
 check('Existe ubicación inequívoca con espacio documentado',!!target);
 await section('Entradas y salidas');await page.evaluate(()=>{document.activeElement?.blur();window.scrollTo(0,0)});await page.screenshot({path:path.join(captures,'02-bloques-entrada.png'),fullPage:true});
 await selectMaterial(target.numero_parte);
 await page.evaluate(()=>{document.activeElement?.blur();window.scrollTo(0,0)});await page.screenshot({path:path.join(captures,'03-confirmacion-entrada.png'),fullPage:true});
 let lost;
 await page.route('**/api/continuacion/movimientos',async route=>{
  const response=await route.fetch();lost={body:route.request().postDataJSON(),key:route.request().headers()['idempotency-key'],result:await response.json()};
  await route.abort('failed');
 },{times:1});
 await page.getByRole('button',{name:'Confirmar entrada',exact:true}).click();
 await expect(page.getByRole('button',{name:'Comprobar la misma operación',exact:true})).toBeVisible();
 check('Pérdida de respuesta conserva ID estable',!!lost?.result.id_movimiento);
 await page.getByRole('button',{name:'Comprobar la misma operación',exact:true}).click();
 await expect(page.locator('.mov-comprobante')).toContainText(lost.result.id_movimiento);
 check('Reintento del navegador registra una sola entrada',fs.readFileSync(journal,'utf8').trim().split('\n').length===2);
 check('Conversión de caja a piezas',lost.result.cantidad_movimiento===target.cantidad_por_caja);
 await page.evaluate(()=>{document.activeElement?.blur();window.scrollTo(0,0)});await page.screenshot({path:path.join(captures,'04-comprobante-entrada.png'),fullPage:true});
 const afterEntry=await inventory();let row=afterEntry.filas.find(r=>r.id_ubicacion===target.id_ubicacion);
 check('Inventario coincide con comprobante',row.cantidad===lost.result.saldo_posterior&&total(afterEntry)===10435+target.cantidad_por_caja);
 const twice=await request('/api/continuacion/movimientos',lost.body,lost.key);
 check('Reintento API devuelve mismo comprobante',twice.status===200&&JSON.stringify(twice.data)===JSON.stringify(lost.result));
 check('Mismo ID con otra cantidad se rechaza',(await request('/api/continuacion/movimientos',{...lost.body,cantidad:2},lost.key)).status===409);
 check('POST sin antifalsificación se rechaza',(await request('/api/continuacion/movimientos',lost.body,randomUUID(),false)).status===400);
 check('Administración no registra atenciones',(await request('/api/atenciones/iniciar',{ubicacion:target.id_ubicacion})).status===403);
 check('Cantidad cero se rechaza',(await request('/api/continuacion/movimientos',{ubicacion:row.id_ubicacion,tipo:'SALIDA',cantidad:0,version:row.version_concurrencia})).status===400);
 check('Fracciones se rechazan',(await request('/api/continuacion/movimientos',{ubicacion:row.id_ubicacion,tipo:'SALIDA',cantidad:1.5,version:row.version_concurrencia})).status===400);
 check('No se permiten más cajas de las que hay',(await request('/api/continuacion/movimientos',{ubicacion:row.id_ubicacion,tipo:'SALIDA',cantidad:Math.ceil(row.cantidad/row.cantidad_por_caja)+1,version:row.version_concurrencia})).status===409);
 check('No se permite sobrellenado',(await request('/api/continuacion/movimientos',{ubicacion:row.id_ubicacion,tipo:'ENTRADA',cantidad:row.cajas_disponibles+1,version:row.version_concurrencia})).status===409);
 await page.getByRole('button',{name:'Salida · cajas',exact:true}).click();await selectMaterial(target.numero_parte);
 await page.getByRole('checkbox',{name:'Representa consumo simulado de producción'}).check();
 await page.evaluate(()=>{document.activeElement?.blur();window.scrollTo(0,0)});await page.screenshot({path:path.join(captures,'05-confirmacion-salida.png'),fullPage:true});
 await page.getByRole('button',{name:'Confirmar salida',exact:true}).click();
 await expect(page.locator('.mov-comprobante')).toContainText('Salida de');
 const afterExit=await inventory();row=afterExit.filas.find(r=>r.id_ubicacion===target.id_ubicacion);
 check('Salida de una caja descuenta su empaque (o el resto de la caja abierta)',row.cantidad===lost.result.saldo_posterior-Math.min(target.cantidad_por_caja,lost.result.saldo_posterior));
 const newHistory=(await request('/api/historial?origen=CONTINUACION')).data;
 check('Historial integra nuevas operaciones una vez',newHistory.total===2&&new Set(newHistory.filas.map(r=>r.id_movimiento)).size===2);
 check('Hechos distinguen consumo y proceso generador',newHistory.filas.filter(r=>r.es_consumo_produccion).length===1&&newHistory.filas.every(r=>r.proceso_generador==='PORTAL_CONTINUACION_V1'));
 check('Movimiento no inventa intervalo de atención',newHistory.filas.every(r=>r.fecha_hora_inicio===null&&r.fecha_hora_fin===null));
 const exportResponse=await page.request.get(baseURL+'/api/historial/exportar?origen=CONTINUACION');
 const exported=(await exportResponse.text()).trim().split('\n').map(l=>JSON.parse(l));
 check('Exportación incluye continuación sin duplicados',exportResponse.ok()&&exported.length===2&&exported[0].id_movimiento===newHistory.filas[0].id_movimiento);
 check('Historial combinado suma únicamente operaciones nuevas',(await request('/api/historial')).data.total===230978);
 const histResponse=page.waitForResponse(r=>r.url().includes('/api/cobertura/historica'));
 await section('Resumen');await expect(page.getByTestId('existencias-conocidas')).toContainText('10,454');
 const historical=await(await histResponse).json();
 check('Cobertura histórica usa su propio espacio y calendario',historical.fuente.espacio==='HISTORICO_21'&&historical.ventana.completa&&historical.filas.length>0);
 const counts=data=>data.alarmas.reduce((acc,a)=>(acc[a.resultado.estado]=(acc[a.resultado.estado]??0)+1,acc),{});
 const firstCounts=counts(historical);
 console.log(JSON.stringify({corte1:historical.corte,estados:firstCounts}));
 fs.writeFileSync(path.join(evidence,'corte1-api.json'),JSON.stringify(historical,null,2));
 check('Primer corte muestra riesgo de paro observado',firstCounts.RIESGO_PARO>0);
 check('Primer corte contiene estimaciones válidas',Object.keys(firstCounts).some(k=>k!=='SIN_DATOS'));
 await expect(page.getByText('Histórico de solo lectura ·',{exact:false}).first()).toBeVisible();
 await page.evaluate(()=>{document.activeElement?.blur();window.scrollTo(0,0)});await page.screenshot({path:path.join(captures,'06-cobertura-historica.png'),fullPage:true});
 const secondResponse=page.waitForResponse(r=>r.url().includes('/api/cobertura/historica'),{timeout:60000});
 await page.getByLabel('Corte de demostración',{exact:true}).selectOption('2026-04-06T15:31:11');
 await page.getByRole('button',{name:'Consultar cobertura histórica',exact:true}).click({timeout:10000});
 const second=await(await secondResponse).json();
 check('Segundo corte muestra agotados observados',counts(second).AGOTADO>0);
 await expect(page.getByRole('button',{name:'Consultar cobertura histórica',exact:true})).toBeEnabled();
 await page.screenshot({path:path.join(captures,'09-corte-agotamiento.png'),fullPage:true});
 const options=(await request('/api/historial/opciones')).data;
 check('Selector contiene 12 responsables ficticios',options.responsables.length===12);
 check('Selector conserva números de parte completos',options.materiales.includes('17A 919 225 B  RDW'));
 const full=(await request('/api/historial?origen=HISTORICO_21&desde=2025-10-25&hasta=2026-10-24')).data;
 check('Maestro anual excluye aperturas y pruebas posteriores',full.total>200000&&full.total<230901&&full.filas.every(r=>r.espacio_identificadores==='HISTORICO_21'));
 const person=options.responsables[0];
 const pq=new URLSearchParams({origen:'HISTORICO_21',responsable:person.nombre_responsable,turno:String(person.id_turno),desde:'2025-10-25',hasta:'2026-10-24'});
 const personal=(await request('/api/historial?'+pq)).data;
 check('Filtro responsable y turno mantiene atribución original',personal.total>0&&personal.filas.every(r=>r.responsable_simulado===person.nombre_responsable&&r.id_turno===person.id_turno));
 const exactQ=new URLSearchParams({origen:'HISTORICO_21',parte:'17A 919 225 B  RDW',exacta:'true',desde:'2025-10-25',hasta:'2026-10-24'});
 const exact=(await request('/api/historial?'+exactQ)).data;
 check('Filtro material exacto conserva sufijos',exact.total>0&&exact.filas.every(r=>r.numero_parte==='17A 919 225 B  RDW'));
 const exportedExact=await page.request.get(baseURL+'/api/historial/exportar?'+exactQ);
 const exactLines=(await exportedExact.text()).trim().split('\n').map(JSON.parse);
 check('Exportación concilia con filtros de material y fecha',exactLines.length===exact.total&&exactLines.every(r=>r.numero_parte==='17A 919 225 B  RDW'));
 const noExact=(await request('/api/historial?origen=HISTORICO_21&exacta=true&parte=17A')).data;
 check('Consulta exacta no resuelve coincidencias parciales',noExact.total===0);
 await section('Historial de movimientos');
 await expect(page.getByLabel('Material',{exact:true})).toBeVisible();
 await expect(page.getByLabel('Responsable ficticio',{exact:true}).locator('option')).toHaveCount(13);
 await page.getByLabel('Material',{exact:true}).selectOption('17A 919 225 B  RDW');
 const filterResponse=page.waitForResponse(r=>r.url().includes('/api/historial?')&&r.url().includes('parte=17A'));
 await page.getByRole('button',{name:'Consultar movimientos',exact:true}).click();await filterResponse;await expect(page.getByRole('button',{name:'Consultar movimientos',exact:true})).toBeEnabled({timeout:60000});
 await expect(page.getByRole('region',{name:'Movimientos del histórico maestro'}).locator('tbody tr').first()).toContainText('17A 919 225 B RDW');
 await page.screenshot({path:path.join(captures,'10-historial-material.png'),fullPage:true});
 await page.getByLabel('Material',{exact:true}).selectOption('');
 await page.getByLabel('Responsable ficticio',{exact:true}).selectOption(person.nombre_responsable);
 await page.getByLabel('Turno del histórico',{exact:true}).selectOption(String(person.id_turno));
 const personResponse=page.waitForResponse(r=>r.url().includes('/api/historial?')&&r.url().includes('responsable='));
 await page.getByRole('button',{name:'Consultar movimientos',exact:true}).click();await personResponse;await expect(page.getByRole('button',{name:'Consultar movimientos',exact:true})).toBeEnabled({timeout:60000});
 await expect(page.getByRole('region',{name:'Movimientos del histórico maestro'}).locator('tbody tr').first()).toContainText(person.nombre_responsable);
 await page.screenshot({path:path.join(captures,'11-responsable-turno.png'),fullPage:true});
 await section('Entradas y salidas');await page.getByRole('button',{name:'Atenciones · inicio y fin',exact:true}).click();
 await expect(page.getByText('Aún no hay intervalos de atención registrados para esta consulta.',{exact:false})).toBeVisible();
 await page.screenshot({path:path.join(captures,'12-atenciones-explicacion.png'),fullPage:true});
 fs.writeFileSync(path.join(evidence,'cortes-y-filtros.json'),JSON.stringify({corte1:{corte:historical.corte,estados:firstCounts},corte2:{corte:second.corte,estados:counts(second)},responsables:options.responsables.length,materiales:options.materiales.length,maestroAnual:full.total,filtroMaterial:exact.total,filtroResponsable:personal.total},null,2));
 check('Consulta histórica no añade hechos',fs.readFileSync(journal,'utf8').trim().split('\n').length===3);
 await section('Inventario');await page.evaluate(()=>{document.activeElement?.blur();window.scrollTo(0,0)});await page.screenshot({path:path.join(captures,'07-inventario.png')});
 await section('Entradas y salidas');await page.setViewportSize({width:820,height:1180});
 await page.evaluate(()=>{document.activeElement?.blur();window.scrollTo(0,0)});await page.screenshot({path:path.join(captures,'08-bloques-tablet.png'),fullPage:true});
 check('Pantalla táctil sin desbordamiento horizontal',await page.evaluate(()=>document.documentElement.scrollWidth<=innerWidth));
 await page.setViewportSize({width:1440,height:1000});
 const concurrentBody={ubicacion:row.id_ubicacion,tipo:'SALIDA',cantidad:1,version:row.version_concurrencia};
 const a=randomUUID(),b=randomUUID();
 const [one,two]=await Promise.all([request('/api/continuacion/movimientos',concurrentBody,a),request('/api/continuacion/movimientos',concurrentBody,b)]);
 check('Confirmaciones simultáneas distintas no pierden saldo',[one.status,two.status].sort().join(',')==='200,409');
 row=(await inventory()).filas.find(r=>r.id_ubicacion===target.id_ubicacion);
 const sameBody={...concurrentBody,version:row.version_concurrencia},sameKey=randomUUID();
 const [same1,same2]=await Promise.all([request('/api/continuacion/movimientos',sameBody,sameKey),request('/api/continuacion/movimientos',sameBody,sameKey)]);
 check('Confirmaciones simultáneas idénticas se aplican una vez',same1.status===200&&same2.status===200&&same1.data.id_movimiento===same2.data.id_movimiento);
 const finalState=await inventory();const finalContents=fs.readFileSync(journal,'utf8');
 check('Diario no duplica saldos calculados ni contraseñas',!finalContents.includes('saldoAnterior')&&!finalContents.includes('saldoPosterior')&&!finalContents.includes(password)&&!finalContents.includes(hash));
 await stop();await start();
 const restarted=await inventory();
 check('Saldo y movimientos persisten tras reiniciar',total(restarted)===total(finalState)&&restarted.fuente.movimientosNuevos===4);
 const replay=await request('/api/continuacion/movimientos',lost.body,lost.key);
 check('ID original conserva resultado tras reiniciar',replay.status===200&&JSON.stringify(replay.data)===JSON.stringify(lost.result));
 check('Reiniciar y consultar no añade hechos',fs.readFileSync(journal,'utf8')===finalContents);
 check('Navegador sin errores JavaScript',errors.length===0);
 fs.writeFileSync(path.join(evidence,'pruebas-api-navegador.json'),JSON.stringify({base:'Motherson_L1_MVP_Demo',diario:'TEMPORAL_ELIMINADO_AL_FINAL',sql:'SOLO_LECTURA',casos:passed,conteos:{inicial:10435,proyeccionOriginal:230976,movimientosNuevos:4,saldoFinalTemporal:total(restarted)},historico:{posiciones:historical.filas.length,estados:historical.alarmas.reduce((acc,a)=>(acc[a.resultado.estado]=(acc[a.resultado.estado]??0)+1,acc),{})},erroresNavegador:errors},null,2)+'\n');
 console.log(JSON.stringify({resultado:'CORRECTO',comprobaciones:passed.length,capturas:12}));
}finally{
 await browser?.close();await stop();fs.rmSync(diary,{recursive:true,force:true});
}
