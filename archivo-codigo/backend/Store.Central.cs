using System.Text.Json;
using System.Text.Json.Nodes;
using Dapper;
using Microsoft.Data.SqlClient;
namespace Motherson;
public record CentralEntryInput(string PositionKey,int Boxes,string ExpectedVersion,int AsignacionId,long? SolicitudId=null);
public record CentralAssignmentInput(int UsuarioId,string Clave,DateTimeOffset DesdeUtc,DateTimeOffset HastaUtc,bool EsCobertura,string Motivo,string? TurnoCodigo=null);
public record CentralAdjustmentInput(string PositionKey,long Delta,string ExpectedVersion,string Motivo);
public record CentralRequestInput(string PositionKey,int Boxes,string ExpectedVersion);
public record CentralStageInput(string Stage,string ExpectedVersion,int? AsignacionId=null);
public record CentralCatalogInput(string Parte,string Descripcion,string Estacion,string Estanteria,string Nivel,string Puesto,long Factor,int CapacidadCajas,long Minimo,string Motivo);
public record CentralRetireInput(string PositionKey,string? Sustituto,DateTimeOffset HastaUtc,string Motivo);
public sealed partial class Store
{
    static long Micro(DateTime utc)=>(DateTime.SpecifyKind(utc,DateTimeKind.Utc).Ticks-DateTime.UnixEpoch.Ticks)/10;
    static readonly TimeZoneInfo Mexico=TimeZoneInfo.FindSystemTimeZoneById("America/Mexico_City");
    static DateTimeOffset MexicoAt(DateTime utc)=>TimeZoneInfo.ConvertTime(new DateTimeOffset(DateTime.SpecifyKind(utc,DateTimeKind.Utc)),Mexico);
    static string CentralShift(DateTimeOffset local)=>local.TimeOfDay>=TimeSpan.FromHours(6)&&local.TimeOfDay<TimeSpan.FromHours(15)?"PFEP T1":local.TimeOfDay>=TimeSpan.FromHours(15)&&local.TimeOfDay<TimeSpan.FromHours(23.5)?"PFEP T2":"FUERA";
    static JsonObject Obj(string raw)=>JsonNode.Parse(raw)!.AsObject();
    async Task<DateTime> CentralLock(SqlConnection c,SqlTransaction t)
    {
        int n=await c.QuerySingleAsync<int>("DECLARE @r int;EXEC @r=sp_getapplock @Resource='HistCentral',@LockMode='Exclusive',@LockOwner='Transaction',@LockTimeout=10000;SELECT @r",transaction:t);
        if(n<0)throw new RuleException("El histórico está ocupado. Reintenta con la misma transacción.",409);
        var state=await c.QuerySingleOrDefaultAsync("SELECT BaseMaxT,Listo FROM HistEstado WHERE Id=1",transaction:t);
        if(state is null||!(bool)state.Listo)throw new RuleException("El histórico central no está listo.",503);
        var now=await c.QuerySingleAsync<DateTime>("SELECT SYSUTCDATETIME()",transaction:t);
        if(Micro(now)<=(long)state.BaseMaxT)throw new RuleException("El reloj del servidor debe estar después del último evento del histórico base.",409);
        return now;
    }
    async Task<JsonObject> CentralRow(SqlConnection c,SqlTransaction t,string table,string id)
    {
        await foreach(var registro in NormalizedJournal.Read(c,t,table,current:true,identidad:id))return registro.Row;
        throw new RuleException("Registro central inexistente.",404);
    }
    async Task Change(SqlConnection c,SqlTransaction t,Actor actor,string table,string id,JsonObject row)
    {
        await NormalizedJournal.Append(c,t,actor.Id,table,id,row);
    }

    async Task<long> Event(SqlConnection c,SqlTransaction t,Actor actor,DateTime now,string kind,string key,long before,long after,long delta=0,int boxes=0,long? order=null,int? assignment=null,Guid? transaction=null,long demand=0,long missing=0,string? process=null,JsonObject? provenance=null)
    {
        long id=await c.QuerySingleAsync<long>("SELECT NEXT VALUE FOR SECUENCIA_REGISTRO",transaction:t);var local=MexicoAt(now);string shift=CentralShift(local);
        var row=new JsonObject{["id"]=id,["t"]=Micro(now),["local"]=local.ToString("yyyy-MM-ddTHH:mm:ss.ffffffzzz"),["kind"]=kind,["key"]=key,["scenario"]="CONTINUACION",["order_id"]=order,["before"]=before,["after"]=after,["delta"]=delta,["demand"]=demand,["missing"]=missing,["boxes"]=boxes,["shift"]=shift,["actor"]=process??"APLICACION_MOTHERSON",["responsible_id"]=process is null?"USER:"+actor.Id:null,["responsible_name"]=process is null?actor.DisplayName??actor.Name:null,["responsible_shift"]=shift,["responsibility_role"]=kind,["responsibility_nature"]=process is null?"CUENTA_DE_ACCESO":"PROCESO",["origin"]=process is null?"CONTINUACION_MANUAL_SIMULADA":"CONSUMO_SIMULADO_CONTINUACION",["user_id"]=actor.Id,["assignment_id"]=assignment,["transaction_id"]=transaction?.ToString(),["recorded_utc"]=DateTime.SpecifyKind(now,DateTimeKind.Utc).ToString("O")};
        row["applied_roles"]=string.Join(", ",actor.Roles);
        if(assignment is not null){var a=await c.QuerySingleAsync("SELECT EsCobertura,AutorizadoPor,HastaUtc FROM HistAsignaciones WHERE Id=@id",new{id=assignment},t);row["is_cover"]=(bool)a.EsCobertura;row["assignment_authorizer"]=(int)a.AutorizadoPor;row["assignment_until"]=DateTime.SpecifyKind((DateTime)a.HastaUtc,DateTimeKind.Utc).ToString("O");row["turn_access_id"]=await c.QuerySingleOrDefaultAsync<int?>("SELECT TOP(1) Id FROM HistTurnosAcceso WHERE UsuarioId=@uid AND Codigo=@shift AND DesdeUtc<=@now AND HastaUtc>@now AND RevocadoUtc IS NULL ORDER BY DesdeUtc DESC",new{uid=actor.Id,shift,now},t);}
        if(provenance is not null)foreach(var item in provenance)row[item.Key]=item.Value?.DeepClone();
        await Change(c,t,actor,"events",id.ToString(),row);return id;
    }
    async Task<dynamic> Position(SqlConnection c,SqlTransaction t,string key,string? version=null)
    {
        var p=await c.QuerySingleOrDefaultAsync("SELECT p.*,pl.Codigo Puesto FROM HistPosiciones p WITH(UPDLOCK,HOLDLOCK) JOIN PuestosLogisticos pl ON pl.Id=p.PuestoId WHERE Clave=@key",new{key},t)??throw new RuleException("Posición fuera del histórico maestro o pendiente de validación.",404);
        if(version is not null)SimulationRules.Version(version,(byte[])p.Version);return p;
    }
    async Task<dynamic> CentralGrant(SqlConnection c,SqlTransaction t,Actor actor,string key,int? assignment,DateTime now)
    {
        if(!actor.Permissions.Contains("entry"))throw new RuleException("Sin permiso de entrada.",403);
        int turnAccess=await RequireTurn(c,t,actor,now);string shift=CentralShift(MexicoAt(now));
        var rows=(await c.QueryAsync("SELECT a.* FROM HistAsignaciones a JOIN HistPosiciones p ON p.Clave=a.Clave WHERE a.UsuarioId=@uid AND a.Clave=@key AND (@assignment IS NULL OR a.Id=@assignment) AND a.RevocadoUtc IS NULL AND a.DesdeUtc<=@now AND a.HastaUtc>@now AND (p.RetiradoUtc IS NULL OR p.RetiradoUtc>@now) AND (a.TurnoCodigo IS NULL OR a.TurnoCodigo=@shift) AND (@leader=0 OR a.EsCobertura=1 AND a.TurnoCodigo=@shift)",new{uid=actor.Id,key,assignment,shift,now,leader=actor.Roles.Contains("lider surtidores")},t)).ToArray();
        if(rows.Length!=1)throw new RuleException("Asignación central no vigente. El líder necesita cobertura autorizada.",403);((IDictionary<string,object>)rows[0])["TurnAccessId"]=turnAccess;return rows[0];
    }
    async Task<long> ReservedCentral(SqlConnection c,SqlTransaction t,string key,long? except=null)
    {
        return await c.QuerySingleAsync<long>("SELECT COALESCE(SUM(CAST(Cajas AS bigint)),0) FROM HistOrdenActual WHERE Clave=@key AND Cerrada=0 AND Reservada=1 AND (@except IS NULL OR Id<>@except)",new{key,except},t,commandTimeout:90);
    }
    public async Task<(bool wide,string[] posts,string[] keys)> CentralAccess(Actor actor)
    {
        bool wide=WideScope(actor);var allowed=(await Query("SELECT DISTINCT h.Clave,p.Codigo Puesto FROM HistPosiciones h JOIN PuestosLogisticos p ON p.Id=h.PuestoId WHERE EXISTS(SELECT 1 FROM HistAsignaciones a WHERE a.UsuarioId=@uid AND a.Clave=h.Clave AND a.RevocadoUtc IS NULL AND a.DesdeUtc<=SYSUTCDATETIME() AND a.HastaUtc>SYSUTCDATETIME()) OR EXISTS(SELECT 1 FROM HistEntradas e WHERE e.UsuarioId=@uid AND e.Clave=h.Clave)",new{uid=actor.Id})).ToArray();
        return(wide,allowed.Select(r=>(string)r.Puesto).Distinct().ToArray(),allowed.Select(r=>(string)r.Clave).ToArray());
    }
    public async Task<IEnumerable<dynamic>> CentralOptions(Actor actor)
    {
        var context=JsonSerializer.SerializeToElement(await AccessContext(actor));if(!context.GetProperty("canEnter").GetBoolean())return Array.Empty<dynamic>();string shift=context.GetProperty("activeShift").GetString()!;
        var rows=(await Query("SELECT p.Clave,p.Parte,p.Descripcion,p.Estacion,p.Estanteria,p.Nivel,pl.Codigo Puesto,p.Factor PiezasPorCaja,p.Saldo SaldoRegistradoPiezas,p.CapacidadCajas,CAST(p.CapacidadCajas AS bigint)*p.Factor CapacidadNominalPiezas,CONVERT(varchar(18),CONVERT(binary(8),p.Version),1) Version,a.Id AsignacionId,a.EsCobertura,a.HastaUtc,a.TurnoCodigo FROM HistAsignaciones a JOIN HistPosiciones p ON p.Clave=a.Clave JOIN PuestosLogisticos pl ON pl.Id=p.PuestoId WHERE a.UsuarioId=@uid AND a.RevocadoUtc IS NULL AND a.DesdeUtc<=SYSUTCDATETIME() AND a.HastaUtc>SYSUTCDATETIME() AND (p.RetiradoUtc IS NULL OR p.RetiradoUtc>SYSUTCDATETIME()) AND (a.TurnoCodigo IS NULL OR a.TurnoCodigo=@shift) AND (@leader=0 OR a.EsCobertura=1 AND a.TurnoCodigo=@shift) ORDER BY p.Estacion,p.Estanteria,p.Nivel,p.Parte",new{uid=actor.Id,shift,leader=actor.Roles.Contains("lider surtidores")})).ToArray();
        var reserves=(await Query("SELECT Clave,SUM(CAST(Cajas AS bigint)) Cajas FROM HistOrdenActual WHERE Reservada=1 AND Cerrada=0 GROUP BY Clave")).ToDictionary(r=>(string)r.Clave,r=>(long)r.Cajas);
        foreach(var r in rows)
        {
            var dict=(IDictionary<string,object>)r;string k=(string)r.Clave;
            long reserved=reserves.GetValueOrDefault(k);
            dict["CajasReservadas"]=reserved;dict["CajasDisponibles"]=(int)r.CapacidadCajas-((long)r.SaldoRegistradoPiezas+(long)r.PiezasPorCaja-1)/(long)r.PiezasPorCaja-reserved;dict["Ubicacion"]=$"{r.Estacion} / {r.Estanteria} / {r.Nivel}";
        }
        return rows;
    }
    public async Task<IEnumerable<dynamic>> CentralReservations(Actor actor)
    {
        var options=(await CentralOptions(actor)).Select(r=>(string)r.Clave).ToHashSet();
        var orders=await Query("SELECT Id SolicitudId,Clave,Cajas FROM HistOrdenActual WHERE Cerrada=0 AND Reservada=1");
        return orders.Where(r=>options.Contains((string)r.Clave));
    }
    public Task<string> CentralEntry(Actor actor,Guid requestKey,CentralEntryInput input)=>Write(actor,requestKey,input,"central-entry",async(c,t)=>
    {
        var now=await CentralLock(c,t);var p=await Position(c,t,input.PositionKey,input.ExpectedVersion);var grant=await CentralGrant(c,t,actor,input.PositionKey,input.AsignacionId,now);
        long? orderId=input.SolicitudId;JsonObject? order=null;int boxes=input.Boxes;
        if(orderId is not null)
        {
            order=await CentralRow(c,t,"orders",orderId.Value.ToString());if(order["key"]?.GetValue<string>()!=input.PositionKey||order["closed"] is not null||order["reserved_at"] is null)throw new RuleException("Solicitud cerrada, no reservada o de otra posición.",409);
            boxes=order["boxes"]!.GetValue<int>();if(boxes!=input.Boxes)throw new RuleException("Las cajas deben coincidir con la reserva.",409);
        }
        long delta=Rules.Delta("ENTRADA",boxes,(long)p.Factor);long after=Rules.Balance((long)p.Saldo,delta,true,"ENTRADA",checked((long)(int)p.CapacidadCajas*(long)p.Factor));
        long reserved=await ReservedCentral(c,t,input.PositionKey,orderId),occupied=((long)p.Saldo+(long)p.Factor-1)/(long)p.Factor;
        if(occupied+reserved+boxes>(int)p.CapacidadCajas)throw new RuleException("Capacidad comprometida por ocupación y reservas.",409);
        var id=Guid.NewGuid();var local=MexicoAt(now);var shift=CentralShift(local);long eventId=await Event(c,t,actor,now,"ENTRADA_CONFIRMADA",input.PositionKey,(long)p.Saldo,after,delta,boxes,orderId,(int)grant.Id,id);
        await c.ExecuteAsync("UPDATE l1.POSICION SET saldo_piezas=@after WHERE codigo_posicion_origen=@key;INSERT l1.COMPROBANTE_ENTRADA(id_registro,id_transaccion,id_posicion,id_usuario,id_asignacion,cantidad_cajas,piezas_por_caja,saldo_anterior,saldo_posterior,cantidad_movimiento,origen,fecha_registro_utc,id_turno,fecha_operativa,id_solicitud) VALUES(@eventId,@id,(SELECT id_posicion FROM l1.POSICION WHERE codigo_posicion_origen=@key),@uid,@assignment,@boxes,@factor,@before,@after,@delta,'ENTRADA_MANUAL_SIMULADA',@now,(SELECT id_turno FROM l1.TURNO WHERE codigo_turno=@shift),@day,@orderId)",new{eventId,id,key=input.PositionKey,uid=actor.Id,assignment=(int)grant.Id,boxes,factor=(long)p.Factor,before=(long)p.Saldo,after,delta,now,shift,day=local.Date,orderId},t);
        if(order is not null)
        {
            order["delivered"]=Micro(now);order["closed"]=Micro(now);var snapshots=Obj(order["responsibles"]?.GetValue<string>()??"{}");
            var who=new JsonObject{["id"]="USER:"+actor.Id,["name"]=actor.DisplayName??actor.Name,["post"]=(string)p.Puesto,["shift"]=shift,["fictional"]=false};snapshots["deliveredBy"]=who.DeepClone();snapshots["confirmedBy"]=who.DeepClone();order["responsibles"]=snapshots.ToJsonString();
            await Change(c,t,actor,"orders",orderId!.Value.ToString(),order);await Event(c,t,actor,now,"CIERRE_ORDEN",input.PositionKey,after,after,order:orderId,assignment:(int)grant.Id);
        }
        var stops=await c.QueryAsync<int>("SELECT s.id_paro FROM l1.PARO s JOIN l1.POSICION p ON p.id_posicion=s.id_posicion WHERE p.codigo_posicion_origen=@key AND s.fecha_fin_utc IS NULL AND NOT EXISTS(SELECT 1 FROM l1.PARO n WHERE n.id_paro=s.id_paro AND n.id_version>s.id_version)",new{key=input.PositionKey},t);
        if(after>0)foreach(var stop in stops){var row=await CentralRow(c,t,"stops",stop.ToString());if(row["manual"]?.GetValue<int>()==1)continue;row["end"]=Micro(now);await Change(c,t,actor,"stops",stop.ToString(),row);await Event(c,t,actor,now,"PARO_FIN",input.PositionKey,after,after,order:row["order_id"]?.GetValue<long>());}
        await EvaluateCentralAlarms(c,t,actor,now,new[]{input.PositionKey},eventId,"Evaluación tras entrada confirmada");
        return new{id,eventoId=eventId,saldoPiezas=after,saldoAnteriorPiezas=(long)p.Saldo,deltaPiezas=delta,cajas=boxes,factor=(long)p.Factor,registradoUtc=DateTime.SpecifyKind(now,DateTimeKind.Utc),cutDate=local.ToString("yyyy-MM-dd"),cutTime=local.ToString("HH:mm:ss.ffffff"),positionKey=input.PositionKey,usuarioId=actor.Id,asignacionId=(int)grant.Id,origen="CONTINUACION_MANUAL_SIMULADA",simulado=true};
    });
    public Task<string> CentralAssign(Actor actor,Guid key,CentralAssignmentInput input)=>Write(actor,key,input,"central-assignment",async(c,t)=>
    {
        if(!actor.Permissions.Contains("assign"))throw new RuleException("Sin permiso para asignar.",403);await CentralLock(c,t);
        if(input.HastaUtc<=input.DesdeUtc||input.HastaUtc<=DateTimeOffset.UtcNow||string.IsNullOrWhiteSpace(input.Motivo)||input.Motivo.Length>500)throw new RuleException("Indica vigencia y motivo válidos.",400);
        await Position(c,t,input.Clave);var roles=(await c.QueryAsync<string>("SELECT r.Codigo FROM UsuarioRol ur JOIN Roles r ON r.Id=ur.RolId JOIN Usuarios u ON u.Id=ur.UsuarioId WHERE u.Id=@id AND u.Activo=1",new{id=input.UsuarioId},t)).ToArray();
        if(input.EsCobertura?!roles.Contains("lider surtidores"):roles.Contains("lider surtidores")||!roles.Any(r=>new[]{"administrador","materialista","liberador","kitting","montacargas","secuenciador","surtidor SZ01","surtidor SZ02"}.Contains(r)))throw new RuleException("El perfil no admite este alcance; el líder requiere cobertura.",403);
        if(input.TurnoCodigo is not null)ProfileRules.Shift(input.TurnoCodigo);
        if(input.EsCobertura&&input.TurnoCodigo is null)throw new RuleException("La cobertura del líder requiere turno explícito.",400);
        if(input.EsCobertura&&input.HastaUtc-input.DesdeUtc>TimeSpan.FromDays(7))throw new RuleException("Cobertura de hasta siete días por autorización.",400);
        if(await c.QuerySingleAsync<int>("SELECT COUNT(*) FROM HistAsignaciones WHERE UsuarioId=@UsuarioId AND Clave=@Clave AND RevocadoUtc IS NULL AND (TurnoCodigo IS NULL OR @turno IS NULL OR TurnoCodigo=@turno) AND DesdeUtc<@end AND HastaUtc>@start",new{input.UsuarioId,input.Clave,turno=input.TurnoCodigo,start=input.DesdeUtc.UtcDateTime,end=input.HastaUtc.UtcDateTime},t)>0)throw new RuleException("Existe una asignación que se solapa; revócala antes de sustituirla.",409);
        var id=await c.QuerySingleAsync<int>("DECLARE @nuevo TABLE(id int);INSERT seguridad.ASIGNACION(id_usuario,id_posicion,fecha_inicio_utc,fecha_fin_utc,es_cobertura,motivo,id_usuario_autorizador,id_turno) OUTPUT inserted.id_asignacion INTO @nuevo VALUES(@UsuarioId,(SELECT id_posicion FROM l1.POSICION WHERE codigo_posicion_origen=@Clave),@start,@end,@EsCobertura,@Motivo,@uid,(SELECT id_turno FROM l1.TURNO WHERE codigo_turno=@TurnoCodigo));SELECT id FROM @nuevo",new{input.UsuarioId,input.Clave,start=input.DesdeUtc.UtcDateTime,end=input.HastaUtc.UtcDateTime,input.EsCobertura,input.Motivo,input.TurnoCodigo,uid=actor.Id},t);return new{id};
    });
    public Task<string> CentralRevoke(Actor actor,Guid key,int id)=>Write(actor,key,new{id},"central-revoke",async(c,t)=>
    {
        if(!actor.Permissions.Contains("assign"))throw new RuleException("Sin permiso para revocar.",403);await CentralLock(c,t);
        if(await c.ExecuteAsync("UPDATE seguridad.ASIGNACION SET fecha_revocacion_utc=SYSUTCDATETIME(),id_usuario_revocador=@uid WHERE id_asignacion=@id AND fecha_revocacion_utc IS NULL",new{id,uid=actor.Id},t)!=1)throw new RuleException("Asignación ya revocada o inexistente.",409);return new{id};
    });
    public async Task<object> CentralAdmin(Actor actor)
    {
        bool manage=actor.Permissions.Any(p=>p is "admin" or "assign" or "catalog");if(!manage)throw new RuleException("Acceso de administración restringido.",403);
        return new{roles=ProfileRules.Roles,turnAssignments=actor.Permissions.Contains("assign")?await Query("SELECT a.*,u.Nombre,u.NombreVisible FROM HistTurnosAcceso a JOIN Usuarios u ON u.Id=a.UsuarioId ORDER BY a.Id DESC"):[],users=await Query("SELECT u.Id,u.Nombre,u.NombreVisible,u.Activo,u.CuentaSimulada,r.Codigo Rol FROM Usuarios u JOIN UsuarioRol ur ON ur.UsuarioId=u.Id JOIN Roles r ON r.Id=ur.RolId"),positions=await Query("SELECT h.*,p.Codigo Puesto FROM HistPosiciones h JOIN PuestosLogisticos p ON p.Id=h.PuestoId ORDER BY Parte,Estacion,Estanteria,Nivel"),assignments=actor.Permissions.Contains("assign")?await Query("SELECT a.*,u.Nombre,h.Parte,h.Estacion,h.Estanteria,h.Nivel,p.Codigo Puesto FROM HistAsignaciones a JOIN Usuarios u ON u.Id=a.UsuarioId JOIN HistPosiciones h ON h.Clave=a.Clave JOIN PuestosLogisticos p ON p.Id=h.PuestoId ORDER BY a.Id DESC"):[],permissions=actor.Permissions.Contains("admin")?await Query("SELECT r.Codigo Rol,p.Permiso FROM Roles r JOIN RolPermiso p ON p.RolId=r.Id"):[],audit=actor.Permissions.Contains("admin")?await Query("SELECT TOP(100) * FROM Auditoria WHERE Accion LIKE 'central-%' OR Accion='user-create' ORDER BY Id DESC"):[]};
    }
}

public sealed partial class Store
{
    public Task<string> CentralRequest(Actor actor,Guid idempotency,CentralRequestInput input)=>Write(actor,idempotency,input,"central-request",async(c,t)=>
    {
        if(!actor.Permissions.Contains("request"))throw new RuleException("Sin permiso para crear solicitudes.",403);
        var now=await CentralLock(c,t);var p=await Position(c,t,input.PositionKey,input.ExpectedVersion);
        if(p.RetiradoUtc is DateTime end&&end<=now)throw new RuleException("Material retirado para nuevas solicitudes.",409);
        if(await c.QuerySingleAsync<int>("SELECT COUNT(*) FROM HistOrdenActual WHERE Clave=@key AND Cerrada=0",new{key=input.PositionKey},t)>0)throw new RuleException("Ya existe una solicitud abierta para esta posición; continúa su atención.",409);
        await RequireTurn(c,t,actor,now);
        Rules.Delta("ENTRADA",input.Boxes,(long)p.Factor);
        long reserved=await ReservedCentral(c,t,input.PositionKey),occupied=((long)p.Saldo+(long)p.Factor-1)/(long)p.Factor;
        if(occupied+reserved+input.Boxes>(int)p.CapacidadCajas)throw new RuleException("Espacio insuficiente para reservar esas cajas.",409);
        long id=await c.QuerySingleAsync<long>("SELECT NEXT VALUE FOR SECUENCIA_SOLICITUD",transaction:t);var local=MexicoAt(now);
        var who=new JsonObject{["id"]="USER:"+actor.Id,["name"]=actor.DisplayName??actor.Name,["post"]=(string)p.Puesto,["shift"]=CentralShift(local),["fictional"]=false};
        var row=new JsonObject{["id"]=id,["key"]=input.PositionKey,["scenario"]="CONTINUACION",["start"]=Micro(now),["accepted"]=null,["transfer"]=null,["delivered"]=null,["closed"]=null,["reserved_at"]=Micro(now),["reserved_pieces"]=input.Boxes*(long)p.Factor,["boxes"]=input.Boxes,["post"]=(string)p.Puesto,["start_shift"]=CentralShift(local),["working_minutes"]=null,["responsibles"]=new JsonObject{["requestedBy"]=who}.ToJsonString()};
        await Change(c,t,actor,"orders",id.ToString(),row);await Event(c,t,actor,now,"SOLICITUD",input.PositionKey,(long)p.Saldo,(long)p.Saldo,order:id);await Event(c,t,actor,now,"RESERVA_CAPACIDAD",input.PositionKey,(long)p.Saldo,(long)p.Saldo,boxes:input.Boxes,order:id);await c.ExecuteAsync("UPDATE l1.POSICION SET saldo_piezas=saldo_piezas WHERE codigo_posicion_origen=@key",new{key=input.PositionKey},t);
        return new{id,cutDate=local.ToString("yyyy-MM-dd"),cutTime=local.ToString("HH:mm:ss.ffffff"),reservada=true};
    });
    public Task<string> CentralStage(Actor actor,Guid key,long id,CentralStageInput input)=>Write(actor,key,new{id,input},"central-stage",async(c,t)=>
    {
        if(!actor.Roles.Any(r=>r is "administrador" or "lider surtidores"))throw new RuleException("El surtidor registra entradas; las demás etapas requieren líder con cobertura o administrador.",403);
        var now=await CentralLock(c,t);var order=await CentralRow(c,t,"orders",id.ToString());string pos=order["key"]!.GetValue<string>();var p=await Position(c,t,pos,input.ExpectedVersion);var grant=await CentralGrant(c,t,actor,pos,input.AsignacionId,now);
        if(order["closed"] is not null)throw new RuleException("La solicitud ya está cerrada.",409);
        string field,kind,role;
        switch(input.Stage){case "accept":field="accepted";kind="ACEPTACION";role="acceptedBy";break;case "transfer":field="transfer";kind="TRASLADO";role="transferredBy";if(order["accepted"] is null)throw new RuleException("Acepta la solicitud antes del traslado.",409);break;case "reserve":field="reserved_at";kind="RESERVA_CAPACIDAD";role="";break;default:throw new RuleException("Etapa no válida.",400);}
        if(order[field] is not null)throw new RuleException("La etapa ya está registrada.",409);
        int boxes=order["boxes"]?.GetValue<int>()??0;if(boxes<=0)boxes=1;
        if(field=="reserved_at")
        {
            long reserved=await ReservedCentral(c,t,pos),occupied=((long)p.Saldo+(long)p.Factor-1)/(long)p.Factor;
            if(occupied+reserved+boxes>(int)p.CapacidadCajas)throw new RuleException("Aún no cabe la caja; conserva la solicitud pendiente.",409);
            order["boxes"]=boxes;order["reserved_pieces"]=boxes*(long)p.Factor;
        }
        var local=MexicoAt(now);order[field]=Micro(now);
        if(role!=""){var snapshots=Obj(order["responsibles"]?.GetValue<string>()??"{}");snapshots[role]=new JsonObject{["id"]="USER:"+actor.Id,["name"]=actor.DisplayName??actor.Name,["post"]=(string)p.Puesto,["shift"]=CentralShift(local),["fictional"]=false};order["responsibles"]=snapshots.ToJsonString();}
        await Change(c,t,actor,"orders",id.ToString(),order);await Event(c,t,actor,now,kind,pos,(long)p.Saldo,(long)p.Saldo,boxes:field=="reserved_at"?boxes:0,order:id,assignment:(int)grant.Id);await c.ExecuteAsync("UPDATE l1.POSICION SET saldo_piezas=saldo_piezas WHERE codigo_posicion_origen=@pos",new{pos},t);
        return new{id,stage=input.Stage,cutDate=local.ToString("yyyy-MM-dd"),cutTime=local.ToString("HH:mm:ss.ffffff")};
    });
    public Task<string> CentralAdjust(Actor actor,Guid key,CentralAdjustmentInput input)=>Write(actor,key,input,"central-adjustment",async(c,t)=>
    {
        if(!actor.Permissions.Contains("admin"))throw new RuleException("Ajuste reservado a administración.",403);EvidenceRules.Reason(input.Motivo);if(input.Delta==0)throw new RuleException("El ajuste debe cambiar el saldo.",400);
        var now=await CentralLock(c,t);var p=await Position(c,t,input.PositionKey,input.ExpectedVersion);long after=checked((long)p.Saldo+input.Delta);if(after<0||after>(long)(int)p.CapacidadCajas*(long)p.Factor)throw new RuleException("El ajuste excede los límites de saldo.",409);
        long reserved=await ReservedCentral(c,t,input.PositionKey);if((after+(long)p.Factor-1)/(long)p.Factor+reserved>(int)p.CapacidadCajas)throw new RuleException("El ajuste invade capacidad reservada.",409);
        var id=Guid.NewGuid();long eventId=await Event(c,t,actor,now,"AJUSTE_AUDITADO",input.PositionKey,(long)p.Saldo,after,input.Delta,transaction:id);var local=MexicoAt(now);
        await c.ExecuteAsync("UPDATE l1.POSICION SET saldo_piezas=@after WHERE codigo_posicion_origen=@pos;INSERT l1.COMPROBANTE_ENTRADA(id_registro,id_transaccion,id_posicion,id_usuario,id_asignacion,cantidad_cajas,piezas_por_caja,saldo_anterior,saldo_posterior,cantidad_movimiento,origen,fecha_registro_utc,id_turno,fecha_operativa,id_solicitud) VALUES(@eventId,@id,(SELECT id_posicion FROM l1.POSICION WHERE codigo_posicion_origen=@pos),@uid,NULL,NULL,1,@before,@after,@delta,'AJUSTE_MANUAL_SIMULADO',@now,(SELECT id_turno FROM l1.TURNO WHERE codigo_turno=@shift),@day,NULL)",new{after,pos=input.PositionKey,eventId,id,uid=actor.Id,before=(long)p.Saldo,delta=input.Delta,now,shift=CentralShift(local),day=local.Date},t);
        return new{id,eventoId=eventId,saldoPiezas=after,saldoAnteriorPiezas=(long)p.Saldo,registradoUtc=DateTime.SpecifyKind(now,DateTimeKind.Utc),cutDate=local.ToString("yyyy-MM-dd"),cutTime=local.ToString("HH:mm:ss.ffffff")};
    });
    public Task<string> CentralCatalog(Actor actor,Guid key,CentralCatalogInput input)=>Write(actor,key,input,"central-catalog",async(c,t)=>
    {
        if(!actor.Permissions.Contains("catalog"))throw new RuleException("Sin permiso de catálogo.",403);EvidenceRules.Reason(input.Motivo);
        if(new[]{input.Parte,input.Descripcion,input.Estacion,input.Estanteria,input.Nivel}.Any(string.IsNullOrWhiteSpace)||input.Parte.Length>100||input.Descripcion.Length>300||input.Estacion.Length>80||input.Estanteria.Length>80||input.Nivel.Length>30||input.CapacidadCajas<=0||input.Minimo<0)throw new RuleException("Completa los datos del material y la posición de Línea 1.",400);
        Rules.Delta("ENTRADA",1,input.Factor);long capacity=checked((long)input.CapacidadCajas*input.Factor);if(capacity>int.MaxValue)throw new RuleException("La capacidad indicada excede el límite permitido.",400);if(input.Minimo>capacity)throw new RuleException("El mínimo supera la capacidad.",400);
        var now=await CentralLock(c,t);int post=await c.QuerySingleOrDefaultAsync<int>("SELECT Id FROM PuestosLogisticos WHERE Codigo=@Puesto",input,t);if(post==0)throw new RuleException("Puesto fuera de los seis autorizados.",400);
        if(await c.QuerySingleAsync<int>("SELECT COUNT(*) FROM HistPosiciones WHERE Parte=@Parte AND Estacion=@Estacion AND Estanteria=@Estanteria AND Nivel=@Nivel AND PuestoId=@post",new{input.Parte,input.Estacion,input.Estanteria,input.Nivel,post},t)>0)throw new RuleException("La combinación ya existe.",409);
        string pos="POS-"+Guid.NewGuid().ToString("N")[..20];
        var detail=new JsonObject{["key"]=pos,["parte"]=input.Parte,["estacion"]=input.Estacion,["estanteria"]=input.Estanteria,["nivel"]=input.Nivel,["puesto"]=input.Puesto,["factor"]=input.Factor,["capacidadCajas"]=input.CapacidadCajas,["capacidadPiezas"]=capacity,["simulationStatus"]="INCLUIDO",["positionNature"]="LOGICA_SIMULADA",["reviewReasons"]=new JsonArray("Alta manual simulada; validar posición física"),["descripcion"]=input.Descripcion};
        var row=new JsonObject{["key"]=pos,["part"]=input.Parte,["station"]=input.Estacion,["rack"]=input.Estanteria,["level"]=input.Nivel,["post"]=input.Puesto,["factor"]=input.Factor,["capacity_boxes"]=input.CapacidadCajas,["capacity"]=capacity,["minimum"]=input.Minimo,["initial"]=0,["status"]="INCLUIDO",["data"]=detail.ToJsonString(),["created_t"]=Micro(now)};
        await Change(c,t,actor,"catalog",pos,row);
        await c.ExecuteAsync("UPDATE l1.POSICION SET descripcion_operativa=@Descripcion WHERE codigo_posicion_origen=@pos",new{pos,input.Parte,input.Descripcion,input.Estacion,input.Estanteria,input.Nivel,post,input.Factor,input.CapacidadCajas,input.Minimo,created=Micro(now)},t);
        await Event(c,t,actor,now,"INVENTARIO_INICIAL",pos,0,0);var local=MexicoAt(now);return new{id=pos,cutDate=local.ToString("yyyy-MM-dd"),cutTime=local.ToString("HH:mm:ss.ffffff")};
    });
    public Task<string> CentralRetire(Actor actor,Guid key,CentralRetireInput input)=>Write(actor,key,input,"central-retire",async(c,t)=>
    {
        if(!actor.Permissions.Contains("catalog"))throw new RuleException("Sin permiso de catálogo.",403);EvidenceRules.Reason(input.Motivo);var now=await CentralLock(c,t);var p=await Position(c,t,input.PositionKey);
        if(input.Sustituto==input.PositionKey||input.HastaUtc<DateTimeOffset.UtcNow.AddMinutes(-1)||p.RetiradoUtc is not null)throw new RuleException("Vigencia inválida o baja ya registrada.",400);
        if(input.Sustituto is not null)await Position(c,t,input.Sustituto);
        await c.ExecuteAsync("UPDATE l1.POSICION SET fecha_retiro_utc=@end,id_posicion_sustituta=(SELECT id_posicion FROM l1.POSICION WHERE codigo_posicion_origen=@Sustituto) WHERE codigo_posicion_origen=@PositionKey",new{end=input.HastaUtc.UtcDateTime,input.Sustituto,input.PositionKey},t);
        var row=await CentralRow(c,t,"catalog",input.PositionKey);var detail=Obj(row["data"]!.GetValue<string>());detail["retiredUtc"]=input.HastaUtc.UtcDateTime.ToString("O");detail["replacement"]=input.Sustituto;row["data"]=detail.ToJsonString();await Change(c,t,actor,"catalog",input.PositionKey,row);
        return new{id=input.PositionKey,retired=true,previousReferencesPreserved=true};
    });
}
