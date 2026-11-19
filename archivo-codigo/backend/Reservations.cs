namespace Motherson;
public record CapacityInput(int MaterialId,int UbicacionId,int CajasOcupadas,int ReservaSegundos,bool Habilitada,string Motivo);
public record EndShiftInput(int TurnoId,DateTime Hasta,string Motivo);
public record CatalogResult(int MaterialId,int UbicacionId,int EstacionId);
public record ReviewedInput(CatalogInput Asignacion,long EmpaqueStagingId);
public static class CapacityRules
{
    public static long? Free(int? nominal,int? occupied,bool verified,long reserved)=>verified&&nominal is >0&&occupied is >=0?nominal.Value-(long)occupied.Value-reserved:null;
    public static void Require(long boxes,long? free)
    {
        if(boxes is <1 or >1000000)throw new RuleException("Cajas fuera de rango.",400);
        if(free is null)throw new RuleException("Capacidad u ocupación física sin verificar. No se reservó espacio.");
        if(boxes>free)throw new RuleException("Las cajas exceden el espacio libre tras considerar las reservas activas.");
    }
}
