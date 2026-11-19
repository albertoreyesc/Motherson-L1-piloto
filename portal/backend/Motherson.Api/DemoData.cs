using System.Data;
using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json;
using Dapper;
using Microsoft.Data.SqlClient;
using Microsoft.VisualBasic.FileIO;

namespace Motherson;

public static class DemoData
{
    static readonly string[] Order = ["ESTACION", "COLOR", "PUESTO_LOGISTICO", "TURNO", "ESTANTERIA", "MATERIAL", "UBICACION", "REGISTRO"];
    static readonly Dictionary<string, string[]> Columns = new() {
        ["ESTACION"]=["id_estacion","id_linea","nombre_estacion"], ["COLOR"]=["id_color","nombre"],
        ["PUESTO_LOGISTICO"]=["id_puesto","Nombre","Tipo","usuario","password"],
        ["TURNO"]=["id_turno","nombre_turno","hora_inicio","hora_fin"],
        ["ESTANTERIA"]=["id_estanteria","id_estacion","estanteria","nivel","cajas_estanteria"],
        ["MATERIAL"]=["numero_parte","id_puesto","id_color","descripcion","uso","cantidad_por_caja","stock","minimo"],
        ["UBICACION"]=["id_ubicacion","numero_parte","id_estanteria","cantidad"],
        ["REGISTRO"]=["id_registro","id_puesto","id_ubicacion","id_turno","fecha_hora_inicio","fecha_hora_fin"] };
    static Type TypeOf(string name) => name.StartsWith("fecha_hora_") ? typeof(DateTime)
        : name.StartsWith("hora_") ? typeof(TimeSpan)
        : name.StartsWith("id_") || name is "cantidad_por_caja" or "stock" or "minimo" or "cantidad" or "cajas_estanteria" ? typeof(int) : typeof(string);

    public static Dictionary<string, DataTable> Read(string folder)
    {
        var manifest = JsonSerializer.Deserialize<Dictionary<string,string>>(File.ReadAllText(Path.Combine(folder,"sha256.json")))!;
        var tables = new Dictionary<string, DataTable>();
        foreach (var name in Order)
        {
            var file = Path.Combine(folder, "datos", name + ".csv");
            var hash = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(file)));
            if (!manifest.TryGetValue(name,out var expected) || !hash.Equals(expected,StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("CSV modificado o no acreditado: " + name);
            using var parser = new TextFieldParser(file) { TextFieldType=FieldType.Delimited, HasFieldsEnclosedInQuotes=true, TrimWhiteSpace=false };
            parser.SetDelimiters(",");
            var headers = parser.ReadFields() ?? [];
            if (!headers.SequenceEqual(Columns[name])) throw new InvalidDataException("Columnas incorrectas en " + name);
            var data = new DataTable(name);
            foreach (var header in headers) data.Columns.Add(header, TypeOf(header));
            while (!parser.EndOfData)
            {
                var fields = parser.ReadFields()!;
                if (fields.Length!=headers.Length) throw new InvalidDataException("Fila incompleta en " + name);
                var values = fields.Select((text,index) => text=="" ? DBNull.Value : TypeOf(headers[index])==typeof(int) ? (object)int.Parse(text,CultureInfo.InvariantCulture)
                    : TypeOf(headers[index])==typeof(DateTime) ? DateTime.ParseExact(text,"yyyy-MM-dd HH:mm:ss",CultureInfo.InvariantCulture)
                    : TypeOf(headers[index])==typeof(TimeSpan) ? TimeSpan.Parse(text,CultureInfo.InvariantCulture) : text).ToArray();
                data.Rows.Add(values);
            }
            tables.Add(name,data);
        }
        if(tables["PUESTO_LOGISTICO"].Rows.Cast<DataRow>().Any(r=>r["usuario"]!=DBNull.Value||r["password"]!=DBNull.Value))
            throw new InvalidDataException("La carga de demostración no admite credenciales.");
        return tables;
    }

    public static async Task Load(SqlConnection sql, string folder)
    {
        var tables = Read(folder);
        await using var tx = (SqlTransaction)await sql.BeginTransactionAsync(IsolationLevel.Serializable);
        var locked = await sql.ExecuteScalarAsync<int>("DECLARE @r int;EXEC @r=sys.sp_getapplock @Resource='mes_inventario_v1',@LockMode='Exclusive',@LockOwner='Transaction',@LockTimeout=10000;SELECT @r",transaction:tx);
        if(locked<0) throw new InvalidOperationException("Inventario ocupado.");
        if(await sql.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM mes.BASE_INVENTARIO",transaction:tx)!=0 ||
           await sql.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM mes.OPERACION",transaction:tx)!=0 ||
           !await sql.ExecuteScalarAsync<bool>("SELECT es_simulado FROM mes.CONFIGURACION WHERE id=1",transaction:tx))
            throw new InvalidOperationException("La base no es una demostración vacía pendiente de apertura.");
        foreach(var name in Order)
            if(await sql.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM dbo."+name,transaction:tx)!=0)
                throw new InvalidOperationException("La tabla ya contiene datos: "+name);
        foreach(var name in Order)
        {
            using var bulk = new SqlBulkCopy(sql,SqlBulkCopyOptions.KeepIdentity|SqlBulkCopyOptions.CheckConstraints,tx) {
                DestinationTableName="dbo."+name, BatchSize=2000, BulkCopyTimeout=120 };
            foreach(DataColumn column in tables[name].Columns) bulk.ColumnMappings.Add(column.ColumnName,column.ColumnName);
            await bulk.WriteToServerAsync(tables[name]);
            var count=await sql.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM dbo."+name,transaction:tx);
            if(count!=tables[name].Rows.Count) throw new InvalidDataException("Conteo inesperado en "+name);
        }
        await sql.ExecuteAsync("mes.FijarSaldoInicial",transaction:tx,commandType:CommandType.StoredProcedure);
        await tx.CommitAsync();
    }

    public static async Task<int> Run(string[] args,IConfiguration configuration)
    {
        var index=Array.IndexOf(args,"--cargar-demo");
        if(index+1>=args.Length) throw new ArgumentException("Indica la carpeta de los CSV.");
        var folder=Path.GetFullPath(args[index+1]);var tables=Read(folder);
        Console.WriteLine(JsonSerializer.Serialize(new {datos="SIMULADOS", tablas=tables.ToDictionary(t=>t.Key,t=>t.Value.Rows.Count),aplicar=args.Contains("--aplicar")}));
        if(!args.Contains("--aplicar")) return 0;
        var target=AzurePreflight.Validate(configuration);
        await using var sql=new SqlConnection(target.ConnectionString);await sql.OpenAsync();await Load(sql,folder);
        Console.WriteLine("Carga de demostración y apertura confirmadas.");return 0;
    }
}
