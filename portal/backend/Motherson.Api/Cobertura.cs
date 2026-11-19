using System.Text.Json;
namespace Motherson;

public record IntervaloLaborable(DateTime Inicio, DateTime Fin);
public record VentanaCobertura(IReadOnlyList<IntervaloLaborable> Intervalos, string? Motivo)
{
    public bool Completa => Motivo is null && Intervalos.Count > 0;
    public decimal Minutos => Intervalos.Sum(i => (decimal)(i.Fin - i.Inicio).Ticks / TimeSpan.TicksPerMinute);
}
public record ConsumoCobertura(string Id, int Ubicacion, string Parte, string Espacio,
    string Tipo, int Piezas, DateTime Fecha, bool Simulado);
public record ResultadoCobertura(string Estado, string Prioridad, string Causa,
    decimal? Minutos, decimal? TasaPiezasHora, long PiezasAplicadas, int Observaciones,
    DateTime? UltimoConsumo, string Metodo = "CONSUMO_60_MIN_LABORABLES");

// Calendario documental: se conservan sus días explícitos, sin extenderlo con festivos inferidos.
public sealed class CalendarioCobertura
{
    readonly DateOnly desde, hasta;
    readonly Dictionary<DateOnly, bool> dias;
    readonly (TimeOnly Inicio, TimeOnly Fin)[] turnos;
    readonly TimeZoneInfo zona;
    public string Fuente { get; }
    public string HuellaFuente { get; }
    public string Periodo => $"{desde:yyyy-MM-dd} a {hasta.AddDays(-1):yyyy-MM-dd}";
    public CalendarioCobertura(DateOnly desde, DateOnly hasta, Dictionary<DateOnly, bool> dias,
        (TimeOnly Inicio, TimeOnly Fin)[] turnos, string fuente, string huella)
    {
        this.desde = desde; this.hasta = hasta; this.dias = dias;
        this.turnos = turnos.OrderBy(t => t.Inicio).ToArray();
        zona = TimeZoneInfo.FindSystemTimeZoneById("America/Mexico_City");
        Fuente = fuente; HuellaFuente = huella;
        if (desde >= hasta || this.turnos.Length == 0 || this.turnos.Any(t => t.Inicio >= t.Fin)
            || this.turnos.Zip(this.turnos.Skip(1)).Any(t => t.First.Fin > t.Second.Inicio))
            throw new InvalidDataException("Calendario o turnos inválidos.");
    }
    public static CalendarioCobertura Leer(string path)
    {
        using var doc = JsonDocument.Parse(File.ReadAllText(path)); var r = doc.RootElement;
        if (r.GetProperty("maestro").GetString() != "4d577245a22d" || r.GetProperty("zona").GetString() != "America/Mexico_City")
            throw new InvalidDataException("Calendario de otro histórico o zona.");
        var dias = r.GetProperty("dias").EnumerateArray().ToDictionary(
            d => DateOnly.Parse(d.GetProperty("date").GetString()!), d => d.GetProperty("working").GetBoolean());
        var turnos = r.GetProperty("turnos").EnumerateArray().Select(t =>
            (TimeOnly.Parse(t.GetProperty("start").GetString()!), TimeOnly.Parse(t.GetProperty("end").GetString()!))).ToArray();
        return new(DateOnly.Parse(r.GetProperty("desde").GetString()!), DateOnly.Parse(r.GetProperty("hasta_exclusivo").GetString()!),
            dias, turnos, r.GetProperty("fuente").GetString()!, r.GetProperty("sha256_fuente").GetString()!);
    }
    DateTime Utc(DateOnly dia, TimeOnly hora) => TimeZoneInfo.ConvertTimeToUtc(dia.ToDateTime(hora), zona);
    public VentanaCobertura Ventana(DateTime corte)
    {
        corte = DateTime.SpecifyKind(corte, DateTimeKind.Utc);
        var dia = DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(corte, zona));
        if (dia < desde || dia >= hasta) return new([], "FUERA_CALENDARIO_DOCUMENTADO");
        var tramos = new List<IntervaloLaborable>(); decimal faltan = 60;
        for (; dia >= desde; dia = dia.AddDays(-1))
        {
            if (!dias.TryGetValue(dia, out var laboral)) return new([], "DIA_SIN_CALENDARIO");
            if (!laboral) continue;
            foreach (var turno in turnos.Reverse())
            {
                var inicio = Utc(dia, turno.Inicio); var fin = Utc(dia, turno.Fin);
                if (fin > corte) fin = corte;
                if (fin <= inicio) continue;
                var disponibles = (decimal)(fin - inicio).Ticks / TimeSpan.TicksPerMinute;
                var tomar = Math.Min(faltan, disponibles);
                tramos.Add(new(fin.AddTicks(-(long)(tomar * TimeSpan.TicksPerMinute)), fin)); faltan -= tomar;
                if (faltan == 0) return new(tramos.OrderBy(t => t.Inicio).ToArray(), null);
            }
        }
        return new([], "VENTANA_INCOMPLETA");
    }
}

public record ResultadoMinimo(long? Total, string Estado);

public static class MotorCobertura
{
    public static ResultadoMinimo Minimo(IEnumerable<int?> cantidades, int? minimo)
    {
        var filas = cantidades.ToArray();
        long? total = filas.Length > 0 && filas.All(c => c.HasValue && c.Value >= 0) ? filas.Sum(c => (long)c!.Value) : null;
        return new(total, total is null ? "SIN_DATOS" : total == 0 ? "AGOTADO" : minimo is null || minimo < 0 ? "SIN_DATOS" : total <= minimo ? "BAJO_MINIMO" : "SOBRE_MINIMO");
    }
    public const string Politica = "COBERTURA_60_30_15_V1";
    public static string Clasificar(decimal minutos) => minutos >= 60 ? "NORMAL" : minutos >= 30 ? "ADVERTENCIA" : minutos >= 15 ? "CRITICO" : "RIESGO_PARO";
    public static ResultadoCobertura Evaluar(int? saldo, int ubicacion, string parte, DateTime corte,
        VentanaCobertura ventana, DateTime? apertura, IEnumerable<ConsumoCobertura> consumos, string espacio = "MVP")
    {
        ResultadoCobertura SinDatos(string causa) => new("SIN_DATOS", "N/D", causa, null, null, 0, 0, null);
        if (saldo is null || saldo < 0) return SinDatos("SALDO_DESCONOCIDO");
        if (saldo == 0) return new("AGOTADO", "ALTA / CRÍTICA", "SALDO_CERO", 0, null, 0, 0, null);
        if (!ventana.Completa || ventana.Minutos != 60) return SinDatos(ventana.Motivo ?? "VENTANA_INCOMPLETA");
        if (ventana.Intervalos.Any(i => i.Inicio >= i.Fin || i.Fin > corte)) return SinDatos("CORTE_INCONSISTENTE");
        if (apertura is null || apertura > ventana.Intervalos[0].Inicio) return SinDatos("OBSERVACION_INCOMPLETA_DESDE_APERTURA");
        // Cada consulta conserva su espacio de identificadores; nunca se unen por coincidencia numérica.
        var elegibles = consumos.Where(c => c.Espacio == espacio && c.Simulado && c.Ubicacion == ubicacion && c.Parte == parte
            && c.Tipo == "SALIDA" && c.Piezas > 0 && ventana.Intervalos.Any(i => c.Fecha >= i.Inicio && c.Fecha < i.Fin)).ToArray();
        if (elegibles.GroupBy(c => c.Id).Any(g => g.Distinct().Count() > 1)) return SinDatos("IDENTIFICADOR_CONFLICTIVO");
        var unicos = elegibles.DistinctBy(c => c.Id).ToArray();
        if (unicos.Length == 0) return SinDatos("SIN_CONSUMO_RECIENTE");
        long piezas = unicos.Sum(c => (long)c.Piezas); var ultimo = unicos.Max(c => c.Fecha);
        // Dos observaciones es un criterio conservador explícito, pendiente de validar con planta.
        if (unicos.Length < 2) return new("SIN_DATOS", "N/D", "MUESTRA_INSUFICIENTE", null, null, piezas, unicos.Length, ultimo);
        decimal tasa = piezas / ventana.Minutos * 60;
        decimal minutos = saldo.Value / tasa * 60;
        var estado = Clasificar(minutos);
        var prioridad = estado == "NORMAL" ? "NORMAL" : estado == "ADVERTENCIA" ? "MEDIA" : estado == "CRITICO" ? "CRÍTICA" : "ALTA / CRÍTICA";
        return new(estado, prioridad, "RITMO_DE_REFERENCIA_SIN_REPOSICIONES", minutos, tasa, piezas, unicos.Length, ultimo);
    }
}
