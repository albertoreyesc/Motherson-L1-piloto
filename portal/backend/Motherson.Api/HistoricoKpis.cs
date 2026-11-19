using System.IO.Compression;
using System.Text.Json;

namespace Motherson;

internal sealed partial class HistoricoDemo
{
    sealed record PosicionKpi(int Id, string Parte, string Estacion, int Puesto);
    sealed record PersonaKpi(string Codigo, string Nombre, int Puesto, int Turno);
    sealed record EtapaKpi(string Codigo, DateTime? Fecha, string? Responsable, string? Nombre, int? Turno);
    sealed class VersionKpi(int id, int solicitud, int posicion, int puesto, int? turno, DateTime? revision, string proceso)
    {
        public int Id = id, Solicitud = solicitud, Posicion = posicion, Puesto = puesto;
        public int? Turno = turno;
        public DateTime? Revision = revision;
        public string Proceso = proceso;
        public List<EtapaKpi> Etapas = [];
    }
    VersionKpi[]? versionesKpi;
    Dictionary<int, PosicionKpi> posicionesKpi = [];
    PersonaKpi[] personasKpi = [];
    readonly SemaphoreSlim kpiMutex = new(1);
    void RecorrerKpi(string nombre, Action<JsonElement> visitar, CancellationToken t)
    {
        using var archivo = File.OpenRead(Path.Combine(carpeta, nombre + ".jsonl.gz"));
        using var gzip = new GZipStream(archivo, CompressionMode.Decompress);
        using var lector = new StreamReader(gzip);
        while (lector.ReadLine() is { } linea) { t.ThrowIfCancellationRequested(); using var doc = JsonDocument.Parse(linea); visitar(doc.RootElement); }
    }
    async Task CargarKpis(CancellationToken t)
    {
        await kpiMutex.WaitAsync(t);
        try {
            if (versionesKpi is not null) return;
            Corte();
            posicionesKpi = LeerFuente("posiciones").Select(j => new PosicionKpi(Entero(j,"id_posicion")!.Value,
                Texto(j,"numero_parte")!, Texto(j,"codigo_estacion")!, Entero(j,"id_puesto")!.Value)).ToDictionary(p => p.Id);
            personasKpi = LeerFuente("responsables").Select(j => new PersonaKpi(Texto(j,"codigo_responsable")!,
                Texto(j,"nombre_responsable")!, Entero(j,"id_puesto")!.Value, Entero(j,"id_turno")!.Value)).ToArray();
            var versiones = new Dictionary<int, VersionKpi>();
            RecorrerKpi("solicitudes", j => {
                var v = new VersionKpi(Entero(j,"id_version")!.Value, Entero(j,"id_solicitud")!.Value,
                    Entero(j,"id_posicion")!.Value, Entero(j,"id_puesto")!.Value, Entero(j,"id_turno_inicio"),
                    Fecha(j,"fecha_revision_utc"), Texto(j,"proceso_generador") ?? "SIMULADOR_ANUAL");
                if (!versiones.TryAdd(v.Id, v)) throw ErrorArchivo();
            }, t);
            RecorrerKpi("actuaciones", j => {
                var id = Entero(j,"id_version_solicitud")!.Value;
                if (!versiones.TryGetValue(id, out var v) || v.Solicitud != Entero(j,"id_solicitud")) throw ErrorArchivo();
                var e = new EtapaKpi(Texto(j,"codigo_etapa")!, Fecha(j,"fecha_actuacion_utc"),
                    Texto(j,"codigo_responsable"), Texto(j,"nombre_responsable"), Entero(j,"id_turno"));
                if (v.Etapas.Any(x => x.Codigo == e.Codigo)) throw ErrorArchivo();
                v.Etapas.Add(e);
            }, t);
            versionesKpi = versiones.Values.ToArray();
        } finally { kpiMutex.Release(); }
    }
    public async Task<object> OpcionesKpis(CancellationToken t)
    {
        await CargarKpis(t);
        return new { corteSugerido = "2026-10-23T23:30", desde = "2026-10-19", hasta = "2026-10-25",
            estaciones = posicionesKpi.Values.Select(p => p.Estacion).Distinct().Order().ToArray(),
            materiales = posicionesKpi.Values.Select(p => p.Parte).Distinct().Order().ToArray(),
            responsables = personasKpi.Select(p => new { codigo = p.Codigo, nombre = p.Nombre, puesto = p.Puesto, turno = p.Turno }),
            corteOriginal = Corte() };
    }
    public async Task<(int Puesto, int Turno)?> AlcancePersonaKpi(string? codigo, CancellationToken t)
    {
        await CargarKpis(t);
        if (string.IsNullOrEmpty(codigo)) return null;
        var p = personasKpi.SingleOrDefault(p => p.Codigo == codigo) ?? throw new RuleException("Responsable no disponible.",400);
        return (p.Puesto, p.Turno);
    }
    public async Task<OperacionKpi[]> OperacionesKpis(DateTime corte, CancellationToken t)
    {
        await CargarKpis(t);
        return versionesKpi!.Where(v => v.Revision is null || v.Revision <= corte).GroupBy(v => v.Solicitud)
            .Select(g => g.OrderBy(v => v.Revision ?? DateTime.MinValue).ThenBy(v => v.Id).Last())
            .Select(v => {
                posicionesKpi.TryGetValue(v.Posicion, out var pos);
                var inicio = v.Etapas.SingleOrDefault(e => e.Codigo == "start")?.Fecha;
                var fin = v.Etapas.SingleOrDefault(e => e.Codigo == "delivered")?.Fecha;
                // No se usa la atribución de una etapa futura ni se inventa el timestamp de confirmed.
                var actor = v.Etapas.Where(e => e.Fecha.HasValue && e.Fecha <= corte && !string.IsNullOrEmpty(e.Responsable))
                    .OrderBy(e => e.Fecha).LastOrDefault();
                var persona = personasKpi.SingleOrDefault(p => p.Codigo == actor?.Responsable);
                return new OperacionKpi(v.Solicitud, pos?.Parte ?? "", pos?.Estacion ?? "", v.Posicion,
                    v.Puesto, v.Turno, inicio, fin, persona?.Codigo, persona?.Nombre, actor?.Turno,
                    pos is not null, v.Proceso);
            }).Where(o => o.Inicio is null || o.Inicio <= corte).ToArray();
    }
}
