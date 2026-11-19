using System.Text.Json;

namespace Motherson;

public sealed partial class Store
{
    // Proyección de consulta. No crea registros ni objetos en SQL Server.
    public async Task<object> ConsultarKpis(Cuenta actor, IQueryCollection q, CancellationToken token)
    {
        var fuente = archivoDemo;
        if (fuente is null && !SqlTransaccional) throw new RuleException("Histórico no disponible.", 503);
        string Texto(string nombre, string defecto = "") => q[nombre].FirstOrDefault() ?? defecto;
        var corteTexto = Texto("corte", "2026-10-23T23:30");
        var desdeTexto = Texto("desde", "2026-10-19");
        var hastaTexto = Texto("hasta", "2026-10-25");
        var corte = KpisMotor.FechaLocal(corteTexto);
        var desde = KpisMotor.FechaLocal(desdeTexto);
        var hasta = KpisMotor.FechaLocal(hastaTexto).AddDays(1);
        if (corte > DateTime.UtcNow || hasta <= desde || (hasta - desde).TotalDays > 732)
            throw new RuleException("Revisa el corte y el periodo (máximo dos años).", 400);
        int? Numero(string nombre) {
            var valor = Texto(nombre);
            if (valor.Length == 0) return null;
            if (!int.TryParse(valor, out var n) || n <= 0) throw new RuleException("Filtro numérico inválido.",400);
            return n;
        }
        var puesto = Numero("puesto"); var turno = Numero("turno");
        if (turno is not null and not 2 and not 3) throw new RuleException("Turno PFEP inválido.",400);
        var estacion = Texto("estacion"); var parte = Texto("parte");
        var responsable = Texto("responsable"); var estado = Texto("estado");
        if (!new[] { "", "NORMAL", "ADVERTENCIA", "CRITICO", "AGOTADO", "SIN_DATOS" }.Contains(estado))
            throw new RuleException("Estado inválido.",400);
        (int Puesto, int Turno)? persona = fuente is null ? null : await fuente.AlcancePersonaKpi(responsable, token);
        if (actor.PuestoId is int autorizado && ((puesto.HasValue && puesto != autorizado) ||
            (persona.HasValue && persona.Value.Puesto != autorizado))) throw new RuleException("Puesto no autorizado.",403);

        InventarioAlarma[] filas; object cobertura; string modo; DateTime fechaStock;
        var continuacion = await EstadoContinuacion();
        if (corte < continuacion.Base.FechaStock && fuente is null)
            throw new RuleException("No hay un saldo acreditado para ese corte. Las atenciones simuladas no permiten reconstruir inventario anterior a la apertura.", 400);
        if (corte < continuacion.Base.FechaStock && fuente is not null) {
            var datos = await fuente.Cobertura(corte, actor.PuestoId, token);
            filas = datos.Filas;
            modo = "HISTORICO_MAESTRO"; fechaStock = corte;
            cobertura = RespuestaCobertura(filas, corte, datos.Consumos,
                id => datos.Aperturas.TryGetValue(id, out var apertura) ? apertura : null, "HISTORICO_21",
                new { modo, corteOriginal = fuente.Corte() });
        } else {
            // Reconstruye el corte pedido; no emplea el saldo futuro del diario.
            filas = JsonSerializer.Deserialize<InventarioAlarma[]>(JsonSerializer.Serialize(continuacion.Base.Filas))!;
            var porId = filas.ToDictionary(f => f.id_ubicacion);
            foreach (var a in continuacion.Ajustes.Where(a => a.FechaUtc <= corte))
                DiarioMovimientos.AplicarAjuste(filas, a);
            foreach (var r in continuacion.Resultados.Where(r => r.FechaMovimiento <= corte).OrderBy(r => r.FechaMovimiento)) {
                var h = continuacion.Hechos.Single(h => h.IdOperacion == r.IdOperacion);
                porId[h.Captura.Ubicacion].cantidad = r.SaldoPosterior;
            }
            foreach (var g in filas.GroupBy(f => f.numero_parte)) {
                long? total = g.All(f => f.cantidad.HasValue) ? g.Sum(f => (long)f.cantidad!.Value) : null;
                foreach (var f in g) f.stock_total_material = total;
            }
            modo = "MAESTRO_Y_CONTINUACION"; fechaStock = continuacion.Base.FechaStock;
            var consumos = continuacion.Hechos.Where(h => h.FechaUtc <= corte && h.Captura.EsConsumo)
                .Select(h => new ConsumoCobertura("CONTINUACION:" + h.IdOperacion, h.Captura.Ubicacion,
                    h.Parte, "CONTINUACION", "SALIDA", continuacion.Piezas(h), h.FechaUtc, true)).ToArray();
            cobertura = RespuestaCobertura(filas, corte, consumos, _ => continuacion.InicioObservacion,
                "CONTINUACION", new { modo, fechaStockInicial = fechaStock });
        }
        DiarioMovimientos.CalcularCapacidad(filas);
        bool PuestoIncluido(int id) => (!actor.PuestoId.HasValue || id == actor.PuestoId) &&
            (!puesto.HasValue || id == puesto) && (!persona.HasValue || id == persona.Value.Puesto);
        filas = filas.Where(f => PuestoIncluido(f.id_puesto) && (estacion == "" || f.estacion == estacion) &&
            (parte == "" || f.numero_parte == parte)).ToArray();
        if (filas.Select(f => f.id_ubicacion).Distinct().Count() != filas.Length)
            throw new RuleException("La consulta contiene posiciones repetidas.",503);
        var materiales = KpisMotor.Materiales(filas);
        var evaluables = materiales.Count(m => m.Estado != "SIN_DATOS");
        var visibles = materiales.Where(m => estado == "" || m.Estado == estado).ToArray();
        var partesVisibles = visibles.Select(m => m.Parte).ToHashSet(StringComparer.Ordinal);
        var posiciones = filas.Where(f => partesVisibles.Contains(f.numero_parte)).ToArray();
        object Indicador(string codigo) {
            var n = visibles.Count(m => m.Estado == codigo);
            return new { cantidad = n, porcentaje = evaluables > 0 ? (decimal?)n * 100 / evaluables : null };
        }
        var conocidas = posiciones.Where(f => f.cantidad is >= 0).ToArray();
        var empaqueValido = conocidas.Where(f => f.cantidad_por_caja is > 0).ToArray();
        var inventario = conocidas.Length > 0 ? (decimal?)conocidas.Sum(f => (decimal)f.cantidad!.Value) : null;
        var cajas = empaqueValido.Length > 0 ? (decimal?)empaqueValido.Sum(f => (decimal)f.cantidad!.Value / f.cantidad_por_caja!.Value) : null;
        var ocupacion = posiciones.GroupBy(f => (f.estacion, f.estanteria, f.nivel)).Select(g => {
            var capacidades = g.Select(f => f.cajas_estanteria).Distinct().ToArray();
            var capacidad = capacidades.Length == 1 && capacidades[0] > 0 ? capacidades[0] : null;
            decimal? fisicas = g.All(f => f.cantidad is >= 0 && f.cantidad_por_caja is > 0)
                ? g.Sum(f => decimal.Ceiling((decimal)f.cantidad!.Value / f.cantidad_por_caja!.Value)) : null;
            return new { estacion = g.Key.estacion, estanteria = g.Key.estanteria, nivel = g.Key.nivel,
                capacidadCajas = capacidad, cajasFisicasEstimadas = fisicas,
                porcentaje = capacidad.HasValue && fisicas.HasValue ? fisicas * 100m / capacidad : null,
                posiciones = g.Count(), capacidadAmbigua = capacidades.Length > 1 };
        }).OrderByDescending(r => r.porcentaje).ToArray();
        var agotadas = posiciones.Where(f => materiales.Single(m => m.Parte == f.numero_parte).Estado == "AGOTADO")
            .GroupBy(f => f.estacion).Select(g => new { estacion = g.Key, materiales = g.Select(f => f.numero_parte).Distinct().Count() }).ToArray();

        // La solicitud identifica una operación; sus siete etapas no se cuentan siete veces.
        var operaciones = (fuente is null ? Array.Empty<OperacionKpi>() : await fuente.OperacionesKpis(corte, token)).Concat(await OperacionesPortalKpis(corte)).Where(o => PuestoIncluido(o.Puesto) &&
            (estacion == "" || o.Estacion == estacion) && (parte == "" || o.Parte == parte) &&
            (!turno.HasValue || o.TurnoInicio == turno) && (responsable == "" || o.Responsable == responsable)).ToArray();
        var completadas = operaciones.Where(o => KpisMotor.Completada(o, desde, hasta, corte)).ToArray();
        var abiertas = operaciones.Where(o => KpisMotor.Abierta(o,corte)).ToArray();
        var iniciadas = operaciones.Where(o => o.Inicio >= desde && o.Inicio < hasta && o.Inicio <= corte).ToArray();
        var alcanceCalidad = completadas.Concat(abiertas).Concat(iniciadas).DistinctBy(o => o.Id).ToArray();
        var incompletas = alcanceCalidad.Count(o => string.IsNullOrEmpty(o.Responsable) || !o.UbicacionValida);
        var negativas = alcanceCalidad.Count(o => o.Fin <= corte && o.Fin < o.Inicio);
        var intervalos = KpisMotor.Intervalos(iniciadas);
        var repeticiones = iniciadas.GroupBy(o => (o.Parte,o.Estacion,KpisMotor.Dia(o.Inicio!.Value)))
            .Select(g => new { parte = g.Key.Parte, estacion = g.Key.Estacion, dia = g.Key.Item3,
                operaciones = g.Count(), adicionales = Math.Max(g.Count()-1,0) }).Where(g => g.adicionales > 0).ToArray();
        var ultimas = operaciones.Where(o => o.Inicio.HasValue && o.Fin.HasValue && o.Fin >= o.Inicio && o.Fin <= corte)
            .GroupBy(o => (o.Parte,o.Estacion)).Select(g => { var fin = g.Max(o => o.Fin)!.Value;
                return new { parte = g.Key.Parte, estacion = g.Key.Estacion, fin, horas = (corte-fin).TotalHours }; }).OrderByDescending(r => r.horas).ToArray();
        var idsCobertura = posiciones.Select(f => f.id_ubicacion).ToHashSet();
        var coberturaJson = JsonSerializer.SerializeToElement(cobertura);
        var alarmas = coberturaJson.GetProperty("alarmas").EnumerateArray()
            .Where(a => idsCobertura.Contains(a.GetProperty("id_ubicacion").GetInt32())).Select(a => a.Clone()).ToArray();
        return new {
            contexto = new { corte, corteLocal = corteTexto, desde = desdeTexto, hasta = hastaTexto, desdeUtc = desde,
                hastaUtcExclusivo = hasta, estacion, parte, puesto, turnoInicio = turno, responsable, estado,
                linea = "L1", zona = "America/Mexico_City", modo, fechaStock, simulado = true,
                maestro = SqlTransaccional ? "AZURE_SQL" : HistoricoDemo.Base, politicaMinimo = "MINIMO_120_V1", denominador = evaluables,
                notaDenominador = "Materiales evaluables en el alcance, antes del filtro Estado.",
                notaStock = "El periodo y turno filtran actividad; el inventario es compartido y se reconstruye al corte. El responsable restringe inventario a su puesto, no a un saldo personal.",
                notaActividad = "Completados por entrega; turno del filtro = inicio. Abiertos incluyen arrastre. Identidad = última etapa fechada conocida al corte.",
                notaContinuacion = "Las atenciones del portal (dbo.REGISTRO: inicio y fin por puesto y ubicación) se suman como reabastecimientos (proceso PORTAL_REGISTRO). Las capturas de entradas y salidas de material no son reabastecimientos.",
                fuente = "Histórico maestro sellado y continuación del portal; proyección de consulta sin persistencia." },
            estadoActual = new { inventario, cajasEquivalentes = cajas, posiciones = posiciones.Length,
                posicionesConSaldo = conocidas.Length, sinSaldo = posiciones.Length-conocidas.Length,
                sinEmpaque = posiciones.Count(f => f.cantidad_por_caja is null or <= 0),
                sinEvaluar = materiales.Count(m => m.Estado == "SIN_DATOS"), materialesEvaluados = evaluables,
                advertencia = Indicador("ADVERTENCIA"), critico = Indicador("CRITICO"), agotado = Indicador("AGOTADO"),
                distribucion = new[] {"NORMAL","ADVERTENCIA","CRITICO","AGOTADO","SIN_DATOS"}.Select(e => new { estado = e, cantidad = visibles.Count(m => m.Estado == e) }),
                materiales = visibles, inventarioDetalle = posiciones, estacionesAgotadas = agotadas, ocupacion,
                cobertura = new { alarmas, calendario = coberturaJson.GetProperty("calendario").Clone(),
                    politica = coberturaJson.GetProperty("politica").Clone(), ventana = coberturaJson.GetProperty("ventana").Clone() } },
            actividad = new { completados = completadas.Length, enCurso = abiertas.Length,
                repetidos = KpisMotor.Repeticiones(iniciadas), incompletos = incompletas,
                intervaloPromedioHoras = intervalos.Length > 0 ? (double?)intervalos.Average() : null,
                intervalosEvaluados = intervalos.Length, operacionesIniciadas = iniciadas.Length,
                operacionesEvaluadasCalidad = alcanceCalidad.Length, intervalosNegativos = negativas,
                porDia = completadas.GroupBy(o => KpisMotor.Dia(o.Fin!.Value)).OrderBy(g => g.Key).Select(g => new { dia = g.Key, cantidad = g.Count() }),
                responsables = completadas.GroupBy(o => (o.Responsable,o.Nombre,o.TurnoResponsable)).Select(g => new { codigo = g.Key.Responsable,
                    nombre = g.Key.Nombre, turno = g.Key.TurnoResponsable, cantidad = g.Count() }).OrderByDescending(g => g.cantidad).Take(5),
                materiales = completadas.GroupBy(o => o.Parte).Select(g => new { parte = g.Key, cantidad = g.Count() }).OrderByDescending(g => g.cantidad),
                ultimas, repeticiones,
                operaciones = alcanceCalidad.OrderByDescending(o => o.Inicio).Select(o => new { id = o.Id, parte = o.Parte, estacion = o.Estacion,
                    posicion = o.Posicion, puesto = o.Puesto, turnoInicio = o.TurnoInicio, inicio = o.Inicio,
                    fin = o.Fin <= corte ? o.Fin : null, turnoFin = o.Fin.HasValue && o.Fin <= corte ? KpisMotor.Turno(o.Fin.Value) : null,
                    responsable = o.Responsable, nombre = o.Nombre, turnoResponsable = o.TurnoResponsable, proceso = o.Proceso,
                    completada = KpisMotor.Completada(o,desde,hasta,corte), abierta = KpisMotor.Abierta(o,corte), ubicacionValida = o.UbicacionValida }) }
        };
    }
}
