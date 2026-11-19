using Dapper;
namespace Motherson;
public static class SqlPreflight {public static async Task<int> Run(Store store){try{await using var c=await store.Open();int version=await c.QuerySingleAsync<int>("SELECT MAX(Version) FROM SchemaVersion");Console.WriteLine("SQL disponible; esquema "+version+"; consulta sin escrituras");return version==21?0:1;}catch{Console.Error.WriteLine("No se pudo confirmar la conexión o el esquema normalizado. No se muestran credenciales.");return 1;}}}
