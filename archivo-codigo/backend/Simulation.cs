using System.Diagnostics;
using System.Text.Json;
namespace Motherson;

public record SimulationConfig(string Scenario="A",int Seed=42,string MaterialKey="",int Events=24,decimal Rate=35,decimal JitterMin=15,decimal JitterMax=30,decimal MicroProbability=.05m,long MinimumPieces=15,decimal DelayMinMinutes=2,decimal DelayMaxMinutes=6,string? StartLocal=null,bool AutoReplenishment=true,decimal Speed=60);
public record SimulationControl(string Action,decimal? Speed=null);
public record SimulationEntry(string Key,int Boxes,string ExpectedVersion);
public record QuickEntry(int MaterialId,int UbicacionId,int Cajas,string ExpectedVersion,int? AsignacionId=null);
public record StopInput(string Key,string Cause,bool Close=false);
public record ResponsibilityInput(int UsuarioId,int PuestoId);
public record CauseInput(string Codigo,string Nombre,bool Activa=true);
public static class SimulationRules
{
    public static string Shift(DateTime utc)
    {
        var local=TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utc,DateTimeKind.Utc),ShiftRules.Zone("America/Mexico_City"));
        var m=local.Hour*60+local.Minute;return m>=360&&m<900?"PFEP T1":m>=900&&m<1410?"PFEP T2":"Fuera de turno";
    }
    public static void Version(string expected,byte[] current)
    {
        byte[] value;try{value=expected.StartsWith("0x",StringComparison.OrdinalIgnoreCase)?Convert.FromHexString(expected[2..]):Convert.FromBase64String(expected);}catch{throw new RuleException("Versión inválida.",400);}
        if(!value.SequenceEqual(current))throw new RuleException("El saldo cambió; actualiza antes de confirmar.");
    }
    public static void Speed(decimal value){if(value<.1m||value>86400)throw new RuleException("Velocidad entre 0.1 y 86400.",400);}
}
public sealed class PythonSimulation(IConfiguration config)
{
    public static readonly JsonSerializerOptions Json=new(JsonSerializerDefaults.Web);
    public async Task<JsonElement> Execute(object input)
    {
        var bundled=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),".cache/codex-runtimes/codex-primary-runtime/dependencies/python/bin/python3");
        var psi=new ProcessStartInfo(config["SimulationPython"]??(File.Exists(bundled)?bundled:"python3")){UseShellExecute=false,RedirectStandardInput=true,RedirectStandardOutput=true,RedirectStandardError=true,CreateNoWindow=true};
        psi.ArgumentList.Add(Path.Combine(Bootstrap.Root(),"simulation/engine.py"));
        using var p=Process.Start(psi)??throw new RuleException("No fue posible iniciar Python.",503);
        var output=p.StandardOutput.ReadToEndAsync();var errors=p.StandardError.ReadToEndAsync();
        await p.StandardInput.WriteAsync(JsonSerializer.Serialize(input,Json));p.StandardInput.Close();
        using var timeout=new CancellationTokenSource(TimeSpan.FromSeconds(12));
        try{await p.WaitForExitAsync(timeout.Token);}catch{p.Kill(true);throw new RuleException("El motor Python excedió el plazo.",503);}
        if(p.ExitCode!=0){await errors;throw new RuleException("Configuración rechazada por el motor Python. Revisa ritmo, variación, periodo y material.",400);}
        using var doc=JsonDocument.Parse(await output);return doc.RootElement.Clone();
    }
}
