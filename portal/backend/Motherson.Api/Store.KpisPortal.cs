using Dapper;

namespace Motherson;

// Atenciones capturadas en el portal (dbo.REGISTRO) como operaciones de reabastecimiento para los KPI de Actividad
// logística, según las definiciones del Equipo 2: inicio = fecha_hora_inicio, fin = fecha_hora_fin (NULL mientras
// la atención sigue abierta), material y estación por REGISTRO -> UBICACION -> ESTANTERIA -> ESTACION.
// Solo lectura: no crea objetos ni modifica datos en SQL Server.
internal sealed class FilaRegistroKpi
{
    public int id_registro { get; set; }
    public int id_puesto { get; set; }
    public int id_ubicacion { get; set; }
    public int? id_turno { get; set; }
    public DateTime fecha_hora_inicio { get; set; }
    public DateTime? fecha_hora_fin { get; set; }
    public string? numero_parte { get; set; }
    public string? nombre_estacion { get; set; }
    public string? puesto { get; set; }
}

public sealed partial class Store
{
    internal async Task<OperacionKpi[]> OperacionesPortalKpis(DateTime corte)
    {
        if (!ModeloPuestos) return [];
        await using var c = await Open();
        // Se lee por la vista de atenciones: entrega las fechas en UTC aunque REGISTRO guarde hora de México.
        var filas = await c.QueryAsync<FilaRegistroKpi>($"""
            SELECT r.id_registro,r.id_puesto,r.id_ubicacion,r.id_turno,r.fecha_hora_inicio,r.fecha_hora_fin,
                   r.numero_parte,s.nombre_estacion,r.puesto
            FROM {EsquemaVistas}.vw_kpi_atenciones r
            LEFT JOIN dbo.UBICACION u ON u.id_ubicacion=r.id_ubicacion
            LEFT JOIN dbo.ESTANTERIA e ON e.id_estanteria=u.id_estanteria
            LEFT JOIN dbo.ESTACION s ON s.id_estacion=e.id_estacion
            WHERE r.fecha_hora_inicio<=@corte
            """, new { corte });
        // Id negativo: evita confundir un id_registro del portal con una solicitud del histórico maestro.
        return filas.Select(f => new OperacionKpi(-f.id_registro, f.numero_parte ?? "", f.nombre_estacion ?? "",
            f.id_ubicacion, f.id_puesto, f.id_turno, f.fecha_hora_inicio, f.fecha_hora_fin,
            string.IsNullOrEmpty(f.puesto) ? null : "PUESTO:" + f.puesto, f.puesto, null,
            f.numero_parte is not null, "PORTAL_REGISTRO")).ToArray();
    }
}
