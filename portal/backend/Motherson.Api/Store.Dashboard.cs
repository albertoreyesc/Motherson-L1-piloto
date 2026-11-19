using System.Text.Json;

namespace Motherson;

// Incorporado del ZIP (3). Configura el informe propio y conserva el formato anterior.
// El enlace NO se guarda en el código ni en GitHub: se lee de la variable MVP_POWERBI_URL
// o del archivo privado .local/dashboard-equipo2.json con la forma {"url":"https://app.powerbi.com/..."}.
// Solo se aceptan enlaces https de Power BI, para que el portal no incruste otros sitios.
public sealed record DashboardEquipo2(bool Configurado, string? Url, string Modo="sesion_microsoft", string? Motivo=null, string? AvisoDatos=null);

public sealed partial class Store
{
    public DashboardEquipo2 DashboardEquipo2()
    {
        if(configuration["PowerBI:Mode"]=="AppOwnsData") return new(PowerBiEmbed.Configurado(configuration),null,"privado",PowerBiEmbed.Configurado(configuration)?null:"El Dashboard está pendiente de capacidad e integración de Power BI. Puedes usar el inventario, el historial y los KPIs del portal.");
        var url = configuration["MVP_POWERBI_URL"];
        if (string.IsNullOrWhiteSpace(url))
        {
            var archivo = Path.Combine(Raiz, ".local", "dashboard-equipo2.json");
            if (File.Exists(archivo))
            {
                using var json = JsonDocument.Parse(File.ReadAllText(archivo));
                url = json.RootElement.TryGetProperty("url", out var valor) ? valor.GetString() : null;
            }
        }
        if (string.IsNullOrWhiteSpace(url)) return new(false, null);
        if (!EsEnlacePowerBi(url))
            throw new RuleException("El enlace del dashboard debe ser un enlace https de Power BI (app.powerbi.com).", 503);
        return new(true, url.Trim(), AvisoDatos: configuration["PowerBI:DataNotice"]);
    }

    public static bool EsEnlacePowerBi(string url) => PowerBiEmbed.EmbedUrlValida(url.Trim());
}
