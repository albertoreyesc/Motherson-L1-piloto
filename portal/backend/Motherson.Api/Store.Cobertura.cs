using System.Text.Json;
using System.Data;
using Dapper;
using Microsoft.Data.SqlClient;
namespace Motherson;

public sealed class InventarioAlarma
{
    public int id_ubicacion { get; set; }
    public string numero_parte { get; set; } = "";
    public string? descripcion { get; set; }
    public string? uso { get; set; }
    public int id_puesto { get; set; }
    public string puesto { get; set; } = "";
    public int? cantidad_por_caja { get; set; }
    public int? minimo { get; set; }
    public int? cantidad { get; set; }
    public int? version_concurrencia { get; set; }
    public string? variante_revision { get; set; }
    public DateTime? fecha_stock { get; set; }
    public int id_estanteria { get; set; }
    public string estanteria { get; set; } = "";
    public string nivel { get; set; } = "";
    public int? cajas_estanteria { get; set; }
    public int id_estacion { get; set; }
    public string estacion { get; set; } = "";
    public long? stock_total_material { get; set; }
    public long? cajas_disponibles { get; set; }
    public bool es_simulado { get; set; }
}
public sealed class LecturaConsumo
{
    public int id_registro { get; set; }
    public int id_ubicacion { get; set; }
    public string numero_parte { get; set; } = "";
    public string tipo_movimiento { get; set; } = "";
    public int cantidad_movimiento { get; set; }
    public DateTime fecha_movimiento { get; set; }
    public bool es_simulado { get; set; }
}
public sealed class AperturaCobertura
{
    public int id_ubicacion { get; set; }
    public DateTime fecha_movimiento { get; set; }
}
public sealed partial class Store
{
    static readonly Lazy<CalendarioCobertura?> Calendario = new(() =>
    {
        try { return CalendarioCobertura.Leer(Path.Combine(Environment.GetEnvironmentVariable("MVP_ROOT") ?? ".", "config", "calendario-historico.json")); }
        catch (Exception ex) when (ex is IOException or JsonException or InvalidDataException or FormatException or KeyNotFoundException) { return null; }
    });
    public async Task<object> InventoryState(Cuenta actor)
    {
        if (ContinuacionHabilitada) return await InventarioContinuacion(actor);
        if (ModeloPuestos) return await InventarioPuestos(actor);
        await using var c = await Open();
        await using var tx = (SqlTransaction)await c.BeginTransactionAsync(IsolationLevel.Serializable);
        // Comparte el recurso ya usado por RegistrarMovimiento: misma lectura para saldo y corte.
        var bloqueo = await c.ExecuteScalarAsync<int>("DECLARE @r int; EXEC @r=sp_getapplock @Resource='MVP_INVENTARIO',@LockMode='Shared',@LockOwner='Transaction',@LockTimeout=3000;SELECT @r;", transaction: tx);
        if (bloqueo < 0) throw new RuleException("Inventario ocupado. Actualiza la consulta.", 409);
        var corte = await c.ExecuteScalarAsync<DateTime>("SELECT SYSUTCDATETIME()", transaction: tx);
        var filas = (await c.QueryAsync<InventarioAlarma>(Inventario + " ORDER BY v.numero_parte,v.id_ubicacion", transaction: tx)).ToArray();
        var calendario = Calendario.Value;
        var ventana = calendario?.Ventana(corte) ?? new VentanaCobertura([], "CALENDARIO_NO_DISPONIBLE");
        ConsumoCobertura[] consumos = []; Dictionary<int, DateTime> aperturas = [];
        if (ventana.Completa)
        {
            var desde = ventana.Intervalos[0].Inicio;
            var p = new DynamicParameters(); p.Add("desde", desde, DbType.DateTime2); p.Add("corte", corte, DbType.DateTime2);
            var entradas = await c.QueryAsync<AperturaCobertura>("SELECT id_ubicacion,fecha_movimiento FROM dbo.REGISTRO WHERE tipo_movimiento='APERTURA' AND fecha_movimiento<=@corte", p, tx);
            aperturas = entradas.ToDictionary(a => a.id_ubicacion, a => a.fecha_movimiento);
            var registros = await c.QueryAsync<LecturaConsumo>("SELECT r.id_registro,r.id_ubicacion,u.numero_parte,r.tipo_movimiento,r.cantidad_movimiento,r.fecha_movimiento,CONVERT(bit,1) es_simulado FROM dbo.REGISTRO r JOIN dbo.UBICACION u ON u.id_ubicacion=r.id_ubicacion WHERE r.tipo_movimiento='SALIDA' AND r.origen='CAPTURA_SIMULADA' AND r.fecha_movimiento>=@desde AND r.fecha_movimiento<@corte", p, tx);
            consumos = registros.Select(r => new ConsumoCobertura("MVP:" + r.id_registro, r.id_ubicacion, r.numero_parte, "MVP", r.tipo_movimiento, r.cantidad_movimiento, r.fecha_movimiento, r.es_simulado)).ToArray();
        }
        await tx.CommitAsync();
        var alarmas = filas.Select(r => new
        {
            r.id_ubicacion, r.numero_parte,
            resultado = MotorCobertura.Evaluar(r.cantidad, r.id_ubicacion, r.numero_parte, corte, ventana,
                aperturas.TryGetValue(r.id_ubicacion, out var apertura) ? apertura : null, consumos)
        }).ToArray();
        var minimos = filas.GroupBy(r => r.numero_parte).Select(g =>
        {
            var minimo = g.First().minimo;
            var comparacion = MotorCobertura.Minimo(g.Select(r => r.cantidad), minimo);
            return new { numero_parte = g.Key, saldo_total = comparacion.Total, minimo, estado = comparacion.Estado, ubicaciones = g.Count() };
        }).ToArray();
        return new { corte, politica = MotorCobertura.Politica, simulado = true, filas, alarmas, minimos,
            calendario = new { fuente = calendario?.Fuente, huellaFuente = calendario?.HuellaFuente, periodo = calendario?.Periodo, motivo = ventana.Motivo },
            ventana = new { intervalos = ventana.Intervalos, minutosLaborables = ventana.Minutos, completa = ventana.Completa },
            observacionesMinimas = 2, metodo = "CONSUMO_60_MIN_LABORABLES" };
    }
}
