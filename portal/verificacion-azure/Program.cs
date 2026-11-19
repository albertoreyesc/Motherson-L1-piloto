using System.Text.Json;
using Microsoft.Data.SqlClient;
using Dapper;
using Motherson;
using Microsoft.Extensions.Configuration;
var root=Path.GetFullPath(args[0]);
var privateData=JsonDocument.Parse(File.ReadAllText(Path.Combine(root,".local/accesos-iniciales-azure.json")));
var connection=JsonDocument.Parse(File.ReadAllText(Path.Combine(root,".local/conexiones-compartida.json"))).RootElement.GetProperty("app").GetString()!;
var settings=new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string,string?>{["ConnectionStrings:Sql"]=connection,["MVP_BASE_COMPARTIDA"]="Motherson_L1"}).Build();AzurePreflight.Validate(settings);
var passed=new List<string>();void Check(bool ok,string message){if(!ok)throw new Exception(message);passed.Add(message);}
await using var app=new SqlConnection(connection);await app.OpenAsync();
Check(await app.ExecuteScalarAsync<string>("SELECT USER_NAME()") == "portal_app","Conexión real Azure como portal_app");
Check(await app.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM mes.BASE_INVENTARIO")==1,"Apertura SQL confirmada");
Check(await app.ExecuteScalarAsync<bool>("SELECT es_simulado FROM mes.CONFIGURACION WHERE id=1"),"Demostración identificada como simulada");
var counts=(await app.QueryAsync("SELECT 'ESTACION' tabla,COUNT(*) filas FROM dbo.ESTACION UNION ALL SELECT 'COLOR',COUNT(*) FROM dbo.COLOR UNION ALL SELECT 'PUESTO_LOGISTICO',COUNT(*) FROM dbo.PUESTO_LOGISTICO UNION ALL SELECT 'TURNO',COUNT(*) FROM dbo.TURNO UNION ALL SELECT 'ESTANTERIA',COUNT(*) FROM dbo.ESTANTERIA UNION ALL SELECT 'MATERIAL',COUNT(*) FROM dbo.MATERIAL UNION ALL SELECT 'UBICACION',COUNT(*) FROM dbo.UBICACION UNION ALL SELECT 'REGISTRO',COUNT(*) FROM dbo.REGISTRO")).ToArray();
var expected=new Dictionary<string,int>{{"ESTACION",27},{"COLOR",15},{"PUESTO_LOGISTICO",6},{"TURNO",2},{"ESTANTERIA",85},{"MATERIAL",85},{"UBICACION",85},{"REGISTRO",58422}};
Check(counts.All(x=>expected[(string)x.tabla]==(int)x.filas),"Ocho tablas concilian con los CSV aprobados");
Check(await app.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM dbo.MATERIAL m WHERE stock<>(SELECT COALESCE(SUM(cantidad),0) FROM dbo.UBICACION u WHERE u.numero_parte=m.numero_parte)")==0,"Stock agregado concilia con ubicaciones");
Check(await app.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM dbo.UBICACION WHERE cantidad<0")==0,"No hay existencias negativas");
Check(await app.ExecuteScalarAsync<int>("SELECT COALESCE(HAS_PERMS_BY_NAME('dbo.UBICACION','OBJECT','UPDATE'),0)")==0,"Backend sin escritura directa de existencias");
Check(await app.ExecuteScalarAsync<int>("SELECT COALESCE(HAS_PERMS_BY_NAME('dbo.RegistrarMovimiento','OBJECT','EXECUTE'),0)")==0,"Procedimiento antiguo denegado al backend");
Check(await app.ExecuteScalarAsync<int>("SELECT COALESCE(HAS_PERMS_BY_NAME('mes.RegistrarMovimiento','OBJECT','EXECUTE'),0)")==1,"Backend autorizado para movimiento transaccional");
var readerConfig=new SqlConnectionStringBuilder(connection){UserID="powerbi_lector",Password=privateData.RootElement.GetProperty("powerBi").GetProperty("password").GetString()};
await using var reader=new SqlConnection(readerConfig.ConnectionString);await reader.OpenAsync();
Check(await reader.ExecuteScalarAsync<string>("SELECT USER_NAME()") == "powerbi_lector","Conexión real Azure como powerbi_lector");
Check(await reader.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM dbo.vw_registro_bi")==58422,"Fuente de atenciones BI concilia 58,422 filas");
Check(await reader.ExecuteScalarAsync<int>("SELECT COALESCE(SUM(cantidad),0) FROM dbo.UBICACION")==await app.ExecuteScalarAsync<int>("SELECT COALESCE(SUM(cantidad),0) FROM dbo.vw_kpi_inventario_actual"),"Inventario lector BI y backend coincide");
Check(await reader.ExecuteScalarAsync<int>("SELECT COALESCE(HAS_PERMS_BY_NAME('mes.RegistrarMovimiento','OBJECT','EXECUTE'),0)")==0,"Lector BI sin permiso de capturas");
try{await reader.QueryAsync("SELECT TOP(1) [password] FROM dbo.PUESTO_LOGISTICO");throw new Exception("BI pudo leer hashes");}catch(SqlException e)when(e.Number is 229 or 230){passed.Add("Lector BI no puede consultar hashes de accesos");}
if(args.Contains("--transacciones-revertidas"))
{
 Check(await app.ExecuteScalarAsync<bool>("SELECT es_simulado FROM mes.CONFIGURACION WHERE id=1"),"Pruebas reversibles habilitadas solo en demostración");
 var before=await app.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM mes.MOVIMIENTO");
 var partial=await app.QuerySingleAsync("SELECT TOP(1) u.id_ubicacion,u.cantidad,u.version_concurrencia FROM dbo.UBICACION u JOIN dbo.MATERIAL m ON m.numero_parte=u.numero_parte WHERE u.cantidad>0 AND u.cantidad<m.cantidad_por_caja ORDER BY u.id_ubicacion");
 await using(var transaction=app.BeginTransaction())
 {
  var body=JsonSerializer.Serialize(new MovimientoNuevo((int)partial.id_ubicacion,"SALIDA",1,(int)partial.version_concurrencia));
  var response=await app.QuerySingleAsync<string>("mes.RegistrarMovimiento",new{operacion=Guid.NewGuid(),cuenta="VERIFICACION_DEMO_REVERTIDA",tipo_cuenta="ADMIN_PUESTOS",puesto_cuenta=(int?)null,solicitud=body},transaction:transaction,commandType:System.Data.CommandType.StoredProcedure);
  var receipt=JsonDocument.Parse(response).RootElement;
  Check(receipt.GetProperty("saldo_posterior").GetInt32()==0 && receipt.GetProperty("cantidad_movimiento").GetInt32()==(int)partial.cantidad,"Última caja incompleta descuenta únicamente sus piezas en Azure");
  transaction.Rollback();
 }
 Check(await app.ExecuteScalarAsync<int>("SELECT cantidad FROM dbo.UBICACION WHERE id_ubicacion=@id",new{id=(int)partial.id_ubicacion})==(int)partial.cantidad && await app.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM mes.MOVIMIENTO")==before,"Rollback externo conserva saldo e historial juntos");
 try
 {
  await app.QuerySingleAsync<string>("mes.RegistrarMovimiento",new{operacion=Guid.NewGuid(),cuenta="VERIFICACION_SIN_ACCESO",tipo_cuenta="PUESTO",puesto_cuenta=1,solicitud=JsonSerializer.Serialize(new MovimientoNuevo((int)partial.id_ubicacion,"SALIDA",1,(int)partial.version_concurrencia))},commandType:System.Data.CommandType.StoredProcedure);
  throw new Exception("Un puesto pudo realizar salida");
 }
 catch(SqlException e)when(e.Number==51003){passed.Add("SQL rechaza salida de cuenta de puesto");}
}
Console.WriteLine(JsonSerializer.Serialize(new{resultado="CORRECTO",entorno="AZURE_SQL_REAL",fecha=DateTimeOffset.UtcNow,total=passed.Count,casos=passed,conteos=counts,piezas=await app.ExecuteScalarAsync<int>("SELECT SUM(cantidad) FROM dbo.UBICACION"),movimientos=await app.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM mes.MOVIMIENTO"),ajustes=await app.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM mes.AJUSTE"),legado=await app.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM mes.HISTORICO_LEGADO")}));
