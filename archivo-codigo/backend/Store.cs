using System.Data;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Dapper;
using Microsoft.AspNetCore.Identity;
using Microsoft.Data.SqlClient;

namespace Motherson;
public sealed partial class Store(IConfiguration config)
{
    public bool Lab => config.GetValue<bool>("LabMode");
    public string ConnectionString => config.GetConnectionString("Sql") ?? throw new RuleException("Configura ConnectionStrings__Sql.",503);
    public async Task<SqlConnection> Open()
    {
        var c = new SqlConnection(ConnectionString);
        try { await c.OpenAsync(); } catch { await c.DisposeAsync(); throw; }
        var simulated = await c.QuerySingleAsync<bool>("SELECT Simulado FROM dbo.Configuracion WHERE Id=1");
        if (!simulated || !Lab || c.Database is not("Motherson_L1_Claro" or "Motherson_L1_Claro_Pruebas" or "Motherson_L1_Claro_Restore" or "Motherson_L1_Claro_DDL")) { await c.DisposeAsync(); throw new RuleException("La copia requiere modo simulado y una base autorizada del modelo L1 claro.",503); }
        if(await c.QuerySingleAsync<int>("SELECT MAX(Version) FROM SchemaVersion")!=21){await c.DisposeAsync();throw new RuleException("La aplicación requiere el esquema claro 21.",503);}
        return c;
    }
    public async Task<IEnumerable<dynamic>> Query(string sql,object? p=null) { await using var c=await Open();return await c.QueryAsync(sql,p); }
    public async Task<Actor> Actor(int id)
    {
        await using var c=await Open();return await ReadActor(c,null,id);
    }
    public async Task<int?> Login(LoginInput input)
    {
        if(string.IsNullOrWhiteSpace(input.Username)||string.IsNullOrWhiteSpace(input.Password)||input.Username.Length>80||input.Password.Length>200)return null;
        await using var c=await Open();var u=await c.QuerySingleOrDefaultAsync("SELECT Id,PasswordHash FROM Usuarios WHERE Nombre=@Username AND Activo=1",input);
        var hasher=new PasswordHasher<string>();
        // A fixed valid dummy hash equalizes the expensive password check for unknown names.
        var hash=u is null ? DummyHash : (string)u.PasswordHash;
        return hasher.VerifyHashedPassword(input.Username,hash,input.Password)==PasswordVerificationResult.Failed || u is null ? null : (int)u.Id;
    }
    static readonly string DummyHash=new PasswordHasher<string>().HashPassword("dummy",Convert.ToHexString(RandomNumberGenerator.GetBytes(32)));
    public async Task<string> Write(Actor actor,Guid key,object body,string action,Func<SqlConnection,SqlTransaction,Task<object>> operation)
    {
        var hash=Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(action+JsonSerializer.Serialize(body))));
        await using var c=await Open();await using var t=(SqlTransaction)await c.BeginTransactionAsync(IsolationLevel.Serializable);
        try
        {
            await AccessLock(c,t,action=="user-create"||action=="central-user-change");
            var fresh=await ReadActor(c,t,actor.Id);if(fresh.AuthRevision!=actor.AuthRevision)throw new RuleException("Los permisos cambiaron. Inicia sesión otra vez.",401);
            int locked=await c.QuerySingleAsync<int>("DECLARE @r int; EXEC @r=sp_getapplock @Resource=@resource,@LockMode='Exclusive',@LockOwner='Transaction',@LockTimeout=5000; SELECT @r;",new{resource=$"motherson:{actor.Id}:{key}"},t);
            if(locked<0)throw new RuleException("Petición ocupada. Reintenta con la misma clave.");
            var old=await c.QuerySingleOrDefaultAsync("SELECT Hash,Respuesta FROM Peticiones WHERE UsuarioId=@id AND Clave=@key",new{id=actor.Id,key},t);
            if(old is not null){if((string)old.Hash!=hash)throw new RuleException("La clave de idempotencia ya corresponde a otra operación.");await t.CommitAsync();return (string)old.Respuesta;}
            var result=await operation(c,t);var json=JsonSerializer.Serialize(result,new JsonSerializerOptions(JsonSerializerDefaults.Web));
            await c.ExecuteAsync("INSERT sistema.PETICION_IDEMPOTENTE(id_usuario,id_peticion,huella_peticion,respuesta_json) VALUES(@id,@key,@hash,@json); INSERT sistema.AUDITORIA(id_usuario,accion,entidad,detalle) VALUES(@id,@action,@entity,@detail);",new{id=actor.Id,key,hash,json,action,entity=key.ToString(),detail=JsonSerializer.Serialize(body)},t);
            await t.CommitAsync();return json;
        }
        catch { await t.RollbackAsync(); throw; }
    }
    static bool WideScope(Actor a)=>a.Permissions.Any(p=>p is "admin" or "supervise" or "catalog");
    public Task<string> CreateUser(Actor a,Guid k,UserInput i)=>Write(a,k,i.NombreVisible is null&&!i.CuentaSimulada?(object)new{i.Username,i.Role,i.PuestoId}:new{i.Username,i.Role,i.PuestoId,i.NombreVisible,i.CuentaSimulada},"user-create",async(c,t)=>
    {
        if(!a.Permissions.Contains("admin"))throw new RuleException("Gestión de cuentas reservada al administrador.",403);
        if(i.Username is null||i.Password is null||i.Username.Length is <3 or >80 || i.Password.Length is <12 or >200)throw new RuleException("Usuario 3–80 caracteres; contraseña 12–200.",400);
        if(string.IsNullOrWhiteSpace(i.Username)||i.Username!=i.Username.Trim())throw new RuleException("Usuario sin espacios al inicio o al final.",400);
        if(i.NombreVisible?.Length>100||i.CuentaSimulada&&(!Lab||!i.Username.StartsWith("sim.",StringComparison.Ordinal)||string.IsNullOrWhiteSpace(i.NombreVisible)||i.Role=="administrador"))throw new RuleException("Cuenta simulada inválida o fuera del laboratorio.",400);
        var hash=new PasswordHasher<string>().HashPassword(i.Username,i.Password);
        int id=await c.QuerySingleAsync<int>("INSERT seguridad.USUARIO(nombre_usuario,hash_contrasena,nombre_visible,es_cuenta_simulada) OUTPUT inserted.id_usuario VALUES(@Username,@hash,@NombreVisible,@CuentaSimulada)",new{i.Username,hash,i.NombreVisible,i.CuentaSimulada},t);
        int count=await c.ExecuteAsync("INSERT seguridad.USUARIO_ROL SELECT @id,id_rol FROM seguridad.ROL WHERE codigo_rol=@Role",new{id,i.Role},t);if(count!=1)throw new RuleException("Rol inválido.",400);
        if(i.PuestoId is not null)await c.ExecuteAsync("INSERT seguridad.USUARIO_PUESTO VALUES(@id,@PuestoId)",new{id,i.PuestoId},t);
        return new{id};
    });
}
