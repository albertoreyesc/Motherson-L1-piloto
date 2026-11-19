using System.Data;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Dapper;

namespace Motherson;

public sealed partial class Store
{
    public bool SqlTransaccional => configuration.GetValue<bool>("MVP_SQL_TRANSACCIONAL");
    static readonly JsonSerializerOptions SqlJson = new() { PropertyNameCaseInsensitive = true };
    sealed record SolicitudAjuste(CambioCapacidad[] Capacidades, CambioMaterial[] Materiales);

    async Task<JsonElement> EjecutarSql(Cuenta actor, Guid id, object solicitud, string procedure)
    {
        if (id == Guid.Empty) throw new RuleException("Falta el identificador de operación.", 400);
        actor = await Actor(actor.Id, actor.TipoCuenta, actor.SelloAcceso);
        await using var sql = await Open();
        var json = await sql.QuerySingleAsync<string>(procedure, new {
            operacion = id, cuenta = actor.Name, tipo_cuenta = actor.TipoCuenta,
            puesto_cuenta = actor.PuestoId, solicitud = JsonSerializer.Serialize(solicitud)
        }, commandType: CommandType.StoredProcedure);
        return JsonSerializer.Deserialize<JsonElement>(json);
    }

    async Task<EstadoDiario> LeerEstadoSql()
    {
        await using var sql = await Open();
        await using var tx = await sql.BeginTransactionAsync(IsolationLevel.Serializable);
        var locked = await sql.ExecuteScalarAsync<int>("DECLARE @r int;EXEC @r=sys.sp_getapplock @Resource='mes_inventario_v1',@LockMode='Shared',@LockOwner='Transaction',@LockTimeout=10000;SELECT @r", transaction: tx);
        if (locked < 0) throw new RuleException("Inventario ocupado.", 409);
        var baseline = await sql.QuerySingleAsync("SELECT fecha_utc,filas_json FROM mes.BASE_INVENTARIO WHERE id=1", transaction: tx);
        string initialJson = baseline.filas_json;
        DateTime date = baseline.fecha_utc;
        var initialRows = JsonSerializer.Deserialize<InventarioAlarma[]>(initialJson, SqlJson)!;
        DiarioMovimientos.CalcularCapacidad(initialRows);
        var initial = new BaseMovimientos(sql.Database, Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(initialJson))), date, initialRows);
        var rows = (await sql.QueryAsync<InventarioAlarma>(FilasPuestosSql, new { puesto = (int?)null }, tx)).ToArray();
        var operations = (await sql.QueryAsync("SELECT o.*,m.numero_parte,m.id_puesto,m.empaque_aplicado,m.piezas,m.saldo_anterior,m.saldo_posterior,m.tipo movimiento_tipo FROM mes.OPERACION o LEFT JOIN mes.MOVIMIENTO m ON m.id_operacion=o.id_operacion ORDER BY o.secuencia", transaction: tx)).ToArray();
        await tx.CommitAsync();
        var facts = new List<HechoMovimiento>(); var results = new List<ResultadoMovimiento>(); var adjustments = new List<AjusteCatalogo>();
        foreach (var op in operations)
        {
            Guid id = op.id_operacion; DateTime when = op.fecha_utc; int seq = checked((int)(long)op.secuencia);
            string request = op.solicitud; string account = op.cuenta; string accountType = op.tipo_cuenta;
            int? scope = op.puesto_cuenta;
            if ((string)op.tipo == "MOVIMIENTO")
            {
                var input = JsonSerializer.Deserialize<MovimientoNuevo>(request, SqlJson)!;
                facts.Add(new(id, seq, input, account, accountType, scope, (string)op.numero_parte, (int)op.id_puesto, (int)op.empaque_aplicado, when));
                results.Add(new(id, "SQL:" + id, (string)op.movimiento_tipo, (int)op.piezas, (int)op.saldo_anterior, (int)op.saldo_posterior, when));
            }
            else
            {
                var input = JsonSerializer.Deserialize<SolicitudAjuste>(request, SqlJson)!;
                adjustments.Add(new(id, seq, account, accountType, input.Capacidades, when, input.Materiales));
            }
        }
        return new(initial, date, rows, facts.ToArray(), results.ToArray(), adjustments.ToArray());
    }

    public async Task<object> OpcionesSql()
    {
        await using var sql = await Open();
        return new {
            maestro = "AZURE_SQL", materiales = await sql.QueryAsync<string>("SELECT numero_parte FROM dbo.MATERIAL UNION SELECT numero_parte FROM mes.vw_historial_json WHERE numero_parte IS NOT NULL ORDER BY numero_parte"),
            estaciones = await sql.QueryAsync<string>("SELECT nombre_estacion FROM dbo.ESTACION ORDER BY nombre_estacion"),
            responsables = await sql.QueryAsync("SELECT DISTINCT responsable codigo_responsable,responsable nombre_responsable,CONVERT(int,NULL) id_turno FROM mes.vw_historial_json WHERE responsable IS NOT NULL ORDER BY responsable"),
            turnos = await sql.QueryAsync("SELECT id_turno,nombre_turno codigo_turno FROM dbo.TURNO ORDER BY id_turno"),
            desde = "", hasta = ""
        };
    }

    public async Task<object> CorteSql()
    {
        await using var sql = await Open();
        return await sql.QuerySingleAsync("SELECT 'AZURE_SQL' maestro,1 revision,b.fecha_utc fecha_stock,b.fecha_utc corte_fuente,b.fecha_utc fecha_carga,CONVERT(bit,1) sellado,c.es_simulado FROM mes.BASE_INVENTARIO b CROSS JOIN mes.CONFIGURACION c WHERE b.id=1");
    }

    async Task<object> CoberturaSqlAlCorte(Cuenta actor, DateTime corte)
    {
        var state=await LeerEstadoSql();
        if(corte<state.Base.FechaStock)throw new RuleException("No hay un saldo acreditado antes de la apertura SQL.",400);
        var rows=JsonSerializer.Deserialize<InventarioAlarma[]>(JsonSerializer.Serialize(state.Base.Filas))!;
        var events=state.Hechos.Select(h=>(Seq:h.Secuencia,Fact:(HechoMovimiento?)h,Adjustment:(AjusteCatalogo?)null))
            .Concat(state.Ajustes.Select(a=>(Seq:a.Secuencia,Fact:(HechoMovimiento?)null,Adjustment:(AjusteCatalogo?)a))).OrderBy(e=>e.Seq);
        foreach(var e in events)
        {
            if(e.Fact is {} fact && fact.FechaUtc<=corte)
                rows.Single(r=>r.id_ubicacion==fact.Captura.Ubicacion).cantidad=state.Resultados.Single(r=>r.IdOperacion==fact.IdOperacion).SaldoPosterior;
            if(e.Adjustment is {} adjustment && adjustment.FechaUtc<=corte)DiarioMovimientos.AplicarAjuste(rows,adjustment);
        }
        foreach(var group in rows.GroupBy(r=>r.numero_parte))foreach(var row in group)row.stock_total_material=group.Sum(r=>(long)(r.cantidad??0));
        DiarioMovimientos.CalcularCapacidad(rows);
        var scope=rows.Where(r=>actor.PuestoId is null||r.id_puesto==actor.PuestoId).ToArray();
        var consumptions=state.Hechos.Where(h=>h.FechaUtc<=corte&&h.Captura.EsConsumo).Select(h=>new ConsumoCobertura(
            "SQL:"+h.IdOperacion,h.Captura.Ubicacion,h.Parte,"CONTINUACION","SALIDA",state.Piezas(h),h.FechaUtc,true)).ToArray();
        return RespuestaCobertura(scope,corte,consumptions,_=>state.InicioObservacion,"CONTINUACION",new {modo="SQL_AL_CORTE",fechaStockInicial=state.Base.FechaStock, inicioObservacion=state.InicioObservacion, movimientosNuevos=state.Hechos.Count(h=>h.FechaUtc<=corte && (actor.PuestoId is null || h.Puesto==actor.PuestoId))});
    }

    const string FiltroHistorialSql = """
        FROM mes.vw_historial_json WHERE (@parte IS NULL OR
            (@exacta=1 AND numero_parte COLLATE Latin1_General_100_BIN2=@parte COLLATE Latin1_General_100_BIN2) OR
            (@exacta=0 AND numero_parte LIKE '%'+@parte+'%'))
        AND (@id IS NULL OR (id_ubicacion=@id AND espacio_identificadores=@espacio))
        AND (@desde IS NULL OR fecha_movimiento>=@desde) AND (@hasta IS NULL OR fecha_movimiento<@hasta)
        AND (@origen IS NULL OR espacio_identificadores=@origen)
        AND (@responsable IS NULL OR responsable=@responsable) AND (@turno IS NULL OR id_turno=@turno)
        """;
    static object ParametrosHistorial(FiltroHistorico f,int pagina=1)=>new {
        parte=f.Parte,exacta=f.Exacta,id=f.Id,espacio=f.Espacio,desde=f.Inicio,hasta=f.Fin,
        origen=f.Origen,responsable=f.Responsable,turno=f.Turno,offset=(pagina-1)*50 };
    internal async Task<object> PaginaHistorialSql(FiltroHistorico filtro, int pagina, CancellationToken token)
    {
        await using var sql=await Open();
        using var result=await sql.QueryMultipleAsync(new CommandDefinition(
            "SELECT COUNT(*) "+FiltroHistorialSql+";SELECT datos "+FiltroHistorialSql+
            " ORDER BY fecha_movimiento DESC,id_movimiento OFFSET @offset ROWS FETCH NEXT 50 ROWS ONLY;",
            ParametrosHistorial(filtro,pagina),cancellationToken:token));
        var total=await result.ReadSingleAsync<int>();
        var rows=(await result.ReadAsync<string>()).Select(r=>JsonSerializer.Deserialize<JsonElement>(r)).ToArray();
        return new{total,pagina,filas=rows};
    }
    internal async Task ExportarHistorialSql(FiltroHistorico filtro, HttpResponse response, CancellationToken token)
    {
        response.ContentType="application/x-ndjson";
        response.Headers.ContentDisposition="attachment; filename=movimientos-motherson.jsonl";
        await using var sql=await Open();
        await using var command=sql.CreateCommand();
        command.CommandText="SELECT datos "+FiltroHistorialSql+" ORDER BY fecha_movimiento DESC,id_movimiento";
        var args=ParametrosHistorial(filtro);
        foreach(var property in args.GetType().GetProperties())command.Parameters.AddWithValue("@"+property.Name,property.GetValue(args)??DBNull.Value);
        await using var reader=await command.ExecuteReaderAsync(token);
        while(await reader.ReadAsync(token))await response.WriteAsync(reader.GetString(0)+"\n",token);
    }
}
