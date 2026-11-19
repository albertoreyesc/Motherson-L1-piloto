using Microsoft.Extensions.Configuration;
using Motherson;

// Pruebas sin SQL: impiden apuntar accidentalmente a la base del otro equipo
// o desactivar la validación del certificado en el perfil nuevo.
var passed = 0;
IConfiguration Config(string? connection, string database = "Motherson_L1") =>
    new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> {
        ["ConnectionStrings:Sql"] = connection, ["MVP_BASE_COMPARTIDA"] = database }).Build();
string Connection(string server = AzurePreflight.Server, string database = AzurePreflight.Database,
    string encrypt = "True", string trust = "False") =>
    $"Server={server};Database={database};Encrypt={encrypt};TrustServerCertificate={trust}";
void Accept(IConfiguration config) { AzurePreflight.Validate(config); passed++; }
void Reject(IConfiguration config)
{
    try { AzurePreflight.Validate(config); }
    catch (Exception error) when (error is InvalidOperationException or ArgumentException) { passed++; return; }
    throw new Exception("Se aceptó una conexión que debía rechazarse.");
}
Accept(Config(Connection()));
Accept(Config(Connection("tcp:" + AzurePreflight.Server + ",1433")));
Accept(Config(Connection(encrypt: "Strict")));
Reject(Config(null));
Reject(Config(Connection("mothersonbdd.database.windows.net", "db")));
Reject(Config(Connection(database: "master")));
Reject(Config(Connection(), "db"));
Reject(Config(Connection(encrypt: "False")));
Reject(Config(Connection(trust: "True")));
Reject(Config(Connection(AzurePreflight.Server + ".example.com")));
Reject(Config(Connection(AzurePreflight.Server + ",1444")));
Console.WriteLine($"{passed} comprobaciones de destino y cifrado aprobadas; sin conexión SQL.");

void Check(bool ok,string name){if(!ok)throw new Exception(name);passed++;}
Check(PowerBiEmbed.EmbedUrlValida("https://app.powerbi.com/reportEmbed?reportId=00000000-0000-0000-0000-000000000001"),"URL segura rechazada");
foreach(var url in new[]{"https://app.powerbi.com/view?r=publico","http://app.powerbi.com/reportEmbed","https://app.powerbi.com.ejemplo.org/reportEmbed","https://evil@ app.powerbi.com/reportEmbed","https://app.powerbi.com:444/reportEmbed"})Check(!PowerBiEmbed.EmbedUrlValida(url),"URL insegura aceptada");
var settings=new Dictionary<string,string?>{["PowerBI:Mode"]="AppOwnsData",["PowerBI:CapacityReady"]="true",["PowerBI:TenantId"]="00000000-0000-0000-0000-000000000001",["PowerBI:ClientId"]="00000000-0000-0000-0000-000000000002",["PowerBI:WorkspaceId"]="00000000-0000-0000-0000-000000000003",["PowerBI:ReportId"]="00000000-0000-0000-0000-000000000004",["PowerBI:ClientSecret"]="SECRETO_SERVIDOR_PRUEBA"};
IConfiguration Options()=>new ConfigurationBuilder().AddInMemoryCollection(settings).Build();
Check(PowerBiEmbed.Configurado(Options()),"Configuración completa rechazada");
settings["PowerBI:CapacityReady"]="false";Check(!PowerBiEmbed.Configurado(Options()),"Capacidad sin acreditar aceptada");
using(var fake=new FakePowerBi())using(var http=new HttpClient(fake))
{
 try{await new PowerBiEmbed(Options(),http,new FakeCredential()).Generar(default);throw new Exception("Se emitió acceso sin configuración");}catch(RuleException e)when(e.Status==503){Check(fake.Requests==0,"Se consultó Power BI sin configuración");}
 settings["PowerBI:CapacityReady"]="true";
 var result=await new PowerBiEmbed(Options(),http,new FakeCredential()).Generar(default);var json=System.Text.Json.JsonSerializer.Serialize(result);
 Check(fake.Requests==2,"Flujo REST incompleto");Check(json.Contains("TOKEN_EMBED_PRUEBA")&&!json.Contains("TOKEN_ENTRA_PRUEBA")&&!json.Contains("SECRETO_SERVIDOR_PRUEBA"),"Respuesta expone identidad de aplicación");
 Check(fake.Limited,"Token no limitado al informe/dataset configurados");
}
Console.WriteLine($"{passed} comprobaciones locales de destino, cifrado y Power BI aprobadas. Power BI REST simulado; publicación real no probada.");

sealed class FakeCredential:Azure.Core.TokenCredential
{
 public override Azure.Core.AccessToken GetToken(Azure.Core.TokenRequestContext requestContext,CancellationToken cancellationToken)=>new("TOKEN_ENTRA_PRUEBA",DateTimeOffset.UtcNow.AddHours(1));
 public override ValueTask<Azure.Core.AccessToken> GetTokenAsync(Azure.Core.TokenRequestContext requestContext,CancellationToken cancellationToken)=>ValueTask.FromResult(GetToken(requestContext,cancellationToken));
}
sealed class FakePowerBi:HttpMessageHandler
{
 public int Requests;public bool Limited;
 protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken cancellationToken)
 {
  Requests++;if(request.Headers.Authorization?.Parameter!="TOKEN_ENTRA_PRUEBA")throw new Exception("Autenticación REST ausente");
  object value;
  if(request.Method==HttpMethod.Get)value=new{id="00000000-0000-0000-0000-000000000004",datasetId="00000000-0000-0000-0000-000000000005",embedUrl="https://app.powerbi.com/reportEmbed?reportId=00000000-0000-0000-0000-000000000004"};
  else
  {
   using var doc=System.Text.Json.JsonDocument.Parse(await request.Content!.ReadAsStringAsync(cancellationToken));var r=doc.RootElement;
   Limited=r.GetProperty("reports").GetArrayLength()==1&&!r.GetProperty("reports")[0].GetProperty("allowEdit").GetBoolean()&&r.GetProperty("datasets").GetArrayLength()==1&&r.GetProperty("datasets")[0].GetProperty("id").GetString()=="00000000-0000-0000-0000-000000000005";
   value=new{token="TOKEN_EMBED_PRUEBA",expiration=DateTimeOffset.UtcNow.AddMinutes(30)};
  }
  return new(System.Net.HttpStatusCode.OK){Content=System.Net.Http.Json.JsonContent.Create(value)};
 }
}
