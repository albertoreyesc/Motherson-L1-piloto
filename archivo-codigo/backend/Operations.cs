namespace Motherson;

// Dates are operational dates, anchored to shift start in the plant timezone.
public record ShiftDefinition(int Id,int InicioMinuto,int FinMinuto,string ZonaHoraria,DateTime Desde,DateTime? Hasta,DateTime? RetiradoUtc=null);
public record RetireShiftInput(string Motivo);
public record ShiftMatch(int? TurnoId,DateTime? FechaOperativa);
public record ShiftInput(string Codigo,string Nombre,int InicioMinuto,int FinMinuto,string ZonaHoraria,DateTime Desde,DateTime? Hasta,string Motivo);
public record ShiftAssignment(int UsuarioId,int TurnoId,DateTime Desde,DateTime? Hasta);
public record MinimumInput(int MaterialId,int UbicacionId,long? MinimoPiezas,bool Aprobado,string Motivo);
public record ShiftCutInput(int TurnoId,DateTime FechaOperativa,int MaterialId,int UbicacionId);
public static class ShiftRules
{
    public static TimeZoneInfo Zone(string name)
    {
        try { return TimeZoneInfo.FindSystemTimeZoneById(name); }
        catch(Exception e) when(e is TimeZoneNotFoundException or InvalidTimeZoneException or ArgumentException){throw new RuleException("Zona horaria inválida.",400);}
    }
    public static void Validate(ShiftDefinition s)
    {
        if(s.InicioMinuto is <0 or >1439 || s.FinMinuto is <0 or >1439 || s.InicioMinuto==s.FinMinuto || s.Hasta?.Date<s.Desde.Date)throw new RuleException("Horario o vigencia de turno inválidos.",400);
        Zone(s.ZonaHoraria);
    }
    public static ShiftMatch Match(DateTime utc,IEnumerable<ShiftDefinition> shifts)
    {
        var matches=new List<ShiftMatch>();
        foreach(var s in shifts)
        {
            if(s.RetiradoUtc is not null && utc>=s.RetiradoUtc.Value)continue;
            var local=TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utc,DateTimeKind.Utc),Zone(s.ZonaHoraria));
            int minute=local.Hour*60+local.Minute;bool night=s.FinMinuto<s.InicioMinuto;
            bool inside=night?minute>=s.InicioMinuto||minute<s.FinMinuto:minute>=s.InicioMinuto&&minute<s.FinMinuto;
            var day=night&&minute<s.FinMinuto?local.Date.AddDays(-1):local.Date;
            if(inside&&day>=s.Desde.Date&&(s.Hasta is null||day<=s.Hasta.Value.Date))matches.Add(new(s.Id,day));
        }
        if(matches.Count>1)throw new RuleException("Horarios de turno superpuestos; requiere configuración administrativa.");
        return matches.Count==1?matches[0]:new(null,null);
    }
    public static (DateTime Start,DateTime End) Window(ShiftDefinition s,DateTime day)
    {
        day=day.Date;if(day<s.Desde.Date||(s.Hasta is not null&&day>s.Hasta.Value.Date))throw new RuleException("Fecha fuera de la vigencia del turno.",400);
        var zone=Zone(s.ZonaHoraria);
        var start=DateTime.SpecifyKind(day.AddMinutes(s.InicioMinuto),DateTimeKind.Unspecified);
        var end=DateTime.SpecifyKind(day.AddDays(s.FinMinuto<s.InicioMinuto?1:0).AddMinutes(s.FinMinuto),DateTimeKind.Unspecified);
        if(zone.IsInvalidTime(start)||zone.IsInvalidTime(end)||zone.IsAmbiguousTime(start)||zone.IsAmbiguousTime(end))throw new RuleException("Límite ambiguo por cambio de horario; requiere revisión.");
        return(TimeZoneInfo.ConvertTimeToUtc(start,zone),TimeZoneInfo.ConvertTimeToUtc(end,zone));
    }
    public static bool Overlap(ShiftDefinition a,ShiftDefinition b)
    {
        int ae=a.FinMinuto+(a.FinMinuto<a.InicioMinuto?1440:0),be=b.FinMinuto+(b.FinMinuto<b.InicioMinuto?1440:0);
        long ad=DateOnly.FromDateTime(a.Desde).DayNumber,al=a.Hasta is null?4000000:DateOnly.FromDateTime(a.Hasta.Value).DayNumber;
        long bd=DateOnly.FromDateTime(b.Desde).DayNumber,bl=b.Hasta is null?4000000:DateOnly.FromDateTime(b.Hasta.Value).DayNumber;
        for(int offset=-1;offset<=1;offset++)if(Math.Max(a.InicioMinuto,b.InicioMinuto+offset*1440)<Math.Min(ae,be+offset*1440)&&Math.Max(ad,bd-offset)<=Math.Min(al,bl-offset))return true;
        return false;
    }
}
