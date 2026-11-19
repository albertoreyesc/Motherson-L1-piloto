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
