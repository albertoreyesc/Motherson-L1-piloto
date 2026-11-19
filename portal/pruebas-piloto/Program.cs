using Motherson;
using Dapper;
using Microsoft.Data.SqlClient;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.FileProviders;
var root=Path.GetFullPath(args[0]);var passed=new List<string>();
void Check(bool ok,string name){if(!ok)throw new Exception(name);passed.Add(name);Console.WriteLine("OK "+name);}
var settings=new Dictionary<string,string?>{
 ["Pilot:Enabled"]="true",["MVP_AZURE_SERVER"]="motherson-piloto-verificacion.database.windows.net",
 ["MVP_BASE_COMPARTIDA"]="Motherson_L1",["MVP_SQL_TRANSACCIONAL"]="true",
 ["ConnectionStrings:Sql"]="Server=motherson-piloto-verificacion.database.windows.net;Database=Motherson_L1;Encrypt=True;TrustServerCertificate=False",
 ["MVP_ROOT"]=root+"/portal/.local/pilot-tests/data",["MVP_KEYS_PATH"]=root+"/portal/.local/pilot-tests/keys",["MVP_IMAGES_PATH"]=root+"/portal/.local/pilot-tests/images"};
IConfiguration Config()=>new ConfigurationBuilder().AddInMemoryCollection(settings).Build();
var env=new HostEnv{EnvironmentName="Production",ContentRootPath=root+"/portal/backend/Motherson.Api"};
PilotHosting.Validate(Config(),env);Check(true,"Perfil aislado de producción válido");
void Rejected(string key,string? value,string name){var old=settings.GetValueOrDefault(key);settings[key]=value;try{PilotHosting.Validate(Config(),env);throw new Exception("Configuración indebida aceptada: "+name);}catch(InvalidOperationException){Check(true,name);}finally{settings[key]=old;}}
Rejected("MVP_AZURE_SERVER",AzurePreflight.Server,"Servidor existente rechazado");
Rejected("PowerBI:AllowTrialTokens","true","Tokens de prueba rechazados en Production");
Rejected("MVP_IMAGES_PATH",env.ContentRootPath+"/wwwroot/fotos","Imágenes privadas fuera del directorio público");
Rejected("MVP_KEYS_PATH","keys","Rutas privadas relativas rechazadas");
Rejected("MVP_ADMIN_PUESTOS_HASH","compartido","Administrador compartido rechazado");
var adminConnection=Environment.GetEnvironmentVariable("PILOT_TEST_SQL")??throw new Exception("Falta conexión local de prueba");
var target=new SqlConnectionStringBuilder(adminConnection);Check(target.DataSource.StartsWith("127.0.0.1,")&&target.InitialCatalog=="Motherson_L1","Suite limitada a SQL local aislado");
await using var sql=new SqlConnection(adminConnection);await sql.OpenAsync();
Check(await sql.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM sys.tables WHERE is_ms_shipped=0")==0,"Base local inicialmente vacía");
foreach(var file in new[]{"010_tablas.sql","011_movimientos.sql","012_objetos_portal.sql","013_indices_bi.sql","015_diario_sql.sql","016_operaciones_transaccionales.sql","017_vistas_y_apertura.sql"})
 foreach(var batch in System.Text.RegularExpressions.Regex.Split(File.ReadAllText(Path.Combine(root,"portal/db/azure",file)),@"^\s*GO\s*$",System.Text.RegularExpressions.RegexOptions.Multiline|System.Text.RegularExpressions.RegexOptions.IgnoreCase))
 if(!string.IsNullOrWhiteSpace(batch))await sql.ExecuteAsync(batch,commandTimeout:120);
foreach(var batch in System.Text.RegularExpressions.Regex.Split(File.ReadAllText(Path.Combine(root,"despliegue/piloto/cuentas.sql")),@"^\s*GO\s*$",System.Text.RegularExpressions.RegexOptions.Multiline|System.Text.RegularExpressions.RegexOptions.IgnoreCase))
 if(!string.IsNullOrWhiteSpace(batch))await sql.ExecuteAsync(batch,commandTimeout:120);
await DemoData.Load(sql,Path.Combine(root,"datos/simulados"));Check(await sql.ExecuteScalarAsync<int>("SELECT SUM(cantidad) FROM dbo.UBICACION")==10474,"Carga simulada de 10474 piezas");
var hash=new PasswordHasher<string>().HashPassword("",Environment.GetEnvironmentVariable("PILOT_TEST_PASSWORD")!);
for(var i=0;i<80;i++)await sql.ExecuteAsync("INSERT mes.USUARIO_PILOTO(usuario,nombre,rol,id_puesto,password_hash) VALUES(@usuario,@usuario,@rol,@puesto,@hash)",new{usuario="persona"+i,rol=i==0?"ADMIN":i==79?"LECTOR":"PUESTO",puesto=i==0||i==79?(int?)null:1,hash});
// Usuarios SQL únicos del contenedor de pruebas, sin contraseñas de Azure.
var appPassword=Environment.GetEnvironmentVariable("PILOT_TEST_APP_PASSWORD")!;
await sql.ExecuteAsync("CREATE USER portal_app WITH PASSWORD='"+appPassword.Replace("'","''")+"'; CREATE USER powerbi_lector WITHOUT LOGIN;");
var grants=File.ReadAllText(Path.Combine(root,"portal/db/azure/020_permisos.sql"));await sql.ExecuteAsync(grants);await sql.ExecuteAsync("DENY EXECUTE ON dbo.GuardarAccesoPuesto TO mes_portal");
var appTarget=new SqlConnectionStringBuilder(adminConnection){UserID="portal_app",Password=appPassword};settings["ConnectionStrings:Sql"]=appTarget.ConnectionString;settings["ASPNETCORE_ENVIRONMENT"]="Development";
await using(var check=new SqlConnection(appTarget.ConnectionString)){await check.OpenAsync();Check(await check.ExecuteScalarAsync<int>("SELECT IS_ROLEMEMBER('db_owner')")==0,"Aplicación sin db_owner");Check(await check.ExecuteScalarAsync<int>("SELECT HAS_PERMS_BY_NAME('dbo.UBICACION','OBJECT','UPDATE')")==0,"Aplicación sin actualización directa de saldos");}
using var store=new Store(Config());await store.DetectarModelo();await store.PrepararContinuacion();var password=Environment.GetEnvironmentVariable("PILOT_TEST_PASSWORD")!;
var a=(await store.Login(new("persona0",password)))!;var p=(await store.Login(new("persona1",password)))!;var reader=(await store.Login(new("persona79",password)))!;
Check(a.Name=="persona0"&&a.Permissions.Contains("exit"),"Administrador personal identificado");Check(p.Name=="persona1"&&p.PuestoId==1&&!p.Permissions.Contains("exit"),"Operador personal limitado a su puesto");Check(!reader.Permissions.Contains("entry")&&!reader.Permissions.Contains("historico"),"Lector sin capturas ni historial global");
Check(await store.Login(new("admin.local",password)) is null,"No existe acceso admin.local compartido en piloto");
Check(await store.Login(new("persona1","incorrecta")) is null,"Contraseña errónea rechazada");
var row=await sql.QuerySingleAsync<dynamic>("SELECT TOP 1 u.id_ubicacion,u.version_concurrencia,u.cantidad,m.cantidad_por_caja FROM dbo.UBICACION u JOIN dbo.MATERIAL m ON m.numero_parte=u.numero_parte WHERE m.id_puesto=1 AND u.cantidad>=m.cantidad_por_caja ORDER BY u.id_ubicacion");
var id=(int)row.id_ubicacion;var version=(int)row.version_concurrencia;var op=Guid.NewGuid();
await store.GuardarMovimiento(a,new MovimientoNuevo(id,"SALIDA",1,version,false),op);
var same=await store.GuardarMovimiento(a,new MovimientoNuevo(id,"SALIDA",1,version,false),op);
Check(await sql.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM mes.OPERACION WHERE id_operacion=@op",new{op})==1,"UUID repetido no duplica movimiento");
await store.GuardarMovimiento(p,new MovimientoNuevo(id,"ENTRADA",1,version+1,false),Guid.NewGuid());
Check(await sql.ExecuteScalarAsync<int>("SELECT cantidad FROM dbo.UBICACION WHERE id_ubicacion=@id",new{id})==(int)row.cantidad,"Operador personal captura entrada y restaura saldo");
await sql.ExecuteAsync("UPDATE mes.USUARIO_PILOTO SET activo=0 WHERE usuario='persona1'");
try{await store.Actor(p.Id,p.TipoCuenta,p.SelloAcceso);throw new Exception("Revocación falló");}catch(RuleException e)when(e.Status==401){Check(true,"Revocación invalida sesión personal");}
await sql.ExecuteAsync("UPDATE mes.USUARIO_PILOTO SET activo=1 WHERE usuario='persona1'");
File.WriteAllText(Path.Combine(root,"portal/.local/pilot-tests/config.json"),System.Text.Json.JsonSerializer.Serialize(settings));
File.WriteAllText(Path.Combine(root,"documentacion/evidencia-integracion/PILOTO_LOCAL.json"),System.Text.Json.JsonSerializer.Serialize(new{estado="APROBADO",alcance="SQL local nuevo, no Azure",comprobaciones=passed},new System.Text.Json.JsonSerializerOptions{WriteIndented=true}));
Console.WriteLine($"{passed.Count} verificaciones del piloto aprobadas.");
sealed class HostEnv:IHostEnvironment{public string EnvironmentName{get;set;}="Production";public string ApplicationName{get;set;}="Test";public string ContentRootPath{get;set;}="";public IFileProvider ContentRootFileProvider{get;set;}=new NullFileProvider();}
