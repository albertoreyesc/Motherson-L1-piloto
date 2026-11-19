using System.Data;
using System.Diagnostics;
using System.Text.Json;
using Dapper;
using Microsoft.Data.SqlClient;
namespace Motherson;

public sealed class CentralHistory(Store store,IConfiguration config)
{
    public const string MasterName="Histórico de Línea 1 · seis puestos y responsables";
    public const string MasterId="4d577245a22d";
    internal static readonly JsonSerializerOptions Json=new(JsonSerializerDefaults.Web);
    readonly SemaphoreSlim projectionLock=new(1);
    long projectionVersion=-1;
    string Projection=>config["CentralProjectionPath"]??Path.Combine(Bootstrap.Root(),".local","central","portal.sqlite");
    static string Python(IConfiguration config)
    {
        var bundled=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),".cache/codex-runtimes/codex-primary-runtime/dependencies/python/bin/python3");
        return config["SimulationPython"]??(File.Exists(bundled)?bundled:"python3");
    }
    Process Start(params string[] args)
    {
        var info=new ProcessStartInfo(Python(config)){RedirectStandardInput=true,RedirectStandardOutput=true,RedirectStandardError=true,UseShellExecute=false};
        info.Environment["PYTHONDONTWRITEBYTECODE"]="1";
        foreach(var arg in args)info.ArgumentList.Add(arg);
        return Process.Start(info)??throw new RuleException("No fue posible preparar la proyección histórica.",503);
    }
    internal static JsonElement Source()
    {
        using var doc=JsonDocument.Parse(File.ReadAllText(Path.Combine(Bootstrap.Root(),"sources/annual/registry.json")));
        var run=doc.RootElement.GetProperty("runs").EnumerateArray().Single(r=>r.GetProperty("runId").GetString()==MasterId);
        var value=System.Text.Json.Nodes.JsonNode.Parse(run.GetRawText())!.AsObject();
        foreach(var field in new[]{"directory","index"}){string path=value[field]!.GetValue<string>();if(!Path.IsPathFullyQualified(path))value[field]=Path.GetFullPath(Path.Combine(Bootstrap.Root(),path));}
        return JsonSerializer.SerializeToElement(value);
    }
    internal async Task<(JsonElement report,long version)> EnsureProjection(CancellationToken cancellation)
    {
        await projectionLock.WaitAsync(cancellation);
        try
        {
            await using var c=await store.Open();await using var t=(SqlTransaction)await c.BeginTransactionAsync(IsolationLevel.Serializable,cancellation);
            await c.ExecuteAsync("DECLARE @n int;EXEC @n=sp_getapplock @Resource='HistCentral',@LockMode='Shared',@LockOwner='Transaction',@LockTimeout=30000;IF @n<0 THROW 51000,'Historico ocupado',1",transaction:t);
            var state=await c.QuerySingleOrDefaultAsync("SELECT * FROM HistEstado WHERE Id=1 AND Listo=1",transaction:t)??throw new RuleException("Importa el histórico central antes de continuar.",503);
            using var reportDoc=JsonDocument.Parse((string)state.ReportJson);var report=reportDoc.RootElement.Clone();long version=(long)state.UltimaRevision;
            var sidecar=Path.ChangeExtension(Projection,".projection.json");
            if(projectionVersion<0&&File.Exists(sidecar)&&File.Exists(Projection))
            {
                try{using var ready=JsonDocument.Parse(await File.ReadAllTextAsync(sidecar,cancellation));var r=ready.RootElement;if(r.GetProperty("signature").GetString()==(string)state.Signature&&r.GetProperty("schema").GetInt32()==6&&r.GetProperty("version").GetInt64()<=version)projectionVersion=r.GetProperty("version").GetInt64();}catch(JsonException){}catch(KeyNotFoundException){}
            }
            if(projectionVersion!=version||!File.Exists(Projection))
            {
                bool reset=projectionVersion<0||!File.Exists(Projection);var source=Source();
                using var process=Start(Path.Combine(Bootstrap.Root(),"simulation/central_projection.py"),"project");
                var stdout=process.StandardOutput.ReadToEndAsync(cancellation);var stderr=process.StandardError.ReadToEndAsync(cancellation);
                await process.StandardInput.WriteLineAsync(JsonSerializer.Serialize(new{path=Projection,reset,schemaIndex=source.GetProperty("index").GetString(),signature=(string)state.Signature,config=report.GetProperty("config"),version,baseEvents=708948},Json));
                if(reset)
                {
                    foreach(var table in NormalizedJournal.Fields.Keys)
                     await foreach(var row in NormalizedJournal.Read(c,t,table,baseOnly:true,cancel:cancellation))await process.StandardInput.WriteLineAsync(JsonSerializer.Serialize(new{table=row.Table,key=row.Key,json=row.Row.ToJsonString()},Json));
                }
                foreach(var table in NormalizedJournal.Fields.Keys)
                 await foreach(var row in NormalizedJournal.Read(c,t,table,baseOnly:false,after:reset?0:projectionVersion,through:version,cancel:cancellation))await process.StandardInput.WriteLineAsync(JsonSerializer.Serialize(new{table=row.Table,key=row.Key,json=row.Row.ToJsonString()},Json));
                process.StandardInput.Close();await process.WaitForExitAsync(cancellation);await stdout;var error=await stderr;
                if(process.ExitCode!=0)throw new RuleException("No se pudo reconstruir la proyección central: "+error[..Math.Min(250,error.Length)],503);
                projectionVersion=version;
            }
            await t.CommitAsync(cancellation);return(report,version);
        }
        finally{projectionLock.Release();}
    }
    public async Task<JsonElement> Read(Actor actor,IQueryCollection? query,CancellationToken cancellation,bool metadata=false)
    {
        var (report,version)=await EnsureProjection(cancellation);var access=await store.CentralAccess(actor);
        var parameters=query?.Where(p=>new[]{"section","date","time","shift","material","post","responsible","from","status","kind","offset","limit"}.Contains(p.Key)).ToDictionary(p=>p.Key,p=>p.Value.ToString());
        if(parameters?.Values.Any(v=>v.Length>100)==true)throw new RuleException("Filtro demasiado largo.",400);
        var source=Source();
        using var process=Start(Path.Combine(Bootstrap.Root(),"simulation/annual_reader.py"));
        var stdout=process.StandardOutput.ReadToEndAsync(cancellation);var stderr=process.StandardError.ReadToEndAsync(cancellation);
        await process.StandardInput.WriteAsync(JsonSerializer.Serialize(new{operation=metadata?"meta":"view",directory=source.GetProperty("directory").GetString(),index=Projection,report,central=true,version,access.wide,access.posts,access.keys,query=parameters??new()},Json));process.StandardInput.Close();
        await process.WaitForExitAsync(cancellation);var raw=await stdout;await stderr;
        if(process.ExitCode!=0)throw new RuleException("La consulta central no está disponible.",503);
        using var parsed=JsonDocument.Parse(raw);var result=parsed.RootElement.Clone();if(result.TryGetProperty("error",out var e))throw new RuleException(e.GetString()??"Consulta inválida.",result.GetProperty("status").GetInt32());return result;
    }
}
