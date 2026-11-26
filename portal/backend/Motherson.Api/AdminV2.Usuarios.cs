using System.Data;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Dapper;
using Microsoft.AspNetCore.Identity;
namespace Motherson;

/// <summary>
/// Pantalla "Usuarios" (solo ADMIN): crear cuentas personales del piloto con su rol, líneas y puesto,
/// editarlas, cambiar contraseña y desactivarlas. Las escrituras pasan por mes2.UsuarioCrear y
/// mes2.UsuarioActualizar (portal/db/administracion/003_usuarios.sql), que vuelven a validar todo.
/// </summary>
public sealed partial class AdminV2
{
 static readonly PasswordHasher<string> HasherUsuarios=new();
 sealed record UsuarioFila(int Id,string Usuario,string Nombre,string Rol,bool Activo);
 sealed record AccesoFila(int Usuario,int Linea,string? Puesto);
 sealed record LineaFila(int Id,string Nombre);
 sealed record PuestoFila(int Linea,string Id,string? Nombre,string? Activo);

 async Task AdminGlobal(Cuenta actor)
 {
  Exigir(actor.Piloto,"La administración de usuarios requiere una cuenta personal del piloto.",403);
  await using var sql=await store.Open();
  var ok=await sql.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM mes.USUARIO_PILOTO WHERE id_usuario=@id AND rol='ADMIN' AND activo=1",new{id=actor.Id});
  Exigir(ok==1,"Solo administración puede gestionar usuarios.",403);
 }

 public async Task<object> Usuarios(Cuenta actor)
 {
  await AdminGlobal(actor);await using var sql=await store.Open();
  var usuarios=(await sql.QueryAsync<UsuarioFila>("SELECT id_usuario Id,usuario Usuario,nombre Nombre,rol Rol,activo Activo FROM mes.USUARIO_PILOTO ORDER BY activo DESC,usuario")).ToArray();
  var accesos=(await sql.QueryAsync<AccesoFila>("SELECT usuario Usuario,linea Linea,puesto Puesto FROM mes2.ACCESO")).ToArray();
  var lineas=(await sql.QueryAsync<LineaFila>("SELECT id Id,nombre Nombre FROM mes2.LINEA ORDER BY id")).ToArray();
  var puestos=(await sql.QueryAsync<PuestoFila>("SELECT linea Linea,id Id,JSON_VALUE(datos,'$.nombre') Nombre,JSON_VALUE(datos,'$.activo') Activo FROM mes2.ENTIDAD WHERE tipo='puesto'")).ToArray();
  return new{
   usuarios=usuarios.Select(u=>new{id=u.Id,usuario=u.Usuario,nombre=u.Nombre,rol=u.Rol,activo=u.Activo,propio=u.Id==actor.Id,
    accesos=accesos.Where(a=>a.Usuario==u.Id).OrderBy(a=>a.Linea).Select(a=>new{linea=a.Linea,puesto=a.Puesto}).ToArray()}).ToArray(),
   lineas=lineas.Select(l=>new{id=l.Id,nombre=l.Nombre,
    puestos=puestos.Where(p=>p.Linea==l.Id).Select(p=>new{id=p.Id,nombre=p.Nombre??p.Id,activo=p.Activo!="false"}).OrderBy(p=>p.nombre,StringComparer.CurrentCulture).ToArray()}).ToArray()
  };
 }

 /// <summary>Crea (sin id) o actualiza (con id) una cuenta. La contraseña solo es obligatoria al crear.</summary>
 public async Task<object> UsuarioGuardar(Cuenta actor,JsonObject input)
 {
  await AdminGlobal(actor);
  var id=input["id"] is null?0:Entero(input,"id",1,int.MaxValue);
  var nombre=Texto(input,"nombre",120,true);
  var rol=Texto(input,"rol",10,true);Exigir(rol is "ADMIN" or "PUESTO" or "LECTOR","Rol inválido.");
  var activo=input["activo"] is null||B(input,"activo",true);
  Exigir(input["password"] is null||input["password"] is JsonValue pv&&pv.TryGetValue<string>(out _),"La contraseña debe ser texto.");
  var password=S(input,"password");
  Exigir(id>0||password.Length>0,"Indica la contraseña inicial.");
  Exigir(password.Length==0||password.Length is >=12 and <=512,"La contraseña debe tener al menos 12 caracteres.");
  var accesos=input["accesos"] as JsonArray??throw new RuleException("Indica al menos una línea.",400);
  var lista=new List<(int Linea,string? Puesto)>();
  foreach(var node in accesos)
  {
   var a=node as JsonObject??throw new RuleException("Acceso inválido.",400);
   var linea=Entero(a,"linea",1,4);var puesto=Texto(a,"puesto",100);
   Exigir(rol!="PUESTO"||puesto.Length>0,"Elige el puesto de la Línea "+linea+".");
   Exigir(lista.All(x=>x.Linea!=linea),"Línea repetida.");
   lista.Add((linea,rol=="PUESTO"?puesto:null));
  }
  Exigir(lista.Count>0,"Indica al menos una línea.");
  var json=JsonSerializer.Serialize(lista.Select(x=>new{linea=x.Linea,puesto=x.Puesto}));
  var hash=password.Length>0?HasherUsuarios.HashPassword("",password):null;
  await using var sql=await store.Open();
  // Una cuenta activa de puesto logístico no puede quedar ligada a un puesto desactivado (Configurar línea).
  if(activo&&rol=="PUESTO")
   foreach(var (linea,puesto) in lista)
   {
    var estado=await sql.QuerySingleOrDefaultAsync<string?>("SELECT COALESCE(JSON_VALUE(datos,'$.activo'),'true') FROM mes2.ENTIDAD WHERE linea=@linea AND tipo='puesto' AND id=@puesto",new{linea,puesto});
    Exigir(estado!="false","El puesto elegido en la Línea "+linea+" está desactivado. Elige otro o reactívalo en Configurar línea.",409);
   }
  if(id==0)
  {
   var usuario=Texto(input,"usuario",80,true);
   Exigir(Regex.IsMatch(usuario,@"^[A-Za-z0-9._-]{3,80}$")&&!usuario.Equals("admin.local",StringComparison.OrdinalIgnoreCase),
    "Usuario: de 3 a 80 caracteres, sin espacios ni acentos (letras, números, punto, guion o guion bajo).");
   var nuevo=await sql.QuerySingleAsync<int>("mes2.UsuarioCrear",new{admin=actor.Id,usuario,nombre,rol,hash,accesos=json},commandType:CommandType.StoredProcedure);
   return new{id=nuevo,creado=true};
  }
  await sql.ExecuteAsync("mes2.UsuarioActualizar",new{admin=actor.Id,id,nombre,rol,activo,hash,accesos=json},commandType:CommandType.StoredProcedure);
  return new{id,creado=false};
 }
}
