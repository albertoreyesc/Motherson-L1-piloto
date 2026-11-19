namespace Motherson;
public sealed class RuleException(string message, int status = 409) : Exception(message) { public int Status { get; } = status; }
public static class Rules
{
    public static long Delta(string type, long quantity, long factor)
    {
        if (quantity < 0 || quantity > 1000000 || (type != "CONTEO" && quantity == 0)) throw new RuleException("Cantidad entera fuera de rango (1–1,000,000).", 400);
        if (factor <= 0 || factor > 1000000) throw new RuleException("Factor de empaque sin aprobar.", 400);
        return type switch { "ENTRADA" => checked(quantity * factor), "CONSUMO" => -quantity, "CONTEO" => quantity, _ => throw new RuleException("Tipo inválido.",400) };
    }
    public static long Balance(long current, long delta, bool verified, string type, long? capacity)
    {
        if (!verified && type != "CONTEO") throw new RuleException("Se requiere conteo físico inicial verificado; capacidad no es stock.");
        if (verified && type == "CONTEO") throw new RuleException("El conteo inicial ya fue registrado. Ajustes posteriores requieren un flujo autorizado.");
        long result = checked(current + delta);
        if (result < 0) throw new RuleException("Saldo insuficiente. No se registró el movimiento.");
        if (capacity is not null && result > capacity && type == "ENTRADA") throw new RuleException("La entrada excede la capacidad nominal validada.");
        return result;
    }
    public static decimal? Coverage(long? stock, long consumed, decimal hours, bool valid) => valid && stock >= 0 && consumed > 0 && hours > 0 ? stock / (consumed / hours) : null;
}
public record LoginInput(string Username, string Password);
public record MoveInput(int MaterialId,int UbicacionId,int? EstacionId,int? PuestoId,string Tipo,long Cantidad,string Motivo);
public record RequestInput(int MaterialId,int UbicacionId,int EstacionId,int PuestoId,int Cajas);
public record PeriodInput(int MaterialId,int UbicacionId);
public record CatalogInput(string Parte,string Descripcion,long PiezasPorCaja,int PuestoId,string Estacion,string Linea,string Ubicacion,string Estanteria,string Nivel,int? CapacidadCajas,decimal? UmbralHoras,long? StagingId,string Motivo);
public record UserInput(string Username,string Password,string Role,int? PuestoId,string? NombreVisible=null,bool CuentaSimulada=false);
public record Actor(int Id,string Name,string[] Roles,string[] Permissions,string? DisplayName=null,bool CuentaSimulada=false,int AuthRevision=0);
public record MoveResult(Guid Id,long SaldoPiezas,long DeltaPiezas,DateTime RegistradoUtc,bool Simulado,long Cajas=0,long Factor=0,long SaldoAnterior=0,int MaterialId=0,int UbicacionId=0,int UsuarioId=0,int? PuestoId=null,int? AsignacionId=null,string? Turno=null,DateTime? FechaOperativa=null);
