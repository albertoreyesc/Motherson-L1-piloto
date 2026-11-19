using System.Data;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Dapper;
using Microsoft.AspNetCore.Identity;
using Microsoft.Data.SqlClient;
namespace Motherson;
public sealed class RuleException(string message,int status=409):Exception(message){public int Status{get;}=status;}
public record LoginInput(string Username,string Password);
public record EntradaInput(int Ubicacion,int Cajas,int Version);
public record SalidaInput(int Ubicacion,int Piezas,int Version);
public record Cuenta(int Id,string Name,string? DisplayName,string Perfil,string TipoCuenta,int? PuestoId){
 [System.Text.Json.Serialization.JsonIgnore] public string? SelloAcceso{get;init;}
 public string[] Permissions=>TipoCuenta switch{"ADMIN_PUESTOS"=>["view","historico","administrar_accesos","entry","exit","imagenes","ajustar_catalogo"],"PUESTO"=>["view","atencion","entry"],_=>Perfil switch{"ADMINISTRADOR"=>["view","entry","exit","catalog"],"MOVIMIENTOS"=>["view","entry"],"ENTRADAS"=>["view","entry"],_=>["view"]}};public bool Simulado=>true;}
public sealed partial class Store(IConfiguration configuration){
 public const string Inventario="""
 SELECT v.id_ubicacion,v.numero_parte,v.descripcion,v.uso,v.id_puesto,v.puesto,v.cantidad_por_caja,v.minimo,v.cantidad,v.version_concurrencia,v.id_estanteria,v.estanteria,v.nivel,v.cajas_estanteria,v.id_estacion,v.estacion,v.stock_total_material,v.es_simulado,CASE WHEN o.pendientes>0 OR v.cajas_estanteria IS NULL THEN NULL WHEN v.cajas_estanteria-o.ocupacion<0 THEN 0 ELSE v.cajas_estanteria-o.ocupacion END cajas_disponibles
 FROM reporting.vw_kpi_inventario_actual v OUTER APPLY(
 SELECT SUM(CASE WHEN u.cantidad IS NULL OR m.cantidad_por_caja IS NULL THEN 1 ELSE 0 END) pendientes,SUM((CONVERT(bigint,u.cantidad)+m.cantidad_por_caja-1)/m.cantidad_por_caja) ocupacion
 FROM dbo.UBICACION u JOIN dbo.MATERIAL m ON m.numero_parte=u.numero_parte WHERE u.id_estanteria=v.id_estanteria)o
 """;
 // Base compartida (Azure del Equipo 2): se autoriza por nombre con MVP_BASE_COMPARTIDA. Sin esa variable nada cambia.
 public static bool EsBaseCompartida(string db,IConfiguration cfg)=>cfg["MVP_BASE_COMPARTIDA"] is {Length:>0} b&&string.Equals(db,b,StringComparison.OrdinalIgnoreCase);
 public static bool BaseAutorizada(string db,IConfiguration cfg)=>db is "Motherson_L1_MVP_Desarrollo" or "Motherson_L1_MVP_Pruebas" or HistoricoDemo.Base||EsBaseCompartida(db,cfg);
 public async Task<SqlConnection> Open(){var c=new SqlConnection(configuration.GetConnectionString("Sql")??throw new RuleException("Falta conexión SQL.",503));await c.OpenAsync();if(!BaseAutorizada(c.Database,configuration)){await c.DisposeAsync();throw new RuleException("Base MVP no autorizada.",503);}return c;}
 public async Task<Cuenta> Actor(int id,string tipo="USUARIO",string? sello=null){
  if(ModeloPuestos)return await ActorPuesto(id,tipo,sello);
  if(tipo!="USUARIO")throw new RuleException("Sesión inválida.",401);
  await using var c=await Open();
  var asignacion=await c.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM sys.columns WHERE object_id=OBJECT_ID('dbo.USUARIOS') AND name='id_puesto'")>0?"id_puesto":"CONVERT(int,NULL)";
  return await c.QuerySingleOrDefaultAsync<Cuenta>("SELECT id_usuarios Id,nombre_usuario Name,nombre_visible DisplayName,perfil Perfil,CONVERT(varchar(10),'USUARIO') TipoCuenta,"+asignacion+" PuestoId FROM dbo.USUARIOS WHERE id_usuarios=@id AND esta_activo=1",new{id})??throw new RuleException("La sesión venció o la cuenta está inactiva.",401);
 }
 static readonly PasswordHasher<string> Hasher=new();static readonly string Dummy=Hasher.HashPassword("",Guid.NewGuid().ToString());
 public async Task<Cuenta?> Login(LoginInput input){
  if(string.IsNullOrWhiteSpace(input.Username)||string.IsNullOrEmpty(input.Password)||input.Username.Length>80||input.Password.Length>512)return null;
  if(ModeloPuestos)return await LoginPuesto(input);
  await using var c=await Open();var u=await c.QuerySingleOrDefaultAsync("SELECT id_usuarios,hash_contrasena FROM dbo.USUARIOS WHERE nombre_usuario=@nombre AND esta_activo=1",new{nombre=input.Username});
  var hash=u is null?Dummy:(string)u.hash_contrasena;var valid=Hasher.VerifyHashedPassword("",hash,input.Password)!=PasswordVerificationResult.Failed;
  return u is not null&&valid?await Actor((int)u.id_usuarios):null;
 }
 public async Task<IEnumerable<dynamic>> Query(string sql,object? args=null){await using var c=await Open();return await c.QueryAsync(sql,args);}
 static object? AtributosCatalogo(){
  var root=Environment.GetEnvironmentVariable("MVP_ROOT");if(root is null)return null;var path=Path.Combine(root,"config","catalogo-documentado.json");
  try{return File.Exists(path)?JsonSerializer.Deserialize<JsonElement>(File.ReadAllText(path)):null;}catch(IOException){return null;}catch(JsonException){return null;}
 }
 public async Task<object> Catalog(Cuenta actor){if(ModeloPuestos)return await CatalogoPuestos(actor);await using var c=await Open();return new{atributos=AtributosCatalogo(),materiales=await c.QueryAsync("SELECT m.numero_parte,m.id_puesto,m.descripcion,m.uso,m.cantidad_por_caja,m.minimo,p.nombre puesto FROM dbo.MATERIAL m JOIN dbo.PUESTO_LOGISTICO p ON p.id_puesto=m.id_puesto ORDER BY numero_parte"),estaciones=await c.QueryAsync("SELECT * FROM dbo.ESTACION ORDER BY nombre_estacion"),estanterias=await c.QueryAsync("SELECT * FROM dbo.ESTANTERIA ORDER BY id_estacion,estanteria,nivel"),puestos=await c.QueryAsync("SELECT id_puesto,nombre,tipo FROM dbo.PUESTO_LOGISTICO ORDER BY nombre"),turnos=await c.QueryAsync("SELECT id_turno,nombre_turno,CONVERT(varchar(8),hora_inicio) hora_inicio,CONVERT(varchar(8),hora_fin) hora_fin FROM dbo.TURNO ORDER BY hora_inicio"),corte=await c.QuerySingleAsync("SELECT maestro,revision,corte_fuente,fecha_carga,sellado FROM archivo.CORTE")};}
 public async Task<object> Move(Cuenta actor,int ubicacion,string tipo,int cantidad,int version,Guid key){if(key==Guid.Empty||ubicacion<=0||version<=0||cantidad<=0||cantidad>1000000)throw new RuleException("Revisa cantidad, ubicación y versión.",400);var huella=Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new{ubicacion,tipo,cantidad,version}))));await using var c=await Open();return await c.QuerySingleAsync("dbo.RegistrarMovimiento",new{usuario=actor.Id,ubicacion,tipo,cantidad,version,operacion=key,huella},commandType:CommandType.StoredProcedure);}
 public async Task<object> History(string? parte,string? ubicacion,string? desde,string? hasta,string? origen,int pagina,CancellationToken token,bool exacta=false,string? responsable=null,int? turno=null){
  if(pagina<1||pagina>50000)throw new RuleException("Página fuera de rango.",400);
  var filtro=ValidarFiltro(parte,ubicacion,desde,hasta,origen,exacta,responsable,turno);
  if(ContinuacionHabilitada)return await HistorialContinuacion(filtro,pagina,token);
  if(archivoDemo is not null)return await archivoDemo.Pagina(filtro,pagina,token);
  if(exacta||responsable is not null||turno is not null)throw new RuleException("Estos filtros requieren el paquete histórico maestro.",400);
  const string filter=" WHERE (@parte IS NULL OR numero_parte LIKE @parte) AND (@id IS NULL OR(id_ubicacion=@id AND espacio_identificadores=@espacio)) AND(@inicio IS NULL OR fecha_movimiento>=@inicio) AND(@fin IS NULL OR fecha_movimiento<@fin) AND(@origen IS NULL OR espacio_identificadores=@origen)";
  var args=new{parte=filtro.Parte is null?null:"%"+filtro.Parte+"%",id=filtro.Id,espacio=filtro.Espacio,inicio=filtro.Inicio,fin=filtro.Fin,origen=filtro.Origen,offset=(pagina-1)*50};await using var c=await Open();
  var total=await c.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM reporting.vw_kpi_movimientos"+filter,args);var filas=await c.QueryAsync("SELECT * FROM reporting.vw_kpi_movimientos"+filter+" ORDER BY fecha_movimiento DESC,id_movimiento DESC OFFSET @offset ROWS FETCH NEXT 50 ROWS ONLY",args);return new{total,pagina,filas};
 }
 internal static FiltroHistorico ValidarFiltro(string? parte,string? ubicacion,string? desde,string? hasta,string? origen,bool exacta=false,string? responsable=null,int? turno=null){
  DateTime? Fecha(string? text,bool fin){if(string.IsNullOrEmpty(text))return null;if(!DateTime.TryParseExact(text,"yyyy-MM-dd",null,System.Globalization.DateTimeStyles.None,out var dt))throw new RuleException("Usa fecha AAAA-MM-DD.",400);if(fin)dt=dt.AddDays(1);return TimeZoneInfo.ConvertTimeToUtc(dt,TimeZoneInfo.FindSystemTimeZoneById("America/Mexico_City"));}
  var inicio=Fecha(desde,false);var fin=Fecha(hasta,true);if(inicio.HasValue&&fin.HasValue&&inicio>=fin)throw new RuleException("Intervalo de fechas inválido.",400);
  int? id=null;string? espacio=null;if(!string.IsNullOrEmpty(ubicacion)){var bits=ubicacion.Split(':');if(bits.Length!=2||bits[0] is not("MVP" or "HISTORICO_21" or "CONTINUACION")||!int.TryParse(bits[1],out var n)||n<=0)throw new RuleException("Ubicación debe indicar MVP:id, HISTORICO_21:id o CONTINUACION:id.",400);id=n;espacio=bits[0];}
  if(origen is not(null or "" or "MVP" or "HISTORICO_21" or "CONTINUACION"))throw new RuleException("Origen inválido.",400);
  if(responsable?.Length>150||turno is <1 or >3)throw new RuleException("Responsable o turno histórico inválido.",400);
  return new(string.IsNullOrWhiteSpace(parte)?null:parte,id,espacio,inicio,fin,string.IsNullOrEmpty(origen)?null:origen,exacta,string.IsNullOrWhiteSpace(responsable)?null:responsable,turno);
 }
}
