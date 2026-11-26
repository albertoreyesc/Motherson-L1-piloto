using System.Text.Json.Nodes;
using Dapper;
using Microsoft.Data.SqlClient;
namespace Motherson;

// Observaciones de Motherson (reunión 16/11/2026):
// - Ubicaciones: no borrarlas; poder desactivarlas y dar de alta una nueva (también para moverlas de estantería).
// - Cambio de revisión de una parte: duplicar el material existente, editar solo lo que cambia y desactivar el anterior.
// - Puestos, estaciones y estanterías tampoco se borran: se desactivan y se pueden reactivar (26/11/2026).
public sealed partial class AdminV2
{
 /// <summary>Las ubicaciones creadas antes de esta versión no tienen el campo y se consideran activas.</summary>
 public static bool Activa(JsonObject u)=>B(u,"activa",true);

 static JsonObject NuevaUbicacion(V2Estado e,string material,string estanteria)=>new()
 {["id"]=Nuevo(),["material"]=material,["estanteria"]=estanteria,["cantidad"]=0,["apertura"]=e.Ahora.ToString("O"),["activa"]=true};

 static void Reactivar(JsonObject u){u["activa"]=true;u.Remove("desactivada");u.Remove("motivoDesactivacion");}

 /// <summary>
 /// UBICACION_ESTADO: desactiva o reactiva una ubicación sin borrarla (conserva movimientos y atenciones).
 /// Al desactivar con saldo, exige una estantería destino y traslada las piezas a la ubicación del mismo
 /// material en esa estantería (la crea o la reactiva). El traslado no es consumo ni salida.
 /// </summary>
 static void EstadoUbicacion(V2Estado e,JsonObject input,JsonObject resultado)
 {
  Admin(e);var u=e.Obtener("ubicacion",Texto(input,"id",100,true));var motivo=Texto(input,"motivo",250,true);
  Exigir(input["activa"] is JsonValue av&&av.TryGetValue<bool>(out _),"Indica si la ubicación queda activa.");var activar=B(input,"activa");
  var m=MaterialUbicacion(e,u);
  resultado["ubicacion"]=S(u,"id");resultado["material"]=S(m,"id");resultado["numeroParte"]=S(m,"numeroParte");resultado["estanteria"]=S(u,"estanteria");resultado["motivo"]=motivo;resultado["activa"]=activar;
  if(activar)
  {
   Exigir(!Activa(u),"La ubicación ya está activa.",409);Exigir(B(m,"activo",true),"El material está inhabilitado: reactívalo antes que su ubicación.",409);
   Exigir(Activo(e.Obtener("estanteria",S(u,"estanteria"))),"La estantería está desactivada: reactívala en Configurar línea antes que esta ubicación.",409);
   Reactivar(u);u.Remove("desactivadaPor");return;
  }
  Exigir(Activa(u),"La ubicación ya está desactivada.",409);
  Exigir(!e.Todos("atencion").Any(a=>S(a,"fin")==""&&S(a,"ubicacion")==S(u,"id")),"Hay una atención abierta en esta ubicación. Finalízala antes de desactivarla.",409);
  var destino=Texto(input,"destino",100);var saldo=N(u,"cantidad");
  Exigir(saldo==0||destino.Length>0,"La ubicación tiene "+saldo+" piezas: elige la estantería destino para trasladarlas o retíralas antes con una salida.",409);
  if(destino.Length>0)
  {
   var rack=e.Obtener("estanteria",destino);Exigir(destino!=S(u,"estanteria"),"La estantería destino debe ser distinta de la actual.",409);
   Exigir(Activo(rack),"La estantería destino está desactivada.",409);
   var nueva=e.Todos("ubicacion").FirstOrDefault(x=>S(x,"material")==S(m,"id")&&S(x,"estanteria")==destino);
   if(nueva is null){Exigir(B(m,"activo",true),"El material está inhabilitado: no se crean ubicaciones nuevas; retira su remanente con una salida.",409);nueva=NuevaUbicacion(e,S(m,"id"),destino);e.Poner("ubicacion",nueva);}
   else if(!Activa(nueva)){Reactivar(nueva);nueva.Remove("desactivadaPor");}
   nueva["cantidad"]=checked(N(nueva,"cantidad")+saldo);u["cantidad"]=0;
   Exigir(Ocupacion(e,destino)<=N(rack,"capacidad"),"La estantería destino no tiene espacio para "+saldo+" piezas.",409);
   resultado["destino"]=S(nueva,"id");resultado["estanteriaDestino"]=destino;resultado["piezasTrasladadas"]=saldo;
  }
  u["activa"]=false;u["desactivada"]=e.Ahora.ToString("O");u["motivoDesactivacion"]=motivo;
 }

 /// <summary>Puestos, estaciones y estanterías creados antes de esta versión no traen el campo: se consideran activos.</summary>
 public static bool Activo(JsonObject o)=>B(o,"activo",true);

 static string NombreEstanteria(V2Estado e,JsonObject r)=>string.Join(" / ",new[]{S(e.Datos.GetValueOrDefault("estacion:"+S(r,"estacion"))??new JsonObject(),"nombre"),S(r,"nombre"),S(r,"nivel")}.Where(x=>x.Length>0));

 /// <summary>
 /// PUESTO_ESTADO, ESTACION_ESTADO y ESTANTERIA_ESTADO: desactivan o reactivan sin borrar, para no romper el historial,
 /// los KPIs ni el informe de Power BI. Lo desactivado deja de ofrecerse para operar, pero sigue en las consultas.
 /// - Puesto: sin materiales activos, sin atenciones abiertas y sin cuentas activas que entren con él.
 /// - Estación: sin estanterías activas.
 /// - Estantería: sin piezas ni atenciones abiertas; sus ubicaciones (vacías) se desactivan con ella y vuelven al reactivarla.
 /// </summary>
 static async Task EstadoEstructura(V2Estado e,string tipo,JsonObject input,JsonObject resultado,SqlConnection sql)
 {
  Admin(e);
  var kind=tipo switch{"PUESTO_ESTADO"=>"puesto","ESTACION_ESTADO"=>"estacion",_=>"estanteria"};
  var cosa=kind switch{"puesto"=>"El puesto","estacion"=>"La estación",_=>"La estantería"};
  var o=e.Obtener(kind,Texto(input,"id",100,true));var motivo=Texto(input,"motivo",250,true);
  Exigir(input["activo"] is JsonValue av&&av.TryGetValue<bool>(out _),"Indica si queda activo o desactivado.");var activar=B(input,"activo");
  var id=S(o,"id");
  resultado["id"]=id;resultado["nombre"]=kind=="estanteria"?NombreEstanteria(e,o):S(o,"nombre");resultado["motivo"]=motivo;resultado["activo"]=activar;
  if(activar)
  {
   Exigir(!Activo(o),cosa+(kind=="puesto"?" ya está activo.":" ya está activa."),409);
   if(kind=="estanteria")
   {
    var estacion=e.Obtener("estacion",S(o,"estacion"));
    Exigir(Activo(estacion),"La estación "+S(estacion,"nombre")+" está desactivada: reactívala primero.",409);
    // Regresan las ubicaciones que se desactivaron junto con la estantería (si su material sigue activo).
    var n=0;
    foreach(var u in e.Todos("ubicacion").Where(u=>S(u,"estanteria")==id&&!Activa(u)&&S(u,"desactivadaPor")=="estanteria"&&B(MaterialUbicacion(e,u),"activo",true)))
    {Reactivar(u);u.Remove("desactivadaPor");n++;}
    resultado["ubicacionesReactivadas"]=n;
   }
   o["activo"]=true;o.Remove("desactivado");o.Remove("motivoDesactivacion");return;
  }
  Exigir(Activo(o),cosa+(kind=="puesto"?" ya está desactivado.":" ya está desactivada."),409);
  switch(kind)
  {
   case "puesto":
   {
    var materiales=e.Todos("material").Count(m=>S(m,"puesto")==id&&B(m,"activo",true));
    Exigir(materiales==0,"El puesto tiene "+materiales+(materiales==1?" material activo. Inhabilítalo":" materiales activos. Inhabilítalos")+" en Materiales y catálogos antes de desactivarlo.",409);
    Exigir(!e.Todos("atencion").Any(a=>S(a,"fin")==""&&S(a,"puesto")==id),"El puesto tiene una atención abierta. Finalízala antes de desactivarlo.",409);
    var cuentas=(await sql.QueryAsync<string>("SELECT u.usuario FROM mes2.ACCESO a JOIN mes.USUARIO_PILOTO u ON u.id_usuario=a.usuario WHERE a.linea=@linea AND a.puesto=@puesto AND u.activo=1 ORDER BY u.usuario",new{linea=e.Linea,puesto=id})).ToArray();
    Exigir(cuentas.Length==0,(cuentas.Length==1?"La cuenta "+cuentas[0]+" entra":"Las cuentas "+string.Join(", ",cuentas)+" entran")+" con este puesto. Cámbiala de puesto o desactívala en Usuarios antes.",409);
    break;
   }
   case "estacion":
   {
    var racks=e.Todos("estanteria").Count(r=>S(r,"estacion")==id&&Activo(r));
    Exigir(racks==0,"La estación tiene "+racks+(racks==1?" estantería activa. Desactívala":" estanterías activas. Desactívalas")+" primero en la pestaña Estanterías.",409);
    break;
   }
   default:
   {
    var ubicaciones=e.Todos("ubicacion").Where(u=>S(u,"estanteria")==id).ToArray();
    var piezas=ubicaciones.Sum(u=>(long)N(u,"cantidad"));
    Exigir(piezas==0,"La estantería tiene "+piezas+" piezas. Muévelas en Estaciones y ubicaciones o retíralas con una salida antes de desactivarla.",409);
    Exigir(!e.Todos("atencion").Any(a=>S(a,"fin")==""&&ubicaciones.Any(u=>S(u,"id")==S(a,"ubicacion"))),"Hay una atención abierta en esta estantería. Finalízala antes de desactivarla.",409);
    var n=0;
    foreach(var u in ubicaciones.Where(Activa))
    {u["activa"]=false;u["desactivada"]=e.Ahora.ToString("O");u["motivoDesactivacion"]=motivo;u["desactivadaPor"]="estanteria";n++;}
    resultado["ubicacionesDesactivadas"]=n;
    break;
   }
  }
  o["activo"]=false;o["desactivado"]=e.Ahora.ToString("O");o["motivoDesactivacion"]=motivo;
 }

 /// <summary>
 /// MATERIAL_DUPLICAR: crea un material nuevo (p. ej. la siguiente revisión) copiando los datos del original.
 /// Los campos enviados sustituyen a los copiados. Opcionalmente asigna las mismas estanterías con saldo cero
 /// e inhabilita el original (su remanente se retira con una salida, como cualquier material inhabilitado).
 /// </summary>
 static void DuplicarMaterial(V2Estado e,JsonObject input,JsonObject resultado)
 {
  Admin(e);var origen=e.Obtener("material",Texto(input,"id",100,true));var motivo=Texto(input,"motivo",250,true);var numero=Texto(input,"numeroParte",80,true);
  Exigir(!e.Todos("material").Any(x=>S(x,"numeroParte")==numero),"Número de parte ya existente.",409);
  var m=Copia(origen);m["id"]=Nuevo();m["numeroParte"]=numero;m["activo"]=true;m["origen"]=S(origen,"id");m["duplicado"]=e.Ahora.ToString("O");
  foreach(var campo in new[]{"descripcion","variante","uso","revision","modelo"})if(input[campo] is not null)m[campo]=Texto(input,campo,campo=="descripcion"?250:80);
  foreach(var campo in new[]{"empaque","minimo"})if(input[campo] is not null)m[campo]=Entero(input,campo,campo=="empaque"?1:0,campo=="empaque"?1000000:100000000);
  e.Poner("material",m);
  var copiadas=new JsonArray();
  if(B(input,"copiarUbicaciones"))
   foreach(var u in e.Todos("ubicacion").Where(u=>S(u,"material")==S(origen,"id")&&Activa(u)).ToArray())
   {var n=NuevaUbicacion(e,S(m,"id"),S(u,"estanteria"));e.Poner("ubicacion",n);copiadas.Add(S(n,"id"));}
  var inhabilitar=B(input,"inhabilitarOrigen");if(inhabilitar)origen["activo"]=false;
  resultado["id"]=S(m,"id");resultado["numeroParte"]=numero;resultado["origen"]=S(origen,"id");resultado["numeroParteOrigen"]=S(origen,"numeroParte");
  resultado["motivo"]=motivo;resultado["ubicaciones"]=copiadas;resultado["origenInhabilitado"]=inhabilitar;
 }
}
