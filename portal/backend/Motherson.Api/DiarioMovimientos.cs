using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Motherson;

public record MovimientoNuevo(int Ubicacion, string Tipo, int Cantidad, int Version, bool EsConsumo = false);
public record BaseMovimientos(string BaseDatos, string HuellaHistorico, DateTime FechaStock,
    InventarioAlarma[] Filas);
public record HechoMovimiento(Guid IdOperacion, int Secuencia, MovimientoNuevo Captura,
    string Cuenta, string TipoCuenta, int? PuestoCuenta, string Parte, int Puesto,
    int? PiezasPorCaja, DateTime FechaUtc)
{
    // Desde el 17/11/2026 las salidas también se capturan en cajas y guardan su empaque.
    // Una salida sin PiezasPorCaja es anterior a ese cambio y se capturó en piezas.
    public bool EnCajas => Captura.Tipo == "ENTRADA" || PiezasPorCaja is not null;
    public string Unidad => EnCajas ? "CAJAS" : "PIEZAS";
    public string Proceso => "PORTAL_CONTINUACION_V1";
}
/// Ajuste de catálogo hecho desde el portal (17/11/2026): capacidad en cajas de una estantería (+ / −)
/// o datos de material (importación de Excel). Anterior es el valor que la persona vio al editar;
/// si cambió en el inter, se rechaza todo el ajuste.
public record CambioCapacidad(int Estanteria, int? Anterior, int Nueva);
/// Campo: descripcion, uso, cantidad_por_caja, minimo o variante. Los valores viajan como texto.
public record CambioMaterial(string Parte, string Campo, string? Anterior, string? Nuevo);
public record AjusteCatalogo(Guid IdOperacion, int Secuencia, string Cuenta, string TipoCuenta,
    CambioCapacidad[] Capacidades, DateTime FechaUtc, CambioMaterial[]? Materiales = null)
{
    public string Proceso => "PORTAL_AJUSTE_CATALOGO_V1";
}
public record ResultadoMovimiento(Guid IdOperacion, string IdMovimiento, string TipoMovimiento,
    int CantidadMovimiento, int SaldoAnterior, int SaldoPosterior, DateTime FechaMovimiento);
public record EstadoDiario(BaseMovimientos Base, DateTime InicioObservacion, InventarioAlarma[] Filas,
    HechoMovimiento[] Hechos, ResultadoMovimiento[] Resultados, AjusteCatalogo[] Ajustes)
{
    /// Piezas que realmente movió el hecho (las cajas ya convertidas).
    public int Piezas(HechoMovimiento h) => Resultados.First(r => r.IdOperacion == h.IdOperacion).CantidadMovimiento;
}

/// Un solo escritor durante toda la vida del proceso. El JSONL es el registro duradero;
/// los saldos y comprobantes se reconstruyen, nunca se escriben en SQL.
public sealed class DiarioMovimientos : IDisposable
{
    static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    readonly SemaphoreSlim mutex = new(1);
    readonly FileStream escritor;
    readonly FileStream archivo;
    readonly BaseMovimientos inicial;
    readonly DateTime inicio;
    readonly List<HechoMovimiento> hechos = [];
    readonly List<ResultadoMovimiento> resultados = [];
    readonly Dictionary<Guid, int> operaciones = [];
    readonly List<AjusteCatalogo> ajustes = [];
    readonly Dictionary<Guid, int> operacionesAjuste = [];
    // Movimientos y ajustes en el mismo orden del archivo: así se reconstruye el estado paso a paso.
    readonly List<object> eventos = [];
    DateTime ultima;
    string ultimaHuella = new('0', 64);
    long longitud;
    bool averiado;
    readonly Func<DateTime> reloj;
    // Espejo: SQL también recibe cada movimiento, así que debe coincidir con saldo inicial + diario.
    readonly bool espejo;
    sealed record Sobre(int Version, string Tipo, JsonElement Datos, string Anterior, string Huella);
    sealed record Cabecera(BaseMovimientos Base, DateTime InicioObservacion);

    public DiarioMovimientos(string carpeta, BaseMovimientos origen, Func<DateTime>? reloj = null, bool espejo = false)
    {
        this.reloj = reloj ?? (() => DateTime.UtcNow);
        this.espejo = espejo;
        Directory.CreateDirectory(carpeta);
        if (!OperatingSystem.IsWindows()) File.SetUnixFileMode(carpeta,
            UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        try { escritor = new(Path.Combine(carpeta, "escritor.lock"), FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None); }
        catch (IOException) { throw new RuleException("Otro portal tiene abierta la continuación. Detén ese portal antes de iniciar otro escritor.", 503); }
        try
        {
            archivo = new(Path.Combine(carpeta, "eventos.jsonl"), FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.Read);
            if (!OperatingSystem.IsWindows()) File.SetUnixFileMode(Path.Combine(carpeta, "eventos.jsonl"), UnixFileMode.UserRead | UnixFileMode.UserWrite);
            if (archivo.Length == 0)
            {
                inicial = Copia(origen); inicio = this.reloj(); ultima = inicio;
                if (inicio < inicial.FechaStock) throw new InvalidDataException("El reloj precede al saldo inicial.");
                Anexar("BASE", new Cabecera(inicial, inicio));
            }
            else
            {
                archivo.Position = archivo.Length - 1;
                if (archivo.ReadByte() != '\n') throw new InvalidDataException("Última línea incompleta.");
                archivo.Position = 0;
                using var lector = new StreamReader(archivo, new UTF8Encoding(false, true), leaveOpen: true);
                var first = LeerSobre(lector.ReadLine() ?? throw new InvalidDataException());
                if (first.Tipo != "BASE") throw new InvalidDataException("Falta cabecera.");
                var cabecera = first.Datos.Deserialize<Cabecera>(Json) ?? throw new InvalidDataException();
                inicial = cabecera.Base; inicio = cabecera.InicioObservacion; ultima = inicio;
                if (inicio < inicial.FechaStock) throw new InvalidDataException("Inicio inválido.");
                if (!espejo) VerificarBase(origen);
                while (lector.ReadLine() is { } line)
                {
                    var sobre = LeerSobre(line);
                    if (sobre.Tipo == "AJUSTE")
                    {
                        var ajuste = sobre.Datos.Deserialize<AjusteCatalogo>(Json) ?? throw new InvalidDataException();
                        if (ajuste.Secuencia != ajustes.Count + 1 || operacionesAjuste.ContainsKey(ajuste.IdOperacion)
                            || operaciones.ContainsKey(ajuste.IdOperacion) || ajuste.FechaUtc < ultima
                            || ajuste.TipoCuenta != "ADMIN_PUESTOS" || string.IsNullOrWhiteSpace(ajuste.Cuenta))
                            throw new InvalidDataException("Ajuste de catálogo inválido.");
                        ValidarAjuste(Reconstruir(), ajuste.Capacidades, ajuste.Materiales);
                        AgregarAjuste(ajuste);
                        continue;
                    }
                    if (sobre.Tipo != "MOVIMIENTO") throw new InvalidDataException("Tipo de línea desconocido.");
                    var hecho = sobre.Datos.Deserialize<HechoMovimiento>(Json) ?? throw new InvalidDataException();
                    if (hecho.Secuencia != hechos.Count + 1 || operaciones.ContainsKey(hecho.IdOperacion)
                        || operacionesAjuste.ContainsKey(hecho.IdOperacion) || hecho.FechaUtc < ultima)
                        throw new InvalidDataException("Secuencia inválida.");
                    var filas = Reconstruir();
                    var fila = Validar(filas, hecho.Captura, hecho.EnCajas);
                    if (fila.numero_parte != hecho.Parte || fila.id_puesto != hecho.Puesto || string.IsNullOrWhiteSpace(hecho.Cuenta)
                        || hecho.TipoCuenta is not ("ADMIN_PUESTOS" or "PUESTO")
                        || (hecho.TipoCuenta == "PUESTO" && (hecho.PuestoCuenta != fila.id_puesto || hecho.Captura.Tipo != "ENTRADA"))
                        || (hecho.EnCajas && hecho.PiezasPorCaja != fila.cantidad_por_caja))
                        throw new InvalidDataException("Atribución o empaque inválidos.");
                    AgregarEnMemoria(hecho, Calcular(hecho, fila));
                }
                if (espejo) VerificarBase(origen);
                archivo.Position = archivo.Length;
            }
            longitud = archivo.Length;
        }
        catch (Exception ex)
        {
            archivo?.Dispose(); escritor.Dispose();
            throw new RuleException("La continuación no pudo verificarse o abrirse para escritura. Conserva el archivo y revisa el procedimiento de recuperación. Detalle: " + ex.Message, 503);
        }
    }

    static BaseMovimientos Copia(BaseMovimientos b) => JsonSerializer.Deserialize<BaseMovimientos>(JsonSerializer.Serialize(b, Json), Json)!;
    public static string HuellaBase(BaseMovimientos b) => Hash(JsonSerializer.Serialize(b, Json));
    static string Hash(string texto) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(texto)));
    public void VerificarBase(BaseMovimientos origen)
    {
        var esperada = espejo ? Esperada() : inicial;
        if (HuellaBase(origen) == HuellaBase(esperada)) return;
        throw new RuleException((espejo
            ? "El inventario o catálogo de la base compartida no coincide con los movimientos registrados por el portal. Alguien lo modificó fuera del portal o un movimiento no se aplicó. No se mezclaron los saldos."
            : "Cambió el inventario o catálogo SQL respecto del inicio de la continuación. No se mezclaron los saldos. Revisa el origen antes de operar.")
            + " " + Diferencias(esperada, origen), 503);
    }
    /// Explica qué no coincide (portal = saldo inicial + diario; SQL = lo que hay hoy en la base). Máximo 10 diferencias.
    static string Diferencias(BaseMovimientos portal, BaseMovimientos sql)
    {
        var d = new List<string>();
        if (portal.BaseDatos != sql.BaseDatos) d.Add($"base {portal.BaseDatos} en el portal y {sql.BaseDatos} en SQL");
        if (portal.HuellaHistorico != sql.HuellaHistorico) d.Add("cambió el manifiesto del histórico");
        if (portal.FechaStock != sql.FechaStock) d.Add($"fecha del saldo inicial {portal.FechaStock:yyyy-MM-dd HH:mm:ss} en el portal y {sql.FechaStock:yyyy-MM-dd HH:mm:ss} en SQL");
        var enSql = sql.Filas.GroupBy(f => f.id_ubicacion).ToDictionary(g => g.Key, g => g.First());
        foreach (var p in portal.Filas)
        {
            if (!enSql.TryGetValue(p.id_ubicacion, out var q)) { d.Add($"la ubicación {p.id_ubicacion} ({p.numero_parte}) ya no está en SQL"); continue; }
            var a = JsonSerializer.SerializeToElement(p, Json);
            var b = JsonSerializer.SerializeToElement(q, Json);
            foreach (var campo in a.EnumerateObject())
            {
                var valorSql = b.TryGetProperty(campo.Name, out var v) ? v.GetRawText() : "(sin dato)";
                if (valorSql != campo.Value.GetRawText())
                    d.Add($"ubicación {p.id_ubicacion} ({p.numero_parte}) · {campo.Name}: portal {campo.Value.GetRawText()}, SQL {valorSql}");
            }
        }
        var enPortal = portal.Filas.Select(f => f.id_ubicacion).ToHashSet();
        foreach (var q in sql.Filas.Where(f => !enPortal.Contains(f.id_ubicacion)))
            d.Add($"la ubicación {q.id_ubicacion} ({q.numero_parte}) es nueva en SQL");
        if (d.Count == 0) d.Add("mismo contenido en distinto orden o filas repetidas");
        return $"Diferencias ({d.Count}): " + string.Join("; ", d.Take(10)) + (d.Count > 10 ? "; …" : ".");
    }
    // Saldo inicial más movimientos y ajustes de capacidad del diario, sin tocar versión: lo que debe devolver SQL.
    BaseMovimientos Esperada() { var b = Copia(inicial); AplicarHechos(b.Filas); return b; }
    void AplicarHechos(InventarioAlarma[] filas)
    {
        foreach (var evento in eventos)
        {
            if (evento is AjusteCatalogo a) { AplicarAjuste(filas, a); continue; }
            var h = (HechoMovimiento)evento;
            var fila = filas.Single(f => f.id_ubicacion == h.Captura.Ubicacion);
            var piezas = checked((int)PiezasDe(h.Captura, h.PiezasPorCaja, fila.cantidad!.Value));
            var delta = h.Captura.Tipo == "ENTRADA" ? piezas : -piezas;
            fila.cantidad = checked(fila.cantidad!.Value + delta);
            foreach (var material in filas.Where(f => f.numero_parte == fila.numero_parte))
                material.stock_total_material = checked(material.stock_total_material!.Value + delta);
        }
    }
    Sobre LeerSobre(string linea)
    {
        var s = JsonSerializer.Deserialize<Sobre>(linea, Json) ?? throw new InvalidDataException();
        if (s.Version != 1 || s.Anterior != ultimaHuella || s.Huella != Hash(s.Tipo + "\n" + s.Anterior + "\n" + s.Datos.GetRawText()))
            throw new InvalidDataException("Huella o cadena inválida.");
        ultimaHuella = s.Huella; return s;
    }
    void Anexar(string tipo, object datos)
    {
        var element = JsonSerializer.SerializeToElement(datos, Json);
        var huella = Hash(tipo + "\n" + ultimaHuella + "\n" + element.GetRawText());
        var bytes = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new Sobre(1, tipo, element, ultimaHuella, huella), Json) + "\n");
        try { archivo.Write(bytes); archivo.Flush(flushToDisk: true); }
        catch (IOException) { averiado = true; throw new RuleException("No se confirmó la escritura duradera. Detén el portal y verifica la continuación antes de reintentar.", 503); }
        ultimaHuella = huella; longitud = archivo.Length;
    }
    void VerificarArchivo()
    {
        if (averiado || archivo.Length != longitud)
            throw new RuleException("La continuación cambió fuera del portal o quedó incompleta. Detén la captura y verifica el archivo.", 503);
        // Se comprueba el contenido al abrir. Durante la sesión solo este escritor tiene acceso de escritura.
    }
    public async Task<EstadoDiario> Leer()
    {
        await mutex.WaitAsync();
        try { VerificarArchivo(); return new(Copia(inicial), inicio, Reconstruir(), hechos.ToArray(), resultados.ToArray(), ajustes.ToArray()); }
        finally { mutex.Release(); }
    }
    public async Task<ResultadoMovimiento> Registrar(MovimientoNuevo captura, Cuenta actor, Guid id,
        Func<Task<(Cuenta Actor, BaseMovimientos Base)>> verificar, Func<int, int, Task>? aplicar = null)
    {
        await mutex.WaitAsync();
        try
        {
            VerificarArchivo();
            var actual = await verificar(); VerificarBase(actual.Base); actor = actual.Actor;
            if (id == Guid.Empty || captura.Tipo is not ("ENTRADA" or "SALIDA") || captura.Ubicacion <= 0)
                throw new RuleException("Identificador, ubicación o tipo inválido.", 400);
            var autorizada = inicial.Filas.SingleOrDefault(f => f.id_ubicacion == captura.Ubicacion)
                ?? throw new RuleException("Ubicación no disponible.", 400);
            if ((actor.TipoCuenta != "ADMIN_PUESTOS" && (actor.TipoCuenta != "PUESTO" || actor.PuestoId != autorizada.id_puesto))
                || !actor.Permissions.Contains(captura.Tipo == "ENTRADA" ? "entry" : "exit"))
                throw new RuleException("Operación no autorizada para tu cuenta y puesto.", 403);
            if (operacionesAjuste.ContainsKey(id)) throw new RuleException("El identificador ya corresponde a otra operación.", 409);
            if (operaciones.TryGetValue(id, out var n))
            {
                var anterior = hechos[n];
                if (anterior.Captura != captura || anterior.Cuenta != actor.Name || anterior.TipoCuenta != actor.TipoCuenta || anterior.PuestoCuenta != actor.PuestoId)
                    throw new RuleException("El identificador ya corresponde a otra operación.", 409);
                return resultados[n];
            }
            var fila = Validar(Reconstruir(), captura, enCajas: true);
            var ahora = reloj();
            if (ahora < ultima) throw new RuleException("El reloj del servidor retrocedió. Revisa la hora antes de operar.", 503);
            var hecho = new HechoMovimiento(id, hechos.Count + 1, captura, actor.Name, actor.TipoCuenta, actor.PuestoId,
                fila.numero_parte, fila.id_puesto, fila.cantidad_por_caja, ahora);
            var resultado = Calcular(hecho, fila);
            // Primero SQL y después el diario. Si el diario no se confirma, se revierte en SQL.
            var piezas = captura.Tipo == "ENTRADA" ? resultado.CantidadMovimiento : -resultado.CantidadMovimiento;
            if (aplicar is not null) await aplicar(captura.Ubicacion, piezas);
            try { Anexar("MOVIMIENTO", hecho); }
            catch (RuleException) when (aplicar is not null)
            {
                try { await aplicar(captura.Ubicacion, -piezas); } catch (Exception) { /* queda bloqueado por VerificarBase */ }
                throw;
            }
            AgregarEnMemoria(hecho, resultado);
            return resultado;
        }
        finally { mutex.Release(); }
    }
    void AgregarEnMemoria(HechoMovimiento h, ResultadoMovimiento r)
    { operaciones.Add(h.IdOperacion, hechos.Count); hechos.Add(h); resultados.Add(r); eventos.Add(h); ultima = h.FechaUtc; }
    void AgregarAjuste(AjusteCatalogo a)
    { operacionesAjuste.Add(a.IdOperacion, ajustes.Count); ajustes.Add(a); eventos.Add(a); ultima = a.FechaUtc; }

    /// Registra un ajuste de catálogo. En la base compartida primero se aplica en SQL y después en el diario;
    /// si el diario no se confirma, SQL regresa a los valores anteriores (aplicar con revertir = true).
    public Task<AjusteCatalogo> RegistrarAjuste(CambioCapacidad[] cambios, Cuenta actor, Guid id,
        Func<Task<(Cuenta Actor, BaseMovimientos Base)>> verificar, Func<AjusteCatalogo, bool, Task>? aplicar = null) =>
        RegistrarAjuste(cambios, [], actor, id, verificar, aplicar);
    public async Task<AjusteCatalogo> RegistrarAjuste(CambioCapacidad[] capacidades, CambioMaterial[] materiales, Cuenta actor, Guid id,
        Func<Task<(Cuenta Actor, BaseMovimientos Base)>> verificar, Func<AjusteCatalogo, bool, Task>? aplicar = null)
    {
        await mutex.WaitAsync();
        try
        {
            VerificarArchivo();
            var actual = await verificar(); VerificarBase(actual.Base); actor = actual.Actor;
            if (id == Guid.Empty) throw new RuleException("Falta un identificador válido de operación.", 400);
            if (actor.TipoCuenta != "ADMIN_PUESTOS" || !actor.Permissions.Contains("ajustar_catalogo"))
                throw new RuleException("Solo la administración puede ajustar el catálogo.", 403);
            if (operaciones.ContainsKey(id)) throw new RuleException("El identificador ya corresponde a otra operación.", 409);
            capacidades ??= []; materiales ??= [];
            if (operacionesAjuste.TryGetValue(id, out var n))
            {
                var previo = ajustes[n];
                if (!previo.Capacidades.SequenceEqual(capacidades) || !(previo.Materiales ?? []).SequenceEqual(materiales)
                    || previo.Cuenta != actor.Name)
                    throw new RuleException("El identificador ya corresponde a otra operación.", 409);
                return previo;
            }
            ValidarAjuste(Reconstruir(), capacidades, materiales);
            var ahora = reloj();
            if (ahora < ultima) throw new RuleException("El reloj del servidor retrocedió. Revisa la hora antes de operar.", 503);
            var ajuste = new AjusteCatalogo(id, ajustes.Count + 1, actor.Name, actor.TipoCuenta, capacidades, ahora,
                materiales.Length > 0 ? materiales : null);
            if (aplicar is not null) await aplicar(ajuste, false);
            try { Anexar("AJUSTE", ajuste); }
            catch (RuleException) when (aplicar is not null)
            {
                try { await aplicar(ajuste, true); }
                catch (Exception) { /* queda bloqueado por VerificarBase */ }
                throw;
            }
            AgregarAjuste(ajuste);
            return ajuste;
        }
        finally { mutex.Release(); }
    }

    /// La capacidad nueva no puede dejar cajas fuera: nunca menor que las cajas que ya ocupan la estantería
    /// (una caja abierta cuenta como completa).
    static void ValidarAjuste(InventarioAlarma[] filas, CambioCapacidad[]? cambios, CambioMaterial[]? materiales)
    {
        cambios ??= []; materiales ??= [];
        if (cambios.Length + materiales.Length == 0 || cambios.Length > 500 || materiales.Length > 5000
            || cambios.Select(c => c.Estanteria).Distinct().Count() != cambios.Length
            || materiales.Select(m => (m.Parte, m.Campo)).Distinct().Count() != materiales.Length)
            throw new RuleException("Revisa los cambios del catálogo.", 400);
        // Los datos de material van primero: un nuevo empaque cambia cuántas cajas ocupa cada estantería.
        var despues = Copia(new BaseMovimientos("", "", DateTime.MinValue, filas)).Filas;
        foreach (var m in materiales)
        {
            var grupo = despues.Where(f => f.numero_parte == m.Parte).ToArray();
            if (grupo.Length == 0) throw new RuleException($"El material {m.Parte} no está en el inventario del portal.", 400);
            if (ValorMaterial(grupo[0], m.Campo) != m.Anterior)
                throw new RuleException($"El material {m.Parte} cambió mientras se preparaba la importación. Vuelve a cargar el archivo.", 409);
            if (m.Nuevo == m.Anterior) throw new RuleException($"El material {m.Parte} no cambia en {m.Campo}.", 400);
            ValidarValorMaterial(m);
            AplicarMaterial(grupo, m);
        }
        foreach (var c in cambios)
        {
            var grupo = despues.Where(f => f.id_estanteria == c.Estanteria).ToArray();
            if (grupo.Length == 0) throw new RuleException("Estantería no disponible.", 400);
            var actuales = grupo.Select(f => f.cajas_estanteria).Distinct().ToArray();
            if (actuales.Length != 1) throw new RuleException("La estantería tiene capacidades distintas registradas. Revísala antes de ajustarla.", 409);
            if (actuales[0] != c.Anterior) throw new RuleException("La capacidad cambió mientras la editabas. Actualiza y vuelve a intentarlo.", 409);
            if (c.Nueva is < 1 or > 10000) throw new RuleException("La capacidad debe estar entre 1 y 10,000 cajas.", 400);
            if (c.Nueva == c.Anterior) throw new RuleException("La capacidad no cambió.", 400);
            var ocupadas = grupo.Where(f => f.cantidad is >= 0 && f.cantidad_por_caja is > 0)
                .Sum(f => CajasOcupadas(f.cantidad!.Value, f.cantidad_por_caja!.Value));
            if (c.Nueva < ocupadas)
                throw new RuleException($"La estantería tiene {ocupadas} {(ocupadas == 1 ? "caja ocupada" : "cajas ocupadas")}; la capacidad no puede ser menor.", 409);
        }
    }
    public static string? ValorMaterial(InventarioAlarma f, string campo) => campo switch
    {
        "descripcion" => f.descripcion,
        "uso" => f.uso,
        "cantidad_por_caja" => f.cantidad_por_caja?.ToString(System.Globalization.CultureInfo.InvariantCulture),
        "minimo" => f.minimo?.ToString(System.Globalization.CultureInfo.InvariantCulture),
        "variante" => f.variante_revision,
        _ => throw new RuleException("Campo de material no permitido: " + campo, 400)
    };
    public static void ValidarValorMaterial(CambioMaterial m)
    {
        var v = m.Nuevo;
        var valido = m.Campo switch
        {
            "descripcion" => v is { Length: > 0 and <= 250 } && v == v.Trim(),
            "uso" => v is "COMPARTIDO" or "EXCLUSIVO",
            "cantidad_por_caja" => int.TryParse(v, System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out var cpc) && cpc is >= 1 and <= 1000000,
            "minimo" => int.TryParse(v, System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out var min) && min is >= 0 and <= 100000000,
            "variante" => v is { Length: > 0 and <= 10 } && v == v.Trim(),
            _ => false
        };
        if (!valido) throw new RuleException($"Valor no válido para {m.Campo} en {m.Parte}: «{v}».", 400);
    }
    static void AplicarMaterial(IEnumerable<InventarioAlarma> grupo, CambioMaterial m)
    {
        foreach (var f in grupo)
            switch (m.Campo)
            {
                case "descripcion": f.descripcion = m.Nuevo; break;
                case "uso": f.uso = m.Nuevo; break;
                case "cantidad_por_caja": f.cantidad_por_caja = m.Nuevo is null ? null : int.Parse(m.Nuevo, System.Globalization.CultureInfo.InvariantCulture); break;
                case "minimo": f.minimo = m.Nuevo is null ? null : int.Parse(m.Nuevo, System.Globalization.CultureInfo.InvariantCulture); break;
                case "variante": f.variante_revision = m.Nuevo; break;
            }
    }
    public static void AplicarAjuste(InventarioAlarma[] filas, AjusteCatalogo a)
    {
        foreach (var m in a.Materiales ?? []) AplicarMaterial(filas.Where(f => f.numero_parte == m.Parte), m);
        foreach (var c in a.Capacidades)
            foreach (var f in filas.Where(f => f.id_estanteria == c.Estanteria)) f.cajas_estanteria = c.Nueva;
    }
    /// Piezas de una captura. Entradas: cajas × empaque. Salidas en cajas: se retiran primero las cajas
    /// cerradas y al final la caja abierta, así que nunca se descuenta más que el saldo.
    /// Salidas sin empaque (anteriores al 17/11/2026): la cantidad ya está en piezas.
    static long PiezasDe(MovimientoNuevo c, int? piezasPorCaja, int saldo) =>
        c.Tipo == "ENTRADA" ? (long)c.Cantidad * (piezasPorCaja ?? 0)
        : piezasPorCaja is int ppc ? Math.Min((long)c.Cantidad * ppc, saldo)
        : c.Cantidad;
    /// Cajas que ocupa un saldo: una caja con piezas sueltas ocupa el lugar de una caja completa.
    static long CajasOcupadas(int saldo, int piezasPorCaja) => ((long)saldo + piezasPorCaja - 1) / piezasPorCaja;
    static InventarioAlarma Validar(InventarioAlarma[] filas, MovimientoNuevo c, bool enCajas)
    {
        if (c.Tipo is not ("ENTRADA" or "SALIDA") || c.Cantidad is < 1 or > 1000000 || c.Version < 1 || (c.Tipo == "ENTRADA" && c.EsConsumo))
            throw new RuleException("Revisa tipo, cantidad entera y finalidad del movimiento.", 400);
        var fila = filas.SingleOrDefault(f => f.id_ubicacion == c.Ubicacion) ?? throw new RuleException("Ubicación no disponible.", 400);
        if (fila.version_concurrencia != c.Version) throw new RuleException("Cambió la disponibilidad. Actualiza y confirma de nuevo.", 409);
        if (fila.cantidad is null) throw new RuleException("No hay un saldo inequívoco para esta ubicación.", 409);
        if (enCajas && fila.cantidad_por_caja is not > 0) throw new RuleException("Falta un empaque válido (piezas por caja) para este material.", 400);
        if (c.Tipo == "SALIDA" && enCajas)
        {
            var ocupadas = CajasOcupadas(fila.cantidad.Value, fila.cantidad_por_caja!.Value);
            if (ocupadas == 0) throw new RuleException("No hay existencias en esta ubicación.", 409);
            if (c.Cantidad > ocupadas)
                throw new RuleException($"Solo hay {ocupadas} {(ocupadas == 1 ? "caja" : "cajas")} en esta ubicación.", 409);
        }
        long piezas = PiezasDe(c, enCajas ? fila.cantidad_por_caja : null, fila.cantidad.Value);
        if (piezas is <= 0 or > int.MaxValue) throw new RuleException("Falta empaque válido o la cantidad excede el límite.", 400);
        long saldo = fila.cantidad.Value + (c.Tipo == "ENTRADA" ? piezas : -piezas);
        if (saldo is < 0 or > int.MaxValue) throw new RuleException("La cantidad supera las existencias disponibles.", 409);
        if (c.Tipo == "ENTRADA" && (fila.cajas_disponibles is null || c.Cantidad > fila.cajas_disponibles))
            throw new RuleException("No hay espacio validado para esas cajas en la estantería compartida.", 409);
        return fila;
    }
    static ResultadoMovimiento Calcular(HechoMovimiento h, InventarioAlarma fila)
    {
        var piezas = checked((int)PiezasDe(h.Captura, h.PiezasPorCaja, fila.cantidad!.Value));
        return new(h.IdOperacion, "CONTINUACION:" + h.IdOperacion, h.Captura.Tipo, piezas, fila.cantidad!.Value,
            checked(fila.cantidad.Value + (h.Captura.Tipo == "ENTRADA" ? piezas : -piezas)), h.FechaUtc);
    }
    InventarioAlarma[] Reconstruir()
    {
        var filas = Copia(inicial).Filas;
        AplicarHechos(filas);
        foreach (var f in filas) f.version_concurrencia = hechos.Count(h => h.Captura.Ubicacion == f.id_ubicacion) + 1;
        CalcularCapacidad(filas);
        return filas;
    }
    internal static void CalcularCapacidad(InventarioAlarma[] filas)
    {
        foreach (var grupo in filas.GroupBy(f => f.id_estanteria))
        {
            var capacidades = grupo.Select(f => f.cajas_estanteria).Distinct().ToArray();
            long? libre = null;
            if (capacidades.Length == 1 && capacidades[0] is >= 0 && grupo.All(f => f.cantidad is >= 0 && f.cantidad_por_caja is > 0))
            {
                var ocupadas = grupo.Sum(f => CajasOcupadas(f.cantidad!.Value, f.cantidad_por_caja!.Value));
                libre = Math.Max(0, capacidades[0]!.Value - ocupadas);
            }
            foreach (var f in grupo) f.cajas_disponibles = libre;
        }
    }
    public void Dispose() { archivo.Dispose(); escritor.Dispose(); mutex.Dispose(); }
}
