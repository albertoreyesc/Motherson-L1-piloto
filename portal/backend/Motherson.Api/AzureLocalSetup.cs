using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.Data.SqlClient;

namespace Motherson;

public static class AzureLocalSetup
{
    // Generación local. No conecta a Azure ni crea usuarios en la base.
    public static int Run(string root)
    {
        var folder=Path.Combine(Path.GetFullPath(root),".local");Directory.CreateDirectory(folder);
        if(!OperatingSystem.IsWindows())File.SetUnixFileMode(folder,UnixFileMode.UserRead|UnixFileMode.UserWrite|UnixFileMode.UserExecute);
        var names=new[]{"conexiones-compartida.json","admin-puestos-Motherson_L1.json","accesos-iniciales-azure.json","usuarios-azure-privado.sql"};
        if(names.Any(n=>File.Exists(Path.Combine(folder,n))))throw new InvalidOperationException("Ya existe configuración privada; no se sobrescribió.");
        string Password()=>"Aa1!"+Convert.ToBase64String(RandomNumberGenerator.GetBytes(30));
        var app=Password();var reader=Password();var admin=Password();
        var builder=new SqlConnectionStringBuilder{DataSource="tcp:"+AzurePreflight.Server+",1433",InitialCatalog=AzurePreflight.Database,UserID="portal_app",Password=app,Encrypt=SqlConnectionEncryptOption.Mandatory,TrustServerCertificate=false,ConnectTimeout=90};
        void Save(string name,string text)
        {
            var options=new FileStreamOptions{Mode=FileMode.CreateNew,Access=FileAccess.Write};
            if(!OperatingSystem.IsWindows())options.UnixCreateMode=UnixFileMode.UserRead|UnixFileMode.UserWrite;
            using var file=new FileStream(Path.Combine(folder,name),options);using var writer=new StreamWriter(file);writer.Write(text);
        }
        Save(names[0],JsonSerializer.Serialize(new{app=builder.ConnectionString}));
        Save(names[1],JsonSerializer.Serialize(new{usuario="admin.local",hash=new PasswordHasher<string>().HashPassword("",admin)}));
        Save(names[2],JsonSerializer.Serialize(new{estado="PENDIENTE_DE_APLICAR_EN_AZURE",portal=new{usuario="admin.local",password=admin},sqlPortal=new{usuario="portal_app",password=app},powerBi=new{usuario="powerbi_lector",password=reader}}));
        Save(names[3],$"""
            -- PRIVADO. No subir a GitHub ni incluir en entregables públicos.
            SET XACT_ABORT ON;
            IF DB_NAME()<>'Motherson_L1' THROW 51100,'Destino incorrecto.',1;
            IF USER_ID('portal_app') IS NOT NULL OR USER_ID('powerbi_lector') IS NOT NULL
              THROW 51102,'Las cuentas ya existen; no se cambió ninguna contraseña.',1;
            BEGIN TRANSACTION;
            CREATE USER portal_app WITH PASSWORD=N'{app}';
            CREATE USER powerbi_lector WITH PASSWORD=N'{reader}';
            COMMIT;
            """);
        Console.WriteLine("Configuración privada generada localmente en .local; Azure todavía no fue modificado.");return 0;
    }
}
