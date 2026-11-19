using System.Data;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Dapper;
using Microsoft.AspNetCore.Identity;
using Microsoft.Data.SqlClient;
namespace Motherson;
public static class Migration{
 public static string Root=>Environment.GetEnvironmentVariable("MVP_ROOT")??throw new Exception("Define MVP_ROOT al directorio portal.");
 public static string Target=>Environment.GetEnvironmentVariable("MVP_DATABASE")??"Motherson_L1_MVP_Desarrollo";
 static string Admin(string db){if(Target is not("Motherson_L1_MVP_Desarrollo" or "Motherson_L1_MVP_Pruebas"))throw new Exception("Destino no permitido");return new SqlConnectionStringBuilder(Environment.GetEnvironmentVariable("MVP_ADMIN_SQL")??throw new Exception("Usa scripts/local.py")){InitialCatalog=db}.ConnectionString;}
 static string Q(string text)=>"N'"+text.Replace("'","''")+"'";
 static string Password()=>"Mvp!"+Convert.ToHexString(RandomNumberGenerator.GetBytes(20));
 static string Local(string name)=>Path.Combine(Root,".local",name+"-"+Target+".json");
 static void Private(string path,object value){File.WriteAllText(path,JsonSerializer.Serialize(value,new JsonSerializerOptions{WriteIndented=true}));if(!OperatingSystem.IsWindows())File.SetUnixFileMode(path,UnixFileMode.UserRead|UnixFileMode.UserWrite);}
 static async Task<SqlConnection> Connect(string db){var c=new SqlConnection(Admin(db));await c.OpenAsync();return c;}
 static async Task<bool> EsModeloPuestos(SqlConnection c)=>await c.ExecuteScalarAsync<int>("SELECT CASE WHEN OBJECT_ID('reporting.vw_modelo_puestos','V') IS NULL THEN 0 ELSE 1 END")==1;
 static async Task ExigirModeloAnterior(SqlConnection c){if(await EsModeloPuestos(c))throw new InvalidOperationException("Este comando corresponde al modelo anterior. Usa scripts/migrar_puestos.py para el esquema por puesto.");}
 static async Task Script(SqlConnection c,string file){foreach(var batch in Regex.Split(await File.ReadAllTextAsync(Path.Combine(Root,"db",file)),@"^GO\s*$",RegexOptions.Multiline|RegexOptions.IgnoreCase))if(!string.IsNullOrWhiteSpace(batch))await c.ExecuteAsync(batch,commandTimeout:600);}
 static async Task Account(SqlConnection c,SqlTransaction tx,string name,string profile,string password){var hash=new PasswordHasher<string>().HashPassword("",password);await c.ExecuteAsync("IF NOT EXISTS(SELECT 1 FROM dbo.USUARIOS WHERE nombre_usuario=@name) INSERT dbo.USUARIOS(nombre_usuario,nombre_visible,hash_contrasena,esta_activo,perfil) VALUES(@name,@name,@hash,1,@profile)",new{name,hash,profile},tx);}
 static DataTable EventTable(JsonArray fields){var data=new DataTable();foreach(var n in fields){var name=n!.GetValue<string>();var type=name=="fecha_movimiento"?typeof(DateTime):name=="es_base"?typeof(bool):name.StartsWith("id_")||name is "cantidad_piezas" or "cantidad_delta" or "saldo_anterior" or "saldo_posterior"?typeof(int):typeof(string);data.Columns.Add(name,type);}return data;}
 public static async Task Run(string[] args){
  SqlMapper.AddTypeMap(typeof(DateTime),DbType.DateTime2);
  var index=Array.IndexOf(args,"--migrar");if(index<0||args.Length<=index+1)throw new Exception("Indica el plan.json preparado.");var plan=JsonNode.Parse(await File.ReadAllTextAsync(args[index+1]))!.AsObject();
  // Comprueba originales antes de cualquier carga. Nunca se toma un saldo desde una fotografía.
  foreach(var f in plan["fuentes"]!.AsArray()){using var stream=File.OpenRead(f!["ruta"]!.GetValue<string>());if(Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant()!=f["sha256"]!.GetValue<string>())throw new Exception("Una fuente cambió después de preparar el plan.");}
  await using(var master=await Connect("master")){if(await master.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM sys.databases WHERE name=@name",new{name=Target})==0){await master.ExecuteAsync("CREATE DATABASE ["+Target+"]",commandTimeout:120);await master.ExecuteAsync("ALTER DATABASE ["+Target+"] SET RECOVERY SIMPLE");}}
  await using var c=await Connect(Target);await ExigirModeloAnterior(c);await Script(c,"001_mvp.sql");if(await c.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM sys.columns WHERE object_id=OBJECT_ID('dbo.REGISTRO') AND name='es_simulado'")>0)throw new Exception("Aplica primero la migración selectiva de simplificación; no se cambian módulos de una base anterior.");await Script(c,"002_vistas.sql");await Script(c,"003_movimientos.sql");
  var corte=await c.QuerySingleOrDefaultAsync("SELECT huella_fuentes,sellado FROM archivo.CORTE");var huella=plan["huella"]!.GetValue<string>();
  if(corte is not null){if(!(bool)corte.sellado||(string)corte.huella_fuentes!=huella)throw new Exception("Carga previa incompleta o de otra fuente: no se sobrescribe.");Console.WriteLine("Corte ya cargado; no se repite la apertura.");await Access(c);await Verify();return;}
  var access=Local("acceso-inicial");if(!File.Exists(access))Private(access,new{usuario="admin.local",password=Password()});var pwd=JsonNode.Parse(await File.ReadAllTextAsync(access))!["password"]!.GetValue<string>();
  await using(var tx=(SqlTransaction)await c.BeginTransactionAsync()){
   await c.ExecuteAsync("INSERT archivo.CORTE(id,maestro,revision,corte_fuente,huella_fuentes) VALUES(1,@maestro,@revision,@corte,@huella)",new{maestro=plan["maestro"]!.GetValue<string>(),revision=plan["revision"]!.GetValue<int>(),corte=DateTime.Parse(plan["corte"]!.GetValue<string>(),System.Globalization.CultureInfo.InvariantCulture),huella},tx);
   await Account(c,tx,"admin.local","ADMINISTRADOR",pwd);
   foreach(var f in plan["fuentes"]!.AsArray())await c.ExecuteAsync("INSERT archivo.FUENTE VALUES(@nombre,@sha,@filas,@contenido)",new{nombre=f!["nombre"]!.GetValue<string>(),sha=f["sha256"]!.GetValue<string>(),filas=f["filas"]!.GetValue<int>(),contenido=await File.ReadAllBytesAsync(f["ruta"]!.GetValue<string>())},tx,commandTimeout:300);
   using var bulk=new SqlBulkCopy(c,SqlBulkCopyOptions.CheckConstraints|SqlBulkCopyOptions.TableLock,tx){DestinationTableName="archivo.EVENTO",BulkCopyTimeout=600,BatchSize=10000};var data=EventTable(plan["campos_evento"]!.AsArray());foreach(DataColumn col in data.Columns)bulk.ColumnMappings.Add(col.ColumnName,col.ColumnName);
   using var fs=File.OpenRead(plan["archivo_eventos"]!.GetValue<string>());using var gz=new GZipStream(fs,CompressionMode.Decompress);using var reader=new StreamReader(gz);string? line;int count=0;
   while((line=await reader.ReadLineAsync()) is not null){using var j=JsonDocument.Parse(line);var row=data.NewRow();foreach(DataColumn col in data.Columns){var value=j.RootElement.GetProperty(col.ColumnName);row[col]=value.ValueKind==JsonValueKind.Null?DBNull.Value:col.DataType==typeof(DateTime)?value.GetDateTime():col.DataType==typeof(bool)?value.GetBoolean():col.DataType==typeof(int)?value.GetInt32():value.GetString()!;}data.Rows.Add(row);count++;if(data.Rows.Count==10000){await bulk.WriteToServerAsync(data);data.Clear();}}
   if(data.Rows.Count>0)await bulk.WriteToServerAsync(data);
   if(count!=plan["eventos"]!.GetValue<int>())throw new Exception("Conteo histórico distinto");
   foreach(var post in plan["puestos"]!.AsArray())await c.ExecuteAsync("SET IDENTITY_INSERT dbo.PUESTO_LOGISTICO ON;INSERT dbo.PUESTO_LOGISTICO(id_puesto,nombre)VALUES(@id,@name);SET IDENTITY_INSERT dbo.PUESTO_LOGISTICO OFF;",new{id=post![0]!.GetValue<int>(),name=post[1]!.GetValue<string>()},tx);
   await c.ExecuteAsync("SET IDENTITY_INSERT dbo.TURNO ON;INSERT dbo.TURNO(id_turno,nombre_turno,hora_inicio,hora_fin) VALUES(2,N'PFEP T1','06:00','15:00'),(3,N'PFEP T2','15:00','23:30');SET IDENTITY_INSERT dbo.TURNO OFF;",transaction:tx);
   foreach(var p in plan["posiciones"]!.AsArray()){
    var argsPos=new{sid=p!["id_estacion"]!.GetValue<int>(),station=p["estacion"]!.GetValue<string>(),id=p["id"]!.GetValue<int>(),rack=p["estanteria"]!.GetValue<string>(),nivel=p["nivel"]!.GetValue<string>(),cap=p["capacidad"]!.GetValue<int>(),parte=p["parte"]!.GetValue<string>(),post=p["id_puesto"]!.GetValue<int>(),descripcion=p["descripcion"]?.GetValue<string>(),factor=p["factor"]!.GetValue<int>(),minimo=p["minimo"]!.GetValue<int>(),saldo=p["saldo"]!.GetValue<int>()};
    await c.ExecuteAsync("""
    IF NOT EXISTS(SELECT 1 FROM dbo.ESTACION WHERE id_estacion=@sid) BEGIN SET IDENTITY_INSERT dbo.ESTACION ON;INSERT dbo.ESTACION(id_estacion,id_linea,nombre_estacion) VALUES(@sid,1,@station);SET IDENTITY_INSERT dbo.ESTACION OFF;END;
    SET IDENTITY_INSERT dbo.ESTANTERIA ON;INSERT dbo.ESTANTERIA(id_estanteria,id_estacion,estanteria,nivel,cajas_estanteria) VALUES(@id,@sid,@rack,@nivel,@cap);SET IDENTITY_INSERT dbo.ESTANTERIA OFF;
    IF NOT EXISTS(SELECT 1 FROM dbo.MATERIAL WHERE numero_parte=@parte) INSERT dbo.MATERIAL(numero_parte,id_puesto,descripcion,cantidad_por_caja,minimo)VALUES(@parte,@post,@descripcion,@factor,@minimo);
    SET IDENTITY_INSERT dbo.UBICACION ON;INSERT dbo.UBICACION(id_ubicacion,numero_parte,id_estanteria,cantidad,version_concurrencia)VALUES(@id,@parte,@id,NULL,1);SET IDENTITY_INSERT dbo.UBICACION OFF;
    INSERT dbo.REGISTRO(id_usuarios,id_ubicacion,id_puesto,tipo_movimiento,cantidad_movimiento,saldo_anterior,saldo_posterior,fecha_movimiento,fecha_hora_inicio,fecha_hora_fin,id_operacion,huella_peticion,origen)
    SELECT id_usuarios,@id,@post,'APERTURA',@saldo,0,@saldo,SYSUTCDATETIME(),SYSUTCDATETIME(),SYSUTCDATETIME(),NEWID(),REPLICATE('0',64),'APERTURA_SIMULADA' FROM dbo.USUARIOS WHERE nombre_usuario=N'admin.local';
    UPDATE dbo.UBICACION SET cantidad=@saldo,version_concurrencia=2 WHERE id_ubicacion=@id;
    """,argsPos,tx);
   }
   var actual=await c.ExecuteScalarAsync<long>("SELECT COALESCE(SUM(CONVERT(bigint,cantidad)),0) FROM dbo.UBICACION",transaction:tx);var expected=plan["posiciones"]!.AsArray().Sum(x=>(long)x!["saldo"]!.GetValue<int>());if(actual!=expected)throw new Exception("No concilia la apertura");
   await c.ExecuteAsync("UPDATE archivo.CORTE SET sellado=1 WHERE id=1",transaction:tx);await tx.CommitAsync();
  }
  await Access(c);await Verify();Console.WriteLine("Histórico sellado y apertura conciliada. Credenciales iniciales en archivo privado .local; no se imprimen.");
 }
 static async Task Access(SqlConnection c){
  var path=Local("conexiones");JsonObject config;if(File.Exists(path))config=JsonNode.Parse(await File.ReadAllTextAsync(path))!.AsObject();else{config=new JsonObject();var prefix=Target.EndsWith("Pruebas")?"mvp_pruebas":"mvp_desarrollo";foreach(var purpose in new[]{"app","lectura"}){var login=prefix+"_"+purpose;var pwd=Password();var s=new SqlConnectionStringBuilder(Admin(Target)){UserID=login,Password=pwd};config[purpose]=s.ConnectionString;}Private(path,config);}
  foreach(var purpose in new[]{"app","lectura"}){var s=new SqlConnectionStringBuilder(config[purpose]!.GetValue<string>());await using var master=await Connect("master");await master.ExecuteAsync("IF NOT EXISTS(SELECT 1 FROM sys.sql_logins WHERE name="+Q(s.UserID)+") CREATE LOGIN ["+s.UserID+"] WITH PASSWORD="+Q(s.Password)+",CHECK_POLICY=ON;");await c.ExecuteAsync("IF NOT EXISTS(SELECT 1 FROM sys.database_principals WHERE name="+Q(s.UserID)+") CREATE USER ["+s.UserID+"] FOR LOGIN ["+s.UserID+"];GRANT SELECT ON SCHEMA::reporting TO ["+s.UserID+"];DENY INSERT,UPDATE,DELETE ON SCHEMA::archivo TO ["+s.UserID+"];DENY INSERT,UPDATE,DELETE ON SCHEMA::dbo TO ["+s.UserID+"];");if(purpose=="app")await c.ExecuteAsync("GRANT SELECT ON SCHEMA::dbo TO ["+s.UserID+"];GRANT SELECT ON OBJECT::archivo.CORTE TO ["+s.UserID+"];GRANT EXECUTE ON dbo.RegistrarMovimiento TO ["+s.UserID+"];");}
 }
 public static async Task Verify(){await using var c=await Connect(Target);
  if(await EsModeloPuestos(c)){
   var puestos=await c.QuerySingleAsync("SELECT DB_NAME() base_datos,(SELECT COUNT(*) FROM sys.tables WHERE schema_id=SCHEMA_ID('dbo')) tablas_operativas,(SELECT COUNT(*) FROM archivo.EVENTO) eventos_originales,(SELECT COUNT(*) FROM archivo.REGISTRO_MVP) movimientos_previos,(SELECT COUNT(*) FROM dbo.REGISTRO) atenciones,(SELECT SUM(CONVERT(bigint,stock)) FROM dbo.MATERIAL) piezas_al_corte");
   Console.WriteLine(JsonSerializer.Serialize(puestos));return;
  }
  var x=await c.QuerySingleAsync("SELECT DB_NAME() base_datos,(SELECT COUNT(*) FROM sys.tables WHERE schema_id=SCHEMA_ID('dbo')) tablas_operativas,(SELECT COUNT(*) FROM archivo.EVENTO) eventos_originales,(SELECT COUNT(*) FROM reporting.vw_kpi_movimientos WHERE espacio_identificadores='HISTORICO_21') movimientos_historicos,(SELECT COUNT(*) FROM dbo.REGISTRO WHERE tipo_movimiento='APERTURA') aperturas,(SELECT SUM(CONVERT(bigint,cantidad)) FROM dbo.UBICACION) piezas_actuales,(SELECT COUNT(*) FROM reporting.vw_kpi_consumo_diario) dias_consumo_real");Console.WriteLine(JsonSerializer.Serialize(x));}
 public static async Task SeedTests(){if(Target!="Motherson_L1_MVP_Pruebas")throw new Exception("Solo base de pruebas");await using var c=await Connect(Target);await ExigirModeloAnterior(c);var access=JsonNode.Parse(await File.ReadAllTextAsync(Local("acceso-inicial")))!.AsObject();var pwd=access["password"]!.GetValue<string>();await using var tx=(SqlTransaction)await c.BeginTransactionAsync();foreach(var (name,profile) in new[]{("consulta.prueba","CONSULTA"),("entrada.prueba","ENTRADAS")})await Account(c,tx,name,profile,pwd);await c.ExecuteAsync(await File.ReadAllTextAsync(Path.Combine(Root,"db","004_datos_prueba.sql")),transaction:tx);await tx.CommitAsync();await Script(c,"006_prueba_fallo.sql");Console.WriteLine("Datos controlados preparados únicamente en Pruebas.");}
 public static async Task Integrity(){await using var c=await Connect(Target);await ExigirModeloAnterior(c);var result=await c.QuerySingleAsync(await File.ReadAllTextAsync(Path.Combine(Root,"db","005_validacion.sql")),commandTimeout:120);Console.WriteLine(JsonSerializer.Serialize(result));
  if(Target=="Motherson_L1_MVP_Pruebas") {await using var tx=(SqlTransaction)await c.BeginTransactionAsync();
   await c.ExecuteAsync("""
   DECLARE @u int=(SELECT id_ubicacion FROM dbo.UBICACION WHERE numero_parte='LAB-PENDIENTE'),@usr int=(SELECT id_usuarios FROM dbo.USUARIOS WHERE nombre_usuario='admin.local'),@p int=(SELECT id_puesto FROM dbo.MATERIAL WHERE numero_parte='LAB-PENDIENTE');
   INSERT dbo.REGISTRO(id_usuarios,id_ubicacion,id_puesto,tipo_movimiento,cantidad_movimiento,saldo_anterior,saldo_posterior,fecha_movimiento,fecha_hora_inicio,fecha_hora_fin,id_operacion,huella_peticion,origen)
   VALUES(@usr,@u,@p,'APERTURA',0,0,0,SYSUTCDATETIME(),SYSUTCDATETIME(),SYSUTCDATETIME(),NEWID(),REPLICATE('0',64),'APERTURA_REAL'),(@usr,@u,@p,'SALIDA',5,10,5,SYSUTCDATETIME(),SYSUTCDATETIME(),SYSUTCDATETIME(),NEWID(),REPLICATE('0',64),'CAPTURA_REAL'),(@usr,@u,@p,'SALIDA',7,10,3,SYSUTCDATETIME(),SYSUTCDATETIME(),SYSUTCDATETIME(),NEWID(),REPLICATE('0',64),'CAPTURA_SIMULADA');
   IF (SELECT SUM(cantidad_piezas) FROM reporting.vw_kpi_consumo_diario WHERE numero_parte='LAB-PENDIENTE')<>5 THROW 51000,'Filtro real/apertura incorrecto',1;
   """,transaction:tx);await tx.RollbackAsync();Console.WriteLine("Filtro REAL y exclusión de APERTURA comprobados con datos temporales revertidos.");}
 }
 public static async Task TestPermissions(){var config=JsonNode.Parse(await File.ReadAllTextAsync(Local("conexiones")))!;await using var r=new SqlConnection(config["lectura"]!.GetValue<string>());await r.OpenAsync();var n=await r.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM reporting.vw_kpi_movimientos");var denegadas=0;var intentos=await EsModeloPuestos(r)?new[]{
  "UPDATE dbo.MATERIAL SET stock=0 WHERE 1=0","DELETE archivo.EVENTO WHERE 1=0","EXEC dbo.IniciarAtencion 1,1",
  "SELECT TOP(1) [password] FROM dbo.PUESTO_LOGISTICO","SELECT TOP(1) hash_contrasena FROM respaldo_puestos.USUARIOS"
 }:new[]{"UPDATE dbo.UBICACION SET cantidad=0 WHERE 1=0","DELETE archivo.EVENTO WHERE 1=0","EXEC dbo.RegistrarMovimiento 1,1,'SALIDA',1,1,'00000000-0000-0000-0000-000000000001','x'","SELECT TOP(1) hash_contrasena FROM dbo.USUARIOS"};foreach(var sql in intentos){try{await r.ExecuteAsync(sql);throw new Exception("La cuenta lectora recibió un permiso indebido");}catch(SqlException ex)when(ex.Number==229){denegadas++;}}Console.WriteLine(JsonSerializer.Serialize(new{lectura_vistas=n,escrituras_o_secretos_denegados=denegadas}));}
}
