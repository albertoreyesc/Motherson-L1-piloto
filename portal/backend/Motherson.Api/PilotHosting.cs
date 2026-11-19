namespace Motherson;

public static class PilotHosting
{
    public static void Validate(IConfiguration c,IHostEnvironment env)
    {
        if(!c.GetValue<bool>("Pilot:Enabled")) return;
        if(!c.GetValue<bool>("MVP_SQL_TRANSACCIONAL")) throw new InvalidOperationException("El piloto requiere operaciones transaccionales.");
        if(env.IsDevelopment()) return; // Verificación local aislada; no es el perfil publicado.
        AzurePreflight.Validate(c);
        if(c.GetValue<bool>("PowerBI:AllowTrialTokens")) throw new InvalidOperationException("No se admiten tokens de evaluación en el piloto publicado.");
        if(!string.IsNullOrEmpty(c["MVP_ADMIN_PUESTOS_HASH"])) throw new InvalidOperationException("El piloto requiere cuentas personales, sin admin.local compartido.");
        foreach(var key in new[]{"MVP_ROOT","MVP_KEYS_PATH","MVP_IMAGES_PATH"})
        {
            var path=c[key];
            if(string.IsNullOrWhiteSpace(path)||!Path.IsPathFullyQualified(path)) throw new InvalidOperationException("Falta una ruta persistente absoluta del piloto.");
            var full=Path.GetFullPath(path);var publicRoot=Path.GetFullPath(Path.Combine(env.ContentRootPath,"wwwroot"));
            if(full.Equals(publicRoot,StringComparison.OrdinalIgnoreCase)||full.StartsWith(publicRoot+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Los archivos privados no pueden quedar en wwwroot.");
        }
    }
}
