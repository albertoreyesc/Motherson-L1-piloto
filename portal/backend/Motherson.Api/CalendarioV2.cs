using System.Globalization;
using System.Text.Json.Nodes;
namespace Motherson;
public static class CalendarioV2
{
 public static string Clasificar(decimal minutos,JsonObject o)=>minutos>=AdminV2.N(o,"normal",60)?"NORMAL":minutos>=AdminV2.N(o,"advertencia",30)?"ADVERTENCIA":minutos>=AdminV2.N(o,"critico",15)?"CRITICO":"RIESGO_PARO";
 static DateOnly Dia(string s)=>DateOnly.ParseExact(s,"yyyy-MM-dd",CultureInfo.InvariantCulture);
 static int Minuto(string s){var h=TimeOnly.ParseExact(s,"HH:mm",CultureInfo.InvariantCulture);return h.Hour*60+h.Minute;}
 // Offset desde el día de inicio: conserva el turno nocturno y excluye descansos.
 static (int Inicio,int Fin)[] Tramos(JsonObject turno)
 {
  var inicio=Minuto(AdminV2.S(turno,"inicio"));var fin=Minuto(AdminV2.S(turno,"fin"));if(fin==inicio)throw new FormatException();if(fin<inicio)fin+=1440;
  if(turno["descansos"] is not null and not JsonArray)throw new FormatException();
  var descansos=(turno["descansos"] as JsonArray??new()).Select(x=>{var d=x!.AsObject();var a=Minuto(AdminV2.S(d,"inicio"));var b=Minuto(AdminV2.S(d,"fin"));if(a<inicio)a+=1440;if(b<inicio)b+=1440;if(b<=a||a<inicio||b>fin)throw new FormatException();return (Inicio:a,Fin:b);}).OrderBy(x=>x.Inicio).ToArray();
  if(descansos.Length>8||descansos.Zip(descansos.Skip(1)).Any(p=>p.First.Fin>p.Second.Inicio))throw new FormatException();
  var result=new List<(int,int)>();var cursor=inicio;foreach(var d in descansos){if(cursor<d.Inicio)result.Add((cursor,d.Inicio));cursor=d.Fin;}if(cursor<fin)result.Add((cursor,fin));if(result.Count==0)throw new FormatException();return result.ToArray();
 }
 public static void Validar(JsonObject o)
 {
  try{
   var desde=Dia(AdminV2.S(o,"desde"));var hasta=Dia(AdminV2.S(o,"hasta"));if(hasta<desde||hasta.DayNumber-desde.DayNumber>732)throw new FormatException();
   var dias=o["dias"]!.AsArray().Select(x=>x!.GetValue<int>()).ToArray();if(dias.Length==0||dias.Any(x=>x<0||x>6)||dias.Distinct().Count()!=dias.Length)throw new FormatException();
   var turnos=o["turnos"]!.AsArray();if(turnos.Count is <1 or >3)throw new FormatException();
   // Los turnos completos tampoco se solapan durante un descanso.
   var spans=turnos.Select(x=>{var t=x!.AsObject();_ = Tramos(t);var a=Minuto(AdminV2.S(t,"inicio"));var b=Minuto(AdminV2.S(t,"fin"));return (Inicio:a,Fin:b<a?b+1440:b);}).ToArray();
   var ex=o["excepciones"] as JsonObject;if(o["excepciones"] is not null&&ex is null)throw new FormatException();
   if(ex is not null)foreach(var p in ex){var d=Dia(p.Key);if(d<desde||d>hasta||p.Value is not JsonValue v||!v.TryGetValue<bool>(out _))throw new FormatException();}
   var sample=Enumerable.Range(0,hasta.DayNumber-desde.DayNumber+1).Where(n=>ex?[desde.AddDays(n).ToString("yyyy-MM-dd")] is JsonValue v?v.GetValue<bool>():dias.Contains((int)desde.AddDays(n).DayOfWeek)).SelectMany(d=>spans.Select(t=>(Start:d*1440+t.Inicio,End:d*1440+t.Fin))).OrderBy(x=>x.Start).ToArray();
   if(sample.Zip(sample.Skip(1)).Any(p=>p.First.End>p.Second.Start))throw new FormatException();
   foreach(var key in new[]{"normal","advertencia","critico"})if(o.ContainsKey(key)&&(o[key] is not JsonValue n||!n.TryGetValue<int>(out _)))throw new FormatException();
   var normal=AdminV2.N(o,"normal",60);var adv=AdminV2.N(o,"advertencia",30);var crit=AdminV2.N(o,"critico",15);if(crit<1||adv<=crit||normal<=adv||normal>1440)throw new FormatException();
   if(AdminV2.S(o,"fuente").Trim().Length is <1 or >250)throw new FormatException();
   if(o["simulado"] is not null&&(o["simulado"] is not JsonValue sv||!sv.TryGetValue<bool>(out _)))throw new FormatException();
  }catch(Exception e)when(e is FormatException or InvalidOperationException or NullReferenceException or ArgumentException){throw new RuleException("Revisa fechas, días (0=domingo), turnos sin solapamiento, descansos dentro del turno, excepciones y umbrales: 0 < crítico < advertencia < normal. Indica la fuente del calendario.",400);}
 }
 public static VentanaCobertura Ventana(JsonObject? o,DateTime corte)
 {
  if(o is null)return new([],"CALENDARIO_NO_DISPONIBLE");Validar(o);
  var tz=TimeZoneInfo.FindSystemTimeZoneById("America/Mexico_City");var local=TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(corte,DateTimeKind.Utc),tz);var day=DateOnly.FromDateTime(local);var desde=Dia(AdminV2.S(o,"desde"));var hasta=Dia(AdminV2.S(o,"hasta"));
  var turnos=o["turnos"]!.AsArray().SelectMany(t=>Tramos(t!.AsObject())).ToArray();
  // Permite terminar el último turno nocturno después del último día de inicio válido.
  if(day<desde||day>hasta.AddDays(1)||day==hasta.AddDays(1)&&!turnos.Any(t=>t.Fin>1440&&local.TimeOfDay.TotalMinutes<t.Fin-1440))return new([],"FUERA_CALENDARIO_DOCUMENTADO");
  var dias=o["dias"]!.AsArray().Select(x=>x!.GetValue<int>()).ToHashSet();var tramos=new List<IntervaloLaborable>();var ex=o["excepciones"] as JsonObject;
  for(var d=day>hasta?hasta:day;d>=desde;d=d.AddDays(-1)){
   var laborable=ex?[d.ToString("yyyy-MM-dd")] is JsonValue v?v.GetValue<bool>():dias.Contains((int)d.DayOfWeek);if(!laborable)continue;
   foreach(var t in turnos){var start=TimeZoneInfo.ConvertTimeToUtc(d.ToDateTime(TimeOnly.MinValue).AddMinutes(t.Inicio),tz);var end=TimeZoneInfo.ConvertTimeToUtc(d.ToDateTime(TimeOnly.MinValue).AddMinutes(t.Fin),tz);if(end>corte)end=corte;if(end>start)tramos.Add(new(start,end));}
  }
  var escogidos=new List<IntervaloLaborable>();decimal faltan=60;
  foreach(var t in tramos.OrderByDescending(t=>t.Fin)){var take=Math.Min(faltan,(decimal)(t.Fin-t.Inicio).TotalMinutes);escogidos.Add(new(t.Fin.AddMinutes(-(double)take),t.Fin));faltan-=take;if(faltan==0)return new(escogidos.OrderBy(t=>t.Inicio).ToArray(),null);}
  return new([],"VENTANA_INCOMPLETA");
 }
}
