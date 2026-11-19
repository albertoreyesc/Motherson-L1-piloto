using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Dapper;
using Microsoft.AspNetCore.Identity;

namespace Motherson;

public record AccesoPuestoInput(int Puesto, string? Usuario, string? Password, bool Revocar);
public record IniciarAtencionInput(int Ubicacion);
public record FinalizarAtencionInput(int Registro);
public record AjusteCapacidadInput(int Estanteria, int? Anterior, int Nueva);
public record ImportacionMaterialesInput(CambioMaterial[]? Cambios);

public sealed partial class Store
{
    const string AdminLocal = "admin.local";
    public bool ModeloPuestos { get; private set; }
    internal HistoricoDemo? archivoDemo;
    public bool EsDemo => archivoDemo is not null;
    /// Base compartida con el Equipo 2: mismo modelo que Demo; además cada movimiento se aplica en SQL.
    public bool Compartida { get; private set; }
    string EsquemaVistas => EsDemo ? "dbo" : "reporting";

    public async Task DetectarModelo()
    {
        await using var c = await Open();
        Compartida = EsBaseCompartida(c.Database, configuration);
        if (c.Database == HistoricoDemo.Base || Compartida)
        {
            ModeloPuestos = await c.ExecuteScalarAsync<int>(
                "SELECT CASE WHEN OBJECT_ID('dbo.vw_modelo_puestos','V') IS NOT NULL THEN 1 ELSE 0 END") == 1;
            if (!ModeloPuestos)
                throw new InvalidOperationException("La base requiere el modelo por puestos (falta dbo.vw_modelo_puestos).");
            var raiz = configuration["MVP_ROOT"] ?? throw new InvalidOperationException("Define MVP_ROOT para la demostración.");
            archivoDemo = new HistoricoDemo(Path.Combine(raiz, ".local", "historico-demo"));
            return;
        }
        ModeloPuestos = await c.ExecuteScalarAsync<int>(
            "SELECT CASE WHEN OBJECT_ID('reporting.vw_modelo_puestos','V') IS NOT NULL THEN 1 ELSE 0 END") == 1;
    }

    sealed record CredencialPuesto(int Id, string Puesto, string Usuario, string Hash);

    static string Sello(string usuario, string hash) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(usuario + "\n" + hash)));

    Cuenta? AdminPuestos()
    {
        var hash = configuration["MVP_ADMIN_PUESTOS_HASH"];
        return string.IsNullOrWhiteSpace(hash) ? null :
            new(0, AdminLocal, "Administración", "ADMINISTRADOR", "ADMIN_PUESTOS", null) { SelloAcceso = Sello(AdminLocal, hash) };
    }

    static Cuenta CuentaPuesto(CredencialPuesto p) =>
        new(p.Id, p.Usuario, p.Puesto, "PUESTO", "PUESTO", p.Id) { SelloAcceso = Sello(p.Usuario, p.Hash) };

    async Task<Cuenta?> LoginPuesto(LoginInput input)
    {
        if (input.Username.Equals(AdminLocal, StringComparison.OrdinalIgnoreCase))
        {
            var admin = AdminPuestos();
            var hash = configuration["MVP_ADMIN_PUESTOS_HASH"] ?? Dummy;
            return Hasher.VerifyHashedPassword("", hash, input.Password) != PasswordVerificationResult.Failed ? admin : null;
        }

        await using var c = await Open();
        var puesto = await c.QuerySingleOrDefaultAsync<CredencialPuesto>("""
            SELECT id_puesto Id,nombre Puesto,usuario Usuario,[password] Hash
            FROM dbo.PUESTO_LOGISTICO WHERE usuario=@usuario
            """, new { usuario = input.Username });
        var verificado = Hasher.VerifyHashedPassword("", puesto?.Hash ?? Dummy, input.Password);
        return puesto is not null && verificado != PasswordVerificationResult.Failed ? CuentaPuesto(puesto) : null;
    }

    async Task<Cuenta> ActorPuesto(int id, string tipo, string? sello)
    {
        Cuenta? cuenta = null;
        if (tipo == "ADMIN_PUESTOS" && id == 0)
            cuenta = AdminPuestos();
        else if (tipo == "PUESTO")
        {
            await using var c = await Open();
            var puesto = await c.QuerySingleOrDefaultAsync<CredencialPuesto>("""
                SELECT id_puesto Id,nombre Puesto,usuario Usuario,[password] Hash
                FROM dbo.PUESTO_LOGISTICO WHERE id_puesto=@id AND usuario IS NOT NULL
                """, new { id });
            if (puesto is not null) cuenta = CuentaPuesto(puesto);
        }
        return cuenta is not null && sello == cuenta.SelloAcceso
            ? cuenta : throw new RuleException("La sesión venció o el acceso cambió.", 401);
    }

    public async Task GuardarAcceso(AccesoPuestoInput input)
    {
        string? usuario = null;
        string? hash = null;
        if (input.Puesto <= 0) throw new RuleException("Selecciona un puesto válido.", 400);
        if (!input.Revocar)
        {
            usuario = input.Usuario?.Trim();
            if (string.IsNullOrWhiteSpace(usuario) || usuario.Length > 80 ||
                usuario.Equals(AdminLocal, StringComparison.OrdinalIgnoreCase))
                throw new RuleException("Escribe un usuario válido y distinto de la cuenta administrativa.", 400);
            if (input.Password is null || input.Password.Length is < 12 or > 512 || string.IsNullOrWhiteSpace(input.Password))
                throw new RuleException("La contraseña debe tener entre 12 y 512 caracteres.", 400);
            hash = Hasher.HashPassword("", input.Password);
        }
        await using var c = await Open();
        await c.ExecuteAsync("dbo.GuardarAccesoPuesto", new { puesto = input.Puesto, usuario, hash },
            commandType: System.Data.CommandType.StoredProcedure);
    }

    public async Task<object> Atenciones(Cuenta actor, int pagina, int? turno)
    {
        if (pagina is < 1 or > 50000) throw new RuleException("Página fuera de rango.", 400);
        if (turno < 0) throw new RuleException("Turno inválido.", 400);
        await using var c = await Open();
        var args = new { puesto = actor.PuestoId, offset = (pagina - 1) * 50, turno };
        var tabla = $" FROM {EsquemaVistas}.vw_kpi_atenciones WHERE (@puesto IS NULL OR id_puesto=@puesto)";
        if (turno.HasValue)
            tabla += turno == 0 ? " AND id_turno IS NULL" : " AND id_turno=@turno";
        using var datos = await c.QueryMultipleAsync(
            "SELECT COUNT(*)" + tabla + "; SELECT *" + tabla +
            " ORDER BY fecha_hora_inicio DESC,id_registro DESC OFFSET @offset ROWS FETCH NEXT 50 ROWS ONLY;", args);
        var total = await datos.ReadSingleAsync<int>();
        return new { total, pagina, filas = await datos.ReadAsync() };
    }

    public async Task<object> CambiarAtencion(Cuenta actor, int identificador, bool iniciar)
    {
        var puesto = actor.PuestoId ?? throw new RuleException("La captura requiere una cuenta de puesto.", 403);
        if (identificador <= 0) throw new RuleException("Identificador inválido.", 400);
        await using var c = await Open();
        return iniciar
            ? await c.QuerySingleAsync("dbo.IniciarAtencion", new { puesto, ubicacion = identificador },
                commandType: System.Data.CommandType.StoredProcedure)
            : await c.QuerySingleAsync("dbo.FinalizarAtencion", new { puesto, registro = identificador },
                commandType: System.Data.CommandType.StoredProcedure);
    }

    async Task<object> CatalogoPuestos(Cuenta actor)
    {
        await using var c = await Open();
        var scope = new { puesto = actor.PuestoId };
        var materiales = (await c.QueryAsync("""
            SELECT m.numero_parte,m.id_puesto,m.descripcion,m.uso,m.cantidad_por_caja,m.minimo,
                   m.stock,p.nombre puesto,c.nombre variante_revision
            FROM dbo.MATERIAL m JOIN dbo.PUESTO_LOGISTICO p ON p.id_puesto=m.id_puesto
            LEFT JOIN dbo.COLOR c ON c.id_color=m.id_color
            WHERE @puesto IS NULL OR m.id_puesto=@puesto ORDER BY numero_parte
            """, scope)).ToArray();
        var puestos = (await c.QueryAsync("""
            SELECT id_puesto,nombre,CONVERT(nvarchar(60),NULL) tipo,
                   CASE WHEN @puesto IS NULL THEN usuario END usuario,
                   CONVERT(bit,CASE WHEN usuario IS NULL THEN 0 ELSE 1 END) acceso_habilitado
            FROM dbo.PUESTO_LOGISTICO WHERE @puesto IS NULL OR id_puesto=@puesto ORDER BY nombre
            """, scope)).ToArray();
        // Con continuación, la capacidad vigente incluye los ajustes del diario.
        var sitios = ContinuacionHabilitada ? Alcance(await EstadoContinuacion(), actor)
            : (await c.QueryAsync<InventarioAlarma>(FilasPuestosSql, scope)).ToArray();
        var atributos = AtributosCatalogo();
        if (actor.PuestoId.HasValue && atributos is JsonElement doc)
        {
            var partes = materiales.Select(m => (string)m.numero_parte).ToHashSet(StringComparer.Ordinal);
            var codigos = puestos.Select(p => (string)p.nombre).ToHashSet(StringComparer.Ordinal);
            atributos = new {
                version = doc.GetProperty("version"), definicionUso = doc.GetProperty("definicionUso"),
                fuentes = doc.GetProperty("fuentes"),
                materiales = doc.GetProperty("materiales").EnumerateObject().Where(p => partes.Contains(p.Name))
                    .ToDictionary(p => p.Name, p => p.Value),
                puestos = doc.GetProperty("puestos").EnumerateObject().Where(p => codigos.Contains(p.Name))
                    .ToDictionary(p => p.Name, p => p.Value)
            };
        }
        // Con continuación, los datos de material vigentes incluyen lo importado desde Excel (diario).
        object[] materialesVigentes = materiales;
        if (ContinuacionHabilitada)
        {
            var vigentes = sitios.GroupBy(f => f.numero_parte).ToDictionary(g => g.Key, g => g.First(), StringComparer.Ordinal);
            materialesVigentes = materiales.Select(m =>
            {
                string parte = m.numero_parte;
                return vigentes.TryGetValue(parte, out var f)
                    ? (object)new { numero_parte = parte, id_puesto = (int)m.id_puesto, f.descripcion, f.uso, f.cantidad_por_caja,
                        f.minimo, stock = (int?)m.stock, puesto = (string)m.puesto, f.variante_revision }
                    : (object)m;
            }).ToArray();
        }
        return new {
            modelo_puestos = true, continuacion_habilitada = ContinuacionHabilitada, exportacion_historica = EsDemo, atributos, materiales = materialesVigentes, puestos,
            imagenes = VersionesImagenes(materiales.Select(m => (string)m.numero_parte)),
            // Variantes válidas para la importación de Excel (COLOR.nombre de esta base).
            colores = await c.QueryAsync<string>("SELECT nombre FROM dbo.COLOR ORDER BY nombre"),
            estaciones = sitios.Select(s => new { s.id_estacion, nombre_estacion = s.estacion }).Distinct(),
            estanterias = sitios.Select(s => new { s.id_estanteria, s.id_estacion, s.estanteria, s.nivel, s.cajas_estanteria }).Distinct(),
            turnos = await c.QueryAsync("""
                SELECT id_turno,nombre_turno,CONVERT(varchar(8),hora_inicio) hora_inicio,
                       CONVERT(varchar(8),hora_fin) hora_fin FROM dbo.TURNO ORDER BY hora_inicio
                """),
            corte = archivoDemo is { } archivo ? (object)archivo.Corte() :
                await c.QuerySingleAsync("SELECT maestro,revision,corte_fuente,fecha_carga,sellado FROM archivo.CORTE")
        };
    }

    string FilasPuestosSql => $"""
        SELECT * FROM {EsquemaVistas}.vw_kpi_inventario_actual WHERE @puesto IS NULL OR id_puesto=@puesto
        ORDER BY numero_parte,id_ubicacion
        """;

    public async Task<InventarioAlarma[]> FilasPuestos(Cuenta actor)
    {
        if (ContinuacionHabilitada) return Alcance(await EstadoContinuacion(), actor);
        await using var c = await Open();
        return (await c.QueryAsync<InventarioAlarma>(FilasPuestosSql, new { puesto = actor.PuestoId })).ToArray();
    }

    async Task<object> InventarioPuestos(Cuenta actor)
    {
        var filas = await FilasPuestos(actor);
        var minimos = filas.GroupBy(r => r.numero_parte).Select(g =>
        {
            var material = g.First();
            var comparacion = MotorCobertura.Minimo([material.stock_total_material is long saldo ? (int)saldo : null],
                material.minimo);
            return new { numero_parte = g.Key, saldo_total = comparacion.Total,
                minimo = material.minimo, estado = comparacion.Estado, ubicaciones = g.Count() };
        });
        return new { corte = filas.FirstOrDefault()?.fecha_stock, simulado = true, filas, minimos,
            alarmas = Array.Empty<object>(), cobertura_disponible = false };
    }
}
