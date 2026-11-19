using Azure.Identity;
using Azure.Core;
using Dapper;
using Microsoft.Data.SqlClient;
using Motherson;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
var root=Path.GetFullPath(args[0]);var folder=Path.GetFullPath(args[1]);
var diaries=new List<string>();for(var i=2;i<args.Length-1;i++)if(args[i]=="--diario")diaries.Add(Path.GetFullPath(args[i+1]));
var files=LegacyArchive.Read(folder,diaries.ToArray());var plan=LegacyArchive.Validate(files);var total=plan.Values.Sum();
Console.WriteLine(JsonSerializer.Serialize(new{accion="PRESERVAR_LEGADO_SIN_REAPLICAR_SALDOS",filas=total,archivos=plan,aplicar=args.Contains("--aplicar")}));
if(!args.Contains("--aplicar"))return;
var tenant=Environment.GetEnvironmentVariable("AZURE_TENANT_ID")??throw new Exception("Indica el tenant propio mediante AZURE_TENANT_ID.");
if(!Guid.TryParse(tenant,out _))throw new Exception("Tenant inválido.");
var privateFolder=Path.Combine(root,".local");var recordPath=Path.Combine(privateFolder,"identidad-carga-legado.json");
AuthenticationRecord? record=null;if(File.Exists(recordPath)){await using var existing=File.OpenRead(recordPath);record=await AuthenticationRecord.DeserializeAsync(existing);}
var options=new DeviceCodeCredentialOptions{TenantId=tenant,AuthenticationRecord=record,TokenCachePersistenceOptions=new TokenCachePersistenceOptions{Name="MothersonL1CargaLegado"},DeviceCodeCallback=(info,ct)=>{Console.Error.WriteLine(info.Message);return Task.CompletedTask;}};
var credential=new DeviceCodeCredential(options);var scope=new TokenRequestContext(["https://database.windows.net/.default"]);
if(record is null)
{
 record=await credential.AuthenticateAsync(scope);
 var settings=new FileStreamOptions{Mode=FileMode.CreateNew,Access=FileAccess.Write};if(!OperatingSystem.IsWindows())settings.UnixCreateMode=UnixFileMode.UserRead|UnixFileMode.UserWrite;
 await using var save=new FileStream(recordPath,settings);await record.SerializeAsync(save);
}
var token=await credential.GetTokenAsync(scope);
await using var sql=new SqlConnection($"Server=tcp:{AzurePreflight.Server},1433;Initial Catalog={AzurePreflight.Database};Encrypt=True;TrustServerCertificate=False;Connect Timeout=120");sql.AccessToken=token.Token;await sql.OpenAsync();
if(await sql.ExecuteScalarAsync<string>("SELECT DB_NAME()")!=AzurePreflight.Database)throw new Exception("Destino incorrecto");
if(!await sql.ExecuteScalarAsync<bool>("SELECT es_simulado FROM mes.CONFIGURACION WHERE id=1"))throw new Exception("Se esperaba la demostración aprobada");
async Task<string> Snapshot(){var json=JsonSerializer.Serialize(await sql.QueryAsync("SELECT u.id_ubicacion,u.cantidad,u.version_concurrencia,m.stock FROM dbo.UBICACION u JOIN dbo.MATERIAL m ON m.numero_parte=u.numero_parte ORDER BY u.id_ubicacion"));return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(json)));}
var before=await Snapshot();var operations=await sql.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM mes.OPERACION");
var added=await LegacyArchive.Import(sql,files,Console.Error.WriteLine);
var actual=await sql.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM mes.HISTORICO_LEGADO");
var completed=await sql.QueryAsync("SELECT archivo,sha256,filas_esperadas,completo FROM mes.ARCHIVO_LEGADO ORDER BY archivo,sha256");
var stock=await sql.ExecuteScalarAsync<int>("SELECT SUM(cantidad) FROM dbo.UBICACION");
var history=await sql.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM mes.vw_historial_json",commandTimeout:180);
if(actual!=total||completed.Any(r=>!(bool)r.completo)||before!=await Snapshot()||operations!=await sql.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM mes.OPERACION"))throw new Exception("Revisar conciliación posterior; se conservaron los lotes confirmados.");
var evidence=new{resultado="CORRECTO",entorno="AZURE_SQL_REAL",fecha=DateTimeOffset.UtcNow,filas=actual,filasNuevas=added,archivos=completed,piezas=stock,operacionesPreviasConservadas=operations,historialConsultable=history,inventarioSinCambios=true};
var text=JsonSerializer.Serialize(evidence);File.WriteAllText(Path.Combine(privateFolder,"resultado-legado-azure.json"),text);Console.WriteLine(text);
