using Motherson;
using Dapper;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Primitives;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
var root=Path.GetFullPath(args[0]);var checks=new List<string>();
void Check(bool ok,string label){if(!ok)throw new Exception(label);checks.Add(label);Console.WriteLine("OK "+label);}
var adminCs=Environment.GetEnvironmentVariable("MES_V2_ADMIN_SQL")!;var appCs=Environment.GetEnvironmentVariable("MES_V2_APP_SQL")!;
if(!new SqlConnectionStringBuilder(adminCs).DataSource.StartsWith("127.0.0.1,"))throw new Exception("Solo SQL local");
await using var db=new SqlConnection(adminCs);await db.OpenAsync();
var pw=Environment.GetEnvironmentVariable("MES_V2_PASSWORD")!;
if(await db.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM sys.tables WHERE is_ms_shipped=0")==0){
 foreach(var file in new[]{"010_tablas.sql","011_movimientos.sql","012_objetos_portal.sql","013_indices_bi.sql","015_diario_sql.sql","016_operaciones_transaccionales.sql","017_vistas_y_apertura.sql"})
  foreach(var ddl in Regex.Split(File.ReadAllText(root+"/portal/db/azure/"+file),@"^\s*GO\s*$",RegexOptions.Multiline|RegexOptions.IgnoreCase))if(!string.IsNullOrWhiteSpace(ddl))await db.ExecuteAsync(ddl);
 foreach(var ddl in Regex.Split(File.ReadAllText(root+"/despliegue/piloto/cuentas.sql"),@"^\s*GO\s*$",RegexOptions.Multiline|RegexOptions.IgnoreCase))if(!string.IsNullOrWhiteSpace(ddl))await db.ExecuteAsync(ddl);
 await DemoData.Load(db,root+"/datos/simulados");
 var hash=new Microsoft.AspNetCore.Identity.PasswordHasher<string>().HashPassword("",pw);
 foreach(var i in new[]{0,1,79})await db.ExecuteAsync("INSERT mes.USUARIO_PILOTO(usuario,nombre,rol,id_puesto,password_hash) VALUES(@name,@name,@rol,@puesto,@hash)",new{name="persona"+i,rol=i==0?"ADMIN":i==79?"LECTOR":"PUESTO",puesto=i==1?(int?)1:null,hash});
 var appPassword=new SqlConnectionStringBuilder(appCs).Password;
 await db.ExecuteAsync("IF USER_ID('portal_app') IS NULL CREATE USER portal_app WITH PASSWORD='"+appPassword.Replace("'","''")+"';IF USER_ID('powerbi_lector') IS NULL CREATE USER powerbi_lector WITHOUT LOGIN;");
}
await db.ExecuteAsync(File.ReadAllText(root+"/portal/db/azure/020_permisos.sql").Replace("N'Motherson_L1'","N'"+new SqlConnectionStringBuilder(adminCs).InitialCatalog+"'"));
var settings=JsonSerializer.Deserialize<Dictionary<string,string?>>(File.ReadAllText(root+"/portal/.local/v2/config.json"))!;
IConfiguration Cfg(bool admin=false){var d=new Dictionary<string,string?>(settings);d["ConnectionStrings:Sql"]=admin?adminCs:appCs;return new ConfigurationBuilder().AddInMemoryCollection(d).Build();}
using var store=new Store(Cfg());await store.DetectarModelo();await store.PrepararContinuacion();
var a=(await store.Login(new("persona0",pw)))!;var p=(await store.Login(new("persona1",pw)))!;var reader=(await store.Login(new("persona79",pw)))!;
await db.ExecuteAsync("INSERT dbo.REGISTRO(id_puesto,id_ubicacion,id_turno,fecha_hora_inicio,fecha_hora_fin) SELECT TOP 1 m.id_puesto,u.id_ubicacion,(SELECT MIN(id_turno) FROM dbo.TURNO),DATEADD(minute,-30,CONVERT(datetime2,SYSUTCDATETIME() AT TIME ZONE 'UTC' AT TIME ZONE 'Central Standard Time (Mexico)')),CONVERT(datetime2,SYSUTCDATETIME() AT TIME ZONE 'UTC' AT TIME ZONE 'Central Standard Time (Mexico)') FROM dbo.UBICACION u JOIN dbo.MATERIAL m ON m.numero_parte=u.numero_parte");
var total=await db.ExecuteScalarAsync<long>("SELECT SUM(CONVERT(bigint,cantidad)) FROM dbo.UBICACION");
await AdminV2Migration.Ejecutar(Cfg(true),root,false);await AdminV2Migration.Ejecutar(Cfg(true),root,true);await AdminV2Migration.Ejecutar(Cfg(true),root,true);
var api=new AdminV2(store);var state=await api.Leer(a,1);Check(state.Todos("ubicacion").Sum(u=>(long)AdminV2.N(u,"cantidad"))==total,"Apertura concilia saldo vigente, activación idempotente");
Check((await api.Leer(a,2)).Datos.Count==0&&(await api.Leer(a,3)).Datos.Count==0&&(await api.Leer(a,4)).Datos.Count==0,"Líneas 2, 3 y 4 vacías");
async Task Rechaza(Func<Task> action,string label){try{await action();}catch(RuleException){Check(true,label);return;}catch(SqlException ex)when(ex.Number is 51003 or 51004 or 51009){Check(true,label);return;}throw new Exception("Aceptó: "+label);}
await Rechaza(async()=>{await api.Leer(p,2);},"Operador no consulta otra línea");
await Rechaza(async()=>{await api.Ejecutar(reader,1,"PUESTO",new(){["version"]=state.Version,["nombre"]="ILEGAL"},Guid.NewGuid());},"Lector no modifica catálogo");
await using(var app=new SqlConnection(appCs)){await app.OpenAsync();Check(await app.ExecuteScalarAsync<int>("SELECT HAS_PERMS_BY_NAME('mes2.ENTIDAD','OBJECT','UPDATE')")==0,"Aplicación sin actualización SQL directa");}
await Rechaza(async()=>{await db.ExecuteAsync("UPDATE dbo.UBICACION SET cantidad=cantidad WHERE id_ubicacion=(SELECT MIN(id_ubicacion) FROM dbo.UBICACION)");},"Binario anterior no puede escribir saldos tras activación");
async Task<JsonElement> Run(int line,string type,JsonObject input,Guid? id=null){input["version"]=(await api.Leer(a,line)).Version;return await api.Ejecutar(a,line,type,input,id??Guid.NewGuid());}
var puesto=(await Run(2,"PUESTO",new(){["nombre"]="SZ NUEVO"})).GetProperty("id").GetString()!;
var station=(await Run(2,"ESTACION",new(){["nombre"]="ESTACION NUEVA"})).GetProperty("id").GetString()!;
var rack=(await Run(2,"ESTANTERIA",new(){["nombre"]="E01",["nivel"]="A",["estacion"]=station,["capacidad"]=2})).GetProperty("id").GetString()!;
JsonObject Mat(string code)=>new(){["numeroParte"]=code,["puesto"]=puesto,["empaque"]=10,["minimo"]=5,["variante"]="NUEVA 2026"};
var batch=new JsonObject{["version"]=(await api.Leer(a,2)).Version,["permitirAltas"]=true,["autorizarNomenclaturas"]=true,["filas"]=new JsonArray(Mat("000123-CE"),Mat("000123-CF"))};var key=Guid.NewGuid();await api.Ejecutar(a,2,"IMPORTAR",batch,key);var again=await api.Ejecutar(a,2,"IMPORTAR",batch,key);Check((await api.Leer(a,2)).Todos("material").Length==2,"Importación nueva variante, ceros y sufijos; reintento sin duplicado");
var bad=new JsonObject{["permitirAltas"]=true,["autorizarNomenclaturas"]=true,["filas"]=new JsonArray(Mat("VALIDO"),new JsonObject{["numeroParte"]="INVALIDO",["puesto"]=puesto,["empaque"]=-1})};await Rechaza(async()=>{await Run(2,"IMPORTAR",bad);},"Lote inválido rechazado");Check((await api.Leer(a,2)).Todos("material").Length==2,"Lote inválido no deja cambios parciales");
var material=(await api.Leer(a,2)).Todos("material").Single(m=>AdminV2.S(m,"numeroParte")=="000123-CE");var mid=AdminV2.S(material,"id");
var location=(await Run(2,"UBICACION",new(){["material"]=mid,["estanteria"]=rack})).GetProperty("id").GetString()!;
await Run(2,"ENTRADA",new(){["ubicacion"]=location,["cajas"]=1});
var version=(await api.Leer(a,2)).Version;
async Task<bool> Concurrent(){try{await api.Ejecutar(a,2,"ENTRADA",new(){["version"]=version,["ubicacion"]=location,["cajas"]=1},Guid.NewGuid());return true;}catch(RuleException ex)when(ex.Status==409){return false;}catch(SqlException ex)when(ex.Number==51009){return false;}}
var captures=await Task.WhenAll(Concurrent(),Concurrent());Check(captures.Count(b=>b)==1&&AdminV2.N((await api.Leer(a,2)).Obtener("ubicacion",location),"cantidad")==20,"Dos capturas simultáneas: una confirmación, saldo exacto y capacidad respetada");
await Run(2,"MATERIAL_ESTADO",new(){["id"]=mid,["activo"]=false,["motivo"]="Retirar catálogo"});
await Rechaza(async()=>{await Run(2,"ENTRADA",new(){["ubicacion"]=location,["cajas"]=1});},"Material inactivo rechaza entrada");
await Run(2,"SALIDA",new(){["ubicacion"]=location,["cajas"]=1});Check(AdminV2.N((await api.Leer(a,2)).Obtener("ubicacion",location),"cantidad")==10,"Material inactivo permite retirar remanente autorizado");
await Rechaza(async()=>{await Run(2,"MATERIAL_ELIMINAR",new(){["id"]=mid,["motivo"]="Intento",["confirmar"]=true});},"No se elimina material con dependencias");
await Run(2,"MATERIAL_ESTADO",new(){["id"]=mid,["activo"]=true,["motivo"]="Reactivación"});
var start=(await Run(2,"ATENCION_INICIAR",new(){["ubicacion"]=location})).GetProperty("id").GetString()!;
await Run(2,"ATENCION_FINALIZAR",new(){["id"]=start});
var today=TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow,TimeZoneInfo.FindSystemTimeZoneById("America/Mexico_City")).ToString("yyyy-MM-dd");var query=new QueryCollection(new Dictionary<string,StringValues>{{"desde",today},{"hasta",today}});
JsonElement J(object o)=>JsonSerializer.SerializeToElement(o,new JsonSerializerOptions{PropertyNamingPolicy=JsonNamingPolicy.CamelCase});
Check((await api.Leer(a,1)).Todos("atencion").Length==0,"Atenciones cerradas del legado no se cargan en el estado actual");Check(await db.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM mes2.vw_atenciones_bi WHERE linea=1")==await db.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM dbo.REGISTRO WHERE fecha_hora_fin IS NOT NULL"),"Vista BI conserva el legado cerrado sin copiarlo al estado actual");var legacyKpi=J(await api.Kpis(a,1,query));Check(legacyKpi.GetProperty("actividad").GetProperty("completados").GetInt32()==1,"Hoja 2 conserva actividad cerrada del legado mediante consulta por periodo");
var parteConAtencion=await db.ExecuteScalarAsync<string>("SELECT TOP 1 u.numero_parte FROM dbo.REGISTRO r JOIN dbo.UBICACION u ON u.id_ubicacion=r.id_ubicacion ORDER BY r.id_registro DESC");var legacyMat=(await api.Leer(a,1)).Todos("material").Single(m=>AdminV2.S(m,"numeroParteLegado")==parteConAtencion);var originalPart=AdminV2.S(legacyMat,"numeroParte");await Run(1,"MATERIAL_RENOMBRAR",new(){["id"]=AdminV2.S(legacyMat,"id"),["numeroParte"]=originalPart+"-PRUEBA",["motivo"]="Verificar identidad histórica"});Check(J(await api.Kpis(a,1,query)).GetProperty("actividad").GetProperty("completados").GetInt32()==1,"Renombrar material conserva la relación con atenciones históricas");
var kpi=J(await api.Kpis(a,2,query));Check(kpi.GetProperty("actividad").GetProperty("completados").GetInt32()==1&&kpi.GetProperty("actividad").GetProperty("enCurso").GetInt32()==0,"Inicio y cierre persistidos actualizan Hoja 2");
Check(kpi.GetProperty("actividad").GetProperty("intervaloPromedioHoras").ValueKind==JsonValueKind.Null,"Un único inicio da promedio N/D");
var kpiEmpty=J(await api.Kpis(a,3,query));Check(kpiEmpty.GetProperty("actividad").GetProperty("completados").GetInt32()==0,"Línea sin actividad conserva cero real");
Check((await api.Leer(a,1)).Todos("ubicacion").Sum(u=>(long)AdminV2.N(u,"cantidad"))==total,"Cambios de Línea 2 no modifican Línea 1");
using(var restarted=new Store(Cfg())){var reloaded=await new AdminV2(restarted).Leer(a,2);Check(reloaded.Todos("atencion").Length==1&&AdminV2.N(reloaded.Obtener("ubicacion",location),"cantidad")==10,"Nueva instancia recupera estado y atenciones SQL");}
await Run(2,"MATERIAL_RENOMBRAR",new(){["id"]=mid,["numeroParte"]="000123-CE-REV2",["motivo"]="Cambio explícito"});Check(AdminV2.S((await api.Leer(a,2)).Obtener("ubicacion",location),"material")==mid,"Renombrar conserva clave estable y relaciones");
var unused=(await api.Leer(a,2)).Todos("material").Single(m=>AdminV2.S(m,"numeroParte")=="000123-CF");await Run(2,"MATERIAL_ELIMINAR",new(){["id"]=AdminV2.S(unused,"id"),["motivo"]="Sin uso",["confirmar"]=true});Check((await api.Leer(a,2)).Todos("material").Length==1,"Eliminación física solo de material sin uso");
var cal=JsonNode.Parse("""{"desde":"2026-01-01","hasta":"2026-12-31","dias":[0,1,2,3,4,5,6],"turnos":[{"inicio":"22:00","fin":"06:00"}],"fuente":"Calendario controlado de pruebas","normal":60,"advertencia":30,"critico":15}""")!.AsObject();CalendarioV2.Validar(cal);var window=CalendarioV2.Ventana(cal,new DateTime(2026,11,22,9,0,0,DateTimeKind.Utc));Check(window.Completa&&window.Minutos==60,"Turno nocturno: ventana exacta 60 minutos laborables");
Check(CalendarioV2.Ventana(null,DateTime.UtcNow).Motivo=="CALENDARIO_NO_DISPONIBLE","Sin calendario se informa causa concreta");
foreach(var (minutos,esperado) in new[]{(14.999m,"RIESGO_PARO"),(15m,"CRITICO"),(29.999m,"CRITICO"),(30m,"ADVERTENCIA"),(59.999m,"ADVERTENCIA"),(60m,"NORMAL")})Check(CalendarioV2.Clasificar(minutos,cal)==esperado,"Semáforo configurable frontera "+minutos);
var calCustom=(JsonObject)cal.DeepClone();calCustom["normal"]=120;calCustom["advertencia"]=50;calCustom["critico"]=20;Check(CalendarioV2.Clasificar(50,calCustom)=="ADVERTENCIA"&&CalendarioV2.Clasificar(120,calCustom)=="NORMAL","Umbrales personalizados se aplican sin redondear");
var imagen=new DefaultHttpContext();var bytes=Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+aFfcAAAAASUVORK5CYII=");imagen.Request.ContentLength=bytes.Length;imagen.Request.Body=new MemoryStream(bytes);var imageVersion=(await api.Leer(a,2)).Version;await api.Foto(a,2,mid,imagen.Request,false);Check((await api.Leer(a,2)).Version==imageVersion+1,"Foto persistida incrementa versión para excluir carrera con eliminación");
var invalidCal=(JsonObject)cal.DeepClone();invalidCal["normal"]="texto";try{CalendarioV2.Validar(invalidCal);throw new Exception("Umbral inválido aceptado");}catch(RuleException){Check(true,"Calendario rechaza umbral no numérico");}
await Rechaza(async()=>{await api.Estado(a,2,DateTime.UtcNow.AddDays(1));},"No se ofrece un corte histórico futuro");
var historic=(await api.Leer(a,2)).Ahora;await Run(2,"SALIDA",new(){["ubicacion"]=location,["cajas"]=1});Check(AdminV2.N((await api.Leer(a,2,historic)).Obtener("ubicacion",location),"cantidad")==10&&AdminV2.N((await api.Leer(a,2)).Obtener("ubicacion",location),"cantidad")==0,"Corte histórico conserva saldo previo y no altera actual");
await Run(2,"CALENDARIO",cal);Check((await api.Leer(a,2)).Todos("calendario").Length==1,"Calendario validado y persistido por línea");
var revoked=await db.ExecuteAsync("UPDATE mes.USUARIO_PILOTO SET activo=0 WHERE id_usuario=@id",new{id=p.Id});await Rechaza(async()=>{await api.Leer(p,1);},"Cuenta revocada no lee con sesión anterior");await db.ExecuteAsync("UPDATE mes.USUARIO_PILOTO SET activo=1 WHERE id_usuario=@id",new{id=p.Id});
Directory.CreateDirectory(root+"/documentacion/evidencia-integracion/administracion-v2");File.WriteAllText(root+"/documentacion/evidencia-integracion/administracion-v2/sql.json",JsonSerializer.Serialize(new{alcance="SQL Server 2022 local aislado; datos controlados, no Azure",checks},new JsonSerializerOptions{WriteIndented=true}));
Console.WriteLine(checks.Count+" comprobaciones aprobadas.");
