using System.Data;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Dapper;
using Microsoft.Data.SqlClient;
namespace Motherson;

public static partial class NormalizedJournal
{
 public static readonly Dictionary<string,string> ActorStages=new(){["requestedBy"]="start",["acceptedBy"]="accepted",["transferredBy"]="transfer",["deliveredBy"]="delivered",["confirmedBy"]="confirmed"};
 public static (string id,object?[] values) ActorData(JsonObject row){
  var allowed=new[]{"id","name","post","shift","fictional","nature","validFrom","validTo"};
  foreach(var key in row.Select(p=>p.Key))if(!allowed.Contains(key))throw new RuleException("Atribución no modelada: "+key,409);
  var canonical=new JsonObject();foreach(var kv in row.OrderBy(p=>p.Key,StringComparer.Ordinal))canonical[kv.Key]=kv.Value?.DeepClone();
  string id=Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical.ToJsonString()))).ToLowerInvariant();
  var who=row["id"]?.GetValue<string>();int? user=who?.StartsWith("USER:",StringComparison.Ordinal)==true?int.Parse(who[5..],CultureInfo.InvariantCulture):null;
  object? Get(string k)=>row[k]?.GetValue<string>();
  object? Day(string k)=>row[k] is null?null:DateTime.Parse(row[k]!.GetValue<string>(),CultureInfo.InvariantCulture);
  return(id,new object?[]{id,who,Get("name"),Get("post"),Get("shift"),row["fictional"]?.GetValue<bool>(),Get("nature"),Day("validFrom"),Day("validTo"),user,Presence(row)});
 }
 public static JsonObject EventActor(JsonObject row)=>new(){["id"]=row["responsible_id"]?.DeepClone(),["name"]=row["responsible_name"]?.DeepClone(),["shift"]=row["responsible_shift"]?.DeepClone()};
 public static DataTable ActorTable(){var t=new DataTable();string[] cols={"Id","Identificador","Nombre","Puesto","Turno","Ficticio","Naturaleza","Desde","Hasta","CuentaId","Campos"};Type[] types={typeof(string),typeof(string),typeof(string),typeof(string),typeof(string),typeof(bool),typeof(string),typeof(DateTime),typeof(DateTime),typeof(int),typeof(string)};for(int i=0;i<cols.Length;i++)t.Columns.Add(cols[i],types[i]);return t;}
 public static DataTable RevisionTable(){var t=new DataTable();foreach(var f in new[]{new Field("Id","bigint"),new Field("Tabla","varchar(12)"),new Field("Clave","nvarchar(80)"),new Field("UsuarioId","int"),new Field("RegistradoUtc","datetime2(7)"),new Field("Campos","nvarchar(2000)"),new Field("Base","bit")})t.Columns.Add(f.Name,DataType(f.SqlType));return t;}
 public static DataTable EntityTable(string table){var t=new DataTable();t.Columns.Add("RevisionId",typeof(long));foreach(var f in Fields[table])t.Columns.Add(f.Name,DataType(f.SqlType));if(table=="catalog")t.Columns.Add("Naturaleza",typeof(string));if(table=="events")t.Columns.Add("ActorId",typeof(string));if(table=="orders")t.Columns.Add("ResponsablesCampos",typeof(string));return t;}
 public static object[] Values(string table,JsonObject row,long revision,string? actor=null){
  Validate(table,row);var values=new List<object>{revision};values.AddRange(Fields[table].Select(f=>Value(row[f.Name],f.SqlType)));
  if(table=="catalog"){var detail=JsonNode.Parse(row["data"]!.GetValue<string>())!.AsObject();values.Add(detail["positionNature"]?.GetValue<string>()??"DOCUMENTAL");}
  if(table=="events")values.Add(actor is null?DBNull.Value:actor);
  if(table=="orders")values.Add(row["responsibles"] is null?DBNull.Value:Presence(JsonNode.Parse(row["responsibles"]!.GetValue<string>())!.AsObject()));
  return values.ToArray();
 }
 public static DataTable StageTable(){var t=new DataTable();t.Columns.Add("RevisionId",typeof(long));t.Columns.Add("Etapa",typeof(string));t.Columns.Add("Instante",typeof(long));t.Columns.Add("ActorId",typeof(string));t.Columns.Add("ActorPresente",typeof(bool));return t;}
 public static async Task Bulk(SqlConnection c,string table,DataTable data,SqlTransaction? transaction=null){
  if(data.Rows.Count==0)return;data=await ModeloRelacional.Convertir(c,transaction,table,data);using var b=new SqlBulkCopy(c,SqlBulkCopyOptions.KeepNulls | SqlBulkCopyOptions.FireTriggers | (transaction is null?0:SqlBulkCopyOptions.CheckConstraints),transaction){DestinationTableName=ModeloRelacional.NombreFisico(table),BatchSize=3000,BulkCopyTimeout=300};foreach(DataColumn col in data.Columns)b.ColumnMappings.Add(col.ColumnName,col.ColumnName);await b.WriteToServerAsync(data);data.Clear();
 }
 public static async Task EnsurePosition(SqlConnection c,SqlTransaction? t,JsonObject row,bool operable=true){
  string key=row["key"]!.GetValue<string>(),part=row["part"]!.GetValue<string>(),station=row["station"]!.GetValue<string>(),rack=row["rack"]!.GetValue<string>(),level=row["level"]!.GetValue<string>(),post=row["post"]!.GetValue<string>();
  var current=await c.QuerySingleOrDefaultAsync("SELECT p.codigo_posicion_origen Clave,m.numero_parte NumeroParte,e.codigo_estacion Estacion,r.codigo_estanteria Estanteria,u.nivel Nivel,pl.codigo_puesto Puesto FROM l1.POSICION p JOIN l1.MATERIAL m ON m.id_material=p.id_material JOIN l1.UBICACION u ON u.id_ubicacion=p.id_ubicacion JOIN l1.ESTANTERIA r ON r.id_estanteria=u.id_estanteria JOIN l1.ESTACION e ON e.id_estacion=r.id_estacion JOIN l1.PUESTO_LOGISTICO pl ON pl.id_puesto=p.id_puesto WHERE p.codigo_posicion_origen=@key",new{key},t);
  if(current is not null){if((string)current.NumeroParte!=part||(string)current.Estacion!=station||(string)current.Estanteria!=rack||(string)current.Nivel!=level||(string)current.Puesto!=post)throw new RuleException("La identidad de posición no puede reescribirse: usa una sustitución.",409);return;}
  string description="Material documentado · "+part;if(row["created_t"] is not null){var detail=JsonNode.Parse(row["data"]!.GetValue<string>())!;description=detail["descripcion"]?.GetValue<string>()??description;}
  await c.ExecuteAsync("""
   IF NOT EXISTS(SELECT 1 FROM l1.MATERIAL WHERE numero_parte=@part) INSERT l1.MATERIAL(numero_parte) VALUES(@part);
   IF NOT EXISTS(SELECT 1 FROM l1.ESTACION WHERE codigo_estacion=@station) INSERT l1.ESTACION(codigo_estacion) VALUES(@station);
   DECLARE @m int=(SELECT id_material FROM l1.MATERIAL WHERE numero_parte=@part),@e int=(SELECT id_estacion FROM l1.ESTACION WHERE codigo_estacion=@station);
   IF NOT EXISTS(SELECT 1 FROM l1.ESTANTERIA WHERE id_estacion=@e AND codigo_estanteria=@rack) INSERT l1.ESTANTERIA(id_estacion,codigo_estanteria) VALUES(@e,@rack);
   DECLARE @r int=(SELECT id_estanteria FROM l1.ESTANTERIA WHERE id_estacion=@e AND codigo_estanteria=@rack);
   IF NOT EXISTS(SELECT 1 FROM l1.UBICACION WHERE id_estanteria=@r AND nivel=@level) INSERT l1.UBICACION(id_estanteria,nivel) VALUES(@r,@level);
   INSERT l1.POSICION(codigo_posicion_origen,id_material,id_ubicacion,id_puesto,es_operable,descripcion_operativa,saldo_piezas,fecha_alta_utc) SELECT @key,@m,u.id_ubicacion,p.id_puesto,@operable,@description,CASE WHEN @operable=1 THEN 0 END,sistema.DESDE_MICROSEGUNDOS(@created) FROM l1.UBICACION u CROSS JOIN l1.PUESTO_LOGISTICO p WHERE u.id_estanteria=@r AND u.nivel=@level AND p.codigo_puesto=@post;
   """,new{key,part,station,rack,level,post,operable,description,created=row["created_t"]?.GetValue<long>()},t);
 }
 static async Task<string> EnsureActor(SqlConnection c,SqlTransaction t,JsonObject row){var (id,values)=ActorData(row);if(await c.QuerySingleAsync<int>("SELECT COUNT(*) FROM compat.ActorSnapshot WHERE Id=@id",new{id},t)==0){var data=ActorTable();data.Rows.Add(values.Select(v=>v??DBNull.Value).ToArray());await Bulk(c,"compat.ActorSnapshot",data,t);}return id;}
 public static async Task Append(SqlConnection c,SqlTransaction t,int user,string table,string key,JsonObject row){
  Validate(table,row);
  if(table=="events"&&await c.QuerySingleAsync<int>("SELECT COUNT(*) FROM compat.Evento WHERE id=@id",new{id=long.Parse(key,CultureInfo.InvariantCulture)},t)>0)throw new RuleException("No se puede sustituir un evento histórico.",409);
  if(table=="catalog")await EnsurePosition(c,t,row);
  if(table=="orders")await c.ExecuteAsync("IF NOT EXISTS(SELECT 1 FROM compat.Solicitud WHERE Id=@id) INSERT l1.SOLICITUD(id_solicitud,id_posicion) SELECT @id,id_posicion FROM l1.POSICION WHERE codigo_posicion_origen=@key;IF EXISTS(SELECT 1 FROM compat.Solicitud WHERE Id=@id AND Clave<>@key) THROW 51041,'La posición de una solicitud no se puede cambiar',1;",new{id=row["id"]!.GetValue<long>(),key=row["key"]!.GetValue<string>()},t);
  long revision=await c.QuerySingleAsync<long>("SELECT NEXT VALUE FOR l1.SECUENCIA_VERSION",transaction:t);
  
  string? actor=table=="events"?await EnsureActor(c,t,EventActor(row)):null;
  var data=EntityTable(table);data.Rows.Add(Values(table,row,revision,actor));data.Columns.Add("id_usuario_revision",typeof(int));data.Columns.Add("fecha_revision_utc",typeof(DateTime));data.Columns.Add("campos_origen",typeof(string));data.Columns.Add("es_base",typeof(bool));data.Rows[0]["id_usuario_revision"]=user;data.Rows[0]["fecha_revision_utc"]=DateTime.UtcNow;data.Rows[0]["campos_origen"]=Presence(row);data.Rows[0]["es_base"]=false;await Bulk(c,"l1."+Tables[table],data,t);
  if(table=="orders"){
   var stages=StageTable();var responsibles=row["responsibles"] is null?new JsonObject():JsonNode.Parse(row["responsibles"]!.GetValue<string>())!.AsObject();
   foreach(var k in responsibles.Select(p=>p.Key))if(!ActorStages.ContainsKey(k))throw new RuleException("Etapa de responsable no modelada: "+k,409);
   foreach(var stage in new[]{"start","accepted","transfer","delivered","closed","reserved_at","confirmed"}){
    string? personKey=ActorStages.FirstOrDefault(p=>p.Value==stage).Key;bool present=personKey is not null&&responsibles[personKey] is not null;
    string? person=present?await EnsureActor(c,t,responsibles[personKey!]!.AsObject()):null;
    stages.Rows.Add(revision,stage,row[stage]?.GetValue<long>() is long at?at:DBNull.Value,person is null?DBNull.Value:person,present);
   }
   await Bulk(c,"compat.SolicitudEtapa",stages,t);
  }
  if(table=="events"&&row["applied_roles"] is not null){int ordinal=0;foreach(var role in row["applied_roles"]!.GetValue<string>().Split(", ",StringSplitOptions.RemoveEmptyEntries)){
   int written=await c.ExecuteAsync("INSERT l1.REGISTRO_ROL(id_registro,orden_rol,id_rol) SELECT @id,@ordinal,id_rol FROM seguridad.ROL WHERE codigo_rol=@role",new{id=row["id"]!.GetValue<long>(),ordinal=ordinal++,role},t);if(written!=1)throw new RuleException("Rol aplicado desconocido",409);
  }}
  await c.ExecuteAsync("UPDATE sistema.HISTORICO_MAESTRO SET ultima_version=@revision WHERE id_historico=1",new{revision},t);
 }
}
