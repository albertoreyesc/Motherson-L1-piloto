namespace Motherson;

public record AdjustmentInput(int MaterialId,int UbicacionId,long? DiferenciaPiezas,long? ConteoFisicoPiezas,string VersionEsperada,string Motivo);
public record ClosePeriodInput(bool CapturaCompleta,string? FuenteConsumo,string Motivo,long? ConteoFisicoPiezas,string? VersionEsperada);
public static class EvidenceRules
{
    public static (long Delta,long Balance) Adjustment(long before,bool initialized,long? difference,long? physical,long? capacity)
    {
        if(!initialized)throw new RuleException("Registra primero el conteo físico inicial.");
        if(difference.HasValue==physical.HasValue)throw new RuleException("Indica una diferencia firmada o un recuento físico, exclusivamente.",400);
        if(physical is <0 or >1000000000000 || difference is < -1000000000000 or >1000000000000 || difference==0)throw new RuleException("Ajuste fuera de rango; el recuento sí puede confirmar una diferencia cero.",400);
        long delta=physical.HasValue?checked(physical.Value-before):difference!.Value;
        long balance=checked(before+delta);
        if(balance<0)throw new RuleException("El ajuste produciría saldo negativo.");
        // A true physical observation must remain recordable, even if it reveals overcapacity.
        // Positive documentary adjustments still obey the configured limit.
        if(physical is null && delta>0 && capacity is not null && balance>capacity)throw new RuleException("El ajuste positivo excede la capacidad nominal validada.");
        return(delta,balance);
    }
    public static decimal? CoverageMinutes(long? reliableStock,long consumed,decimal elapsedHours,bool complete,bool compatible)
        => complete && compatible ? Rules.Coverage(reliableStock,consumed,elapsedHours,true)*60 : null;
    public static void Reason(string? reason)
    {if(string.IsNullOrWhiteSpace(reason)||reason.Length>500)throw new RuleException("Indica evidencia y motivo de hasta 500 caracteres.",400);}
}
