using System.Data;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Dapper;
using Microsoft.Data.SqlClient;
namespace Motherson;
public static partial class NormalizedJournal {
 public record Field(string Name,string SqlType);
 public static readonly Dictionary<string,string> Tables=new(){["catalog"]="CatalogoVersion",["events"]="Evento",["orders"]="SolicitudVersion",["stops"]="ParoVersion",["cuts"]="CierreVersion",["people"]="PersonaSimulada"};
 public static readonly Dictionary<string,Field[]> Fields=new(){["catalog"]=[new("key","nvarchar(80)"),new("factor","bigint"),new("capacity_boxes","int"),new("capacity","bigint"),new("minimum","bigint"),new("initial","bigint"),new("status","nvarchar(30)"),new("data","nvarchar(max)"),new("created_t","bigint")],["events"]=[new("id","bigint"),new("t","bigint"),new("before","bigint"),new("after","bigint"),new("delta","bigint"),new("demand","bigint"),new("missing","bigint"),new("boxes","bigint"),new("order_id","bigint"),new("local","nvarchar(64)"),new("kind","nvarchar(100)"),new("scenario","nvarchar(100)"),new("shift","nvarchar(20)"),new("actor","nvarchar(100)"),new("responsibility_role","nvarchar(100)"),new("responsibility_nature","nvarchar(100)"),new("origin","nvarchar(100)"),new("engine","nvarchar(100)"),new("alarm_state","nvarchar(100)"),new("alarm_policy","nvarchar(100)"),new("previous_alarm_state","nvarchar(100)"),new("key","nvarchar(80)"),new("user_id","int"),new("assignment_id","int"),new("consumption_ordinal","int"),new("seed","int"),new("authorization_user_id","int"),new("turn_access_id","int"),new("assignment_authorizer","int"),new("transaction_id","uniqueidentifier"),new("consumption_run","uniqueidentifier"),new("recorded_utc","datetime2(7)"),new("planned_utc","datetime2(7)"),new("assignment_until","datetime2(7)"),new("config_hash","char(64)"),new("is_cover","bit"),new("trigger_event_id","bigint"),new("referenceConsumed","bigint"),new("coverageMinutes","decimal(38,24)"),new("ratePiecesMinute","decimal(38,24)"),new("referenceMinutes","decimal(38,24)"),new("referenceStart","nvarchar(64)"),new("referenceEnd","nvarchar(64)"),new("evaluation_reason","nvarchar(500)"),new("samples","int"),new("severity","nvarchar(100)"),new("color","nvarchar(100)"),new("priority","nvarchar(100)"),new("policy","nvarchar(100)"),new("explanation","nvarchar(500)")],["orders"]=[new("id","bigint"),new("key","nvarchar(80)"),new("scenario","nvarchar(100)"),new("reserved_pieces","bigint"),new("boxes","int"),new("post","nvarchar(30)"),new("start_shift","nvarchar(20)"),new("working_minutes","decimal(38,24)"),new("generator","nvarchar(100)"),new("priority","nvarchar(50)"),new("priority_reason","nvarchar(100)"),new("priority_at","bigint"),new("priority_policy","nvarchar(100)"),new("priority_coverage","decimal(38,24)")],["stops"]=[new("id","bigint"),new("key","nvarchar(80)"),new("start","bigint"),new("end","bigint"),new("order_id","bigint"),new("scenario","nvarchar(100)"),new("cause","nvarchar(200)"),new("manual","int")],["cuts"]=[new("id","bigint"),new("t","bigint"),new("local","nvarchar(64)"),new("shift","nvarchar(20)"),new("key","nvarchar(80)"),new("stock","bigint"),new("minimum","bigint"),new("passed","int"),new("pending_order","bigint"),new("reserved","bigint"),new("stop_open","int"),new("user_id","int"),new("responsible_name","nvarchar(150)"),new("recorded_utc","datetime2(7)")],["people"]=[new("id","nvarchar(100)"),new("name","nvarchar(150)"),new("post","nvarchar(30)"),new("shift","nvarchar(20)"),new("data","nvarchar(max)")]};
 public static string Presence(JsonObject row)=>"|"+string.Join("|",row.Select(p=>p.Key).OrderBy(p=>p,StringComparer.Ordinal))+"|";
 public static object Value(JsonNode? v,string type){
  if(v is null)return DBNull.Value;
  var s=v.ToJsonString();if(type=="bit")return v.GetValue<bool>();
  if(type.StartsWith("decimal"))return decimal.Parse(v.ToJsonString(),NumberStyles.Float,CultureInfo.InvariantCulture);
  if(type=="int")return int.Parse(v.ToJsonString(),CultureInfo.InvariantCulture);if(type=="bigint")return long.Parse(v.ToJsonString(),CultureInfo.InvariantCulture);if(type=="float")return double.Parse(v.ToJsonString(),CultureInfo.InvariantCulture);
  if(type=="uniqueidentifier")return Guid.Parse(v.GetValue<string>());
  if(type.StartsWith("datetime2"))return DateTimeOffset.Parse(v.GetValue<string>(),CultureInfo.InvariantCulture).UtcDateTime;
  return v.GetValue<string>();
 }
 public static Type DataType(string type)=>type switch{"bit"=>typeof(bool),"int"=>typeof(int),"bigint"=>typeof(long),"float"=>typeof(double),"uniqueidentifier"=>typeof(Guid),_=>type.StartsWith("datetime2")?typeof(DateTime):type.StartsWith("decimal")?typeof(decimal):typeof(string)};
 public static void Validate(string table,JsonObject row){
  var allowed=Fields[table].Select(f=>f.Name).ToHashSet();
  if(table=="catalog")allowed.UnionWith(new[]{"part","station","rack","level","post"});
  if(table=="events")allowed.UnionWith(new[]{"responsible_id","responsible_name","responsible_shift","applied_roles"});
  if(table=="orders")allowed.UnionWith(new[]{"start","accepted","transfer","delivered","closed","reserved_at","responsibles"});
  foreach(var k in row.Select(p=>p.Key))if(!allowed.Contains(k))throw new RuleException("Campo no modelado: "+table+"."+k,409);
 }
}
