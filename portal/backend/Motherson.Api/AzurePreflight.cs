using System.Text.Json;
using Dapper;
using Microsoft.Data.SqlClient;

namespace Motherson;

// El diagnóstico termina antes de crear claves de sesión o abrir el diario del portal.
public static class AzurePreflight
{
    public const string Server = "motherson-l1-equipo.database.windows.net";
    public const string Database = "Motherson_L1";

    public static SqlConnectionStringBuilder Validate(IConfiguration configuration)
    {
        var text = configuration.GetConnectionString("Sql");
        if (string.IsNullOrWhiteSpace(text)) throw new InvalidOperationException("Falta ConnectionStrings__Sql.");
        var connection = new SqlConnectionStringBuilder(text);
        var expectedServer = ExpectedServer(configuration);
        var host = connection.DataSource.StartsWith("tcp:", StringComparison.OrdinalIgnoreCase)
            ? connection.DataSource[4..] : connection.DataSource;
        if (!host.Equals(expectedServer, StringComparison.OrdinalIgnoreCase) &&
            !host.Equals(expectedServer + ",1433", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("La conexión no apunta al servidor Azure acordado.");
        if (connection.InitialCatalog != Database || configuration["MVP_BASE_COMPARTIDA"] != Database)
            throw new InvalidOperationException("La conexión y MVP_BASE_COMPARTIDA deben usar Motherson_L1.");
        if (connection.TrustServerCertificate || connection.Encrypt == SqlConnectionEncryptOption.Optional)
            throw new InvalidOperationException("Azure requiere cifrado y validación del certificado.");
        return connection;
    }

    public static string ExpectedServer(IConfiguration c)
    {
        if (!c.GetValue<bool>("Pilot:Enabled")) return Server;
        var host = c["MVP_AZURE_SERVER"] ?? "";
        if (!System.Text.RegularExpressions.Regex.IsMatch(host, @"^motherson-piloto-[a-z0-9-]+\.database\.windows\.net$") || host == Server)
            throw new InvalidOperationException("El piloto requiere un servidor independiente con prefijo motherson-piloto-.");
        return host;
    }

    public static async Task<int> Run(IConfiguration configuration, bool configurationOnly)
    {
        try
        {
            var connection = Validate(configuration);
            var root = configuration["MVP_ROOT"];
            var manifest = !string.IsNullOrWhiteSpace(root) &&
                File.Exists(Path.Combine(root, ".local", "historico-demo", "manifiesto.json"));
            var admin = !string.IsNullOrWhiteSpace(configuration["MVP_ADMIN_PUESTOS_HASH"]);
            Console.WriteLine(JsonSerializer.Serialize(new {
                servidor = Server, baseDatos = Database, configuracion = "OK",
                manifiestoPresente = manifest, administradorConfigurado = admin,
                nota = configuration.GetValue<bool>("MVP_SQL_TRANSACCIONAL") ? "Modo SQL transaccional: requiere esquema mes y apertura, sin dependencia del paquete histórico." : "Modo anterior: requiere paquete histórico compatible."
            }));
            if (configurationOnly) return 0;
            await using var sql = new SqlConnection(connection.ConnectionString);
            await sql.OpenAsync();
            var database = await sql.ExecuteScalarAsync<string>("SELECT DB_NAME()");
            if (database != Database) throw new InvalidOperationException("La sesión abrió una base distinta.");
            var objects = await sql.QueryAsync("""
                SELECT v.nombre,v.permiso,
                    COALESCE(HAS_PERMS_BY_NAME(CASE WHEN v.nombre LIKE 'mes.%' THEN v.nombre ELSE 'dbo.'+v.nombre END,'OBJECT',v.permiso),0) AS permitido
                FROM (VALUES ('ESTACION','SELECT'),('COLOR','SELECT'),('PUESTO_LOGISTICO','SELECT'),
                  ('TURNO','SELECT'),('ESTANTERIA','SELECT'),('MATERIAL','SELECT'),('UBICACION','SELECT'),('REGISTRO','SELECT'),
                  ('vw_modelo_puestos','SELECT'),('vw_kpi_inventario_actual','SELECT'),('vw_kpi_atenciones','SELECT'),
                  ('mes.RegistrarMovimiento','EXECUTE'),('IniciarAtencion','EXECUTE'),('FinalizarAtencion','EXECUTE'),
                  ('GuardarAccesoPuesto','EXECUTE'),('mes.AjustarCatalogo','EXECUTE')) v(nombre,permiso)
                """);
            var checks = objects.Select(o => new { nombre = (string)o.nombre, permiso = (string)o.permiso, permitido = (int)o.permitido }).ToArray();
            var arguments = (await sql.QueryAsync<string>("""
                SELECT name FROM sys.parameters WHERE object_id=OBJECT_ID('mes.RegistrarMovimiento')
                AND parameter_id>0 ORDER BY parameter_id
                """)).ToArray();
            var contract = arguments.SequenceEqual(new[] { "@operacion", "@cuenta", "@tipo_cuenta", "@puesto_cuenta", "@solicitud" });
            var ready = checks.All(c => c.permitido == 1) && contract;
            Console.WriteLine(JsonSerializer.Serialize(new { conexion = "OK", comprobaciones = checks,
                contratoMovimiento = contract ? "OK" : "REVISAR", objetosYPermisos = ready ? "OK" : "REVISAR",
                capturasHabilitadas = false }));
            return ready ? 0 : 2;
        }
        catch (SqlException error)
        {
            Console.Error.WriteLine($"Diagnóstico SQL no completado (código {error.Number}). Revisa acceso, cuenta y estado de la base. No se muestra la cadena de conexión.");
            return 2;
        }
        catch (Exception error) when (error is InvalidOperationException or ArgumentException)
        {
            // Los errores de análisis pueden contener partes de la entrada: nunca imprimirla.
            Console.Error.WriteLine("Configuración Azure inválida. Revisa servidor, base, cifrado y variables requeridas. No se abrió el portal.");
            return 2;
        }
    }
}
