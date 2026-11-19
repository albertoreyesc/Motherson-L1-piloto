using System.Data;
using System.Runtime.CompilerServices;
using System.Text.Json.Nodes;
using Dapper;
using Microsoft.Data.SqlClient;
namespace Motherson;
public static partial class NormalizedJournal {
 public record WireRow(string Table,string Key,long Revision,bool Base,JsonObject Row);
 public static async IAsyncEnumerable<WireRow> Read(SqlConnection c,SqlTransaction? transaction,string table,bool? baseOnly=null,long? after=null,long? through=null,bool current=false,[EnumeratorCancellation]CancellationToken cancel=default,string? identidad=null){
  var actors=new Dictionary<string,JsonObject>();var roles=new Dictionary<long,string>();var stages=new Dictionary<long,Dictionary<string,(long? at,string? actor,bool present)>>();
  if(table is "events" or "orders"){
   foreach(var a in await c.QueryAsync("SELECT * FROM compat.ActorSnapshot",transaction:transaction)){
    var source=(IDictionary<string,object>)a;var o=new JsonObject();var columns=new Dictionary<string,string>{{"id","Identificador"},{"name","Nombre"},{"post","Puesto"},{"shift","Turno"},{"fictional","Ficticio"},{"nature","Naturaleza"},{"validFrom","Desde"},{"validTo","Hasta"}};
    foreach(var field in ((string)a.Campos).Split('|',StringSplitOptions.RemoveEmptyEntries)){var v=source[columns[field]];o[field]=v is null?null:v is DateTime dt?JsonValue.Create(dt.ToString("yyyy-MM-dd")):JsonSerializerNode(v);}
    actors[(string)a.Id]=o;
   }
  }
  if(table=="events")foreach(var r in await c.QueryAsync("SELECT er.EventoId,STRING_AGG(CONVERT(nvarchar(max),r.Codigo),', ') WITHIN GROUP(ORDER BY er.Ordinal) Roles FROM compat.EventoRol er JOIN dbo.Roles r ON r.Id=er.RolId GROUP BY er.EventoId",transaction:transaction))roles[(long)r.EventoId]=r.Roles;
  if(table=="orders"){
   await using var stageCommand=c.CreateCommand();stageCommand.Transaction=transaction;stageCommand.CommandTimeout=300;stageCommand.CommandText=ConsultaModelo.Actuaciones(baseOnly,after,through,current,identidad);
   if(baseOnly.HasValue)stageCommand.Parameters.AddWithValue("@base",baseOnly.Value);if(after.HasValue)stageCommand.Parameters.AddWithValue("@after",after.Value);if(through.HasValue)stageCommand.Parameters.AddWithValue("@through",through.Value);
   if(identidad is not null)stageCommand.Parameters.AddWithValue("@identidad",identidad);await using var sr=await stageCommand.ExecuteReaderAsync(cancel);while(await sr.ReadAsync(cancel)){long id=sr.GetInt64(0);if(!stages.TryGetValue(id,out var map))stages[id]=map=new();map[sr.GetString(1)]=(sr.IsDBNull(2)?null:sr.GetInt64(2),sr.IsDBNull(3)?null:sr.GetString(3),sr.GetBoolean(4));}
  }
  await using var command=c.CreateCommand();command.Transaction=transaction;command.CommandTimeout=600;
  command.CommandText=ConsultaModelo.Leer(table,baseOnly,after,through,current,identidad);
  if(baseOnly.HasValue)command.Parameters.AddWithValue("@base",baseOnly.Value);if(after.HasValue)command.Parameters.AddWithValue("@after",after.Value);if(through.HasValue)command.Parameters.AddWithValue("@through",through.Value);
  if(identidad is not null)command.Parameters.AddWithValue("@identidad",identidad);await using var reader=await command.ExecuteReaderAsync(cancel);var ordinals=Enumerable.Range(0,reader.FieldCount).ToDictionary(reader.GetName,i=>i,StringComparer.Ordinal);
  while(await reader.ReadAsync(cancel)){
   long revision=reader.GetInt64(0);var row=new JsonObject();var fields=reader.GetString(3).Split('|',StringSplitOptions.RemoveEmptyEntries);
   object? Get(string name)=>ordinals.TryGetValue(name,out var i)&&!reader.IsDBNull(i)?reader.GetValue(i):null;
   JsonObject? actor=null;if(table=="events"&&Get("ActorId") is string actorId)actor=actors[actorId];
   foreach(var field in fields){
    JsonNode? value;
    if(table=="events"&&field.StartsWith("responsible_",StringComparison.Ordinal)){string property=field=="responsible_id"?"id":field=="responsible_name"?"name":"shift";value=actor?[property]?.DeepClone();}
    else if(table=="events"&&field=="applied_roles")value=roles.TryGetValue((long)Get("id")!,out var applied)?JsonValue.Create(applied):null;
    else if(table=="orders"&&field=="responsibles"){
     var people=new JsonObject();string? presence=Get("ResponsablesCampos") as string;
     if(presence is not null)foreach(var person in presence.Split('|',StringSplitOptions.RemoveEmptyEntries)){var s=stages[revision][ActorStages[person]];people[person]=s.present&&s.actor is not null?actors[s.actor].DeepClone():null;}
     value=JsonValue.Create(people.ToJsonString());
    }
    else if(table=="orders"&&new[]{"start","accepted","transfer","delivered","closed","reserved_at"}.Contains(field))value=stages[revision][field].at is long at?JsonValue.Create(at):null;
    else{var raw=Get(field);value=raw is DateTime dt?JsonValue.Create(DateTime.SpecifyKind(dt,DateTimeKind.Utc).ToString("O")):JsonSerializerNode(raw);}
    row[field]=value;
   }
   yield return new(table,reader.GetString(1),revision,reader.GetBoolean(2),row);
  }
 }
 static JsonNode? JsonSerializerNode(object? value)=>value is null?null:System.Text.Json.JsonSerializer.SerializeToNode(value);
}
