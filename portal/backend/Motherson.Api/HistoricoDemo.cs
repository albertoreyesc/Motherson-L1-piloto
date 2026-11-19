using System.Buffers;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Motherson;

internal sealed record FiltroHistorico(
    string? Parte, int? Id, string? Espacio, DateTime? Inicio, DateTime? Fin, string? Origen, bool Exacta = false, string? Responsable = null, int? Turno = null);

internal sealed partial class HistoricoDemo(string carpeta)
{
    internal const string Base = "Motherson_L1_MVP_Demo";
    const string Movimientos = "movimientos.jsonl.gz";
    static readonly byte[] Salto = [(byte)'\n'];
    readonly SemaphoreSlim lectura = new(1);
    readonly object verificacion = new();
    volatile Fila[]? indice;
    JsonElement? corteVerificado;

    sealed record Fila(byte[] Json, string? Parte, int? Ubicacion, string Espacio, DateTime? Fecha, string? Responsable, int? Turno);

    static RuleException ErrorArchivo() =>
        new("El paquete histórico de demostración falta o no coincide con su manifiesto.", 503);

    public JsonElement Corte()
    {
        lock (verificacion)
        {
            if (corteVerificado.HasValue) return corteVerificado.Value;
            try
            {
                using var documento = JsonDocument.Parse(File.ReadAllBytes(Path.Combine(carpeta, "manifiesto.json")));
                var datos = documento.RootElement;
                var corte = datos.GetProperty("corte");
                if (datos.GetProperty("version").GetInt32() != 1 || !corte.GetProperty("sellado").GetBoolean())
                    throw ErrorArchivo();
                var vistos = new HashSet<string>(StringComparer.Ordinal);
                foreach (var archivo in datos.GetProperty("archivos").EnumerateArray())
                {
                    var nombre = archivo.GetProperty("nombre").GetString()!;
                    if (!NombreArchivo().IsMatch(nombre) || !vistos.Add(nombre))
                        throw ErrorArchivo();
                    using var origen = File.OpenRead(Path.Combine(carpeta, nombre));
                    var huella = Convert.ToHexString(SHA256.HashData(origen));
                    if (!huella.Equals(archivo.GetProperty("sha256").GetString(), StringComparison.OrdinalIgnoreCase))
                        throw ErrorArchivo();
                }
                if (vistos.Count != 12 || !vistos.IsSupersetOf([Movimientos, "registros.jsonl.gz", "movimientos-mvp-originales.jsonl.gz"]))
                    throw ErrorArchivo();
                corteVerificado = corte.Clone();
                return corteVerificado.Value;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException
                or InvalidOperationException or KeyNotFoundException or ArgumentException or FormatException)
            {
                throw ErrorArchivo();
            }
        }
    }

    [GeneratedRegex(@"\A[a-z0-9_-]+\.jsonl\.gz\z")]
    private static partial Regex NombreArchivo();

    async Task<Fila[]> Leer(CancellationToken token)
    {
        if (indice is { } disponible) return disponible;
        await lectura.WaitAsync(token);
        try
        {
            if (indice is { } creado) return creado;
            var cantidad = Corte().GetProperty("movimientos_consultables").GetInt32();
            var filas = new List<Fila>(cantidad);
            await using var origen = File.OpenRead(Path.Combine(carpeta, Movimientos));
            await using var comprimido = new GZipStream(origen, CompressionMode.Decompress);
            using var texto = new StreamReader(comprimido);
            while (await texto.ReadLineAsync(token) is { } linea)
            {
                var bytes = Encoding.UTF8.GetBytes(linea);
                using var json = JsonDocument.Parse(bytes);
                var fila = json.RootElement;
                var ubicacion = fila.GetProperty("id_ubicacion");
                var fecha = fila.GetProperty("fecha_movimiento");
                filas.Add(new(bytes, fila.GetProperty("numero_parte").GetString(),
                    ubicacion.ValueKind == JsonValueKind.Null ? null : ubicacion.GetInt32(),
                    fila.GetProperty("espacio_identificadores").GetString()!,
                    fecha.ValueKind == JsonValueKind.Null ? null : fecha.GetDateTime(),
                    Texto(fila, "responsable_simulado"), Entero(fila, "id_turno")));
            }
            if (filas.Count != cantidad) throw ErrorArchivo();
            return indice = filas.ToArray();
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException
            or JsonException or InvalidOperationException or KeyNotFoundException or FormatException)
        {
            throw ErrorArchivo();
        }
        finally
        {
            lectura.Release();
        }
    }

    static IEnumerable<Fila> Coincidencias(Fila[] filas, FiltroHistorico filtro)
    {
        var parte = filtro.Parte;
        var comodines = parte is not null && parte.IndexOfAny(['%', '_']) >= 0
            ? new Regex(Regex.Escape(parte).Replace("%", ".*").Replace("_", "."),
                RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.NonBacktracking)
            : null;
        return filas.Where(r =>
            (filtro.Origen is null || r.Espacio == filtro.Origen) &&
            (filtro.Id is null || (r.Ubicacion == filtro.Id && r.Espacio == filtro.Espacio)) &&
            (filtro.Inicio is null || r.Fecha >= filtro.Inicio) &&
            (filtro.Fin is null || r.Fecha < filtro.Fin) &&
            (filtro.Responsable is null || r.Responsable == filtro.Responsable) &&
            (filtro.Turno is null || r.Turno == filtro.Turno) &&
            (parte is null || (r.Parte is { } codigo &&
                (filtro.Exacta ? codigo == parte : (comodines?.IsMatch(codigo) ?? codigo.Contains(parte, StringComparison.OrdinalIgnoreCase))))));
    }

    // Metadatos del paquete sellado; consultar no genera eventos ni modifica SQL.
    public async Task<object> Opciones(CancellationToken token)
    {
        var filas = await Leer(token);
        return new {
            materiales = filas.Where(r => r.Espacio == "HISTORICO_21" && r.Parte is not null)
                .Select(r => r.Parte!).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal),
            responsables = LeerFuente("responsables"),
            turnos = LeerFuente("turnos"),
            desde = "2025-10-25", hasta = "2026-10-24",
            maestro = Corte().GetProperty("maestro")
        };
    }

    public async Task<object> Pagina(FiltroHistorico filtro, int pagina, CancellationToken token)
    {
        var coincidencias = Coincidencias(await Leer(token), filtro);
        var filas = new List<JsonElement>(50);
        var inicio = (pagina - 1) * 50;
        var total = 0;
        foreach (var fila in coincidencias)
        {
            if (total >= inicio && filas.Count < 50)
                filas.Add(JsonSerializer.Deserialize<JsonElement>(fila.Json));
            total++;
        }
        return new { total, pagina, filas };
    }

    public async Task Exportar(FiltroHistorico filtro, HttpResponse respuesta, CancellationToken token)
    {
        var filas = Coincidencias(await Leer(token), filtro);
        respuesta.ContentType = "application/x-ndjson; charset=utf-8";
        respuesta.Headers.ContentDisposition = "attachment; filename=movimientos-motherson-l1.jsonl";
        var salida = respuesta.BodyWriter;
        foreach (var fila in filas)
        {
            salida.Write(fila.Json);
            salida.Write(Salto);
            if (salida.UnflushedBytes >= 65536 && (await salida.FlushAsync(token)).IsCompleted)
                return;
        }
        await salida.FlushAsync(token);
    }
}
