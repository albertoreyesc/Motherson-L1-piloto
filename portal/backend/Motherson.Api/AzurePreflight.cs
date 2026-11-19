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
        var host = connection.DataSource.StartsWith("tcp:", StringComparison.OrdinalIgnoreCase)
            ? connection.DataSource[4..] : connection.DataSource;
        if (!host.Equals(Server, StringComparison.OrdinalIgnoreCase) &&
            !host.Equals(Server + ",1433", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("La conexión no apunta al servidor Azure acordado.");
        if (connection.InitialCatalog != Database || configuration["MVP_BASE_COMPARTIDA"] != Database)
            throw new InvalidOperationException("La conexión y MVP_BASE_COMPARTIDA deben usar Motherson_L1.");
        if (connection.TrustServerCertificate || connection.Encrypt == SqlConnectionEncryptOption.Optional)
            throw new InvalidOperationException("Azure requiere cifrado y validación del certificado.");
        return connection;
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
                nota = "El arranque actual requiere el paquete histórico completo y su corte compatible; presencia no implica integridad."
            }));
            if (configurationOnly) return 0;
            await using var sql = new SqlConnection(connection.ConnectionString);
            await sql.OpenAsync();
            var database = await sql.ExecuteScalarAsync<string>("SELECT DB_NAME()");
            if (database != Database) throw new InvalidOperationException("La sesión abrió una base distinta.");
            var objects = await sql.QueryAsync("""
                SELECT v.nombre,v.permiso,
                    COALESCE(HAS_PERMS_BY_NAME('dbo.'+v.nombre,'OBJECT',v.permiso),0) AS permitido
                FROM (VALUES ('ESTACION','SELECT'),('COLOR','SELECT'),('PUESTO_LOGISTICO','SELECT'),
                  ('TURNO','SELECT'),('ESTANTERIA','SELECT'),('MATERIAL','SELECT'),('UBICACION','SELECT'),('REGISTRO','SELECT'),
                  ('vw_modelo_puestos','SELECT'),('vw_kpi_inventario_actual','SELECT'),('vw_kpi_atenciones','SELECT'),
                  ('RegistrarMovimiento','EXECUTE'),('IniciarAtencion','EXECUTE'),('FinalizarAtencion','EXECUTE'),
                  ('GuardarAccesoPuesto','EXECUTE'),('AjustarCapacidadEstanteria','EXECUTE'),('AjustarMateriales','EXECUTE')) v(nombre,permiso)
                """);
            var checks = objects.Select(o => new { nombre = (string)o.nombre, permiso = (string)o.permiso, permitido = (int)o.permitido }).ToArray();
            var arguments = (await sql.QueryAsync<string>("""
                SELECT name FROM sys.parameters WHERE object_id=OBJECT_ID('dbo.RegistrarMovimiento')
                AND parameter_id>0 ORDER BY parameter_id
                """)).ToArray();
            var contract = arguments.SequenceEqual(new[] { "@ubicacion", "@piezas" });
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
