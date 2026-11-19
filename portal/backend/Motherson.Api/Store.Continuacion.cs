using System.Data;
using System.Security.Cryptography;
using System.Text.Json;
using Dapper;

namespace Motherson;

public sealed partial class Store : IDisposable
{
    DiarioMovimientos? diario;
    public bool ContinuacionHabilitada => SqlTransaccional || diario is not null;
    string Raiz => configuration["MVP_ROOT"] ?? throw new RuleException("Falta la raíz del portal.", 503);

    public async Task PrepararContinuacion()
    {
        if (SqlTransaccional)
        {
            await using var sql = await Open();
            if (await sql.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM mes.BASE_INVENTARIO WHERE id=1") != 1)
                throw new InvalidOperationException("Falta cargar y aprobar el saldo inicial de SQL.");
            return;
        }
        if (!EsDemo) return;
        var carpeta = Path.GetFullPath(configuration["MVP_DIARIO_PATH"] ?? Path.Combine(Raiz, ".local", Compartida ? "continuacion-movimientos-compartida" : "continuacion-movimientos"));
        var publica = Path.GetFullPath(Path.Combine(Raiz, "backend", "Motherson.Api", "wwwroot"));
        if (carpeta == publica || carpeta.StartsWith(publica + Path.DirectorySeparatorChar, StringComparison.Ordinal))
            throw new InvalidOperationException("El diario no puede estar en la carpeta pública.");
        diario = new DiarioMovimientos(carpeta, await LeerBaseContinuacion(), espejo: Compartida);
    }

    async Task<BaseMovimientos> LeerBaseContinuacion()
    {
        if (archivoDemo is null) throw new RuleException("Continuación no disponible para esta base.", 503);
        var corte = archivoDemo.Corte(); // Comprueba todas las fuentes selladas antes de utilizar el saldo.
        await using var c = await Open();
        await using var tx = await c.BeginTransactionAsync(IsolationLevel.Serializable);
        var filas = (await c.QueryAsync<InventarioAlarma>(FilasPuestosSql, new { puesto = (int?)null }, tx)).ToArray();
        await tx.CommitAsync();
        var fechas = filas.Select(f => f.fecha_stock).Distinct().ToArray();
        if (filas.Length == 0 || fechas.Length != 1 || fechas[0] is not { } fecha ||
            fecha != corte.GetProperty("fecha_stock").GetDateTime())
            throw new RuleException("El inventario y el corte de sus fuentes no concuerdan.", 503);
        // Sin reparto por ubicación, la vista devuelve cantidad NULL; no se distribuye el total.
        var huella = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(Path.Combine(Raiz, ".local", "historico-demo", "manifiesto.json"))));
        return new(Compartida ? c.Database : HistoricoDemo.Base, huella, fecha, filas);
    }

    async Task<EstadoDiario> EstadoContinuacion()
    {
        if (SqlTransaccional) return await LeerEstadoSql();
        var d = diario ?? throw new RuleException("La continuación no está habilitada.", 503);
        d.VerificarBase(await LeerBaseContinuacion());
        return await d.Leer();
    }

    static InventarioAlarma[] Alcance(EstadoDiario e, Cuenta a) =>
        e.Filas.Where(f => a.PuestoId is null || f.id_puesto == a.PuestoId).ToArray();

    public async Task<object> GuardarMovimiento(Cuenta actor, MovimientoNuevo input, Guid id)
    {
        if (SqlTransaccional)
        {
            if (input.Tipo is not ("ENTRADA" or "SALIDA") || input.Cantidad is < 1 or > 1000000 || input.Version < 1 || (input.Tipo=="ENTRADA" && input.EsConsumo))
                throw new RuleException("Captura inválida.",400);
            return await EjecutarSql(actor, id, input, "mes.RegistrarMovimiento");
        }
        var d = diario ?? throw new RuleException("La continuación no está habilitada.", 503);
        var r = await d.Registrar(input, actor, id, async () =>
            (await Actor(actor.Id, actor.TipoCuenta, actor.SelloAcceso), await LeerBaseContinuacion()),
            Compartida ? AplicarEnSql : null);
        return new { id_operacion = r.IdOperacion, id_movimiento = r.IdMovimiento,
            tipo_movimiento = r.TipoMovimiento, cantidad_movimiento = r.CantidadMovimiento,
            saldo_anterior = r.SaldoAnterior, saldo_posterior = r.SaldoPosterior,
            fecha_movimiento = r.FechaMovimiento, origen = "CONTINUACION",
            fecha_hora_inicio = (DateTime?)null, fecha_hora_fin = (DateTime?)null };
    }

    public async Task<object> AjustarCapacidad(Cuenta actor, AjusteCapacidadInput input, Guid id)
    {
        if (SqlTransaccional) return await EjecutarSql(actor, id, new SolicitudAjuste([new(input.Estanteria, input.Anterior, input.Nueva)], []), "mes.AjustarCatalogo");
        var d = diario ?? throw new RuleException("La continuación no está habilitada.", 503);
        var ajuste = await d.RegistrarAjuste([new CambioCapacidad(input.Estanteria, input.Anterior, input.Nueva)], actor, id,
            async () => (await Actor(actor.Id, actor.TipoCuenta, actor.SelloAcceso), await LeerBaseContinuacion()),
            Compartida ? AplicarAjusteEnSql : null);
        var fila = (await d.Leer()).Filas.First(f => f.id_estanteria == input.Estanteria);
        return new { id_operacion = ajuste.IdOperacion, id_estanteria = input.Estanteria,
            fila.cajas_estanteria, fila.cajas_disponibles, fecha = ajuste.FechaUtc };
    }

    /// Importación de Excel: cambia descripción, uso, piezas por caja, mínimo o variante de materiales existentes.
    /// Todo o nada: si un cambio no es válido, no se aplica ninguno.
    public async Task<object> ImportarMateriales(Cuenta actor, ImportacionMaterialesInput input, Guid id)
    {
        if (SqlTransaccional)
        {
            foreach(var change in input.Cambios ?? []) DiarioMovimientos.ValidarValorMaterial(change);
            return await EjecutarSql(actor, id, new SolicitudAjuste([], input.Cambios ?? []), "mes.AjustarCatalogo");
        }
        var d = diario ?? throw new RuleException("La continuación no está habilitada.", 503);
        var cambios = input.Cambios ?? [];
        if (cambios.Length == 0) throw new RuleException("El archivo no trae cambios.", 400);
        var variantes = cambios.Where(x => x.Campo == "variante" && x.Nuevo is not null).Select(x => x.Nuevo!).Distinct().ToArray();
        if (variantes.Length > 0)
        {
            await using var c = await Open();
            var existentes = (await c.QueryAsync<string>("SELECT nombre FROM dbo.COLOR")).ToHashSet(StringComparer.Ordinal);
            var faltan = variantes.Where(v => !existentes.Contains(v)).ToArray();
            if (faltan.Length > 0)
                throw new RuleException("Estas variantes no existen en el catálogo de colores: " + string.Join(", ", faltan) + ". Solicita a administración que las agregue o deja la celda vacía.", 400);
        }
        var ajuste = await d.RegistrarAjuste([], cambios, actor, id,
            async () => (await Actor(actor.Id, actor.TipoCuenta, actor.SelloAcceso), await LeerBaseContinuacion()),
            Compartida ? AplicarAjusteEnSql : null);
        return new { id_operacion = ajuste.IdOperacion, cambios = ajuste.Materiales?.Length ?? 0,
            materiales = ajuste.Materiales?.Select(m => m.Parte).Distinct().Count() ?? 0, fecha = ajuste.FechaUtc };
    }

    // Base compartida: aplica el ajuste con los procedimientos de 013_azure_ajustes_catalogo.sql, cada uno en una transacción.
    // Para revertir se usan los valores anteriores, en orden inverso.
    async Task AplicarAjusteEnSql(AjusteCatalogo a, bool revertir)
    {
        await using var c = await Open();
        async Task Materiales() { if (a.Materiales is { Length: > 0 } m) await c.ExecuteAsync("dbo.AjustarMateriales",
            new { cambios = JsonSerializer.Serialize(m.Select(x => new { parte = x.Parte, campo = x.Campo, valor = revertir ? x.Anterior : x.Nuevo })) },
            commandType: CommandType.StoredProcedure); }
        async Task Capacidades() { if (a.Capacidades.Length > 0) await c.ExecuteAsync("dbo.AjustarCapacidadEstanteria",
            new { cambios = JsonSerializer.Serialize(a.Capacidades.Select(x => new { estanteria = x.Estanteria, cajas = revertir ? x.Anterior : x.Nueva })) },
            commandType: CommandType.StoredProcedure); }
        if (revertir) { await Capacidades(); await Materiales(); }
        else { await Materiales(); await Capacidades(); }
    }

    // Base compartida: actualiza UBICACION.cantidad y MATERIAL.stock con el procedimiento del Equipo 2.
    // piezas > 0 entra material; piezas < 0 sale.
    async Task AplicarEnSql(int ubicacion, int piezas)
    {
        await using var c = await Open();
        await c.ExecuteAsync("dbo.RegistrarMovimiento", new { ubicacion, piezas }, commandType: CommandType.StoredProcedure);
    }

    async Task<object> InventarioContinuacion(Cuenta actor)
    {
        var e = await EstadoContinuacion();
        var corte = DateTime.UtcNow;
        var filas = Alcance(e, actor);
        var consumos = e.Hechos.Where(h => h.Captura.EsConsumo).Select(h => new ConsumoCobertura(
            "CONTINUACION:" + h.IdOperacion, h.Captura.Ubicacion, h.Parte, "CONTINUACION", "SALIDA",
            e.Piezas(h), h.FechaUtc, true)).ToArray();
        return RespuestaCobertura(filas, corte, consumos, _ => e.InicioObservacion, "CONTINUACION",
            new { modo = "ACTUAL", fechaStockInicial = e.Base.FechaStock, inicioObservacion = e.InicioObservacion,
                movimientosNuevos = e.Hechos.Length, huellaHistorico = e.Base.HuellaHistorico });
    }

    static object RespuestaCobertura(InventarioAlarma[] filas, DateTime corte, ConsumoCobertura[] consumos,
        Func<int, DateTime?> apertura, string espacio, object fuente)
    {
        var calendario = Calendario.Value;
        var ventana = calendario?.Ventana(corte) ?? new([], "CALENDARIO_NO_DISPONIBLE");
        var alarmas = filas.Select(f => new { f.id_ubicacion, f.numero_parte,
            resultado = MotorCobertura.Evaluar(f.cantidad, f.id_ubicacion, f.numero_parte,
                corte, ventana, apertura(f.id_ubicacion), consumos, espacio) }).ToArray();
        var minimos = filas.GroupBy(f => f.numero_parte).Select(g => {
            var m = g.Select(f => f.minimo).Distinct().Count() == 1 ? g.First().minimo : null;
            // SQL conserva un total por material. No se repite ni se reparte entre ubicaciones.
            var r = espacio == "CONTINUACION" && g.First().stock_total_material is long total && total <= int.MaxValue
                ? MotorCobertura.Minimo([(int)total], m)
                : MotorCobertura.Minimo(g.Select(f => f.cantidad), m);
            return new { numero_parte = g.Key, saldo_total = r.Total, minimo = m, estado = r.Estado, ubicaciones = g.Count() };
        }).ToArray();
        return new { corte, politica = MotorCobertura.Politica, simulado = true, filas, alarmas, minimos, fuente,
            calendario = new { fuente = calendario?.Fuente, huellaFuente = calendario?.HuellaFuente,
                periodo = calendario?.Periodo, motivo = ventana.Motivo },
            ventana = new { intervalos = ventana.Intervalos, minutosLaborables = ventana.Minutos, completa = ventana.Completa },
            observacionesMinimas = 2, metodo = "CONSUMO_60_MIN_LABORABLES" };
    }

    public async Task<object> CoberturaHistorica(Cuenta actor, DateTimeOffset corte, CancellationToken token)
    {
        if (corte.UtcDateTime > DateTime.UtcNow) throw new RuleException("El corte no puede estar en el futuro.", 400);
        if (SqlTransaccional) return await CoberturaSqlAlCorte(actor,corte.UtcDateTime);
        if (archivoDemo is null) throw new RuleException("No hay fuentes históricas disponibles.", 503);
        var datos = await archivoDemo.Cobertura(corte.UtcDateTime, actor.PuestoId, token);
        return RespuestaCobertura(datos.Filas, corte.UtcDateTime, datos.Consumos,
            id => datos.Aperturas.TryGetValue(id, out var inicio) ? inicio : null, "HISTORICO_21",
            new { modo = "HISTORICO", espacio = "HISTORICO_21", corteOriginal = archivoDemo.Corte(),
                nota = "Consulta del maestro original; no usa ni altera el inventario actual." });
    }

    async Task<JsonElement[]> MovimientosContinuacion(FiltroHistorico filtro)
    {
        var e = await EstadoContinuacion();
        return e.Hechos.Select((h, i) => {
            var f = e.Base.Filas.Single(f => f.id_ubicacion == h.Captura.Ubicacion);
            var r = e.Resultados[i];
            return JsonSerializer.SerializeToElement(new {
                id_movimiento = r.IdMovimiento, origen = "CONTINUACION", es_simulado = true,
                numero_parte = h.Parte, id_puesto = h.Puesto, id_ubicacion = h.Captura.Ubicacion,
                espacio_identificadores = "CONTINUACION", f.estacion, f.estanteria, f.nivel,
                tipo_movimiento = h.Captura.Tipo, cantidad_piezas = r.CantidadMovimiento,
                cantidad_capturada = h.Captura.Cantidad, unidad_capturada = h.Unidad,
                cantidad_por_caja_aplicada = h.PiezasPorCaja, es_consumo_produccion = h.Captura.EsConsumo,
                saldo_anterior = r.SaldoAnterior, saldo_posterior = r.SaldoPosterior,
                fecha_movimiento = h.FechaUtc, fecha_hora_inicio = (DateTime?)null, fecha_hora_fin = (DateTime?)null,
                usuario_captura = h.Cuenta, tipo_cuenta = h.TipoCuenta, responsable_simulado = (string?)null,
                proceso_generador = h.Proceso, id_turno = (int?)null,
                fecha_saldo_inicial = e.Base.FechaStock
            });
        }).Where(f => HistoricoDemo.Cumple(f, filtro)).ToArray();
    }

    async Task<object> HistorialContinuacion(FiltroHistorico filtro, int pagina, CancellationToken token)
    {
        if (SqlTransaccional) return await PaginaHistorialSql(filtro, pagina, token);
        var nuevas = await MovimientosContinuacion(filtro);
        return await archivoDemo!.PaginaCombinada(filtro, pagina, nuevas, token);
    }
    internal async Task ExportarContinuacion(FiltroHistorico filtro, HttpResponse respuesta, CancellationToken token)
    {
        var nuevas = await MovimientosContinuacion(filtro);
        await archivoDemo!.ExportarCombinado(filtro, nuevas, respuesta, token);
    }
    public void Dispose() => diario?.Dispose();
}
