using System.Data;
using System.Diagnostics;
using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Dapper;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Primitives;
using Motherson;

var connection=Environment.GetEnvironmentVariable("MES_TEST_CONNECTION") ?? throw new Exception("Falta la conexión LOCAL de pruebas.");
var builder=new SqlConnectionStringBuilder(connection);
if(!builder.DataSource.StartsWith("127.0.0.1,",StringComparison.Ordinal)||!builder.InitialCatalog.StartsWith("MES_Integracion_Pruebas_",StringComparison.Ordinal))
 throw new Exception("Las pruebas solo aceptan una base local aislada.");
var root=Path.GetFullPath(args[0]);
await using var sql=new SqlConnection(connection);await sql.OpenAsync();
var passed=new List<string>();
void Check(bool ok,string name){if(!ok)throw new Exception(name);passed.Add(name);}
async Task Reject(Func<Task> action,int code,string name){try{await action();}catch(SqlException e)when(e.Number==code){passed.Add(name);return;}throw new Exception(name);}
Task<int> Value(string query)=>sql.ExecuteScalarAsync<int>(query);
if(args.Contains("--consultas"))
{
 var hash=new PasswordHasher<string>().HashPassword("","Prueba_Consulta_123!");
 var settings=new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string,string?>{["ConnectionStrings:Sql"]=connection,["MVP_BASE_COMPARTIDA"]=builder.InitialCatalog,["MVP_SQL_TRANSACCIONAL"]="true",["MVP_ROOT"]=Path.Combine(root,".local"),["MVP_ADMIN_PUESTOS_HASH"]=hash}).Build();
 using var store=new Store(settings);await store.DetectarModelo();await store.PrepararContinuacion();
 var actor=await store.Login(new("admin.local","Prueba_Consulta_123!"))??throw new Exception("Login de consulta falló");
 var current=JsonSerializer.SerializeToElement(await store.CoberturaHistorica(actor,DateTimeOffset.UtcNow,CancellationToken.None));
 Check(current.GetProperty("filas").GetArrayLength()>0,"Cobertura al corte consulta SQL sin maestro local");
 var opening=await sql.ExecuteScalarAsync<DateTime>("SELECT fecha_utc FROM mes.BASE_INVENTARIO WHERE id=1");
 try{await store.CoberturaHistorica(actor,new DateTimeOffset(DateTime.SpecifyKind(opening.AddSeconds(-1),DateTimeKind.Utc)),CancellationToken.None);throw new Exception("Aceptó corte no acreditado");}catch(RuleException e)when(e.Status==400){passed.Add("Corte anterior a la apertura se rechaza sin inventar saldos");}
 try{await store.CoberturaHistorica(actor,DateTimeOffset.UtcNow.AddMinutes(1),CancellationToken.None);throw new Exception("Aceptó futuro");}catch(RuleException e)when(e.Status==400){passed.Add("Corte futuro se rechaza");}
 var position=await store.Login(new("puesto1","Solo_Pruebas_Locales_123!"))??throw new Exception("Puesto no disponible");
 var scoped=JsonSerializer.SerializeToElement(await store.CoberturaHistorica(position,DateTimeOffset.UtcNow,CancellationToken.None));
 Check(scoped.GetProperty("filas").EnumerateArray().All(r=>r.GetProperty("id_puesto").GetInt32()==1),"Cobertura al corte respeta el puesto autenticado");
 Console.WriteLine(JsonSerializer.Serialize(new{resultado="CORRECTO",total=passed.Count,casos=passed}));return;
}
if(args.Contains("--legado"))
{
 var source=Path.GetFullPath(Path.Combine(root,"../../motherson-demo-2026-11-12/portal/.local"));
 var files=LegacyArchive.Read(Path.Combine(source,"historico-demo"),[
  Path.Combine(source,"continuacion-movimientos-compartida/eventos.jsonl"),Path.Combine(source,"continuacion-movimientos/eventos.jsonl")]);
 var plan=LegacyArchive.Validate(files);var expected=plan.Values.Sum();
 var stock=await Value("SELECT COALESCE(SUM(cantidad),0) FROM dbo.UBICACION");
 Check(await LegacyArchive.Import(sql,files,Console.Error.WriteLine)==(args.Contains("--reintento")?0:expected),args.Contains("--reintento")?"Reimportación completa no duplica ninguna fila":"Legado completo validado y preservado en SQL real");
 Check(await Value("SELECT COUNT(*) FROM mes.HISTORICO_LEGADO")==expected,"Conteos por archivo del legado coinciden con el plan");
 Check(await Value("SELECT COUNT(*) FROM mes.vw_historial_json")==230987,"Consulta normalizada conserva 230,976 movimientos y 11 del diario");
 Check(await Value("SELECT COALESCE(SUM(cantidad),0) FROM dbo.UBICACION")==stock,"Migración completa no vuelve a aplicar ningún saldo");
 Console.WriteLine(JsonSerializer.Serialize(new{resultado="CORRECTO",total=passed.Count,filas=expected,casos=passed}));return;
}
if(args.Contains("--demo"))
{
 await DemoData.Load(sql,Path.Combine(root,"../datos/simulados"));
 Check(await Value("SELECT COUNT(*) FROM dbo.REGISTRO")==58422,"58,422 atenciones cargadas en SQL real");
 Check(await Value("SELECT SUM(cantidad) FROM dbo.UBICACION")==10474,"10,474 piezas conciliadas");
 Check(await Value("SELECT COUNT(*) FROM dbo.MATERIAL")==85,"85 materiales cargados");
 Check(await Value("SELECT COUNT(*) FROM mes.BASE_INVENTARIO")==1,"Apertura SQL persistente sin paquete histórico");
 Check(await Value("SELECT COUNT(*) FROM dbo.vw_registro_bi")==58422,"Fuente de atenciones Power BI conserva las 58,422 filas");
 Check(await Value("SELECT COUNT(*) FROM dbo.REGISTRO WHERE fecha_hora_fin>='2026-10-19' AND fecha_hora_fin<'2026-10-26'")==1146,"Semana de control Power BI: 1,146 atenciones completadas");
 Check(await Value("SELECT SUM(cantidad) FROM dbo.vw_kpi_inventario_actual")==10474,"Vista del portal concilia con inventario de Power BI");
 try{await DemoData.Load(sql,Path.Combine(root,"../datos/simulados"));throw new Exception("Aceptó recarga");}catch(InvalidOperationException){passed.Add("Recarga se rechaza sin borrar datos");}
 Console.WriteLine(JsonSerializer.Serialize(new{resultado="CORRECTO",total=passed.Count,casos=passed}));return;
}
const string testPassword="Solo_Pruebas_Locales_123!";
var adminHash=new PasswordHasher<string>().HashPassword("",testPassword);
await sql.ExecuteAsync("""
 INSERT dbo.ESTACION(id_linea,nombre_estacion) VALUES(1,'PRUEBA');
 INSERT dbo.COLOR(nombre) VALUES('AZUL');
 INSERT dbo.PUESTO_LOGISTICO(Nombre,usuario,[password]) VALUES('P1','puesto1',@hash),('P2','puesto2',@hash);
 INSERT dbo.TURNO(nombre_turno,hora_inicio,hora_fin) VALUES('T1','06:00','15:00'),('T2','15:00','23:30'),('PRUEBA_NOCTURNA','23:30','06:00');
 INSERT dbo.ESTANTERIA(id_estacion,estanteria,nivel,cajas_estanteria) VALUES(1,'R1','A',6),(1,'R2','A',5);
 INSERT dbo.MATERIAL(numero_parte,id_puesto,cantidad_por_caja,stock,minimo,descripcion,uso) VALUES
 ('PRUEBA-A',1,10,25,5,'Pieza A','EXCLUSIVO'),('PRUEBA-B',1,10,10,5,'Pieza B','EXCLUSIVO'),('PRUEBA-C',2,10,10,5,'Pieza C','EXCLUSIVO');
 INSERT dbo.UBICACION(numero_parte,id_estanteria,cantidad) VALUES('PRUEBA-A',1,25),('PRUEBA-B',1,10),('PRUEBA-C',2,10);
 EXEC mes.FijarSaldoInicial;
 """,new{hash=adminHash});
async Task<string> Move(Guid id,int qty,int version,string type="ENTRADA",int location=1,string account="admin.local",string accountType="ADMIN_PUESTOS",int? scope=null)
{
 await using var c=new SqlConnection(connection);await c.OpenAsync();
 return await c.QuerySingleAsync<string>("mes.RegistrarMovimiento",new{operacion=id,cuenta=account,tipo_cuenta=accountType,puesto_cuenta=scope,solicitud=JsonSerializer.Serialize(new MovimientoNuevo(location,type,qty,version))},commandType:CommandType.StoredProcedure);
}
async Task<string> Adjust(Guid id,CambioCapacidad[] caps,CambioMaterial[] materials,string accountType="ADMIN_PUESTOS")=>
 await sql.QuerySingleAsync<string>("mes.AjustarCatalogo",new{operacion=id,cuenta="admin.local",tipo_cuenta=accountType,puesto_cuenta=(int?)null,solicitud=JsonSerializer.Serialize(new{Capacidades=caps,Materiales=materials})},commandType:CommandType.StoredProcedure);
Check(await Value("SELECT dbo.TurnoDeAtencion('2026-11-18T02:00:00')")==3,"Turno nocturno de prueba cruza medianoche sin inventar un turno");
await Reject(()=>Move(Guid.NewGuid(),1,1,"entrada"),51004,"Tipo de movimiento respeta el contrato en mayúsculas");
await Reject(()=>Adjust(Guid.NewGuid(),[],[new("PRUEBA-A","uso","EXCLUSIVO","exclusivo")]),51004,"Uso de material respeta los valores permitidos");
var id=Guid.NewGuid();var receipt=await Move(id,2,1);
Check(await Value("SELECT cantidad FROM dbo.UBICACION WHERE id_ubicacion=1")==45,"Entrada de dos cajas aplica 20 piezas");
Check(receipt==await Move(id,2,1),"Reintento devuelve exactamente el comprobante persistido");
Check(await Value("SELECT COUNT(*) FROM mes.MOVIMIENTO")==1,"Reintento no duplica el historial");
await Reject(()=>Move(id,1,1),51009,"UUID con contenido distinto se rechaza");
await Reject(()=>Move(Guid.NewGuid(),1,1),51009,"Versión obsoleta se rechaza");
await Reject(()=>Move(Guid.NewGuid(),1,2),51009,"Capacidad compartida evita sobrellenado");
await Reject(()=>Move(Guid.NewGuid(),6,2,"SALIDA"),51012,"Salida superior a cajas ocupadas se rechaza");
await Move(Guid.NewGuid(),1,2,"SALIDA");
Check(await Value("SELECT cantidad FROM dbo.UBICACION WHERE id_ubicacion=1")==35,"Salida de una caja descuenta 10 piezas");
await Move(Guid.NewGuid(),4,3,"SALIDA");
Check(await Value("SELECT cantidad FROM dbo.UBICACION WHERE id_ubicacion=1")==0,"Última caja incompleta descuenta solo su remanente");
await Reject(()=>Move(Guid.NewGuid(),1,4,"SALIDA"),51012,"Saldo cero no admite salida");
await Reject(()=>Move(Guid.NewGuid(),1,1,"ENTRADA",3,"puesto1","PUESTO",1),51003,"Puesto no mueve material ajeno");
await Reject(()=>Move(Guid.NewGuid(),1,4,"SALIDA",1,"puesto1","PUESTO",1),51003,"Puesto no registra salidas");
await sql.ExecuteAsync("UPDATE dbo.PUESTO_LOGISTICO SET usuario=NULL WHERE id_puesto=1");
await Reject(()=>Move(Guid.NewGuid(),1,4,"ENTRADA",1,"puesto1","PUESTO",1),51003,"Acceso revocado se rechaza dentro de SQL");
await sql.ExecuteAsync("UPDATE dbo.PUESTO_LOGISTICO SET usuario='puesto1' WHERE id_puesto=1");
async Task<bool> TryMove(Guid key,int version){try{await Move(key,1,version);return true;}catch(SqlException e)when(e.Number==51009){return false;}}
var concurrent=await Task.WhenAll(TryMove(Guid.NewGuid(),4),TryMove(Guid.NewGuid(),4));
Check(concurrent.Count(ok=>ok)==1,"Dos sesiones con la misma versión: solo una confirma");
var concurrentId=Guid.NewGuid();var duplicate=await Task.WhenAll(Move(concurrentId,1,5),Move(concurrentId,1,5));
Check(duplicate[0]==duplicate[1]&&await Value("SELECT cantidad FROM dbo.UBICACION WHERE id_ubicacion=1")==20,"Dos sesiones con el mismo UUID no duplican descuento o entrada");
await Reject(()=>Adjust(Guid.NewGuid(),[new(1,6,2)],[]),51009,"SQL rechaza capacidad menor que cajas ocupadas");
var capId=Guid.NewGuid();var cap=await Adjust(capId,[new(1,6,8)],[]);
Check(cap==await Adjust(capId,[new(1,6,8)],[]),"Ajuste de capacidad es idempotente");
await Reject(()=>Adjust(Guid.NewGuid(),[new(1,6,9)],[]),51009,"Capacidad anterior obsoleta se rechaza");
await Reject(()=>Adjust(Guid.NewGuid(),[],[new("PRUEBA-A","minimo","5","7"),new("PRUEBA-A","uso","EXCLUSIVO","OTRO")]),51004,"Importación inválida cancela el lote");
Check(await Value("SELECT minimo FROM dbo.MATERIAL WHERE numero_parte='PRUEBA-A'")==5,"Importación inválida no deja cambios parciales");
await Adjust(Guid.NewGuid(),[],[new("PRUEBA-A","minimo","5","7"),new("PRUEBA-A","cantidad_por_caja","10","20")]);
Check(await Value("SELECT cantidad FROM dbo.UBICACION WHERE id_ubicacion=1")==20,"Cambiar empaque no cambia las existencias");
await Reject(()=>Adjust(Guid.NewGuid(),[],[new("PRUEBA-A","cantidad_por_caja","20","1")]),51009,"Cambio de empaque que excede capacidad se revierte");
Check(await Value("SELECT cantidad_por_caja FROM dbo.MATERIAL WHERE numero_parte='PRUEBA-A'")==20,"Reversión conserva el empaque anterior");
await Reject(()=>Adjust(Guid.NewGuid(),[],[new("PRUEBA-A","variante",null,"INEXISTE")]),51004,"Variante inexistente no se crea ni aplica");
var before=await Value("SELECT cantidad FROM dbo.UBICACION WHERE id_ubicacion=1");
var count=await Value("SELECT COUNT(*) FROM mes.OPERACION");var versionNow=await Value("SELECT version_concurrencia FROM dbo.UBICACION WHERE id_ubicacion=1");
await sql.ExecuteAsync("CREATE TRIGGER mes.FalloPrueba ON mes.MOVIMIENTO AFTER INSERT AS THROW 51999,'Fallo local de prueba',1;");
await Reject(()=>Move(Guid.NewGuid(),1,versionNow),51999,"Fallo al escribir historial aborta la transacción");
await sql.ExecuteAsync("DROP TRIGGER mes.FalloPrueba");
Check(await Value("SELECT cantidad FROM dbo.UBICACION WHERE id_ubicacion=1")==before&&await Value("SELECT COUNT(*) FROM mes.OPERACION")==count,"Saldo e historial se revierten juntos ante fallo");
await sql.ExecuteAsync("CREATE USER portal_app WITHOUT LOGIN; CREATE USER powerbi_lector WITHOUT LOGIN;");
var permissions=File.ReadAllText(Path.Combine(root,"db/azure/020_permisos.sql")).Replace("DB_NAME() <> N'Motherson_L1'","DB_NAME() <> N'"+builder.InitialCatalog+"'");
await sql.ExecuteAsync(permissions);
async Task AsUser(string user,string statement){await using var c=new SqlConnection(connection);await c.OpenAsync();await c.ExecuteAsync("EXECUTE AS USER='"+user+"';"+statement+";REVERT;");}
await AsUser("powerbi_lector","SELECT id_puesto,Nombre,Tipo FROM dbo.PUESTO_LOGISTICO");passed.Add("Power BI puede leer columnas públicas de puestos");
await Reject(()=>AsUser("powerbi_lector","SELECT [password] FROM dbo.PUESTO_LOGISTICO"),230,"Power BI no puede leer hashes");
await Reject(()=>AsUser("portal_app","UPDATE dbo.UBICACION SET cantidad=0"),229,"Backend no puede actualizar saldos directamente");
await Reject(()=>AsUser("powerbi_lector","EXEC mes.RegistrarMovimiento @operacion=NULL,@cuenta='x',@tipo_cuenta='ADMIN_PUESTOS',@solicitud='{}'"),229,"Power BI no puede ejecutar capturas");
await Reject(()=>AsUser("portal_app","EXEC dbo.RegistrarMovimiento 1,1"),229,"Cuenta del backend no puede usar el procedimiento antiguo sin historial");
Check(await Value("SELECT COUNT(*) FROM dbo.MATERIAL m WHERE stock<>(SELECT SUM(cantidad) FROM dbo.UBICACION u WHERE u.numero_parte=m.numero_parte)")==0,"Stock concilia con la suma de ubicaciones");
Check(await Value("SELECT COUNT(*) FROM mes.MOVIMIENTO WHERE saldo_posterior<>saldo_anterior+CASE WHEN tipo='ENTRADA' THEN piezas ELSE -piezas END")==0,"Cada movimiento conserva sus saldos y cantidad");
var temp=Path.Combine(root,".local","http-pruebas-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(temp);
var config=new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string,string?>{
 ["ConnectionStrings:Sql"]=connection,["MVP_BASE_COMPARTIDA"]=builder.InitialCatalog,["MVP_SQL_TRANSACCIONAL"]="true",["MVP_ROOT"]=temp,["MVP_ADMIN_PUESTOS_HASH"]=adminHash}).Build();
using(var store=new Store(config))
{
 await store.DetectarModelo();await store.PrepararContinuacion();
 Check(store.ContinuacionHabilitada&&!Directory.Exists(Path.Combine(temp,".local","historico-demo")),"Backend arranca sin paquete histórico ni diario JSONL");
 var actor=await store.Login(new("admin.local",testPassword))??throw new Exception("Login falló");
 var response=JsonSerializer.SerializeToElement(await store.Catalog(actor));
 Check(response.GetProperty("continuacion_habilitada").GetBoolean(),"Catálogo habilita capturas SQL");
 var inventory=await store.FilasPuestos(actor);versionNow=inventory.Single(r=>r.id_ubicacion==1).version_concurrencia!.Value;
 await store.GuardarMovimiento(actor,new(1,"ENTRADA",1,versionNow),Guid.NewGuid());
 var now=TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow,TimeZoneInfo.FindSystemTimeZoneById("America/Mexico_City"));
 var query=new QueryCollection(new Dictionary<string,StringValues>{["corte"]=now.ToString("yyyy-MM-ddTHH:mm:ss.fffffff"),["desde"]=now.ToString("yyyy-MM-dd"),["hasta"]=now.ToString("yyyy-MM-dd")});
 var kpis=JsonSerializer.SerializeToElement(await store.ConsultarKpis(actor,query,CancellationToken.None));
 Check(kpis.GetProperty("estadoActual").GetProperty("inventario").GetInt32()<=await Value("SELECT SUM(cantidad) FROM dbo.UBICACION"),"Indicadores consultan inventario SQL sin maestro ficticio obligatorio");
}
using(var restarted=new Store(config))
{
 await restarted.DetectarModelo();await restarted.PrepararContinuacion();
 var actor=await restarted.Login(new("admin.local",testPassword))??throw new Exception("Login falló");
 Check((await restarted.FilasPuestos(actor)).Single(r=>r.id_ubicacion==1).cantidad==await Value("SELECT cantidad FROM dbo.UBICACION WHERE id_ubicacion=1"),"Reinicio del backend conserva saldo e historial en SQL");
}
// Migración de legado: conserva evidencia, es repetible y no vuelve a aplicar saldos.
var legacyRows=new[]{new ArchivoLegado("prueba-preservacion.jsonl",new string('A',64),()=>["{\"evidencia\":\"PRUEBA_LOCAL\"}"],()=>{})};
var previousStock=await Value("SELECT SUM(cantidad) FROM dbo.UBICACION");
Check(await LegacyArchive.Import(sql,legacyRows)==1,"Migración conserva evidencia de legado en SQL");
Check(await LegacyArchive.Import(sql,legacyRows)==0,"Repetir la migración no duplica evidencia");
Check(await Value("SELECT SUM(cantidad) FROM dbo.UBICACION")==previousStock,"Importar historia no vuelve a aplicar saldos");

// Interrupción de un archivo: el lote previo se conserva, pero el archivo no se publica.
var calls=0;var interrupt=true;
IEnumerable<string> BatchRows()
{
 calls++;
 for(var i=0;i<1100;i++)
 {
  if(calls==2&&interrupt&&i==1050)throw new IOException("Interrupción local de prueba");
  yield return JsonSerializer.Serialize(new{id_movimiento="PRUEBA_LOTE_"+i,espacio_identificadores="HISTORICO_21",numero_parte="PRUEBA-A",fecha_movimiento="2000-01-01T00:00:00Z",tipo_movimiento="PRUEBA",cantidad_piezas=0,id_ubicacion=1});
 }
}
var interruptedFile=new[]{new ArchivoLegado("movimientos-diario.jsonl",new string('B',64),BatchRows,()=>{})};
try{await LegacyArchive.Import(sql,interruptedFile);throw new Exception("No interrumpió el archivo");}catch(IOException){passed.Add("Importación interrumpida no marca el archivo como completo");}
Check(await Value("SELECT COUNT(*) FROM mes.HISTORICO_LEGADO WHERE sha256=REPLICATE('B',64)")==1000,"La importación conserva el lote confirmado para reanudar");
Check(await Value("SELECT COUNT(*) FROM mes.vw_historial_json WHERE id_movimiento LIKE 'PRUEBA_LOTE_%'")==0,"El historial oculta archivos incompletos");
interrupt=false;Check(await LegacyArchive.Import(sql,interruptedFile)==100,"Reanudar incorpora únicamente las filas restantes");
Check(await Value("SELECT COUNT(*) FROM mes.vw_historial_json WHERE id_movimiento LIKE 'PRUEBA_LOTE_%'")==1100,"El archivo aparece solo después de verificar el conteo completo");
// Servidor HTTP real con una cuenta local de aplicación restringida.
var login="mes_test_"+Guid.NewGuid().ToString("N");var loginPassword="Aa!"+Guid.NewGuid().ToString("N");
var masterBuilder=new SqlConnectionStringBuilder(connection){InitialCatalog="master"};
await using(var master=new SqlConnection(masterBuilder.ConnectionString))
{await master.OpenAsync();await master.ExecuteAsync("CREATE LOGIN ["+login+"] WITH PASSWORD='"+loginPassword+"';");}
await sql.ExecuteAsync("CREATE USER mes_http FOR LOGIN ["+login+"]; ALTER ROLE mes_portal ADD MEMBER mes_http;");
var appBuilder=new SqlConnectionStringBuilder(connection){UserID=login,Password=loginPassword};
var listener=new System.Net.Sockets.TcpListener(IPAddress.Loopback,0);listener.Start();var port=((IPEndPoint)listener.LocalEndpoint).Port;listener.Stop();
var address="http://127.0.0.1:"+port;
var logs=new System.Collections.Concurrent.ConcurrentQueue<string>();
Process StartServer()
{
 var start=new ProcessStartInfo(Path.Combine(root,".local/dotnet/dotnet")){WorkingDirectory=Path.Combine(root,"backend/Motherson.Api"),RedirectStandardOutput=true,RedirectStandardError=true,UseShellExecute=false};
 start.ArgumentList.Add(Path.Combine(root,"backend/Motherson.Api/bin/Debug/net10.0/Motherson.Api.dll"));start.ArgumentList.Add("--urls");start.ArgumentList.Add(address);
 start.Environment["ConnectionStrings__Sql"]=appBuilder.ConnectionString;
 start.Environment["MVP_BASE_COMPARTIDA"]=builder.InitialCatalog;start.Environment["MVP_SQL_TRANSACCIONAL"]="true";
 start.Environment["MVP_ROOT"]=temp;start.Environment["MVP_KEYS_PATH"]=Path.Combine(temp,"keys");start.Environment["MVP_ADMIN_PUESTOS_HASH"]=adminHash;
 start.Environment["ASPNETCORE_ENVIRONMENT"]="Development";start.Environment.Remove("MVP_AZURE_SERVER");
 var process=Process.Start(start)!;process.OutputDataReceived+=(_,e)=>{if(e.Data is{}line)logs.Enqueue(line);};process.ErrorDataReceived+=(_,e)=>{if(e.Data is{}line)logs.Enqueue(line);};process.BeginOutputReadLine();process.BeginErrorReadLine();return process;
}
async Task WaitServer(Process process)
{
 using var ping=new HttpClient();
 for(var i=0;i<100;i++){if(process.HasExited)throw new Exception("Servidor detenido: "+string.Join("\n",logs));try{var response=await ping.GetAsync(address+"/api/health");if(response.IsSuccessStatusCode)return;}catch(HttpRequestException){}await Task.Delay(200);}
 throw new Exception("Servidor no disponible: "+string.Join("\n",logs));
}
HttpClient Client()=>new(new HttpClientHandler{CookieContainer=new CookieContainer()}){BaseAddress=new Uri(address)};
async Task Csrf(HttpClient client)
{
 var result=await client.GetFromJsonAsync<JsonElement>("/api/auth/csrf");client.DefaultRequestHeaders.Remove("X-XSRF-TOKEN");client.DefaultRequestHeaders.Add("X-XSRF-TOKEN",result.GetProperty("token").GetString());
}
async Task SignIn(HttpClient client,string username)
{await Csrf(client);var response=await client.PostAsJsonAsync("/api/auth/login",new{username,password=testPassword});Check(response.IsSuccessStatusCode,"HTTP autentica "+username);await Csrf(client);}
async Task<HttpResponseMessage> PostMove(HttpClient client,Guid key,int version,string type="ENTRADA")
{
 var request=new HttpRequestMessage(HttpMethod.Post,"/api/continuacion/movimientos"){Content=JsonContent.Create(new{ubicacion=1,tipo=type,cantidad=1,version,esConsumo=false})};request.Headers.Add("Idempotency-Key",key.ToString());return await client.SendAsync(request);
}
var server=StartServer();
try
{
 await WaitServer(server);using var admin=Client();await SignIn(admin,"admin.local");
 Check((await admin.GetAsync("/api/catalogo")).IsSuccessStatusCode,"HTTP sirve catálogo con cuenta SQL restringida");
 var inv=await admin.GetFromJsonAsync<JsonElement>("/api/inventario?alarmas=true");
 var currentVersion=inv.GetProperty("filas").EnumerateArray().Single(r=>r.GetProperty("id_ubicacion").GetInt32()==1).GetProperty("version_concurrencia").GetInt32();
 var key=Guid.NewGuid();var first=await PostMove(admin,key,currentVersion);Check(first.IsSuccessStatusCode,"HTTP confirma una entrada con su sesión y token");
 var firstReceipt=await first.Content.ReadAsStringAsync();var repeated=await PostMove(admin,key,currentVersion);
 Check(repeated.IsSuccessStatusCode&&firstReceipt==await repeated.Content.ReadAsStringAsync(),"HTTP repite UUID sin duplicar el movimiento");
 admin.DefaultRequestHeaders.Remove("X-XSRF-TOKEN");Check((await PostMove(admin,Guid.NewGuid(),currentVersion+1)).StatusCode==HttpStatusCode.BadRequest,"HTTP rechaza captura sin token antifalsificación");await Csrf(admin);
 using var position=Client();await SignIn(position,"puesto1");
 Check((await PostMove(position,Guid.NewGuid(),currentVersion+1,"SALIDA")).StatusCode==HttpStatusCode.Forbidden,"HTTP impide salidas a la cuenta de puesto");
 var capRequest=new HttpRequestMessage(HttpMethod.Post,"/api/catalogo/capacidad"){Content=JsonContent.Create(new{estanteria=1,anterior=8,nueva=9})};capRequest.Headers.Add("Idempotency-Key",Guid.NewGuid().ToString());
 Check((await position.SendAsync(capRequest)).StatusCode==HttpStatusCode.Forbidden,"HTTP impide ajustes de capacidad a un puesto");
 var opened=await position.PostAsJsonAsync("/api/atenciones/iniciar",new{ubicacion=1});Check(opened.IsSuccessStatusCode,"HTTP inicia una atención del puesto");
 var attention=(await opened.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id_registro").GetInt32();
 var reopened=await position.PostAsJsonAsync("/api/atenciones/iniciar",new{ubicacion=1});
 Check((await reopened.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id_registro").GetInt32()==attention,"Repetir inicio no duplica una atención abierta");
 Check((await position.PostAsJsonAsync("/api/atenciones/finalizar",new{registro=attention})).IsSuccessStatusCode,"HTTP finaliza la atención y conserva su intervalo");
 Check((await position.GetAsync("/api/historial")).StatusCode==HttpStatusCode.Forbidden,"HTTP restringe el historial administrativo");
 Check((await position.GetAsync("/api/materiales/imagen?parte=PRUEBA-C")).StatusCode==HttpStatusCode.NotFound,"HTTP impide ver imágenes de material ajeno");
 var image=Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+jRZkAAAAASUVORK5CYII=");
 Check((await admin.PostAsync("/api/materiales/imagen?parte=PRUEBA-A",new ByteArrayContent(image))).IsSuccessStatusCode,"HTTP guarda imagen autorizada");
 Check((await admin.GetByteArrayAsync("/api/materiales/imagen?parte=PRUEBA-A")).SequenceEqual(image),"HTTP recupera la imagen guardada");
 Check((await admin.GetAsync("/api/historial/opciones")).IsSuccessStatusCode,"HTTP ofrece filtros del historial sin archivos maestros");
 var savedStock=await Value("SELECT cantidad FROM dbo.UBICACION WHERE id_ubicacion=1");
 server.Kill(true);await server.WaitForExitAsync();server.Dispose();server=StartServer();await WaitServer(server);
 Check((await admin.GetAsync("/api/auth/me")).IsSuccessStatusCode,"Reinicio HTTP conserva la sesión mediante claves persistentes");
 Check((await admin.GetByteArrayAsync("/api/materiales/imagen?parte=PRUEBA-A")).SequenceEqual(image),"Reinicio HTTP conserva imágenes privadas");
 Check(await Value("SELECT cantidad FROM dbo.UBICACION WHERE id_ubicacion=1")==savedStock,"Reinicio HTTP conserva saldos confirmados");
 Check((await admin.GetAsync("/api/historial")).IsSuccessStatusCode,"Reinicio HTTP conserva consulta del historial SQL");
}
finally
{
 if(!server.HasExited){server.Kill(true);await server.WaitForExitAsync();}server.Dispose();
 await using var master=new SqlConnection(masterBuilder.ConnectionString);await master.OpenAsync();await master.ExecuteAsync("DROP LOGIN ["+login+"];");
}
Console.WriteLine(JsonSerializer.Serialize(new{resultado="CORRECTO",total=passed.Count,casos=passed}));
