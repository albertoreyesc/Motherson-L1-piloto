using System.Security.Claims;
using System.Threading.RateLimiting;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.DataProtection.KeyManagement;
using Microsoft.AspNetCore.HttpOverrides;
using System.Security.Cryptography.X509Certificates;
using Microsoft.Data.SqlClient;
using Motherson;
if(args.Contains("--migrar")){await Migration.Run(args);return;}
if(args.Contains("--verificar-sql")){await Migration.Verify();return;}
if(args.Contains("--integridad")){await Migration.Integrity();return;}
if(args.Contains("--sembrar-pruebas")){await Migration.SeedTests();return;}
if(args.Contains("--probar-permisos")){await Migration.TestPermissions();return;}
if(args.Contains("--preparar-accesos-locales")){var i=Array.IndexOf(args,"--preparar-accesos-locales");if(i+1>=args.Length)throw new ArgumentException("Indica la carpeta del portal.");Environment.ExitCode=AzureLocalSetup.Run(args[i+1]);return;}
var builder=WebApplication.CreateBuilder(args);
if(args.Contains("--piloto-inicializar")||args.Contains("--piloto-usuario")||args.Contains("--piloto-permisos")||args.Contains("--piloto-almacenamiento")||args.Contains("--piloto-revocar"))
{
    try{if(args.Contains("--piloto-inicializar"))await PilotSetup.Initialize(args,builder.Configuration);else if(args.Contains("--piloto-permisos"))await PilotSetup.Permissions(args,builder.Configuration);else if(args.Contains("--piloto-almacenamiento"))await PilotSetup.Storage(args,builder.Configuration);else if(args.Contains("--piloto-revocar"))await PilotSetup.Revoke(builder.Configuration);else await PilotSetup.User(builder.Configuration);}
    catch(Exception ex){Console.Error.WriteLine("Aprovisionamiento piloto detenido. Tipo: "+ex.GetType().Name+(ex is SqlException se?"; SQL "+se.Number:"")+". Revisa el destino y la configuración privada; no se muestran credenciales.");Environment.ExitCode=2;}
    return;
}
if(args.Contains("--administracion-instalar")||args.Contains("--administracion-activar")){
 var flag=args.Contains("--administracion-activar")?"--administracion-activar":"--administracion-instalar";var at=Array.IndexOf(args,flag);
 if(at+1>=args.Length)throw new ArgumentException("Indica raíz del proyecto.");
 await AdminV2Migration.Ejecutar(builder.Configuration,args[at+1],flag=="--administracion-activar");return;
}
PilotHosting.Validate(builder.Configuration,builder.Environment);
if(args.Contains("--migrar-legado")){Environment.ExitCode=await LegacyArchive.Run(args,builder.Configuration);return;}
if(args.Contains("--cargar-demo")){Environment.ExitCode=await DemoData.Run(args,builder.Configuration);return;}
if(args.Contains("--diagnostico-azure")){Environment.ExitCode=await AzurePreflight.Run(builder.Configuration,args.Contains("--solo-configuracion"));return;}
if(!string.IsNullOrWhiteSpace(builder.Configuration["MVP_AZURE_SERVER"])) AzurePreflight.Validate(builder.Configuration);
var database=new SqlConnectionStringBuilder(builder.Configuration.GetConnectionString("Sql")??throw new InvalidOperationException("Falta conexión SQL del MVP.")).InitialCatalog;
if(!Store.BaseAutorizada(database,builder.Configuration))throw new InvalidOperationException("Base MVP no autorizada.");
var cookiePrefix="Motherson.L1.MVP.AccesoPuestoV1."+database;
var protection=builder.Services.AddDataProtection().SetApplicationName(cookiePrefix);
if(builder.Configuration.GetValue<bool>("Pilot:SqlPersistence")&&builder.Configuration.GetValue<bool>("Pilot:Enabled"))
{
    builder.Services.Configure<KeyManagementOptions>(o=>o.XmlRepository=new PilotKeyRepository(builder.Configuration));
    var cert=X509CertificateLoader.LoadPkcs12(Convert.FromBase64String(builder.Configuration["Pilot:KeyEncryptionCertificate"]!),builder.Configuration["Pilot:KeyEncryptionPassword"],OperatingSystem.IsMacOS()?X509KeyStorageFlags.DefaultKeySet:X509KeyStorageFlags.EphemeralKeySet);
    if(!cert.HasPrivateKey||cert.NotAfter.ToUniversalTime()<=DateTime.UtcNow)throw new InvalidOperationException("El certificado de protección debe estar vigente y contener su clave privada.");
    protection.ProtectKeysWithCertificate(cert);
}
else
{
    var keys=Directory.CreateDirectory(builder.Configuration["MVP_KEYS_PATH"]??Path.Combine(builder.Environment.ContentRootPath,".local","keys"));
    if(!OperatingSystem.IsWindows())File.SetUnixFileMode(keys.FullName,UnixFileMode.UserRead|UnixFileMode.UserWrite|UnixFileMode.UserExecute);
    protection.PersistKeysToFileSystem(keys);
}
if(builder.Configuration.GetValue<bool>("Pilot:LoopbackProxy"))
    builder.Services.Configure<ForwardedHeadersOptions>(o=>{o.ForwardedHeaders=ForwardedHeaders.XForwardedProto;o.ForwardLimit=1;});
builder.Services.AddSingleton<Store>();
builder.Services.AddSingleton<AdminV2>();
builder.Services.AddHttpClient<PowerBiEmbed>();
builder.Services.ConfigureHttpJsonOptions(o=>o.SerializerOptions.Converters.Add(new UtcConverter()));
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme).AddCookie(o=>{o.Cookie.Name=cookiePrefix+".Session";o.Cookie.HttpOnly=true;o.Cookie.SameSite=SameSiteMode.Strict;o.Cookie.SecurePolicy=builder.Environment.IsDevelopment()?CookieSecurePolicy.SameAsRequest:CookieSecurePolicy.Always;o.ExpireTimeSpan=TimeSpan.FromMinutes(30);o.SlidingExpiration=false;o.Events.OnRedirectToLogin=c=>{c.Response.StatusCode=401;return Task.CompletedTask;};o.Events.OnRedirectToAccessDenied=c=>{c.Response.StatusCode=403;return Task.CompletedTask;};});
builder.Services.AddAuthorization();builder.Services.AddAntiforgery(o=>{o.HeaderName="X-XSRF-TOKEN";o.Cookie.Name=cookiePrefix+".Xsrf";o.Cookie.SecurePolicy=builder.Environment.IsDevelopment()?CookieSecurePolicy.SameAsRequest:CookieSecurePolicy.Always;o.Cookie.SameSite=SameSiteMode.Strict;o.Cookie.HttpOnly=true;});
builder.Services.AddRateLimiter(o=>{o.RejectionStatusCode=429;o.AddPolicy("login",c=>RateLimitPartition.GetFixedWindowLimiter(c.Connection.RemoteIpAddress?.ToString()??"local",_=>new FixedWindowRateLimiterOptions{PermitLimit=builder.Configuration.GetValue<bool>("Pilot:Enabled")?100:10,Window=TimeSpan.FromMinutes(1),QueueLimit=0}));});
var app=builder.Build();
if(builder.Configuration.GetValue<bool>("Pilot:LoopbackProxy"))app.UseForwardedHeaders();
if(!app.Environment.IsDevelopment()){app.UseHsts();app.UseHttpsRedirection();}
var store=app.Services.GetRequiredService<Store>();
await store.DetectarModelo();
await store.PrepararContinuacion();
var v2=builder.Configuration.GetValue<bool>("MVP_ADMIN_V2");
await using(var check=await store.Open()){
 var active=await Dapper.SqlMapper.ExecuteScalarAsync<int>(check,"IF OBJECT_ID('mes2.CONFIG') IS NULL SELECT 0;ELSE SELECT CONVERT(int,activo) FROM mes2.CONFIG WHERE id=1");
 if((active==1)!=v2)throw new InvalidOperationException("La activación SQL y MVP_ADMIN_V2 deben coincidir. No se permite escribir en un perfil anterior.");
}
app.Use(async(ctx,next)=>{
 if(v2&&ctx.Request.Path.StartsWithSegments("/api")&&!ctx.Request.Path.StartsWithSegments("/api/auth")&&!ctx.Request.Path.StartsWithSegments("/api/v2")&&ctx.Request.Path!="/api/administracion"&&ctx.Request.Path!="/api/health"){
  ctx.Response.StatusCode=409;await ctx.Response.WriteAsJsonAsync(new{error="Este piloto usa administración por línea. Recarga el portal actualizado."});return;
 }await next();
});
app.Use(async(ctx,next)=>{ctx.Response.Headers["X-Content-Type-Options"]="nosniff";ctx.Response.Headers["Referrer-Policy"]="no-referrer";ctx.Response.Headers["X-Frame-Options"]="DENY";ctx.Response.Headers["Content-Security-Policy"]="default-src 'self'; script-src 'self'; style-src 'self' 'unsafe-inline'; img-src 'self' data:; connect-src 'self'; frame-src https://app.powerbi.com https://*.powerbi.com; frame-ancestors 'none'";if(ctx.Request.Path.StartsWithSegments("/api"))ctx.Response.Headers.CacheControl="no-store";
 try{await next();}catch(Exception ex){var status=ex switch{RuleException r=>r.Status,AntiforgeryValidationException=>400,BadHttpRequestException=>400,SqlException s when s.Number==51001=>401,SqlException s when s.Number==51003=>403,SqlException s when s.Number is 51004 or 51010 or 51011 or 547 or 8115=>400,SqlException s when s.Number is 51009 or 51012 or 2601 or 2627 or 1205=>409,SqlException=>503,_=>500};ctx.Response.StatusCode=status;var message=ex is RuleException?ex.Message:ex is SqlException se&&se.Number>=51000&&se.Number<=51012?se.Message:status==400?"Dato o referencia inválida.":status==503?"SQL Server no está disponible.":"No se confirmó la operación.";app.Logger.LogWarning("Petición {Path}: {Status}",ctx.Request.Path,status);await ctx.Response.WriteAsJsonAsync(new{error=message});}});
app.UseAuthentication();app.UseAuthorization();app.UseRateLimiter();
app.Use(async(ctx,next)=>{if(ctx.Request.Path.StartsWithSegments("/api")&&HttpMethods.IsPost(ctx.Request.Method))await ctx.RequestServices.GetRequiredService<IAntiforgery>().ValidateRequestAsync(ctx);await next();});
app.MapGet("/health/live",()=>Results.Ok(new{status="ok"}));
app.MapGet("/api/health",async()=>{await using var c=await store.Open();return Results.Ok(new{api="ok",sql="ok",baseDatos=c.Database,simulado=true});}).RequireAuthorization();
app.MapGet("/api/auth/csrf",(HttpContext c,IAntiforgery af)=>Results.Ok(new{token=af.GetAndStoreTokens(c).RequestToken}));
app.MapPost("/api/auth/login",async(LoginInput input,HttpContext ctx)=>{var actor=await store.Login(input);if(actor is null)return Results.Json(new{error="Credenciales inválidas."},statusCode:401);await ctx.SignInAsync(new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier,actor.Id.ToString()),new Claim("tipo_cuenta",actor.TipoCuenta),new Claim("sello_acceso",actor.SelloAcceso??"")],CookieAuthenticationDefaults.AuthenticationScheme)));return Results.Ok(new{authenticated=true});}).RequireRateLimiting("login");
app.MapPost("/api/auth/logout",async Task<IResult>(HttpContext c)=>{await c.SignOutAsync();return Results.Ok();}).RequireAuthorization();
app.MapGet("/api/auth/me",async Task<IResult>(HttpContext c)=>Results.Ok(await store.Actor(int.Parse(c.User.FindFirstValue(ClaimTypes.NameIdentifier)!),c.User.FindFirstValue("tipo_cuenta")??"",c.User.FindFirstValue("sello_acceso")))).RequireAuthorization();
var api=app.MapGroup("/api").RequireAuthorization();
RouteHandlerBuilder Gate(RouteHandlerBuilder route,string permission)=>route.AddEndpointFilter(async(context,next)=>{var actor=await store.Actor(int.Parse(context.HttpContext.User.FindFirstValue(ClaimTypes.NameIdentifier)!),context.HttpContext.User.FindFirstValue("tipo_cuenta")??"",context.HttpContext.User.FindFirstValue("sello_acceso"));if(!actor.Permissions.Contains(permission))throw new RuleException("Operación no autorizada.",403);context.HttpContext.Items["actor"]=actor;return await next(context);});
Cuenta Who(HttpContext c)=>(Cuenta)c.Items["actor"]!;
Guid Key(HttpContext c)=>Guid.TryParse(c.Request.Headers["Idempotency-Key"],out var key)&&key!=Guid.Empty?key:throw new RuleException("Falta un identificador válido de operación.",400);
api.MapGet("/administracion",()=>Results.Ok(new{habilitada=v2}));
if(v2){
 var admin=app.Services.GetRequiredService<AdminV2>();var scoped=api.MapGroup("/v2/lineas/{linea:int}");
 Gate(api.MapGet("/v2/lineas",async Task<IResult>(HttpContext c)=>Results.Ok(await admin.Lineas(Who(c)))),"view");
 // Usuarios del piloto: solo ADMIN (además se valida en SQL). Ver portal/db/administracion/003_usuarios.sql.
 Gate(api.MapGet("/v2/usuarios",async Task<IResult>(HttpContext c)=>Results.Ok(await admin.Usuarios(Who(c)))),"administrar_accesos");
 Gate(api.MapPost("/v2/usuarios",async Task<IResult>(JsonObject input,HttpContext c)=>Results.Ok(await admin.UsuarioGuardar(Who(c),input))),"administrar_accesos");
 Gate(scoped.MapGet("/estado",async(int linea,DateTimeOffset? corte,HttpContext c)=>Results.Ok(await admin.Estado(Who(c),linea,corte?.UtcDateTime))),"view");
 Gate(scoped.MapPost("/importacion/vista-previa",async(int linea,JsonObject input,HttpContext c)=>Results.Ok(await admin.VistaPrevia(Who(c),linea,input))),"ajustar_catalogo");
 Gate(scoped.MapPost("/operaciones/{tipo}",async(int linea,string tipo,JsonObject input,HttpContext c)=>Results.Ok(await admin.Ejecutar(Who(c),linea,tipo,input,Key(c)))),"view");
 Gate(scoped.MapGet("/historial",async(int linea,int? pagina,HttpContext c)=>Results.Ok(await admin.Historial(Who(c),linea,pagina??1))),"historico");
 Gate(scoped.MapGet("/historial-legado",async(int linea,int? pagina,HttpContext c)=>{await admin.Leer(Who(c),linea);if(linea!=1)throw new RuleException("El legado corresponde exclusivamente a Línea 1.",404);return Results.Ok(await store.History(null,null,null,null,null,pagina??1,c.RequestAborted));}),"historico");
 Gate(scoped.MapGet("/cobertura-legado",async(int linea,DateTimeOffset corte,HttpContext c)=>{await admin.Leer(Who(c),linea);if(linea!=1)throw new RuleException("El legado corresponde a Línea 1.",404);var apertura=(await admin.Leer(Who(c),1)).Apertura;if(apertura.HasValue&&corte.UtcDateTime>=apertura.Value)throw new RuleException("Selecciona un corte anterior a la ampliación para consultar el legado.",400);return Results.Ok(await store.CoberturaHistorica(Who(c),corte,c.RequestAborted));}),"view");
 Gate(scoped.MapGet("/actividad-disponible",async(int linea,HttpContext c)=>Results.Ok(await admin.ActividadDisponible(Who(c),linea))),"historico");
 Gate(scoped.MapGet("/kpis",async(int linea,HttpContext c)=>Results.Ok(await admin.Kpis(Who(c),linea,c.Request.Query))),"historico");
 Gate(scoped.MapGet("/imagen/{material}",async(int linea,string material,HttpContext c)=>await admin.Imagen(Who(c),linea,material)),"view");
 Gate(scoped.MapPost("/imagen/{material}",async(int linea,string material,HttpContext c)=>{await admin.Foto(Who(c),linea,material,c.Request,false);return Results.Ok();}),"imagenes");
 Gate(scoped.MapPost("/imagen/{material}/eliminar",async(int linea,string material,HttpContext c)=>{await admin.Foto(Who(c),linea,material,c.Request,true);return Results.Ok();}),"imagenes");
 Gate(scoped.MapGet("/dashboard",async(int linea,HttpContext c)=>{await admin.Leer(Who(c),linea);return Results.Ok(linea==1?store.DashboardEquipo2():new DashboardEquipo2(false,null,Motivo:"No hay un informe Power BI configurado para esta línea. El informe de Línea 1 no se reutiliza como si fuera de otra línea."));}),"historico");
 Gate(scoped.MapPost("/dashboard-token",async(int linea,HttpContext c,PowerBiEmbed embed)=>{await admin.Leer(Who(c),linea);if(linea!=1)throw new RuleException("Informe no configurado para esta línea.",503);return Results.Ok(await embed.Generar(c.RequestAborted));}),"historico");
}
Gate(api.MapGet("/catalogo",async Task<IResult>(HttpContext c)=>Results.Ok(await store.Catalog(Who(c)))),"view");
Gate(api.MapGet("/inventario",async Task<IResult>(bool? alarmas,HttpContext c)=>{
    if(alarmas is true)return Results.Ok(await store.InventoryState(Who(c)));
    if(store.ModeloPuestos)return Results.Ok(await store.FilasPuestos(Who(c)));
    return Results.Ok(await store.Query(Store.Inventario+" ORDER BY v.numero_parte,v.id_ubicacion"));
}),"view");
if(store.ContinuacionHabilitada)
{
    Gate(api.MapPost("/continuacion/movimientos",async(MovimientoNuevo i,HttpContext c)=>Results.Ok(await store.GuardarMovimiento(Who(c),i,Key(c)))),"view");
    Gate(api.MapPost("/catalogo/materiales",async(ImportacionMaterialesInput i,HttpContext c)=>Results.Ok(await store.ImportarMateriales(Who(c),i,Key(c)))),"ajustar_catalogo");
    Gate(api.MapPost("/catalogo/capacidad",async(AjusteCapacidadInput i,HttpContext c)=>Results.Ok(await store.AjustarCapacidad(Who(c),i,Key(c)))),"ajustar_catalogo");
    Gate(api.MapGet("/cobertura/historica",async(DateTimeOffset corte,HttpContext c,CancellationToken t)=>Results.Ok(await store.CoberturaHistorica(Who(c),corte,t))),"view");
}
if(store.ModeloPuestos)
{
    Gate(api.MapGet("/atenciones",async(int? pagina,int? turno,HttpContext c)=>Results.Ok(await store.Atenciones(Who(c),pagina??1,turno))),"view");
    Gate(api.MapPost("/atenciones/iniciar",async(IniciarAtencionInput i,HttpContext c)=>Results.Ok(await store.CambiarAtencion(Who(c),i.Ubicacion,true))),"atencion");
    Gate(api.MapPost("/atenciones/finalizar",async(FinalizarAtencionInput i,HttpContext c)=>Results.Ok(await store.CambiarAtencion(Who(c),i.Registro,false))),"atencion");
    // Fotos de la pieza: ver con cualquier sesión (cada puesto solo sus materiales); subir y quitar solo administración.
    Gate(api.MapGet("/materiales/imagen",async(string? parte,HttpContext c)=>await store.Imagen(Who(c),parte)),"view");
    Gate(api.MapPost("/materiales/imagen",async(string? parte,HttpContext c)=>Results.Ok(await store.GuardarImagen(Who(c),parte,c.Request,c.RequestAborted))),"imagenes");
    Gate(api.MapPost("/materiales/imagen/eliminar",async(string? parte,HttpContext c)=>Results.Ok(await store.EliminarImagen(Who(c),parte))),"imagenes");
    Gate(api.MapPost("/puestos/acceso",async(AccesoPuestoInput i)=>{await store.GuardarAcceso(i);return Results.Ok();}),"administrar_accesos");
}
else
{
    Gate(api.MapPost("/central/entries",async(EntradaInput i,HttpContext c)=>Results.Ok(await store.Move(Who(c),i.Ubicacion,"ENTRADA",i.Cajas,i.Version,Key(c)))),"entry");
    Gate(api.MapPost("/movimientos/salidas",async(SalidaInput i,HttpContext c)=>Results.Ok(await store.Move(Who(c),i.Ubicacion,"SALIDA",i.Piezas,i.Version,Key(c)))),"exit");
}
// Power BI propio: acceso administrativo; tokens solo por POST autenticado y CSRF.
Gate(api.MapGet("/dashboard-equipo2",()=>Results.Ok(store.DashboardEquipo2())),"historico");
Gate(api.MapPost("/dashboard/embed-token",async(PowerBiEmbed embed,CancellationToken ct)=>Results.Ok(await embed.Generar(ct))),"historico");
Gate(api.MapGet("/historial",async(string? parte,string? ubicacion,string? desde,string? hasta,string? origen,int? pagina,bool? exacta,string? responsable,int? turno,CancellationToken token)=>Results.Ok(await store.History(parte,ubicacion,desde,hasta,origen,pagina??1,token,exacta??false,responsable,turno))),store.ModeloPuestos?"historico":"view");
if (store.archivoDemo is { } || store.SqlTransaccional)
{
    Gate(api.MapGet("/kpis/opciones", async (CancellationToken token) => Results.Ok(store.SqlTransaccional ? await store.OpcionesSql() : await store.archivoDemo!.OpcionesKpis(token))), "historico");
    Gate(api.MapGet("/kpis", async Task<IResult> (HttpContext ctx) => Results.Ok(await store.ConsultarKpis(Who(ctx), ctx.Request.Query, ctx.RequestAborted))), "historico");
    Gate(api.MapGet("/kpis/exportar", async (HttpContext ctx) => {
        var datos = await store.ConsultarKpis(Who(ctx), ctx.Request.Query, ctx.RequestAborted);
        ctx.Response.Headers.ContentDisposition = "attachment; filename=motherson-l1-kpis.json";
        return Results.Json(datos);
    }), "historico");
    Gate(api.MapGet("/historial/opciones", async (CancellationToken token) => Results.Ok(store.SqlTransaccional ? await store.OpcionesSql() : await store.archivoDemo!.Opciones(token))), "historico");
    Gate(api.MapGet("/historial/exportar", async (string? parte, string? ubicacion, string? desde, string? hasta,
        string? origen, bool? exacta, string? responsable, int? turno, HttpContext ctx) => {
            var filtro = Store.ValidarFiltro(parte, ubicacion, desde, hasta, origen, exacta??false, responsable, turno);
            if(store.SqlTransaccional) await store.ExportarHistorialSql(filtro,ctx.Response,ctx.RequestAborted);
            else if(store.ContinuacionHabilitada) await store.ExportarContinuacion(filtro,ctx.Response,ctx.RequestAborted);
            else await store.archivoDemo!.Exportar(filtro,ctx.Response,ctx.RequestAborted);
        }), "historico");
}
app.UseDefaultFiles();app.UseStaticFiles();app.Map("/api/{**rest}",()=>Results.NotFound(new{error="Esta ruta no forma parte del MVP."}));app.MapFallbackToFile("index.html");app.Run();
public sealed class UtcConverter:JsonConverter<DateTime>{public override DateTime Read(ref Utf8JsonReader r,Type t,JsonSerializerOptions o)=>r.GetDateTime().ToUniversalTime();public override void Write(Utf8JsonWriter w,DateTime d,JsonSerializerOptions o)=>w.WriteStringValue(DateTime.SpecifyKind(d,DateTimeKind.Utc).ToString("O"));}
