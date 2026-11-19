using Dapper;
using Microsoft.AspNetCore.Identity;
using Microsoft.Data.SqlClient;
using System.Data;
using System.Text.Json;
using System.Text.RegularExpressions;
namespace Motherson;

// Solo consola administrativa; nunca se invoca desde HTTP ni al iniciar el servidor.
public static class PilotSetup
{
    static SqlConnection Connection(IConfiguration c)
    {
        if(!c.GetValue<bool>("Pilot:Enabled"))throw new InvalidOperationException("Falta el perfil piloto.");
        var target=AzurePreflight.Validate(c);
        if(AzurePreflight.ExpectedServer(c)==AzurePreflight.Server)throw new InvalidOperationException("No se admite el servidor original.");
        return new SqlConnection(target.ConnectionString);
    }
    static async Task Open(SqlConnection sql)
    {
        for(var attempt=0;;attempt++)
        {
            try{await sql.OpenAsync();return;}
            catch(SqlException e)when(e.Number==40613&&attempt<5){await Task.Delay(TimeSpan.FromSeconds(Math.Min(16,2*(1<<attempt))));}
        }
    }
    public static async Task Initialize(string[] args,IConfiguration c)
    {
        await using var sql=Connection(c);await Open(sql);
        if(await sql.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM sys.tables WHERE is_ms_shipped=0")!=0)throw new InvalidOperationException("La base no está vacía; no se instaló nada.");
        var at=Array.IndexOf(args,"--piloto-inicializar");if(at+1>=args.Length)throw new ArgumentException("Indica la raíz del repositorio.");
        var root=Path.GetFullPath(args[at+1]);var folder=Path.Combine(root,"portal","db","azure");
        await using var tx=(SqlTransaction)await sql.BeginTransactionAsync();
        foreach(var file in new[]{"010_tablas.sql","011_movimientos.sql","012_objetos_portal.sql","013_indices_bi.sql","015_diario_sql.sql","016_operaciones_transaccionales.sql","017_vistas_y_apertura.sql"})
            foreach(var batch in Regex.Split(File.ReadAllText(Path.Combine(folder,file)),@"^\s*GO\s*$",RegexOptions.Multiline|RegexOptions.IgnoreCase))
                if(!string.IsNullOrWhiteSpace(batch))await sql.ExecuteAsync(batch,transaction:tx,commandTimeout:120);
        foreach(var batch in Regex.Split(File.ReadAllText(Path.Combine(root,"despliegue","piloto","cuentas.sql")),@"^\s*GO\s*$",RegexOptions.Multiline|RegexOptions.IgnoreCase))
            if(!string.IsNullOrWhiteSpace(batch))await sql.ExecuteAsync(batch,transaction:tx,commandTimeout:120);
        await tx.CommitAsync();
        await DemoData.Load(sql,Path.Combine(root,"datos","simulados"));
        Console.WriteLine("Esquema piloto y datos simulados instalados; todavía faltan usuarios SQL restringidos y cuentas personales.");
    }
    public static async Task Permissions(string[] args,IConfiguration c)
    {
        var at=Array.IndexOf(args,"--piloto-permisos");if(at+1>=args.Length)throw new ArgumentException("Indica la raíz del repositorio.");
        using var input=JsonDocument.Parse(await Console.In.ReadToEndAsync());
        var app=input.RootElement.GetProperty("app").GetString()!;var reader=input.RootElement.GetProperty("reader").GetString()!;
        if(app.Length<24||reader.Length<24)throw new ArgumentException("Se requieren claves nuevas de alta entropía.");
        await using var sql=Connection(c);await Open(sql);
        if(await sql.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM mes.BASE_INVENTARIO WHERE id=1")!=1||await sql.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM sys.database_principals WHERE name IN ('portal_app','powerbi_lector')")!=0)
            throw new InvalidOperationException("La base no es un piloto instalado pendiente de permisos.");
        await using var tx=(SqlTransaction)await sql.BeginTransactionAsync();
        await sql.ExecuteAsync("CREATE USER portal_app WITH PASSWORD='"+app.Replace("'","''")+"';CREATE USER powerbi_lector WITH PASSWORD='"+reader.Replace("'","''")+"';",transaction:tx);
        // El script de roles abre su propia transacción anidada: la transacción externa garantiza todo o nada.
        await sql.ExecuteAsync(File.ReadAllText(Path.Combine(Path.GetFullPath(args[at+1]),"portal/db/azure/020_permisos.sql")),transaction:tx);
        await sql.ExecuteAsync("DENY EXECUTE ON dbo.GuardarAccesoPuesto TO mes_portal",transaction:tx);
        await tx.CommitAsync();Console.WriteLine("Usuarios restringidos del piloto creados; sin db_owner ni escritura directa de saldos.");
    }
    public static async Task Storage(string[] args,IConfiguration c)
    {
        var at=Array.IndexOf(args,"--piloto-almacenamiento");
        if(at+1>=args.Length)throw new ArgumentException("Indica la raíz del repositorio.");
        await using var sql=Connection(c);await Open(sql);
        if(await sql.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM mes.BASE_INVENTARIO WHERE id=1")!=1)throw new InvalidOperationException("Falta el piloto instalado.");
        await using var tx=(SqlTransaction)await sql.BeginTransactionAsync();
        foreach(var batch in Regex.Split(File.ReadAllText(Path.Combine(Path.GetFullPath(args[at+1]),"despliegue/render/almacenamiento.sql")),@"^\s*GO\s*$",RegexOptions.Multiline|RegexOptions.IgnoreCase))
            if(!string.IsNullOrWhiteSpace(batch))await sql.ExecuteAsync(batch,transaction:tx,commandTimeout:120);
        await tx.CommitAsync();Console.WriteLine("Almacenamiento persistente del piloto preparado, sin cambiar inventario ni historial.");
    }
    public static async Task User(IConfiguration c)
    {
        // Entrada JSON por stdin desde un archivo privado o formulario getpass; no argumentos con claves.
        using var input=JsonDocument.Parse(await Console.In.ReadToEndAsync());var u=input.RootElement;
        var usuario=u.GetProperty("usuario").GetString()??"";var nombre=u.GetProperty("nombre").GetString()??"";var rol=u.GetProperty("rol").GetString()??"";
        var password=u.GetProperty("password").GetString()??"";
        int? puesto=u.TryGetProperty("puesto",out var p)&&p.ValueKind==JsonValueKind.Number?p.GetInt32():null;
        if(usuario.Length is < 3 or > 80||usuario.Equals("admin.local",StringComparison.OrdinalIgnoreCase)||nombre.Length is < 1 or > 120||password.Length is < 12 or > 512||rol is not ("ADMIN" or "PUESTO" or "LECTOR")||(rol=="PUESTO")!=puesto.HasValue)
            throw new ArgumentException("Revisa usuario, nombre, rol, puesto y longitud de contraseña.");
        var hash=new PasswordHasher<string>().HashPassword("",password);
        await using var sql=Connection(c);await Open(sql);
        await sql.ExecuteAsync("INSERT mes.USUARIO_PILOTO(usuario,nombre,rol,id_puesto,password_hash) VALUES(@usuario,@nombre,@rol,@puesto,@hash)",new{usuario,nombre,rol,puesto,hash});
        Console.WriteLine("Cuenta individual creada; no se muestran credenciales.");
    }
}
