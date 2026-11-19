namespace Motherson;

internal sealed record OperacionKpi(int Id, string Parte, string Estacion, int Posicion, int Puesto,
    int? TurnoInicio, DateTime? Inicio, DateTime? Fin, string? Responsable, string? Nombre,
    int? TurnoResponsable, bool UbicacionValida, string Proceso);
internal sealed record MaterialKpi(string Parte, decimal? Saldo, int? Minimo, string Estado,
    decimal? PorcentajeMinimo, int Posiciones);

// Funciones puras: las fuentes y el saldo se leen en Store.Kpis; aquí no hay persistencia.
internal static class KpisMotor
{
    public static string Clasificar(decimal? saldo, int? minimo)
    {
        if (saldo is null || saldo < 0) return "SIN_DATOS";
        if (saldo == 0) return "AGOTADO";
        if (minimo is null || minimo < 0) return "SIN_DATOS";
        if (saldo <= minimo) return "CRITICO";
        return saldo <= 1.20m * minimo ? "ADVERTENCIA" : "NORMAL";
    }
    public static MaterialKpi[] Materiales(IEnumerable<InventarioAlarma> filas) => filas.GroupBy(f => f.numero_parte)
        .Select(g => {
            var minimos = g.Select(f => f.minimo).Distinct().ToArray();
            var minimo = minimos.Length == 1 ? minimos[0] : null;
            decimal? saldo = g.All(f => f.cantidad is >= 0) ? g.Sum(f => (decimal)f.cantidad!.Value) : null;
            return new MaterialKpi(g.Key, saldo, minimo, Clasificar(saldo, minimo),
                minimo > 0 && saldo.HasValue ? saldo * 100m / minimo : null, g.Count());
        }).OrderBy(m => m.Parte, StringComparer.Ordinal).ToArray();
    public static bool Completada(OperacionKpi o, DateTime desde, DateTime hasta, DateTime corte) =>
        o.Inicio.HasValue && o.Fin.HasValue && o.Fin >= o.Inicio && o.Fin >= desde && o.Fin < hasta && o.Fin <= corte;
    public static bool Abierta(OperacionKpi o, DateTime corte) => o.Inicio <= corte && (o.Fin is null || o.Fin > corte);
    public static int Repeticiones(IEnumerable<OperacionKpi> operaciones) => operaciones.GroupBy(o =>
        (o.Parte, o.Estacion, Dia(o.Inicio!.Value))).Sum(g => Math.Max(0, g.Count() - 1));
    public static double[] Intervalos(IEnumerable<OperacionKpi> operaciones) => operaciones.GroupBy(o => (o.Parte, o.Estacion))
        .SelectMany(g => { var fechas = g.OrderBy(o => o.Inicio).ThenBy(o => o.Id).Select(o => o.Inicio!.Value).ToArray();
            return fechas.Skip(1).Select((f, i) => (f - fechas[i]).TotalHours); }).ToArray();
    public static readonly TimeZoneInfo Mexico = TimeZoneInfo.FindSystemTimeZoneById("America/Mexico_City");
    public static string Dia(DateTime utc) => TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utc, DateTimeKind.Utc), Mexico).ToString("yyyy-MM-dd");
    public static int? Turno(DateTime utc)
    {
        var hora = TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utc, DateTimeKind.Utc), Mexico).TimeOfDay;
        return hora >= TimeSpan.FromHours(6) && hora < TimeSpan.FromHours(15) ? 2 :
            hora >= TimeSpan.FromHours(15) && hora < TimeSpan.FromMinutes(1410) ? 3 : null;
    }
    public static DateTime FechaLocal(string texto)
    {
        if (!DateTime.TryParseExact(texto, ["yyyy-MM-dd", "yyyy-MM-ddTHH:mm", "yyyy-MM-ddTHH:mm:ss"],
            System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out var fecha))
            throw new RuleException("Fecha de consulta inválida.", 400);
        return TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(fecha, DateTimeKind.Unspecified), Mexico);
    }
}
