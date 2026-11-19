using System.IO.Compression;
using System.Text.Json;

namespace Motherson;

internal sealed record DatosCoberturaHistorica(InventarioAlarma[] Filas,
    ConsumoCobertura[] Consumos, Dictionary<int, DateTime> Aperturas);

internal sealed partial class HistoricoDemo
{
    static Fila ComoFila(JsonElement j) => new(JsonSerializer.SerializeToUtf8Bytes(j),
        Texto(j, "numero_parte"), Entero(j, "id_ubicacion"), Texto(j, "espacio_identificadores")!, Fecha(j, "fecha_movimiento"),
        Texto(j, "responsable_simulado"), Entero(j, "id_turno"));
    internal static bool Cumple(JsonElement j, FiltroHistorico f) => Coincidencias([ComoFila(j)], f).Any();
    async Task<Fila[]> Combinadas(FiltroHistorico f, JsonElement[] nuevas, CancellationToken t) =>
        Coincidencias(await Leer(t), f).Concat(nuevas.Select(ComoFila)).OrderByDescending(r => r.Fecha)
            .ThenBy(r => r.Espacio, StringComparer.Ordinal).ToArray();

    public async Task<object> PaginaCombinada(FiltroHistorico f, int pagina, JsonElement[] nuevas, CancellationToken t)
    {
        var todas = await Combinadas(f, nuevas, t);
        return new { total = todas.Length, pagina,
            filas = todas.Skip((pagina - 1) * 50).Take(50).Select(r => JsonSerializer.Deserialize<JsonElement>(r.Json)) };
    }
    public async Task ExportarCombinado(FiltroHistorico f, JsonElement[] nuevas, HttpResponse respuesta, CancellationToken t)
    {
        respuesta.ContentType = "application/x-ndjson; charset=utf-8";
        respuesta.Headers.ContentDisposition = "attachment; filename=movimientos-motherson-l1.jsonl";
        foreach (var fila in await Combinadas(f, nuevas, t))
        {
            await respuesta.Body.WriteAsync(fila.Json, t);
            await respuesta.Body.WriteAsync(Salto, t);
        }
    }

    sealed record MovimientoFuente(string Id, int Posicion, string Parte, string Tipo, int Piezas,
        int? Saldo, DateTime Fecha, string Proceso, long Orden);
    MovimientoFuente[]? consumosHistoricos;
    readonly SemaphoreSlim coberturaMutex = new(1);
    JsonElement[] LeerFuente(string nombre)
    {
        Corte();
        using var archivo = File.OpenRead(Path.Combine(carpeta, nombre + ".jsonl.gz"));
        using var gzip = new GZipStream(archivo, CompressionMode.Decompress);
        using var lector = new StreamReader(gzip);
        var datos = new List<JsonElement>();
        while (lector.ReadLine() is { } linea) datos.Add(JsonSerializer.Deserialize<JsonElement>(linea));
        return datos.ToArray();
    }
    static string? Texto(JsonElement j, string p) => j.TryGetProperty(p, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;
    static int? Entero(JsonElement j, string p) => j.TryGetProperty(p, out var v) && v.ValueKind == JsonValueKind.Number ? v.GetInt32() : null;
    static DateTime? Fecha(JsonElement j, string p) => Texto(j, p) is { } s ? DateTime.SpecifyKind(DateTime.Parse(s, null,
        System.Globalization.DateTimeStyles.RoundtripKind), DateTimeKind.Utc) : null;
    async Task<MovimientoFuente[]> FuentesCobertura(CancellationToken token)
    {
        await coberturaMutex.WaitAsync(token);
        try
        {
            if (consumosHistoricos is not null) return consumosHistoricos;
            var datos = new List<MovimientoFuente>();
            foreach (var f in await Leer(token))
            {
                if (f.Espacio != "HISTORICO_21") continue;
                using var doc = JsonDocument.Parse(f.Json); var j = doc.RootElement;
                // La proyección declara la posición de origen. Nunca se cruza con un ID SQL del MVP.
                if (Entero(j, "id_posicion_origen") is not { } posicion || f.Fecha is not { } fecha || f.Parte is null) continue;
                var id = Texto(j, "id_movimiento")!;
                if (!long.TryParse(id.Split(':').Last(), out var orden)) throw ErrorArchivo();
                datos.Add(new(id, posicion, f.Parte, Texto(j, "tipo_movimiento")!, Entero(j, "cantidad_piezas") ?? 0,
                    Entero(j, "saldo_posterior"), fecha, Texto(j, "proceso_generador") ?? "", orden));
            }
            if (datos.GroupBy(d => d.Id).Any(g => g.Distinct().Count() != 1)) throw ErrorArchivo();
            return consumosHistoricos = datos.DistinctBy(d => d.Id).ToArray();
        }
        finally { coberturaMutex.Release(); }
    }

    public async Task<DatosCoberturaHistorica> Cobertura(DateTime corte, int? puesto, CancellationToken token)
    {
        var eventos = (await FuentesCobertura(token)).Where(e => e.Fecha <= corte).ToArray();
        var posiciones = LeerFuente("posiciones");
        var versiones = LeerFuente("configuraciones_posicion");
        var filas = new List<InventarioAlarma>();
        var aperturas = new Dictionary<int, DateTime>();
        foreach (var pos in posiciones)
        {
            var id = Entero(pos, "id_posicion")!.Value;
            if (puesto.HasValue && Entero(pos, "id_puesto") != puesto || Fecha(pos, "fecha_alta_utc") > corte
                || Fecha(pos, "fecha_retiro_utc") <= corte) continue;
            var parte = Texto(pos, "numero_parte")!;
            var propios = eventos.Where(e => e.Posicion == id && e.Parte == parte).OrderBy(e => e.Fecha).ThenBy(e => e.Orden).ToArray();
            var config = versiones.Where(v => Entero(v, "id_posicion") == id && (Fecha(v, "fecha_revision_utc") ?? DateTime.MinValue) <= corte)
                .OrderBy(v => Fecha(v, "fecha_revision_utc") ?? DateTime.MinValue).LastOrDefault();
            var f = new InventarioAlarma { id_ubicacion = id, numero_parte = parte,
                id_puesto = Entero(pos, "id_puesto")!.Value, puesto = Texto(pos, "codigo_puesto")!,
                id_estacion = Entero(pos, "id_estacion")!.Value, estacion = Texto(pos, "codigo_estacion")!,
                id_estanteria = Entero(pos, "id_estanteria")!.Value, estanteria = Texto(pos, "codigo_estanteria")!,
                nivel = Texto(pos, "nivel")!, descripcion = Texto(pos, "descripcion_operativa"),
                cantidad = propios.LastOrDefault()?.Saldo, fecha_stock = propios.LastOrDefault()?.Fecha,
                es_simulado = true };
            if (config.ValueKind == JsonValueKind.Object)
            { f.minimo = Entero(config, "minimo_piezas"); f.cantidad_por_caja = Entero(config, "piezas_por_caja"); f.cajas_estanteria = Entero(config, "capacidad_cajas"); }
            if (propios.FirstOrDefault(e => e.Tipo == "APERTURA") is { } apertura) aperturas[id] = apertura.Fecha;
            filas.Add(f);
        }
        // Solo el proceso documental de consumo aporta observaciones. No se interpretan retiros administrativos.
        var consumos = eventos.Where(e => e.Tipo == "SALIDA" && e.Proceso is "SIMULADOR_ANUAL" or "SIMULADOR_CONTINUACION")
            .Select(e => new ConsumoCobertura(e.Id, e.Posicion, e.Parte, "HISTORICO_21", "SALIDA", e.Piezas, e.Fecha, true)).ToArray();
        return new(filas.ToArray(), consumos, aperturas);
    }
}
