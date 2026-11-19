using System.Text.Json;
using System.Text.Json.Nodes;
using Dapper;
using Microsoft.Data.SqlClient;
namespace Motherson;
public record AlarmEvaluationInput(string[] PositionKeys,string Reason);
public sealed partial class Store
{
    const string AlarmPolicy="COBERTURA_60_30_15_V1";
    public Task<string> EvaluateAlarms(Actor actor,Guid requestKey,AlarmEvaluationInput input)=>Write(actor,requestKey,input,"central-alarms-evaluate",async(c,t)=>
    {
        if(!Lab||!actor.Permissions.Contains("admin"))throw new RuleException("La evaluación operativa requiere administración.",403);
        EvidenceRules.Reason(input.Reason);
        if(input.PositionKeys is null||input.PositionKeys.Length is <1 or >100||input.PositionKeys.Distinct().Count()!=input.PositionKeys.Length)throw new RuleException("Selecciona entre 1 y 100 posiciones únicas.",400);
        var now=await CentralLock(c,t);var rows=await EvaluateCentralAlarms(c,t,actor,now,input.PositionKeys,null,input.Reason);
        var local=MexicoAt(now);return new{rows,policy=AlarmPolicy,cutDate=local.ToString("yyyy-MM-dd"),cutTime=local.ToString("HH:mm:ss.ffffff")};
    });
    async Task<List<object>> EvaluateCentralAlarms(SqlConnection c,SqlTransaction t,Actor actor,DateTime now,string[] keys,long? trigger,string reason)
    {
        // Same Python policy and same source events as the read-only historical projection.
        var engine=new PythonSimulation(config);var report=Obj(await c.QuerySingleAsync<string>("SELECT ReportJson FROM HistEstado WHERE Id=1",transaction:t));
        var calendar=report["config"]!.DeepClone();var anchor=DateTime.SpecifyKind(now,DateTimeKind.Utc);
        var window=await engine.Execute(new{operation="alarm-window",calendar,anchorUtc=anchor});var segments=window.GetProperty("segments").EnumerateArray().ToArray();
        long lo=segments.Length>0?segments[0][0].GetInt64():Micro(now),hi=Micro(now);
        var positions=new List<dynamic>();foreach(var key in keys)positions.Add(await Position(c,t,key));
        var events=await c.QueryAsync("""
        SELECT [key],t,delta FROM compat.Evento WHERE [key] IN @keys AND t>@lo AND t<=@hi AND kind='CONSUMO'
        """,new{keys,lo,hi},t,commandTimeout:60);
        var evaluated=await engine.Execute(new{operation="alarm-evaluate",calendar,anchorUtc=anchor,positions=positions.Select(p=>new{key=(string)p.Clave,stock=(long)p.Saldo,createdT=(long?)p.CreadoT,eligible=p.RetiradoUtc is not DateTime until||until>now}),events=events.Select(e=>new{key=(string)e.key,t=(long)e.t,delta=(long)e.delta})});
        var result=new List<object>();
        foreach(var risk in evaluated.GetProperty("rows").EnumerateArray())
        {
            string key=risk.GetProperty("key").GetString()!,state=risk.GetProperty("severity").GetString()!;var p=positions.Single(x=>(string)x.Clave==key);long stock=(long)p.Saldo;
            var ultimo=await c.QuerySingleOrDefaultAsync<int?>("SELECT TOP(1) r.id_registro FROM l1.REGISTRO r JOIN l1.POSICION p ON p.id_posicion=r.id_posicion WHERE p.codigo_posicion_origen=@key AND r.tipo_registro IN('ALARMA_ACTIVADA','ALARMA_ESCALADA','ALARMA_RECUPERADA','ALARMA_ACTUALIZADA') ORDER BY r.fecha_evento_utc DESC,r.id_registro DESC",new{key},t);
            var lastRaw=ultimo is null?null:(await CentralRow(c,t,"events",ultimo.Value.ToString())).ToJsonString();
            var last=lastRaw is null?null:Obj(lastRaw);string? previous=last?["alarm_state"]?.GetValue<string>();
            long? orderId=null;bool created=false,escalated=false;JsonObject? order=null;
            bool urgent=state is "AGOTADO" or "RIESGO_PARO";
            if(urgent)
            {
                orderId=await c.QuerySingleOrDefaultAsync<long?>("SELECT TOP(1) Id FROM HistOrdenActual WHERE Clave=@key AND Cerrada=0 ORDER BY Inicio,Id",new{key},t);
                if(orderId is null)
                {
                    created=true;orderId=await c.QuerySingleAsync<long>("SELECT NEXT VALUE FOR SECUENCIA_SOLICITUD",transaction:t);var local=MexicoAt(now);
                    order=new(){["id"]=orderId,["key"]=key,["scenario"]="CONTINUACION",["start"]=Micro(now),["accepted"]=null,["transfer"]=null,["delivered"]=null,["closed"]=null,["reserved_at"]=null,["reserved_pieces"]=0,["boxes"]=1,["post"]=(string)p.Puesto,["start_shift"]=CentralShift(local),["working_minutes"]=null,["generator"]="MOTOR_ALARMAS",["responsibles"]=new JsonObject{["requestedBy"]=new JsonObject{["id"]="PROCESS:ALARMS",["name"]="Motor de alarmas",["post"]=(string)p.Puesto,["shift"]=CentralShift(local),["fictional"]=false,["nature"]="PROCESO"}}.ToJsonString()};
                }
                else order=await CentralRow(c,t,"orders",orderId.Value.ToString());
                escalated=order["priority"]?.GetValue<string>()!="ALTA / CRÍTICA";
                if(escalated||created)
                {
                    order["priority"]="ALTA / CRÍTICA";order["priority_reason"]=state;order["priority_at"]=Micro(now);order["priority_policy"]=AlarmPolicy;order["priority_coverage"]=JsonNode.Parse(risk.GetProperty("coverageMinutes").GetRawText());
                    await Change(c,t,actor,"orders",orderId.Value.ToString(),order);
                }
                if(created)await Event(c,t,actor,now,"SOLICITUD",key,stock,stock,order:orderId,process:"MOTOR_ALARMAS",provenance:new(){["origin"]="REGLAS_COBERTURA",["alarm_policy"]=AlarmPolicy});
                // Reserving a slot is independent of urgency and never changes stock.
                if(order["reserved_at"] is null)
                {
                    int boxes=Math.Max(1,order["boxes"]?.GetValue<int>()??1);long reserved=await ReservedCentral(c,t,key);
                    if((stock+(long)p.Factor-1)/(long)p.Factor+reserved+boxes<=(int)p.CapacidadCajas)
                    {
                        order["boxes"]=boxes;order["reserved_at"]=Micro(now);order["reserved_pieces"]=boxes*(long)p.Factor;await Change(c,t,actor,"orders",orderId.Value.ToString(),order);
                        await Event(c,t,actor,now,"RESERVA_CAPACIDAD",key,stock,stock,boxes:boxes,order:orderId,process:"MOTOR_ALARMAS",provenance:new(){["origin"]="REGLAS_COBERTURA"});
                    }
                }
            }
            bool notify=previous!=state&&(previous is not null||state is not ("NORMAL" or "SIN_DATOS"));
            if(notify||created||escalated)
            {
                string kind=state=="NORMAL"?"ALARMA_RECUPERADA":previous is null?"ALARMA_ACTIVADA":urgent&&previous is not ("AGOTADO" or "RIESGO_PARO")?"ALARMA_ESCALADA":"ALARMA_ACTUALIZADA";
                var facts=Obj(risk.GetRawText());facts.Remove("key");facts["alarm_state"]=state;facts["alarm_policy"]=AlarmPolicy;facts["trigger_event_id"]=trigger;facts["evaluation_reason"]=reason;facts["origin"]="REGLAS_COBERTURA";facts["previous_alarm_state"]=previous;
                await Event(c,t,actor,now,kind,key,stock,stock,order:orderId??last?["order_id"]?.GetValue<long>(),process:"MOTOR_ALARMAS",provenance:facts);
            }
            result.Add(new{key,severity=state,orderId,created,escalated,recorded=notify||created||escalated});
        }
        return result;
    }
}
