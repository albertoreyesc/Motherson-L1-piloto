using System.Data;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Dapper;
using Microsoft.Data.SqlClient;
namespace Motherson;
public static class AdminV2Migration
{
 public static async Task Ejecutar(IConfiguration cfg,string root,bool activar)
 {
  var cs=cfg.GetConnectionString("Sql")??throw new InvalidOperationException("Falta conexión privada.");var b=new SqlConnectionStringBuilder(cs);
  var local=b.DataSource.StartsWith("127.0.0.1,",StringComparison.Ordinal)&&(b.InitialCatalog=="Motherson_L1"||b.InitialCatalog.StartsWith("MES_Integracion_Pruebas_",StringComparison.Ordinal));
  if(!local){var target=AzurePreflight.Validate(cfg);if(!cfg.GetValue<bool>("Pilot:Enabled")||!target.ConnectionString.Contains("motherson-piloto-",StringComparison.OrdinalIgnoreCase))throw new InvalidOperationException("Solo piloto aislado.");}
  await using var c=new SqlConnection(cs);await c.OpenAsync();
  if(!activar){foreach(var batch in Regex.Split(File.ReadAllText(Path.Combine(root,"portal/db/administracion/001_administracion.sql")),@"^\s*GO\s*$",RegexOptions.Multiline|RegexOptions.IgnoreCase))if(!string.IsNullOrWhiteSpace(batch))await c.ExecuteAsync(batch,commandTimeout:120);Console.WriteLine("Esquema preparado. No activado ni copiado el saldo.");return;}
  await using var tx=(SqlTransaction)await c.BeginTransactionAsync(IsolationLevel.Serializable);
  if(await c.ExecuteScalarAsync<bool>("SELECT activo FROM mes2.CONFIG WITH(UPDLOCK,HOLDLOCK) WHERE id=1",transaction:tx)){await tx.CommitAsync();Console.WriteLine("Administración ya activa; no se repite la apertura.");return;}
  if(await c.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM mes2.EVENTO",transaction:tx)!=0)throw new InvalidOperationException("Hay una apertura previa: no se reabre ni se sobrescribe.");
  var now=await c.ExecuteScalarAsync<DateTime>("SELECT SYSUTCDATETIME()",transaction:tx);var changes=new List<V2Cambio>();
  void Add(string tipo,string id,JsonObject data){data["id"]=id;changes.Add(new(tipo,id,data));}
  foreach(var p in await c.QueryAsync("SELECT id_puesto,nombre FROM dbo.PUESTO_LOGISTICO",transaction:tx))Add("puesto",((int)p.id_puesto).ToString(),new(){["nombre"]=(string)p.nombre});
  foreach(var s in await c.QueryAsync("SELECT id_estacion,nombre_estacion FROM dbo.ESTACION",transaction:tx))Add("estacion",((int)s.id_estacion).ToString(),new(){["nombre"]=(string)s.nombre_estacion});
  foreach(var r in await c.QueryAsync("SELECT id_estanteria,id_estacion,estanteria,nivel,cajas_estanteria FROM dbo.ESTANTERIA",transaction:tx))Add("estanteria",((int)r.id_estanteria).ToString(),new(){["nombre"]=(string)r.estanteria,["estacion"]=((int)r.id_estacion).ToString(),["nivel"]=(string?)r.nivel??"",["capacidad"]=(int)r.cajas_estanteria});
  var materiales=new Dictionary<string,string>(StringComparer.Ordinal);
  foreach(var m in await c.QueryAsync("SELECT m.*,c.nombre variante FROM dbo.MATERIAL m LEFT JOIN dbo.COLOR c ON c.id_color=m.id_color",transaction:tx)){
   var id=Guid.NewGuid().ToString();materiales.Add((string)m.numero_parte,id);
   Add("material",id,new(){["numeroParte"]=(string)m.numero_parte,["descripcion"]=(string?)m.descripcion??"",["uso"]=(string?)m.uso??"",["variante"]=(string?)m.variante??"",["revision"]="",["modelo"]="",["puesto"]=((int)m.id_puesto).ToString(),["empaque"]=(int)m.cantidad_por_caja,["minimo"]=(int)m.minimo,["activo"]=true});
  }
  long total=0;
  foreach(var u in await c.QueryAsync("SELECT * FROM dbo.UBICACION",transaction:tx)){total+=(int)u.cantidad;Add("ubicacion",((int)u.id_ubicacion).ToString(),new(){["material"]=materiales[(string)u.numero_parte],["estanteria"]=((int)u.id_estanteria).ToString(),["cantidad"]=(int)u.cantidad,["apertura"]=DateTime.SpecifyKind(now,DateTimeKind.Utc).ToString("O")});}
  var stock=await c.ExecuteScalarAsync<long>("SELECT COALESCE(SUM(CONVERT(bigint,stock)),0) FROM dbo.MATERIAL",transaction:tx);if(stock!=total)throw new InvalidOperationException("Saldo por ubicación no concilia con material. No se activó.");
  foreach(var a in await c.QueryAsync("SELECT * FROM dbo.vw_kpi_atenciones",transaction:tx)){
   var id="legado-"+(int)a.id_registro;Add("atencion",id,new(){["material"]=materiales[(string)a.numero_parte],["ubicacion"]=((int)a.id_ubicacion).ToString(),["puesto"]=((int)a.id_puesto).ToString(),["responsable"]=(string?)a.puesto??"",["usuario"]=0,["inicio"]=DateTime.SpecifyKind((DateTime)a.fecha_hora_inicio,DateTimeKind.Utc).ToString("O"),["fin"]=a.fecha_hora_fin is null?null:DateTime.SpecifyKind((DateTime)a.fecha_hora_fin,DateTimeKind.Utc).ToString("O"),["turno"]=AdminV2.Turno((DateTime)a.fecha_hora_inicio),["origen"]="REGISTRO_ORIGINAL"});
  }
  if(await c.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM dbo.MATERIAL m OUTER APPLY(SELECT SUM(CONVERT(bigint,cantidad)) cantidad FROM dbo.UBICACION u WHERE u.numero_parte=m.numero_parte) u WHERE CONVERT(bigint,m.stock)<>COALESCE(u.cantidad,0)",transaction:tx)>0)throw new InvalidOperationException("No concilia el saldo por material. No se activó.");
  AdminV2.ValidarIntegridad(new V2Estado(1,0,now,now,"ADMIN",null,changes.ToDictionary(x=>x.Tipo+":"+x.Id,x=>x.Datos!)));
  var json=JsonSerializer.Serialize(changes,new JsonSerializerOptions{PropertyNamingPolicy=JsonNamingPolicy.CamelCase});
  await c.ExecuteAsync("INSERT mes2.ENTIDAD(linea,tipo,id,datos) SELECT 1,tipo,id,datos FROM OPENJSON(@json) WITH(tipo varchar(20),id nvarchar(100),datos nvarchar(max) AS JSON);UPDATE mes2.LINEA SET apertura=@now WHERE id=1;INSERT mes2.EVENTO(id,linea,usuario,cuenta,tipo,fecha,huella,solicitud,cambios,resultado) VALUES(NEWID(),1,NULL,N'MIGRACION','APERTURA',@now,@hash,N'{}',@json,@resultado)",new{json,now,hash=Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(json))),resultado=JsonSerializer.Serialize(new{piezas=total,materiales=materiales.Count,fecha=now})},tx);
  if(await c.ExecuteScalarAsync<int>("SELECT CASE WHEN OBJECT_ID('mes.IMAGEN_PILOTO') IS NOT NULL THEN 1 ELSE 0 END",transaction:tx)==1)
   foreach(var m in materiales)await c.ExecuteAsync("INSERT mes2.IMAGEN(linea,material,tipo,bytes,version) SELECT 1,@id,tipo,contenido,CONVERT(int,version) FROM mes.IMAGEN_PILOTO WHERE numero_parte=@parte",new{id=m.Value,parte=m.Key},tx);
  await c.ExecuteAsync("UPDATE mes2.CONFIG SET activo=1 WHERE id=1",transaction:tx);await tx.CommitAsync();
  Console.WriteLine($"Activación conciliada: {materiales.Count} materiales y {total} piezas. Datos anteriores conservados en lectura.");
 }
}
