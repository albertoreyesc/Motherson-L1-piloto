using System.Data;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Dapper;
using Microsoft.Data.SqlClient;
namespace Motherson;
public record ConsumptionConfig(string[] PositionKeys,string Mode="REFERENCIA",int Seed=42,int EventsPerPosition=4,decimal Rate=120,long MinPieces=1,long MaxPieces=3,decimal Variation=.1m,string Reason="");
public record ConsumptionCommand(string Action,int ControlVersion);
public sealed partial class Store
{
    void ConsumptionPermission(Actor a){if(!Lab||!a.Permissions.Contains("admin"))throw new RuleException("El consumo simulado se controla desde Administración.",403);}
    public async Task<object> ConsumptionStatus(Actor actor,bool detail=false)
    {
        if(detail)ConsumptionPermission(actor);var access=await CentralAccess(actor);
        var jobs=await Query("""
        SELECT TOP(20) e.Id,e.Estado,e.ControlVersion,e.CreadoUtc,e.IniciadoUtc,e.ProximoUtc,e.UltimoError,COALESCE(e.AutorizadoPor,e.UsuarioId) UsuarioId,e.UsuarioId PreparadoPorId,u.Nombre Autor,
          COUNT(*) Total,SUM(CASE WHEN s.Estado<>'PENDIENTE' THEN 1 ELSE 0 END) Progreso,
          SUM(COALESCE(s.Aplicado,0)) Consumido,SUM(COALESCE(s.Faltante,0)) NoAtendido,
          SUM(CASE WHEN s.Estado='OMITIDO' THEN 1 ELSE 0 END) Omitidos
        FROM HistConsumoEjecuciones e JOIN HistConsumoPasos s ON s.EjecucionId=e.Id JOIN Usuarios u ON u.Id=COALESCE(e.AutorizadoPor,e.UsuarioId)
        WHERE (@wide=1 OR s.Clave IN @keys) AND (@detail=1 OR e.Estado IN('EN_CURSO','PAUSADA'))
        GROUP BY e.Id,e.Estado,e.ControlVersion,e.CreadoUtc,e.IniciadoUtc,e.ProximoUtc,e.UltimoError,e.UsuarioId,e.AutorizadoPor,u.Nombre
        ORDER BY e.CreadoUtc DESC
        """,new{access.wide,access.keys,detail});
        var rows=jobs.ToArray();
        if(detail)foreach(var row in rows)
        {
            var d=(IDictionary<string,object>)row;var configRow=(await Query("SELECT ConfigJson,PlanJson FROM HistConsumoEjecuciones WHERE Id=@id",new{id=(Guid)row.Id})).Single();
            using var cfg=JsonDocument.Parse((string)configRow.ConfigJson);using var plan=JsonDocument.Parse((string)configRow.PlanJson);d["Config"]=cfg.RootElement.Clone();d["Plan"]=new{engine=plan.RootElement.GetProperty("engine").GetString(),policy=plan.RootElement.GetProperty("policy").GetString(),assumptions=plan.RootElement.GetProperty("assumptions").Clone(),reference=plan.RootElement.GetProperty("reference").Clone()};
            d["Posiciones"]=await Query("SELECT h.Clave,h.Parte,h.Estacion,h.Estanteria,h.Nivel,p.Codigo Puesto FROM HistConsumoPosiciones s JOIN HistPosiciones h ON h.Clave=s.Clave JOIN PuestosLogisticos p ON p.Id=h.PuestoId WHERE s.EjecucionId=@id ORDER BY h.Parte",new{id=(Guid)row.Id});
            d["Pasos"]=await Query("SELECT TOP(12) Ordinal,Clave,Demanda,ProgramadoUtc,AplicadoUtc,Estado,Aplicado,Faltante,EventoId,Motivo FROM HistConsumoPasos WHERE EjecucionId=@id ORDER BY CASE WHEN Estado='PENDIENTE' THEN 0 ELSE 1 END,Ordinal",new{id=(Guid)row.Id});
        }
        return new{jobs=rows,policy="RELOJ_REAL_SERVIDOR",baseRunId=CentralHistory.MasterId};
    }
    public Task<string> PrepareConsumption(Actor actor,Guid key,ConsumptionConfig input,PythonSimulation python)=>Write(actor,key,input,"central-consumption-prepare",async(c,t)=>
    {
        ConsumptionPermission(actor);if(!Lab)throw new RuleException("Solo entorno simulado.",403);
        EvidenceRules.Reason(input.Reason);
        if(input.PositionKeys is null||input.PositionKeys.Length is <1 or >85||input.PositionKeys.Distinct().Count()!=input.PositionKeys.Length||input.EventsPerPosition<1||(long)input.EventsPerPosition*input.PositionKeys.Length>1000)throw new RuleException("Selecciona posiciones únicas y un máximo de 1000 eventos totales.",400);
        var now=await CentralLock(c,t);var catalog=new List<object>();
        foreach(string pos in input.PositionKeys)
        {
            var p=await Position(c,t,pos);if(p.RetiradoUtc is DateTime end&&end<=now)throw new RuleException("La posición está retirada.",409);
            catalog.Add(new{key=pos,capacity=(long)(int)p.CapacidadCajas*(long)p.Factor});
        }
        var report=Obj(await c.QuerySingleAsync<string>("SELECT ReportJson FROM HistEstado WHERE Id=1",transaction:t));
        var plan=await python.Execute(new{operation="central-continuation",config=input,catalog,calendar=report["config"],anchorUtc=DateTime.SpecifyKind(now,DateTimeKind.Utc)});
        var events=plan.GetProperty("events").EnumerateArray().ToArray();
        if(events.Length!=catalog.Count*input.EventsPerPosition||events.Any(e=>!input.PositionKeys.Contains(e.GetProperty("key").GetString())||e.GetProperty("demand").GetInt64()<=0||e.GetProperty("demand").GetInt64()>1000000))throw new RuleException("Propuesta inválida del motor.",400);
        var id=Guid.NewGuid();var config=JsonSerializer.Serialize(input,new JsonSerializerOptions(JsonSerializerDefaults.Web));
        await c.ExecuteAsync("INSERT sistema.EJECUCION_CONSUMO(id_ejecucion,codigo_historico,estado,id_usuario,configuracion_json,plan_json,cantidad_pasos,fecha_creacion_utc) VALUES(@id,@master,'PREPARADA',@uid,@config,@plan,@total,@now)",new{id,master=CentralHistory.MasterId,uid=actor.Id,config,plan=plan.GetRawText(),total=events.Length,now},t);
        await c.ExecuteAsync("INSERT sistema.ALCANCE_CONSUMO(id_ejecucion,id_posicion) VALUES(@id,(SELECT id_posicion FROM l1.POSICION WHERE codigo_posicion_origen=@pos))",input.PositionKeys.Select(pos=>new{id,pos}),t);
        await c.ExecuteAsync("INSERT sistema.PASO_CONSUMO(id_ejecucion,numero_paso,id_posicion,cantidad_demandada,fecha_programada_utc) VALUES(@id,@ordinal,(SELECT id_posicion FROM l1.POSICION WHERE codigo_posicion_origen=@pos),@qty,@at)",events.Select(e=>new{id,ordinal=e.GetProperty("ordinal").GetInt32(),pos=e.GetProperty("key").GetString(),qty=e.GetProperty("demand").GetInt64(),at=e.GetProperty("plannedUtc").GetDateTime().ToUniversalTime()}),t);
        return new{id,state="PREPARADA",total=events.Length,plan};
    });
    public Task<string> ControlConsumption(Actor actor,Guid key,Guid id,ConsumptionCommand input)=>Write(actor,key,new{id,input},"central-consumption-control",async(c,t)=>
    {
        ConsumptionPermission(actor);var now=await CentralLock(c,t);var job=await c.QuerySingleOrDefaultAsync("SELECT * FROM HistConsumoEjecuciones WITH(UPDLOCK,HOLDLOCK) WHERE Id=@id",new{id},t)??throw new RuleException("Ejecución inexistente.",404);
        if((int)job.ControlVersion!=input.ControlVersion)throw new RuleException("El control cambió; actualiza el estado antes de continuar.",409);
        string state=(string)job.Estado;long offset=(long)job.DesfaseMs;DateTime? next=null;string target;
        switch(input.Action)
        {
            case "start" when state=="PREPARADA":target="EN_CURSO";offset=(long)(now-(DateTime)job.CreadoUtc).TotalMilliseconds;break;
            case "resume" when state=="PAUSADA":target="EN_CURSO";offset+=(long)(now-(DateTime)job.PausadoUtc).TotalMilliseconds;break;
            case "pause" when state=="EN_CURSO":target="PAUSADA";break;
            case "stop" when state is "EN_CURSO" or "PAUSADA" or "PREPARADA":target="DETENIDA";break;
            default:throw new RuleException("La acción no corresponde al estado actual.",409);
        }
        if(target=="EN_CURSO")
        {
            int conflicts=await c.QuerySingleAsync<int>("SELECT COUNT(*) FROM HistConsumoPosiciones p JOIN HistConsumoPosiciones other ON p.Clave=other.Clave AND other.EjecucionId<>p.EjecucionId JOIN HistConsumoEjecuciones e ON e.Id=other.EjecucionId WHERE p.EjecucionId=@id AND e.Estado IN('EN_CURSO','PAUSADA')",new{id},t);
            if(conflicts>0)throw new RuleException("Otra ejecución activa o pausada utiliza alguna de estas posiciones.",409);
            using var plan=JsonDocument.Parse((string)job.PlanJson);if(MexicoAt(now).Date.ToString("yyyy-MM-dd").CompareTo(plan.RootElement.GetProperty("calendarEnd").GetString())>=0)throw new RuleException("Venció el calendario de la propuesta; prepara una nueva ejecución.",409);
            var planned=await c.QuerySingleAsync<DateTime>("SELECT ProgramadoUtc FROM HistConsumoPasos WHERE EjecucionId=@id AND Ordinal=@ordinal",new{id,ordinal=(int)job.Progreso+1},t);next=planned.AddMilliseconds(offset);
        }
        await c.ExecuteAsync("UPDATE sistema.EJECUCION_CONSUMO SET estado=@target,desfase_milisegundos=@offset,fecha_proximo_paso_utc=@next,version_control=version_control+1,fecha_inicio_utc=CASE WHEN @action='start' THEN @now ELSE fecha_inicio_utc END,fecha_pausa_utc=CASE WHEN @target='PAUSADA' THEN @now ELSE NULL END,fecha_fin_utc=CASE WHEN @target='DETENIDA' THEN @now ELSE NULL END,id_usuario_autorizador=CASE WHEN @target='EN_CURSO' THEN @uid ELSE id_usuario_autorizador END,ultimo_error=NULL WHERE id_ejecucion=@id",new{id,target,offset,next,action=input.Action,now,uid=actor.Id},t);
        return new{id,state=target,controlVersion=input.ControlVersion+1,serverUtc=DateTime.SpecifyKind(now,DateTimeKind.Utc)};
    });
    static DateTime? NextConsumptionWindow(JsonElement plan,DateTime now)
    {
        var local=MexicoAt(now);foreach(var d in plan.GetProperty("workDays").EnumerateArray())
        {
            var day=DateOnly.Parse(d.GetString()!);if(day< DateOnly.FromDateTime(local.DateTime))continue;
            var begin=TimeZoneInfo.ConvertTimeToUtc(day.ToDateTime(new TimeOnly(6,0)),Mexico);var end=TimeZoneInfo.ConvertTimeToUtc(day.ToDateTime(new TimeOnly(23,30)),Mexico);
            if(now<end)return now<begin?begin:now;
        }
        return null;
    }
    public async Task<bool> TickCentralConsumption(Guid id)
    {
        if(!Lab)return false;
        await using var c=await Open();await using var t=(SqlTransaction)await c.BeginTransactionAsync(IsolationLevel.Serializable);
        try
        {
            await AccessLock(c,t);var now=await CentralLock(c,t);var job=await c.QuerySingleAsync("SELECT * FROM HistConsumoEjecuciones WITH(UPDLOCK,HOLDLOCK) WHERE Id=@id",new{id},t);
            if((string)job.Estado!="EN_CURSO"||job.ProximoUtc is not DateTime due||due>now){await t.CommitAsync();return false;}
            var actor=await Actor((int)(job.AutorizadoPor??job.UsuarioId));ConsumptionPermission(actor);
            using var planDoc=JsonDocument.Parse((string)job.PlanJson);var plan=planDoc.RootElement;
            var window=NextConsumptionWindow(plan,now);
            if(window is null){await c.ExecuteAsync("UPDATE sistema.EJECUCION_CONSUMO SET estado='PAUSADA',fecha_pausa_utc=@now,fecha_proximo_paso_utc=NULL,version_control=version_control+1,ultimo_error='Venció el calendario de la propuesta' WHERE id_ejecucion=@id",new{id,now},t);await t.CommitAsync();return false;}
            if(window>now){await c.ExecuteAsync("UPDATE sistema.EJECUCION_CONSUMO SET fecha_proximo_paso_utc=@window,desfase_milisegundos=desfase_milisegundos+@wait WHERE id_ejecucion=@id",new{id,window,wait=(long)(window.Value-due).TotalMilliseconds},t);await t.CommitAsync();return false;}
            int ordinal=(int)job.Progreso+1;var step=await c.QuerySingleAsync("SELECT * FROM HistConsumoPasos WITH(UPDLOCK,HOLDLOCK) WHERE EjecucionId=@id AND Ordinal=@ordinal AND Estado='PENDIENTE'",new{id,ordinal},t);
            string pos=(string)step.Clave;var p=await Position(c,t,pos);long before=(long)p.Saldo,requested=(long)step.Demanda,applied=Math.Min(before,requested),missing=requested-applied,after=before-applied;
            bool retired=p.RetiradoUtc is DateTime until&&until<=now;
            string hash=Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes((string)job.ConfigJson)));
            JsonObject Provenance()=>new(){["consumption_run"]=id.ToString(),["consumption_ordinal"]=ordinal,["planned_utc"]=DateTime.SpecifyKind((DateTime)step.ProgramadoUtc,DateTimeKind.Utc).ToString("O"),["engine"]=plan.GetProperty("engine").GetString(),["seed"]=plan.GetProperty("config").GetProperty("seed").GetInt32(),["config_hash"]=hash,["authorization_user_id"]=actor.Id};
            long eventId;
            if(retired)
            {
                applied=missing=0;after=before;eventId=await Event(c,t,actor,now,"DEMANDA_OMITIDA",pos,before,before,process:"SIMULADOR_CONTINUACION",provenance:Provenance());
            }
            else
            {
                long? orderId=after<=(long)p.Minimo?await c.QuerySingleOrDefaultAsync<long?>("SELECT TOP(1) Id FROM HistOrdenActual WHERE Clave=@pos AND Cerrada=0 ORDER BY Inicio",new{pos},t):null;
                bool create=after<=(long)p.Minimo&&orderId is null;
                if(create){orderId=await c.QuerySingleAsync<long>("SELECT NEXT VALUE FOR SECUENCIA_SOLICITUD",transaction:t);await c.ExecuteAsync("INSERT l1.SOLICITUD(id_solicitud,id_posicion) VALUES(@orderId,(SELECT id_posicion FROM l1.POSICION WHERE codigo_posicion_origen=@pos))",new{orderId,pos},t);}
                eventId=await Event(c,t,actor,now,"CONSUMO",pos,before,after,-applied,order:orderId,demand:requested,missing:missing,process:"SIMULADOR_CONTINUACION",provenance:Provenance());
                await c.ExecuteAsync("UPDATE l1.POSICION SET saldo_piezas=@after WHERE codigo_posicion_origen=@pos",new{after,pos},t);
                if(orderId is not null)
                {
                    JsonObject order;
                    if(create)
                    {
                        var local=MexicoAt(now);order=new(){["id"]=orderId.Value,["key"]=pos,["scenario"]="CONTINUACION",["start"]=Micro(now),["accepted"]=null,["transfer"]=null,["delivered"]=null,["closed"]=null,["reserved_at"]=null,["reserved_pieces"]=0,["boxes"]=0,["post"]=(string)p.Puesto,["start_shift"]=CentralShift(local),["working_minutes"]=null,["generator"]="SIMULADOR_CONTINUACION",["responsibles"]=new JsonObject{["requestedBy"]=new JsonObject{["id"]="PROCESS:"+id,["name"]="Simulador de continuación",["post"]=(string)p.Puesto,["shift"]=CentralShift(local),["fictional"]=false,["nature"]="PROCESO"}}.ToJsonString()};
                        await Change(c,t,actor,"orders",orderId.Value.ToString(),order);
                        await Event(c,t,actor,now,"ALERTA",pos,after,after,order:orderId,process:"SIMULADOR_CONTINUACION",provenance:Provenance());
                        await Event(c,t,actor,now,"SOLICITUD",pos,after,after,order:orderId,process:"SIMULADOR_CONTINUACION",provenance:Provenance());
                    }
                    else order=await CentralRow(c,t,"orders",orderId.Value.ToString());
                    if(order["reserved_at"] is null)
                    {
                        int boxes=Math.Max(1,order["boxes"]?.GetValue<int>()??1);long reserved=await ReservedCentral(c,t,pos);
                        if((after+(long)p.Factor-1)/(long)p.Factor+reserved+boxes<=(int)p.CapacidadCajas)
                        {
                            order["boxes"]=boxes;order["reserved_at"]=Micro(now);order["reserved_pieces"]=boxes*(long)p.Factor;await Change(c,t,actor,"orders",orderId.Value.ToString(),order);
                            await Event(c,t,actor,now,"RESERVA_CAPACIDAD",pos,after,after,boxes:boxes,order:orderId,process:"SIMULADOR_CONTINUACION",provenance:Provenance());
                        }
                    }
                }
                if(missing>0&&await c.QuerySingleAsync<int>("SELECT COUNT(*) FROM l1.PARO s JOIN l1.POSICION p ON p.id_posicion=s.id_posicion WHERE p.codigo_posicion_origen=@pos AND s.fecha_fin_utc IS NULL AND NOT EXISTS(SELECT 1 FROM l1.PARO n WHERE n.id_paro=s.id_paro AND n.id_version>s.id_version)",new{pos},t)==0)
                {
                    long stop=await c.QuerySingleAsync<long>("SELECT NEXT VALUE FOR SECUENCIA_CIERRE",transaction:t);await Change(c,t,actor,"stops",stop.ToString(),new(){["id"]=stop,["key"]=pos,["start"]=Micro(now),["end"]=null,["order_id"]=orderId,["scenario"]="CONTINUACION",["cause"]="Desabasto simulado por material",["manual"]=0});
                    await Event(c,t,actor,now,"PARO_INICIO",pos,after,after,order:orderId,process:"SIMULADOR_CONTINUACION",provenance:Provenance());
                }
            }
            if(!retired)await EvaluateCentralAlarms(c,t,actor,now,new[]{pos},eventId,"Evaluación por consumo confirmado de la continuación");
            long offset=(long)job.DesfaseMs+Math.Max(0,(long)(now-due).TotalMilliseconds);DateTime? next=null;
            if(ordinal<(int)job.Total)next=(await c.QuerySingleAsync<DateTime>("SELECT ProgramadoUtc FROM HistConsumoPasos WHERE EjecucionId=@id AND Ordinal=@next",new{id,next=ordinal+1},t)).AddMilliseconds(offset);
            await c.ExecuteAsync("UPDATE sistema.PASO_CONSUMO SET estado=@state,fecha_aplicacion_utc=@now,id_registro=@eventId,cantidad_aplicada=@applied,cantidad_faltante=@missing,motivo=@reason WHERE id_ejecucion=@id AND numero_paso=@ordinal;UPDATE sistema.EJECUCION_CONSUMO SET pasos_aplicados=@ordinal,estado=CASE WHEN @next IS NULL THEN 'FINALIZADA' ELSE estado END,fecha_fin_utc=CASE WHEN @next IS NULL THEN @now ELSE NULL END,fecha_proximo_paso_utc=@next,desfase_milisegundos=@offset,ultimo_error=NULL WHERE id_ejecucion=@id;INSERT sistema.AUDITORIA(id_usuario,accion,entidad,detalle) VALUES(@uid,'central-consumption-event',@entity,@detail)",new{id,ordinal,now,eventId,applied,missing,state=retired?"OMITIDO":"APLICADO",reason=retired?"Posición retirada; no se aplicó la demanda":null,next,offset,uid=actor.Id,entity=id+":"+ordinal,detail=JsonSerializer.Serialize(new{demand=requested,applied,missing,pos,eventId,engine="python-discrete-continuation-v1"})},t);
            await t.CommitAsync();return true;
        }
        catch{await t.RollbackAsync();throw;}
    }
    public async Task PauseConsumptionError(Guid id,string error)
    {
        await using var c=await Open();await using var t=(SqlTransaction)await c.BeginTransactionAsync(IsolationLevel.Serializable);var now=await CentralLock(c,t);
        await c.ExecuteAsync("UPDATE sistema.EJECUCION_CONSUMO SET estado='PAUSADA',fecha_pausa_utc=@now,fecha_proximo_paso_utc=NULL,version_control=version_control+1,ultimo_error=@error WHERE id_ejecucion=@id AND estado='EN_CURSO';IF @@ROWCOUNT>0 INSERT sistema.AUDITORIA(id_usuario,accion,entidad,detalle) SELECT COALESCE(id_usuario_autorizador,id_usuario),'central-consumption-error',CONVERT(nvarchar(80),id_ejecucion),@error FROM sistema.EJECUCION_CONSUMO WHERE id_ejecucion=@id",new{id,now,error="Ejecución pausada sin avanzar el paso: "+error},t);await t.CommitAsync();
    }
}
public sealed class CentralConsumptionWorker(Store store,ILogger<CentralConsumptionWorker> logger):BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stop)
    {
        using var timer=new PeriodicTimer(TimeSpan.FromSeconds(1));
        while(await timer.WaitForNextTickAsync(stop))
        {
            if(!store.Lab)continue;
            try
            {
                var jobs=await store.Query("IF OBJECT_ID('HistConsumoEjecuciones') IS NOT NULL SELECT Id FROM HistConsumoEjecuciones WHERE Estado='EN_CURSO' AND ProximoUtc<=SYSUTCDATETIME()");
                foreach(var job in jobs)for(int n=0;n<16&&!stop.IsCancellationRequested;n++)
                {
                    try{if(!await store.TickCentralConsumption((Guid)job.Id))break;}
                    catch(Exception e){await store.PauseConsumptionError((Guid)job.Id,e.GetType().Name);logger.LogWarning(e,"Consumo {Id} pausado: {Type}",(Guid)job.Id,e.GetType().Name);break;}
                }
            }
            catch(OperationCanceledException) when(stop.IsCancellationRequested){break;}
            catch(Exception e){logger.LogWarning("Consulta de consumo: {Type}",e.GetType().Name);}
        }
    }
}
