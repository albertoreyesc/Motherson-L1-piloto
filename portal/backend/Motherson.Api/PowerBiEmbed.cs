using Azure.Core;
using Azure.Identity;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
namespace Motherson;

// App-owns-data: el secreto de la aplicación nunca sale del servidor.
public sealed class PowerBiEmbed(IConfiguration configuration,HttpClient http,TokenCredential? credential=null)
{
    public static bool Configurado(IConfiguration c) => c["PowerBI:Mode"]=="AppOwnsData"
        && (c.GetValue<bool>("PowerBI:CapacityReady")
            || (c["ASPNETCORE_ENVIRONMENT"]=="Development" && c.GetValue<bool>("PowerBI:AllowTrialTokens")))
        && new[]{"TenantId","ClientId","WorkspaceId","ReportId"}.All(k=>Guid.TryParse(c["PowerBI:"+k],out var id)&&id!=Guid.Empty)
        && !string.IsNullOrWhiteSpace(c["PowerBI:ClientSecret"]);
    public static bool EmbedUrlValida(string? url) => Uri.TryCreate(url,UriKind.Absolute,out var u)
        && u.Scheme=="https" && u.Host=="app.powerbi.com" && u.IsDefaultPort
        && string.IsNullOrEmpty(u.UserInfo) && u.AbsolutePath.Equals("/reportEmbed",StringComparison.OrdinalIgnoreCase);
    public async Task<object> Generar(CancellationToken ct)
    {
        if(!Configurado(configuration))throw new RuleException("La publicación privada de Power BI todavía no está configurada.",503);
        try
        {
            var auth=credential??new ClientSecretCredential(configuration["PowerBI:TenantId"],configuration["PowerBI:ClientId"],configuration["PowerBI:ClientSecret"]);
            var token=await auth.GetTokenAsync(new TokenRequestContext(["https://analysis.windows.net/powerbi/api/.default"]),ct);
            var workspace=Guid.Parse(configuration["PowerBI:WorkspaceId"]!).ToString();var report=Guid.Parse(configuration["PowerBI:ReportId"]!).ToString();
            using var get=new HttpRequestMessage(HttpMethod.Get,$"https://api.powerbi.com/v1.0/myorg/groups/{workspace}/reports/{report}");get.Headers.Authorization=new AuthenticationHeaderValue("Bearer",token.Token);
            using var response=await http.SendAsync(get,ct);
            if(!response.IsSuccessStatusCode)throw new RuleException("Power BI no permitió consultar el informe configurado.",503);
            using var info=JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));var r=info.RootElement;
            if(!Guid.TryParse(r.GetProperty("id").GetString(),out var found)||found.ToString()!=report
                || !Guid.TryParse(r.GetProperty("datasetId").GetString(),out var dataset)
                || !EmbedUrlValida(r.GetProperty("embedUrl").GetString()))throw new RuleException("El informe de Power BI no tiene un contrato compatible.",503);
            using var request=new HttpRequestMessage(HttpMethod.Post,"https://api.powerbi.com/v1.0/myorg/GenerateToken");request.Headers.Authorization=new AuthenticationHeaderValue("Bearer",token.Token);
            request.Content=JsonContent.Create(new{reports=new[]{new{id=report,allowEdit=false}},datasets=new[]{new{id=dataset.ToString()}},lifetimeInMinutes=30});
            using var result=await http.SendAsync(request,ct);
            if(!result.IsSuccessStatusCode)throw new RuleException("Power BI no permitió generar el acceso privado al informe.",503);
            using var json=JsonDocument.Parse(await result.Content.ReadAsStringAsync(ct));var value=json.RootElement;
            var embedToken=value.GetProperty("token").GetString();var expiration=value.GetProperty("expiration").GetDateTimeOffset();
            if(string.IsNullOrWhiteSpace(embedToken)||expiration<=DateTimeOffset.UtcNow.AddSeconds(30))throw new RuleException("Power BI devolvió un acceso vencido.",503);
            return new{reportId=report,embedUrl=r.GetProperty("embedUrl").GetString(),accessToken=embedToken,expiration};
        }
        catch(Azure.Identity.AuthenticationFailedException){throw new RuleException("Revisa la identidad de aplicación de Power BI.",503);}
        catch(HttpRequestException){throw new RuleException("Power BI no está disponible en este momento.",503);}
    }
}
