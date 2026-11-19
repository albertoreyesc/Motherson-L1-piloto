using System.Xml.Linq;
using Dapper;
using Microsoft.AspNetCore.DataProtection.Repositories;
using Microsoft.Data.SqlClient;

namespace Motherson;

// Solo el piloto puede usar SQL como almacenamiento duradero del alojamiento efímero.
public sealed class PilotKeyRepository(IConfiguration configuration) : IXmlRepository
{
    SqlConnection Open()
    {
        for(var attempt=0;;attempt++)
        {
            var c=new SqlConnection(configuration.GetConnectionString("Sql"));
            try { c.Open(); return c; }
            catch(SqlException e) when(e.Number==40613&&attempt<5)
            { c.Dispose(); Thread.Sleep(TimeSpan.FromSeconds(Math.Min(16,2*(1<<attempt)))); }
            catch { c.Dispose(); throw; }
        }
    }
    public IReadOnlyCollection<XElement> GetAllElements()
    {
        using var c=Open();
        return c.Query<string>("SELECT contenido FROM mes.CLAVE_PROTECCION_PILOTO").Select(XElement.Parse).ToArray();
    }
    public void StoreElement(XElement element,string friendlyName)
    {
        using var c=Open();
        c.Execute("mes.GuardarClaveProteccionPiloto",new{nombre=friendlyName,contenido=element.ToString(SaveOptions.DisableFormatting)},commandType:System.Data.CommandType.StoredProcedure);
    }
}

public sealed partial class Store
{
    bool AlmacenamientoSql => configuration.GetValue<bool>("Pilot:Enabled")&&configuration.GetValue<bool>("Pilot:SqlPersistence");
    sealed record ImagenSql(byte[] Contenido,string Tipo,long Version);
    Dictionary<string,long> VersionesImagenesSql(IEnumerable<string> partes)
    {
        using var c=Open().GetAwaiter().GetResult();
        var rows=c.Query<(string Parte,long Version)>("SELECT numero_parte Parte,version Version FROM mes.IMAGEN_PILOTO WHERE numero_parte IN @partes",new{partes=partes.ToArray()});
        return rows.ToDictionary(r=>r.Parte,r=>r.Version,StringComparer.Ordinal);
    }
}
