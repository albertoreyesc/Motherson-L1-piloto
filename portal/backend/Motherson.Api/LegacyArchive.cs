using System.Data;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Dapper;
using Microsoft.Data.SqlClient;

namespace Motherson;

public sealed record ArchivoLegado(string Nombre,string Sha256,Func<IEnumerable<string>> Leer,Action Verificar);
public static class LegacyArchive
{
    static readonly JsonSerializerOptions Web=new(JsonSerializerDefaults.Web);
    static string Hash(string path){using var file=File.OpenRead(path);return Convert.ToHexString(SHA256.HashData(file));}
    static IEnumerable<string> Lines(string path,bool gzip)
    {
        using var file=File.OpenRead(path);using Stream input=gzip?new GZipStream(file,CompressionMode.Decompress):file;
        using var reader=new StreamReader(input);
        while(reader.ReadLine() is {} line){using var doc=JsonDocument.Parse(line);yield return line;}
    }
    public static ArchivoLegado[] Read(string folder,string[] diaries)
    {
        var output=new List<ArchivoLegado>();folder=Path.GetFullPath(folder);
        using var manifest=JsonDocument.Parse(File.ReadAllBytes(Path.Combine(folder,"manifiesto.json")));
        var m=manifest.RootElement;
        if(m.GetProperty("version").GetInt32()!=1||!m.GetProperty("corte").GetProperty("sellado").GetBoolean())throw new InvalidDataException("Manifiesto histórico no sellado.");
        var names=new HashSet<string>(StringComparer.Ordinal);
        foreach(var file in m.GetProperty("archivos").EnumerateArray())
        {
            var name=file.GetProperty("nombre").GetString()!;
            if(!Regex.IsMatch(name,@"\A[a-z0-9_-]+\.jsonl\.gz\z")||!names.Add(name))throw new InvalidDataException("Nombre de fuente inválido o repetido.");
            var path=Path.Combine(folder,name);var expected=file.GetProperty("sha256").GetString()!;
            void Verify(){if(!Hash(path).Equals(expected,StringComparison.OrdinalIgnoreCase))throw new InvalidDataException("Fuente histórica modificada: "+name);}
            Verify();output.Add(new(name,expected.ToUpperInvariant(),()=>Lines(path,true),Verify));
        }
        if(names.Count!=12||!names.Contains("movimientos.jsonl.gz"))throw new InvalidDataException("El paquete debe contener las doce fuentes originales.");
        var manifestPath=Path.Combine(folder,"manifiesto.json");var manifestHash=Hash(manifestPath);
        output.Add(new("manifiesto.json",manifestHash,()=>[File.ReadAllText(manifestPath)],()=>{if(Hash(manifestPath)!=manifestHash)throw new InvalidDataException("El manifiesto cambió.");}));
        foreach(var diary in diaries)
        {
            var path=Path.GetFullPath(diary);var hash=Hash(path);
            var first=JsonDocument.Parse(File.ReadLines(path).First());
            using(first)
            {
                var baseline=first.RootElement.GetProperty("datos").GetProperty("base").Deserialize<BaseMovimientos>(Web)!;
                var temp=Path.Combine(Path.GetTempPath(),"mes-migracion-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(temp);
                try
                {
                    File.Copy(path,Path.Combine(temp,"eventos.jsonl"));
                    using var journal=new DiarioMovimientos(temp,baseline);
                    var state=journal.Leer().GetAwaiter().GetResult();
                    var rows=state.Hechos.Select((h,i)=>{
                        var result=state.Resultados[i];var row=state.Base.Filas.Single(f=>f.id_ubicacion==h.Captura.Ubicacion);
                        return JsonSerializer.Serialize(new{
                            id_movimiento="LEGADO:"+hash+":"+h.IdOperacion,origen="LEGADO_DIARIO",espacio_identificadores="LEGADO_DIARIO",espacio_original="CONTINUACION",base_original=state.Base.BaseDatos,es_simulado=true,
                            numero_parte=h.Parte,id_puesto=h.Puesto,id_ubicacion=h.Captura.Ubicacion,tipo_movimiento=h.Captura.Tipo,
                            cantidad_piezas=result.CantidadMovimiento,cantidad_capturada=h.Captura.Cantidad,unidad_capturada=h.Unidad,
                            cantidad_por_caja_aplicada=h.PiezasPorCaja,es_consumo_produccion=h.Captura.EsConsumo,
                            saldo_anterior=result.SaldoAnterior,saldo_posterior=result.SaldoPosterior,fecha_movimiento=h.FechaUtc,
                            usuario_captura=h.Cuenta,responsable_simulado=(string?)null,id_turno=(int?)null,
                            estacion=row.estacion,estanteria=row.estanteria,nivel=row.nivel,proceso_generador="LEGADO_DIARIO_VALIDADO",sha256_archivo_original=hash});
                    }).ToArray();
                    var normalizedHash=Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(string.Join("\n",rows))));
                    output.Add(new("movimientos-diario.jsonl",normalizedHash,()=>rows,()=>{}));
                }
                finally{Directory.Delete(temp,true);}
            }
            void VerifyDiary(){if(Hash(path)!=hash)throw new InvalidDataException("El diario cambió durante la migración.");}
            output.Add(new("eventos-"+hash[..12]+".jsonl",hash,()=>Lines(path,false),VerifyDiary));
        }
        return output.ToArray();
    }
    public static Dictionary<string,int> Validate(ArchivoLegado[] files)
    {
        var rows=new Dictionary<string,int>();
        foreach(var file in files)
        {
            file.Verificar();var count=0;
            foreach(var line in file.Leer()){using var doc=JsonDocument.Parse(line);count++;}
            file.Verificar();rows[file.Nombre+":"+file.Sha256]=count;
        }
        return rows;
    }
    public static async Task<int> Import(SqlConnection sql,ArchivoLegado[] files)
    {
        var plan=Validate(files);
        files=files.GroupBy(f=>f.Sha256,StringComparer.OrdinalIgnoreCase).Select(g=>g.First()).ToArray();
        var imported=0;
        await sql.ExecuteAsync("CREATE TABLE #legado(sha256 char(64),linea int,archivo nvarchar(120),datos nvarchar(max));");
        async Task<SqlTransaction> Begin()
        {
            var tx=(SqlTransaction)await sql.BeginTransactionAsync(IsolationLevel.Serializable);
            try
            {
                var locked=await sql.ExecuteScalarAsync<int>("DECLARE @r int;EXEC @r=sys.sp_getapplock @Resource='mes_importacion_legado',@LockMode='Exclusive',@LockOwner='Transaction',@LockTimeout=10000;SELECT @r",transaction:tx);
                if(locked<0)throw new InvalidOperationException("Otra importación está en curso.");
                return tx;
            }
            catch{await tx.DisposeAsync();throw;}
        }
        try
        {
            foreach(var file in files)
            {
                file.Verificar();var expected=plan[file.Nombre+":"+file.Sha256];
                await using(var tx=await Begin())
                {
                    await sql.ExecuteAsync("IF NOT EXISTS(SELECT 1 FROM mes.ARCHIVO_LEGADO WHERE sha256=@hash) INSERT mes.ARCHIVO_LEGADO(sha256,archivo,filas_esperadas) VALUES(@hash,@name,@count)",new{hash=file.Sha256,name=file.Nombre,count=expected},tx);
                    var meta=await sql.QuerySingleAsync("SELECT completo,filas_esperadas FROM mes.ARCHIVO_LEGADO WHERE sha256=@hash",new{hash=file.Sha256},tx);
                    if((int)meta.filas_esperadas!=expected)throw new InvalidDataException("El conteo del archivo no coincide con su importación previa.");
                    await tx.CommitAsync();
                    if((bool)meta.completo)continue;
                }
                var table=new DataTable();table.Columns.Add("sha256",typeof(string));table.Columns.Add("linea",typeof(int));table.Columns.Add("archivo",typeof(string));table.Columns.Add("datos",typeof(string));
                async Task WriteBatch()
                {
                    await using var tx=await Begin();
                    await sql.ExecuteAsync("TRUNCATE TABLE #legado",transaction:tx);
                    using var bulk=new SqlBulkCopy(sql,SqlBulkCopyOptions.Default,tx){DestinationTableName="#legado",BatchSize=1000,BulkCopyTimeout=120};
                    foreach(DataColumn c in table.Columns)bulk.ColumnMappings.Add(c.ColumnName,c.ColumnName);
                    await bulk.WriteToServerAsync(table);
                    await sql.ExecuteAsync("IF EXISTS(SELECT 1 FROM #legado s JOIN mes.HISTORICO_LEGADO d ON d.sha256=s.sha256 AND d.linea=s.linea WHERE DATALENGTH(d.datos)<>DATALENGTH(s.datos) OR d.datos COLLATE Latin1_General_100_BIN2<>s.datos COLLATE Latin1_General_100_BIN2) THROW 51009,'La evidencia previa no coincide; no se sobrescribió.',1;",transaction:tx);
                    var added=await sql.ExecuteAsync("INSERT mes.HISTORICO_LEGADO(sha256,linea,archivo,datos) SELECT s.sha256,s.linea,s.archivo,s.datos FROM #legado s WHERE NOT EXISTS(SELECT 1 FROM mes.HISTORICO_LEGADO d WHERE d.sha256=s.sha256 AND d.linea=s.linea)",transaction:tx,commandTimeout:120);
                    await tx.CommitAsync();imported+=added;table.Clear();
                }
                var line=0;
                foreach(var json in file.Leer()){table.Rows.Add(file.Sha256,++line,file.Nombre,json);if(table.Rows.Count==1000)await WriteBatch();}
                if(table.Rows.Count>0)await WriteBatch();
                file.Verificar();
                await using(var tx=await Begin())
                {
                    var actual=await sql.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM mes.HISTORICO_LEGADO WHERE sha256=@hash",new{hash=file.Sha256},tx);
                    if(actual!=expected)throw new InvalidDataException("El archivo no está completo; no se publicó en el historial.");
                    await sql.ExecuteAsync("UPDATE mes.ARCHIVO_LEGADO SET completo=1,confirmado_utc=SYSUTCDATETIME() WHERE sha256=@hash",new{hash=file.Sha256},tx);
                    await tx.CommitAsync();
                }
            }
            return imported;
        }
        finally{if(sql.State==ConnectionState.Open)await sql.ExecuteAsync("DROP TABLE IF EXISTS #legado");}
    }
    public static async Task<int> Run(string[] args,IConfiguration configuration)
    {
        var index=Array.IndexOf(args,"--migrar-legado");if(index+1>=args.Length)throw new ArgumentException("Indica la carpeta histórica.");
        var diaries=new List<string>();for(var i=0;i<args.Length-1;i++)if(args[i]=="--diario")diaries.Add(args[i+1]);
        var files=Read(args[index+1],diaries.ToArray());var plan=Validate(files);
        Console.WriteLine(JsonSerializer.Serialize(new{accion="PRESERVAR_LEGADO_SIN_REAPLICAR_SALDOS",filas=plan,aplicar=args.Contains("--aplicar")}));
        if(!args.Contains("--aplicar"))return 0;
        var target=AzurePreflight.Validate(configuration);await using var sql=new SqlConnection(target.ConnectionString);await sql.OpenAsync();
        var count=await Import(sql,files);Console.WriteLine(JsonSerializer.Serialize(new{filasNuevas=count}));return 0;
    }
}
