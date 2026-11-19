using System.Text.Json.Nodes;
using Dapper;
namespace Motherson;
public record CentralStopInput(string PositionKey,string Cause,string Action,long? StopId=null);
public record CentralCloseInput(string Shift,DateOnly Day);
public sealed partial class Store
{
    public Task<string> CentralStop(Actor actor,Guid key,CentralStopInput input)=>Write(actor,key,input,"central-stop",async(c,t)=>
    {
        if(!actor.Permissions.Contains("stops"))throw new RuleException("Incidencia reservada a líder o supervisor.",403);
        var now=await CentralLock(c,t);await RequireTurn(c,t,actor,now);var p=await Position(c,t,input.PositionKey);long id;string kind;
        if(input.Action=="start")
        {
            var cause=await c.QuerySingleOrDefaultAsync<string>("SELECT Nombre FROM SimCausas WHERE Codigo=@Cause AND Activa=1",input,t)??throw new RuleException("Selecciona una causa activa del catálogo.",400);
            if(await c.QuerySingleAsync<int>("SELECT COUNT(*) FROM l1.PARO s JOIN l1.POSICION p ON p.id_posicion=s.id_posicion WHERE p.codigo_posicion_origen=@PositionKey AND s.fecha_fin_utc IS NULL AND NOT EXISTS(SELECT 1 FROM l1.PARO n WHERE n.id_paro=s.id_paro AND n.id_version>s.id_version)",input,t)>0)throw new RuleException("La posición ya tiene una incidencia abierta.",409);
            id=await c.QuerySingleAsync<long>("SELECT NEXT VALUE FOR SECUENCIA_CIERRE",transaction:t);kind="PARO_INICIO";
            var row=new JsonObject{["id"]=id,["key"]=input.PositionKey,["start"]=Micro(now),["end"]=null,["order_id"]=null,["scenario"]="CONTINUACION",["cause"]=cause,["manual"]=1};await Change(c,t,actor,"stops",id.ToString(),row);
        }
        else if(input.Action=="close"&&input.StopId is not null)
        {
            id=input.StopId.Value;var row=await CentralRow(c,t,"stops",id.ToString());if(row["key"]?.GetValue<string>()!=input.PositionKey||row["end"] is not null)throw new RuleException("Incidencia cerrada o de otra posición.",409);row["end"]=Micro(now);await Change(c,t,actor,"stops",id.ToString(),row);kind="PARO_FIN";
        }
        else throw new RuleException("Acción de incidencia inválida.",400);
        await Event(c,t,actor,now,kind,input.PositionKey,(long)p.Saldo,(long)p.Saldo);var local=MexicoAt(now);return new{id,cutDate=local.ToString("yyyy-MM-dd"),cutTime=local.ToString("HH:mm:ss.ffffff")};
    });
    public Task<string> CentralClose(Actor actor,Guid key,CentralCloseInput input)=>Write(actor,key,input,"central-shift-close",async(c,t)=>
    {
        if(!actor.Permissions.Contains("admin"))throw new RuleException("Cierre reservado a administración.",403);
        var now=await CentralLock(c,t);var end=input.Shift switch{"PFEP T1"=>new TimeOnly(15,0),"PFEP T2"=>new TimeOnly(23,30),_=>throw new RuleException("Turno inválido.",400)};
        var localEnd=input.Day.ToDateTime(end);var utc=TimeZoneInfo.ConvertTimeToUtc(localEnd,Mexico);long at=Micro(utc);
        var baseline=await c.QuerySingleAsync<long>("SELECT BaseMaxT FROM HistEstado WHERE Id=1",transaction:t);
        if(at<=baseline||utc>now)throw new RuleException("Cierra un turno de la continuación que ya haya terminado.",409);
        if(await c.QuerySingleAsync<int>("SELECT COUNT(*) FROM HistCambios WHERE Tabla='cuts' AND TRY_CONVERT(bigint,JSON_VALUE(Json,'$.t'))=@at AND JSON_VALUE(Json,'$.shift')=@shift",new{at,shift=input.Shift},t)>0)throw new RuleException("Este cierre ya está registrado.",409);
        var positions=await c.QueryAsync("SELECT * FROM HistPosiciones WHERE CreadoT IS NULL OR CreadoT<=@at",new{at},t);int count=0;
        foreach(var p in positions)
        {
            string pos=(string)p.Clave;
            long stock=await c.QuerySingleAsync<long>("""
            SELECT COALESCE(
             (SELECT TOP(1) CAST(JSON_VALUE(Json,'$.after') AS bigint) FROM HistCambios WHERE Tabla='events' AND JSON_VALUE(Json,'$.key')=@pos AND TRY_CONVERT(bigint,JSON_VALUE(Json,'$.t'))<=@at ORDER BY TRY_CONVERT(bigint,JSON_VALUE(Json,'$.t')) DESC,TRY_CONVERT(bigint,Clave) DESC),
             (SELECT TOP(1) CAST(JSON_VALUE(Json,'$.after') AS bigint) FROM HistBase WHERE Tabla='events' AND Posicion=@pos AND Tiempo<=@at ORDER BY Tiempo DESC,EventoId DESC))
            """,new{pos,at},t);
            var pending=await c.QuerySingleOrDefaultAsync<long?>("SELECT TOP(1) Id FROM HistOrdenActual WHERE Clave=@pos AND Inicio<=@at AND (CierreT IS NULL OR CierreT>@at) ORDER BY Inicio",new{pos,at},t);
            long reserved=await c.QuerySingleAsync<long>("SELECT COALESCE(SUM(CAST(Cajas AS bigint)),0) FROM HistOrdenActual WHERE Clave=@pos AND ReservaT<=@at AND (CierreT IS NULL OR CierreT>@at)",new{pos,at},t);
            int open=await c.QuerySingleAsync<int>("SELECT COUNT(*) FROM l1.PARO s JOIN l1.POSICION p ON p.id_posicion=s.id_posicion WHERE p.codigo_posicion_origen=@pos AND s.fecha_inicio_utc<=sistema.DESDE_MICROSEGUNDOS(@at) AND (s.fecha_fin_utc IS NULL OR s.fecha_fin_utc>sistema.DESDE_MICROSEGUNDOS(@at)) AND NOT EXISTS(SELECT 1 FROM l1.PARO n WHERE n.id_paro=s.id_paro AND n.id_version>s.id_version)",new{pos,at},t);
            long id=await c.QuerySingleAsync<long>("SELECT NEXT VALUE FOR SECUENCIA_CIERRE",transaction:t);
            var row=new JsonObject{["id"]=id,["t"]=at,["local"]=MexicoAt(utc).ToString("O"),["shift"]=input.Shift,["key"]=pos,["stock"]=stock,["minimum"]=(long)p.Minimo,["passed"]=stock>=(long)p.Minimo?1:0,["pending_order"]=pending,["reserved"]=reserved*(long)p.Factor,["stop_open"]=open>0?1:0,["user_id"]=actor.Id,["responsible_name"]=actor.DisplayName??actor.Name,["recorded_utc"]=DateTime.SpecifyKind(now,DateTimeKind.Utc).ToString("O")};
            await Change(c,t,actor,"cuts",id.ToString(),row);count++;
        }
        return new{id=key,positions=count,cutDate=input.Day.ToString("yyyy-MM-dd"),cutTime=end.ToString("HH:mm:ss")};
    });
}
