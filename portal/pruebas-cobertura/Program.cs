using Motherson;
using System.Text.Json;
var pruebas = new List<object>();
void Test(string nombre, Action fn) { fn(); pruebas.Add(new { caso = nombre, resultado = "APROBADO" }); }
void Check(bool condicion) { if (!condicion) throw new Exception("Falló la condición de prueba."); }
var zona = TimeZoneInfo.FindSystemTimeZoneById("America/Mexico_City");
DateTime Utc(string text) => TimeZoneInfo.ConvertTimeToUtc(DateTime.Parse(text), zona);
var dias = new Dictionary<DateOnly, bool>();
for (var dia = new DateOnly(2026, 10, 16); dia <= new DateOnly(2026, 10, 20); dia = dia.AddDays(1))
    dias[dia] = dia.DayOfWeek is not (DayOfWeek.Saturday or DayOfWeek.Sunday);
CalendarioCobertura Cal(Dictionary<DateOnly, bool>? config = null) => new(new(2026, 10, 16), new(2026, 10, 21), config ?? dias,
    [(new(6, 0), new(15, 0)), (new(15, 0), new(23, 30))], "CALENDARIO_SINTETICO_SOLO_PRUEBA", "sin fuente de planta");
var corte = Utc("2026-10-19T15:30:00"); var ventana = Cal().Ventana(corte);
var apertura = Utc("2026-10-16T06:00:00");
ConsumoCobertura C(string id, string fecha, int piezas = 30, string tipo = "SALIDA", string espacio = "MVP", int ubicacion = 1, string parte = "TEST", bool simulado = true)
    => new(id, ubicacion, parte, espacio, tipo, piezas, Utc(fecha), simulado);
var buenos = new[] { C("MVP:1", "2026-10-19T14:45:00"), C("MVP:2", "2026-10-19T15:15:00") };
ResultadoCobertura Eval(int? saldo, IEnumerable<ConsumoCobertura>? eventos = null, VentanaCobertura? v = null, DateTime? inicio = null)
    => MotorCobertura.Evaluar(saldo, 1, "TEST", corte, v ?? ventana, inicio ?? apertura, eventos ?? buenos);
Test("Saldo desconocido y cero prevalecen sobre tasa/calendario", () => {
    Check(Eval(null).Estado == "SIN_DATOS");
    Check(Eval(0, v: new([], "FUERA_CALENDARIO_DOCUMENTADO")).Estado == "AGOTADO");
});
foreach (var (minutos, estado) in new[] { (14.999m,"RIESGO_PARO"),(15m,"CRITICO"),(29.999m,"CRITICO"),(30m,"ADVERTENCIA"),(59.999m,"ADVERTENCIA"),(60m,"NORMAL") })
    Test("Frontera sin redondear " + minutos, () => Check(MotorCobertura.Clasificar(minutos) == estado));
Test("Saldo/tasa/corte coherentes: 15 piezas a 60 pz/h = 15 min", () => {
    var r = Eval(15); Check(r.Estado == "CRITICO" && r.Minutos == 15 && r.TasaPiezasHora == 60 && r.PiezasAplicadas == 60 && r.Observaciones == 2);
});
Test("Una muestra, tasa cero y consumos antiguos no producen cobertura", () => {
    Check(Eval(15, buenos.Take(1)).Causa == "MUESTRA_INSUFICIENTE");
    Check(Eval(15, [C("0", "2026-10-19T15:10:00",0)]).Minutos == null);
    Check(Eval(15, [C("viejo", "2026-10-16T10:00:00")]).Causa == "SIN_CONSUMO_RECIENTE");
});
Test("Cruce T1/T2 conserva exactamente 60 minutos", () => {
    Check(ventana.Completa && ventana.Minutos == 60 && ventana.Intervalos.Count == 2);
    Check(ventana.Intervalos[0].Inicio == Utc("2026-10-19T14:30:00"));
});
Test("Corte con fracciones conserva la ventana exacta", () => Check(Cal().Ventana(corte.AddTicks(1234567)).Minutos == 60));
Test("Ventana cruza noche y fin de semana sin sumar horas cerradas", () => {
    var v = Cal().Ventana(Utc("2026-10-19T06:30:00"));
    Check(v.Completa && v.Minutos == 60 && v.Intervalos[0].Inicio == Utc("2026-10-16T23:00:00"));
    Check(v.Intervalos[1].Inicio == Utc("2026-10-19T06:00:00"));
});
Test("Cierre explícito se excluye; un día ausente impide estimar", () => {
    var cerrados = new Dictionary<DateOnly,bool>(dias) { [new(2026,10,19)] = false };
    var v = Cal(cerrados).Ventana(Utc("2026-10-20T06:30:00"));
    Check(v.Completa && v.Intervalos[0].Inicio == Utc("2026-10-16T23:00:00"));
    cerrados.Remove(new(2026,10,19)); Check(Cal(cerrados).Ventana(Utc("2026-10-20T06:30:00")).Motivo == "DIA_SIN_CALENDARIO");
});
Test("Calendario fuera de vigencia y ventana inicial incompleta", () => {
    Check(Cal().Ventana(Utc("2026-11-02T12:00:00")).Motivo == "FUERA_CALENDARIO_DOCUMENTADO");
    Check(Cal().Ventana(Utc("2026-10-16T06:30:00")).Motivo == "VENTANA_INCOMPLETA");
});
Test("Apertura reciente y eventos futuros no acreditan ventana completa", () => {
    Check(Eval(15, inicio: Utc("2026-10-19T15:00:00")).Minutos == null);
    Check(Eval(15,[C("futuro","2026-10-19T15:31:00"),C("en_corte","2026-10-19T15:30:00")]).Minutos == null);
    Check(Eval(15,v:new([new(corte,corte.AddMinutes(60))],null)).Causa == "CORTE_INCONSISTENTE");
});
Test("IDs duplicados se cuentan una vez y referencias de archivo no se reaplican", () => {
    var r = Eval(15,buenos.Concat(buenos).Append(C("H21:1","2026-10-19T15:15:00",600,espacio:"HISTORICO_21")));
    Check(r.Observaciones == 2 && r.PiezasAplicadas == 60);
    Check(Eval(15,[buenos[0],buenos[0] with { Piezas = 99 }]).Causa == "IDENTIFICADOR_CONFLICTIVO");
});
Test("Entradas, aperturas, ajustes, otro material/ubicación y operaciones reales se excluyen", () => {
    var otros = new[] { C("a","2026-10-19T15:10:00",99,"APERTURA"), C("e","2026-10-19T15:10:00",99,"ENTRADA"), C("j","2026-10-19T15:10:00",99,"AJUSTE"), C("u","2026-10-19T15:10:00",99,ubicacion:2), C("p","2026-10-19T15:10:00",99,parte:"OTRO"), C("r","2026-10-19T15:10:00",99,simulado:false) };
    Check(Eval(15,buenos.Concat(otros)).PiezasAplicadas == 60);
});
Test("Mínimo por material suma ubicaciones y conserva cantidades desconocidas", () => {
    Check(MotorCobertura.Minimo([5,10],10).Estado == "SOBRE_MINIMO");
    Check(MotorCobertura.Minimo([5,5],10).Estado == "BAJO_MINIMO");
    Check(MotorCobertura.Minimo([5,null],10).Total == null);
    Check(MotorCobertura.Minimo([0,0],null).Estado == "AGOTADO");
    Check(MotorCobertura.Minimo([10],null).Estado == "SIN_DATOS");
    Check(MotorCobertura.Minimo([int.MaxValue,int.MaxValue],10).Total == 4294967294L);
});
Test("Calendario documental completo: 365 días, sin extenderlo a noviembre", () => {
    var r = CalendarioCobertura.Leer(args[0]);
    Check(r.Ventana(Utc("2026-10-05T15:30:00")).Completa);
    Check(r.Ventana(Utc("2026-11-02T12:00:00")).Motivo == "FUERA_CALENDARIO_DOCUMENTADO");
});
Console.WriteLine(JsonSerializer.Serialize(new { politica = MotorCobertura.Politica, total = pruebas.Count, casos = pruebas }));
