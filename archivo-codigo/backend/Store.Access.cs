using System.Data;
using System.Text.Json;
using System.Text.Json.Nodes;
using Dapper;
using Microsoft.AspNetCore.Identity;
using Microsoft.Data.SqlClient;
namespace Motherson;
public record AccountChangeInput(string Action,string? Role=null,string? DisplayName=null,string? Password=null,string Reason="");
public record AccessShiftInput(int UsuarioId,string Codigo,DateTimeOffset DesdeUtc,DateTimeOffset HastaUtc,string Motivo);
public static class ProfileRules
{
    public static readonly string[] Roles=["materialista","lider surtidores","supervisor logistica","ingeniero procesos","planeador","gerencia","administrador"];
    public static bool Operational(string[] roles)=>roles.Any(r=>r is "administrador" or "materialista" or "lider surtidores" or "liberador" or "kitting" or "montacargas" or "secuenciador" or "surtidor SZ01" or "surtidor SZ02");
    public static void Shift(string code){if(code is not ("PFEP T1" or "PFEP T2"))throw new RuleException("Selecciona PFEP T1 o PFEP T2.",400);}
    public static void Password(string? password){if(password is null||password.Length is <12 or >200)throw new RuleException("La contraseña debe tener entre 12 y 200 caracteres.",400);}
    public static void LastAdmin(bool removing,int others){if(removing&&others==0)throw new RuleException("Debe permanecer al menos otro administrador activo.",409);}
}
public sealed partial class Store
{
    async Task AccessLock(SqlConnection c,SqlTransaction t,bool exclusive=false)
    {
        int result=await c.QuerySingleAsync<int>("DECLARE @r int;EXEC @r=sp_getapplock @Resource='HistIdentidades',@LockMode=@mode,@LockOwner='Transaction',@LockTimeout=10000;SELECT @r",new{mode=exclusive?"Exclusive":"Shared"},t);
        if(result<0)throw new RuleException("Accesos ocupados; reintenta con la misma petición.",409);
    }
    async Task<Actor> ReadActor(SqlConnection c,SqlTransaction? t,int id)
    {
        var user=await c.QuerySingleOrDefaultAsync("SELECT Nombre,NombreVisible,CuentaSimulada,AuthRevision FROM Usuarios WHERE Id=@id AND Activo=1",new{id},t)??throw new RuleException("Sesión revocada o cuenta inactiva.",401);
        var roles=(await c.QueryAsync<string>("SELECT r.Codigo FROM Roles r JOIN UsuarioRol ur ON ur.RolId=r.Id WHERE ur.UsuarioId=@id",new{id},t)).ToArray();
        var permissions=(await c.QueryAsync<string>("SELECT DISTINCT p.Permiso FROM UsuarioRol ur JOIN RolPermiso p ON p.RolId=ur.RolId WHERE ur.UsuarioId=@id",new{id},t)).ToArray();
        return new(id,(string)user.Nombre,roles,permissions,(string?)user.NombreVisible,(bool)user.CuentaSimulada,(int)user.AuthRevision);
    }
    async Task<JsonElement> AccessClock(SqlConnection c,SqlTransaction? t,DateTime now)
    {
        var raw=await c.QuerySingleAsync<string>("SELECT ReportJson FROM HistEstado WHERE Id=1 AND Listo=1",transaction:t);using var doc=JsonDocument.Parse(raw);
        return await new PythonSimulation(config).Execute(new{operation="access-clock",calendar=doc.RootElement.GetProperty("config"),anchorUtc=DateTime.SpecifyKind(now,DateTimeKind.Utc)});
    }
    async Task<int> RequireTurn(SqlConnection c,SqlTransaction t,Actor actor,DateTime? at=null)
    {
        var now=at??await c.QuerySingleAsync<DateTime>("SELECT SYSUTCDATETIME()",transaction:t);var clock=await AccessClock(c,t,now);string shift=clock.GetProperty("activeShift").GetString()!;
        if(shift=="FUERA")throw new RuleException("Fuera de jornada PFEP del calendario. Puedes consultar, pero no registrar operaciones de piso.",403);
        var id=await c.QuerySingleOrDefaultAsync<int?>("SELECT TOP(1) Id FROM HistTurnosAcceso WHERE UsuarioId=@uid AND Codigo=@shift AND DesdeUtc<=@now AND HastaUtc>@now AND RevocadoUtc IS NULL ORDER BY DesdeUtc DESC",new{uid=actor.Id,shift,now},t);
        return id??throw new RuleException("No tienes autorización vigente para operar en "+shift+". Solicita el turno al supervisor.",403);
    }
    public async Task<object> AccessContext(Actor actor)
    {
        await using var c=await Open();var now=await c.QuerySingleAsync<DateTime>("SELECT SYSUTCDATETIME()");var clock=await AccessClock(c,null,now);string shift=clock.GetProperty("activeShift").GetString()!;
        var shifts=(await c.QueryAsync("SELECT Id,Codigo,DesdeUtc,HastaUtc FROM HistTurnosAcceso WHERE UsuarioId=@id AND RevocadoUtc IS NULL AND DesdeUtc<=@now AND HastaUtc>@now",new{id=actor.Id,now})).ToArray();
        bool authorized=shifts.Any(s=>(string)s.Codigo==shift);
        int entries=await c.QuerySingleAsync<int>("SELECT COUNT(*) FROM HistAsignaciones a JOIN HistPosiciones p ON p.Clave=a.Clave WHERE a.UsuarioId=@id AND a.RevocadoUtc IS NULL AND a.DesdeUtc<=@now AND a.HastaUtc>@now AND (a.TurnoCodigo IS NULL OR a.TurnoCodigo=@shift) AND (@leader=0 OR a.EsCobertura=1 AND a.TurnoCodigo=@shift) AND (p.RetiradoUtc IS NULL OR p.RetiradoUtc>@now)",new{id=actor.Id,now,shift,leader=actor.Roles.Contains("lider surtidores")});
        return new{serverUtc=DateTime.SpecifyKind(now,DateTimeKind.Utc),local=MexicoAt(now).ToString("O"),activeShift=shift,authorizedShifts=shifts,canEnter=actor.Permissions.Contains("entry")&&authorized&&entries>0,hasCurrentShift=authorized,positionAssignments=entries,zone="America/Mexico_City"};
    }
    public Task<string> AssignAccessShift(Actor actor,Guid key,AccessShiftInput input)=>Write(actor,key,input,"central-access-shift",async(c,t)=>
    {
        if(!actor.Permissions.Contains("assign"))throw new RuleException("Turnos reservados al supervisor o administrador.",403);ProfileRules.Shift(input.Codigo);EvidenceRules.Reason(input.Motivo);var now=await c.QuerySingleAsync<DateTime>("SELECT SYSUTCDATETIME()",transaction:t);
        if(input.HastaUtc<=input.DesdeUtc||input.HastaUtc.UtcDateTime<=now)throw new RuleException("Indica un periodo de autorización válido.",400);
        if(await c.QuerySingleAsync<int>("SELECT COUNT(*) FROM Usuarios WHERE Id=@UsuarioId AND Activo=1",input,t)!=1)throw new RuleException("Cuenta inexistente o inactiva.",400);
        if(await c.QuerySingleAsync<int>("SELECT COUNT(*) FROM HistTurnosAcceso WHERE UsuarioId=@UsuarioId AND Codigo=@Codigo AND RevocadoUtc IS NULL AND DesdeUtc<@end AND HastaUtc>@start",new{input.UsuarioId,input.Codigo,start=input.DesdeUtc.UtcDateTime,end=input.HastaUtc.UtcDateTime},t)>0)throw new RuleException("Ya existe una autorización de este turno en el periodo. Revócala antes de sustituirla.",409);
        var id=await c.QuerySingleAsync<int>("DECLARE @nuevo TABLE(id int);INSERT seguridad.AUTORIZACION_TURNO(id_usuario,id_turno,fecha_inicio_utc,fecha_fin_utc,motivo,id_usuario_autorizador) OUTPUT inserted.id_autorizacion_turno INTO @nuevo VALUES(@UsuarioId,(SELECT id_turno FROM l1.TURNO WHERE codigo_turno=@Codigo),@start,@end,@Motivo,@uid);SELECT id FROM @nuevo",new{input.UsuarioId,input.Codigo,start=input.DesdeUtc.UtcDateTime,end=input.HastaUtc.UtcDateTime,input.Motivo,uid=actor.Id},t);return new{id};
    });
    public Task<string> RevokeAccessShift(Actor actor,Guid key,int id)=>Write(actor,key,new{id},"central-access-shift-revoke",async(c,t)=>
    {
        if(!actor.Permissions.Contains("assign"))throw new RuleException("Sin permiso para revocar turnos.",403);
        if(await c.ExecuteAsync("UPDATE seguridad.AUTORIZACION_TURNO SET fecha_revocacion_utc=SYSUTCDATETIME(),id_usuario_revocador=@uid WHERE id_autorizacion_turno=@id AND fecha_revocacion_utc IS NULL",new{id,uid=actor.Id},t)!=1)throw new RuleException("Turno inexistente o ya revocado.",409);return new{id};
    });
    public Task<string> ChangeAccount(Actor actor,Guid key,int id,AccountChangeInput input)=>Write(actor,key,new{id,input.Action,input.Role,input.DisplayName,input.Reason},"central-user-change",async(c,t)=>
    {
        if(!actor.Permissions.Contains("admin"))throw new RuleException("Gestión de cuentas reservada al administrador.",403);EvidenceRules.Reason(input.Reason);
        if(id==actor.Id&&(input.Action=="deactivate"||input.Action=="role"&&input.Role!="administrador"))throw new RuleException("Otro administrador debe gestionar la retirada de tu propio acceso.",409);
        var u=await c.QuerySingleOrDefaultAsync("SELECT * FROM Usuarios WITH(UPDLOCK,HOLDLOCK) WHERE Id=@id",new{id},t)??throw new RuleException("Cuenta inexistente.",404);
        var roles=(await c.QueryAsync<string>("SELECT r.Codigo FROM UsuarioRol ur JOIN Roles r ON r.Id=ur.RolId WHERE ur.UsuarioId=@id",new{id},t)).ToArray();
        int others=await c.QuerySingleAsync<int>("SELECT COUNT(DISTINCT u.Id) FROM Usuarios u JOIN UsuarioRol ur ON ur.UsuarioId=u.Id JOIN Roles r ON r.Id=ur.RolId WHERE u.Activo=1 AND r.Codigo='administrador' AND u.Id<>@id",new{id},t);
        switch(input.Action)
        {
            case "role":
                if(!ProfileRules.Roles.Contains(input.Role))throw new RuleException("Perfil inválido.",400);ProfileRules.LastAdmin((bool)u.Activo&&roles.Contains("administrador")&&input.Role!="administrador",others);
                await c.ExecuteAsync("DELETE seguridad.USUARIO_ROL WHERE id_usuario=@id;INSERT seguridad.USUARIO_ROL SELECT @id,id_rol FROM seguridad.ROL WHERE codigo_rol=@role;UPDATE seguridad.USUARIO SET version_autorizacion=version_autorizacion+1 WHERE id_usuario=@id",new{id,role=input.Role},t);break;
            case "deactivate":ProfileRules.LastAdmin((bool)u.Activo&&roles.Contains("administrador"),others);await c.ExecuteAsync("UPDATE seguridad.USUARIO SET esta_activo=0,version_autorizacion=version_autorizacion+1 WHERE id_usuario=@id",new{id},t);break;
            case "activate":await c.ExecuteAsync("UPDATE seguridad.USUARIO SET esta_activo=1,version_autorizacion=version_autorizacion+1 WHERE id_usuario=@id",new{id},t);break;
            case "password":ProfileRules.Password(input.Password);var hash=new PasswordHasher<string>().HashPassword((string)u.Nombre,input.Password!);await c.ExecuteAsync("UPDATE seguridad.USUARIO SET hash_contrasena=@hash,version_autorizacion=version_autorizacion+1 WHERE id_usuario=@id",new{id,hash},t);break;
            case "name":if(string.IsNullOrWhiteSpace(input.DisplayName)||input.DisplayName.Length>100)throw new RuleException("Indica un nombre visible de hasta 100 caracteres.",400);await c.ExecuteAsync("UPDATE seguridad.USUARIO SET nombre_visible=@name WHERE id_usuario=@id",new{id,name=input.DisplayName},t);break;
            default:throw new RuleException("Acción de cuenta inválida.",400);
        }
        var after=await c.QuerySingleAsync("SELECT Activo,NombreVisible,AuthRevision FROM Usuarios WHERE Id=@id",new{id},t);
        var afterRoles=(await c.QueryAsync<string>("SELECT r.Codigo FROM UsuarioRol ur JOIN Roles r ON r.Id=ur.RolId WHERE ur.UsuarioId=@id",new{id},t)).ToArray();
        await c.ExecuteAsync("INSERT sistema.AUDITORIA(id_usuario,accion,entidad,detalle) VALUES(@uid,'central-user-state',@entity,@detail)",new{uid=actor.Id,entity=id.ToString(),detail=JsonSerializer.Serialize(new{action=input.Action,reason=input.Reason,before=new{roles,active=(bool)u.Activo,name=(string?)u.NombreVisible,authRevision=(int)u.AuthRevision},after=new{roles=afterRoles,active=(bool)after.Activo,name=(string?)after.NombreVisible,authRevision=(int)after.AuthRevision}})},t);
        return new{id,action=input.Action,sessionsRevoked=input.Action!="name"};
    });
}
