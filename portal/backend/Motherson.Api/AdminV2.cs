using System.Data;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Dapper;
using Microsoft.Data.SqlClient;
namespace Motherson;

public sealed record V2Cambio(string Tipo,string Id,JsonObject? Datos,JsonObject? Antes=null);
public sealed class V2Estado(int linea,int version,DateTime? apertura,DateTime ahora,string rol,string? puesto,Dictionary<string,JsonObject> entidades)
{
 public int Linea=linea,Version=version;public DateTime? Apertura=apertura;public DateTime Ahora=ahora;public string Rol=rol;public string? Puesto=puesto;public Dictionary<string,JsonObject> Datos=entidades;
 public JsonObject[] Todos(string tipo)=>Datos.Where(e=>e.Key.StartsWith(tipo+":",StringComparison.Ordinal)).Select(e=>e.Value).ToArray();
 public JsonObject Obtener(string tipo,string id)=>Datos.GetValueOrDefault(tipo+":"+id)??throw new RuleException("Referencia no encontrada en esta línea.",404);
 public void Poner(string tipo,JsonObject obj)=>Datos[tipo+":"+AdminV2.S(obj,"id")]=obj;
}

public sealed partial class AdminV2(Store store)
{
 internal sealed record Fila(string Tipo,string Id,string Datos);
 public static string S(JsonObject o,string k,string defecto="")=>o[k] is JsonValue v&&v.TryGetValue<string>(out var s)?s:defecto;
 public static int N(JsonObject o,string k,int defecto=0)=>o[k] is JsonValue v&&v.TryGetValue<int>(out var n)?n:defecto;
 public static bool B(JsonObject o,string k,bool defecto=false)=>o[k] is JsonValue v&&v.TryGetValue<bool>(out var b)?b:defecto;
 static JsonObject Copia(JsonObject o)=>(JsonObject)o.DeepClone();
 static string Nuevo()=>Guid.NewGuid().ToString();
 static void Exigir(bool condicion,string texto,int status=400){if(!condicion)throw new RuleException(texto,status);}
 static string Texto(JsonObject o,string campo,int max,bool requerido=false)
 {
  Exigir(o[campo] is null||o[campo] is JsonValue j&&j.TryGetValue<string>(out _),"El campo "+campo+" debe ser texto.");
  var s=S(o,campo).Trim().Normalize();Exigir(s.Length<=max&&(!requerido||s.Length>0),"Revisa "+campo+" (máximo "+max+" caracteres).");return s;
 }
 static int Entero(JsonObject o,string campo,int min,int max)
 {Exigir(o[campo] is JsonValue j&&j.TryGetValue<int>(out var n)&&n>=min&&n<=max,"Revisa el entero "+campo+".");return N(o,campo);}
 static void Admin(V2Estado e)=>Exigir(e.Rol=="ADMIN","Operación reservada a administración.",403);
 static void Puesto(V2Estado e,JsonObject m)=>Exigir(e.Puesto is null||S(m,"puesto")==e.Puesto,"Material de otro puesto.",403);
 public async Task<V2Estado> Leer(Cuenta actor,int linea,DateTime? corte=null)
 {
  Exigir(actor.Piloto,"La ampliación requiere una cuenta personal del piloto.",403);Exigir(linea is >=1 and <=4,"Línea inválida.");
  await using var sql=await store.Open();using var result=await sql.QueryMultipleAsync("mes2.Leer",new{usuario=actor.Id,linea,corte},commandType:CommandType.StoredProcedure);
  var m=await result.ReadSingleAsync();var rows=await result.ReadAsync<Fila>();
  return new(linea,(int)m.version,(DateTime?)m.apertura,DateTime.SpecifyKind((DateTime)m.ahora,DateTimeKind.Utc),(string)m.rol,(string?)m.puesto,rows.ToDictionary(r=>r.Tipo+":"+r.Id,r=>JsonNode.Parse(r.Datos)!.AsObject(),StringComparer.Ordinal));
 }
 public async Task<object> Lineas(Cuenta actor)
 {
  await using var sql=await store.Open();return await sql.QueryAsync("SELECT l.id,l.nombre,l.version,l.apertura FROM mes2.LINEA l JOIN mes2.ACCESO a ON a.linea=l.id JOIN mes.USUARIO_PILOTO u ON u.id_usuario=a.usuario WHERE a.usuario=@id AND u.activo=1 ORDER BY l.id",new{id=actor.Id});
 }
 static JsonObject MaterialUbicacion(V2Estado e,JsonObject u)=>e.Obtener("material",S(u,"material"));
 static long Ocupacion(V2Estado e,string rack)=>e.Todos("ubicacion").Where(u=>S(u,"estanteria")==rack).Sum(u=>((long)N(u,"cantidad")+N(MaterialUbicacion(e,u),"empaque")-1)/N(MaterialUbicacion(e,u),"empaque"));
 public async Task<object> Estado(Cuenta actor,int linea,DateTime? corte=null)
 {
  Exigir(!corte.HasValue||corte.Value<=DateTime.UtcNow,"El corte no puede estar en el futuro.");var e=await Leer(actor,linea,corte);var ms=e.Todos("material").Where(m=>e.Puesto is null||S(m,"puesto")==e.Puesto).ToArray();var ids=ms.Select(m=>S(m,"id")).ToHashSet();
  var us=e.Todos("ubicacion").Where(u=>ids.Contains(S(u,"material"))).Select(u=>{var r=Copia(u);var rack=e.Obtener("estanteria",S(u,"estanteria"));r["libres"]=Math.Max(0,N(rack,"capacidad")-Ocupacion(e,S(rack,"id")));return r;}).ToArray();
  var alarms=await Alarmas(actor,e,corte??e.Ahora,us.Where(Activa).ToArray());
  await using var imageSql=await store.Open();var images=(await imageSql.QueryAsync("SELECT material,version FROM mes2.IMAGEN WHERE linea=@linea",new{linea})).Where(x=>ids.Contains((string)x.material)).ToDictionary(x=>(string)x.material,x=>(int)x.version);
  return new{linea,version=e.Version,corte=corte??e.Ahora,apertura=e.Apertura,rol=e.Rol,puesto=e.Puesto,materiales=ms,ubicaciones=us,
   puestos=e.Todos("puesto").Where(p=>e.Puesto is null||S(p,"id")==e.Puesto),estaciones=e.Todos("estacion"),estanterias=e.Todos("estanteria"),
   imagenes=images,atenciones=e.Todos("atencion").Where(a=>S(a,"fin")==""&&(e.Puesto is null||S(a,"puesto")==e.Puesto)),calendario=e.Todos("calendario").SingleOrDefault(),alarmas=alarms,
   totalPiezas=us.Sum(u=>(long)N(u,"cantidad")),simulado=Entorno.DatosSimulados};
 }
 async Task<object[]> Alarmas(Cuenta actor,V2Estado e,DateTime corte,JsonObject[] ubicaciones)
 {
  var cal=e.Todos("calendario").SingleOrDefault();var ventana=CalendarioV2.Ventana(cal,corte);
  await using var sql=await store.Open();var events=await sql.QueryAsync<string>("SELECT resultado FROM mes2.EVENTO WHERE linea=@linea AND tipo='SALIDA' AND fecha<=@corte AND fecha>=@desde",new{linea=e.Linea,corte,desde=ventana.Completa?ventana.Intervalos[0].Inicio:corte});
  var consumption=events.Select(x=>JsonNode.Parse(x)).OfType<JsonObject>().Where(r=>B(r,"consumo")).ToArray();
  return ubicaciones.Select(u=>{
   var m=MaterialUbicacion(e,u);var id=S(u,"id");var cs=consumption.Where(c=>S(c,"ubicacion")==id).Select((c,i)=>new ConsumoCobertura(S(c,"operacion"),1,S(m,"numeroParte"),"V2","SALIDA",N(c,"piezas"),DateTime.Parse(S(c,"fecha"),null,DateTimeStyles.RoundtripKind),true));
   var apertura=e.Apertura;if(DateTime.TryParse(S(u,"apertura"),null,DateTimeStyles.RoundtripKind,out var ua))apertura=ua;
   var r=MotorCobertura.Evaluar(N(u,"cantidad"),1,S(m,"numeroParte"),corte,ventana,apertura,cs,"V2");
   if(r.Minutos is decimal minutes&&r.Estado!="AGOTADO"&&cal is not null){var status=CalendarioV2.Clasificar(minutes,cal);r=r with{Estado=status,Prioridad=status=="NORMAL"?"NORMAL":status=="ADVERTENCIA"?"MEDIA":"ALTA / CRÍTICA"};}
   return (object)new{ubicacion=id,material=S(m,"id"),resultado=r,referencia=new{corte,ventana.Intervalos,minutosLaborables=ventana.Minutos,fuente=cal is null?null:S(cal,"fuente"),simulado=cal is not null&&B(cal,"simulado"),ultimoMinutoLaborable=ventana.Intervalos.LastOrDefault()?.Fin}};
  }).ToArray();
 }
 public async Task<JsonElement> Ejecutar(Cuenta actor,int linea,string tipo,JsonObject input,Guid key)
 {
  Exigir(key!=Guid.Empty,"Falta identificador de operación.");var original=await Leer(actor,linea);var huella=Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(tipo+"|"+linea+"|"+input.ToJsonString())));
  await using var sql=await store.Open();var previo=await sql.QuerySingleOrDefaultAsync<string>("mes2.Resultado",new{usuario=actor.Id,linea,id=key,huella},commandType:CommandType.StoredProcedure);if(previo is not null)return JsonSerializer.Deserialize<JsonElement>(previo);
  Exigir(original.Rol=="ADMIN"||original.Rol=="PUESTO"&&tipo is "ENTRADA" or "ATENCION_INICIAR" or "ATENCION_FINALIZAR","Operación no autorizada.",403);
  Exigir(Entero(input,"version",0,int.MaxValue)==original.Version,"Los datos cambiaron. Actualiza y revisa la operación.",409);
  var e=new V2Estado(linea,original.Version,original.Apertura,original.Ahora,original.Rol,original.Puesto,original.Datos.ToDictionary(k=>k.Key,k=>Copia(k.Value)));
  JsonObject resultado=new(){["operacion"]=key.ToString(),["tipo"]=tipo,["fecha"]=original.Ahora.ToString("O"),["linea"]=linea,["responsable"]=actor.Name,["rol"]=original.Rol};
  switch(tipo)
  {
   case "IMPORTAR":Importar(e,input,resultado);break;
   case "MATERIAL_ESTADO":case "MATERIAL_ELIMINAR":case "MATERIAL_RENOMBRAR":
   {
    Admin(e);var m=e.Obtener("material",Texto(input,"id",100,true));var motivo=Texto(input,"motivo",250,true);resultado["motivo"]=motivo;resultado["numeroParte"]=S(m,"numeroParte");
    if(tipo=="MATERIAL_ESTADO"){Exigir(input["activo"] is JsonValue av&&av.TryGetValue<bool>(out _),"Indica el estado del material.");m["activo"]=B(input,"activo");}
    if(tipo=="MATERIAL_RENOMBRAR"){var n=Texto(input,"numeroParte",80,true);Exigir(!e.Todos("material").Any(x=>S(x,"numeroParte")==n&&S(x,"id")!=S(m,"id")),"Número de parte ya existente.",409);m["numeroParte"]=n;}
    if(tipo=="MATERIAL_ELIMINAR"){
     Exigir(B(input,"confirmar"),"Confirma la eliminación.");Exigir(!e.Todos("ubicacion").Any(u=>S(u,"material")==S(m,"id"))&&!e.Todos("atencion").Any(a=>S(a,"material")==S(m,"id")),"El material tiene ubicaciones o atenciones. Inhabilítalo.",409);
     var refs=await sql.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM mes2.IMAGEN WHERE linea=@linea AND material=@material",new{linea,material=S(m,"id")});Exigir(refs==0,"El material tiene una imagen asociada. Inhabilítalo.",409);
     // Las atenciones terminadas ya no vienen en el estado: se revisan en SQL para no dejar historial huérfano.
     var atencionesCerradas=await sql.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM mes2.ENTIDAD WHERE linea=@linea AND tipo='atencion' AND JSON_VALUE(datos,'$.material')=@material",new{linea,material=S(m,"id")});Exigir(atencionesCerradas==0,"El material tiene atenciones registradas. Inhabilítalo.",409);
     e.Datos.Remove("material:"+S(m,"id"));
    }break;
   }
   case "MATERIAL_DUPLICAR":DuplicarMaterial(e,input,resultado);break;
   case "UBICACION_ESTADO":EstadoUbicacion(e,input,resultado);break;
   case "PUESTO":case "ESTACION":case "ESTANTERIA":case "UBICACION":
   {
    Admin(e);var id=Texto(input,"id",100);var kind=tipo.ToLowerInvariant();JsonObject o=id.Length>0?e.Obtener(kind,id):new(){["id"]=Nuevo()};
    if(tipo is "PUESTO" or "ESTACION"){o["nombre"]=Texto(input,"nombre",80,true);Exigir(!e.Todos(kind).Any(x=>S(x,"nombre")==S(o,"nombre")&&S(x,"id")!=S(o,"id")),"Nombre ya existente.",409);}
    if(tipo=="ESTANTERIA"){
     e.Obtener("estacion",Texto(input,"estacion",100,true));o["estacion"]=S(input,"estacion");o["nombre"]=Texto(input,"nombre",80,true);o["nivel"]=Texto(input,"nivel",40,true);o["capacidad"]=Entero(input,"capacidad",1,1000000);
     Exigir(N(o,"capacidad")>=Ocupacion(e,S(o,"id")),"La capacidad no puede ser menor que las cajas ocupadas.",409);
     Exigir(!e.Todos(kind).Any(x=>S(x,"estacion")==S(o,"estacion")&&S(x,"nombre")==S(o,"nombre")&&S(x,"nivel")==S(o,"nivel")&&S(x,"id")!=S(o,"id")),"Estantería y nivel ya existentes.",409);
    }
    if(tipo=="UBICACION"){
     Exigir(id.Length==0,"Una ubicación existente no puede reasignarse; conserva su historial.",409);var m=e.Obtener("material",Texto(input,"material",100,true));Exigir(B(m,"activo",true),"El material está inhabilitado.",409);e.Obtener("estanteria",Texto(input,"estanteria",100,true));
     var previa=e.Todos(kind).FirstOrDefault(x=>S(x,"material")==S(input,"material")&&S(x,"estanteria")==S(input,"estanteria"));
     Exigir(previa is null||Activa(previa),"Esa asignación existe desactivada: reactívala en lugar de crear otra.",409);Exigir(previa is null,"Asignación ya existente.",409);
     o["material"]=S(input,"material");o["estanteria"]=S(input,"estanteria");o["cantidad"]=0;o["apertura"]=e.Ahora.ToString("O");o["activa"]=true;
    }e.Poner(kind,o);resultado["id"]=S(o,"id");break;
   }
   case "ENTRADA":case "SALIDA":
   {
    var u=e.Obtener("ubicacion",Texto(input,"ubicacion",100,true));var m=MaterialUbicacion(e,u);Puesto(e,m);var cajas=Entero(input,"cajas",1,1000000);var saldo=N(u,"cantidad");var empaque=N(m,"empaque");var entrada=tipo=="ENTRADA";
    Exigir(!entrada||B(m,"activo",true),"El material está inhabilitado: solo se permite retirar su remanente.",409);Exigir(!entrada||Activa(u),"La ubicación está desactivada: no admite entradas.",409);Exigir(!entrada||!B(input,"consumo"),"Una entrada no es consumo.");Exigir(entrada||cajas<=(saldo+(long)empaque-1)/empaque,"No hay suficientes cajas.",409);
    var piezas=entrada?checked((long)cajas*empaque):Math.Min((long)cajas*empaque,saldo);var nuevo=saldo+(entrada?piezas:-piezas);Exigir(piezas>0&&nuevo>=0&&nuevo<=int.MaxValue,"Cantidad fuera de rango.");
    u["cantidad"]=(int)nuevo;var rack=e.Obtener("estanteria",S(u,"estanteria"));Exigir(Ocupacion(e,S(rack,"id"))<=N(rack,"capacidad"),"La estantería no tiene espacio.",409);
    resultado["ubicacion"]=S(u,"id");resultado["material"]=S(m,"id");resultado["numeroParte"]=S(m,"numeroParte");resultado["piezas"]=(int)piezas;resultado["saldoAnterior"]=saldo;resultado["saldoPosterior"]=(int)nuevo;resultado["consumo"]=B(input,"consumo");
    var atencion=Texto(input,"atencion",100);if(atencion.Length>0){var a=e.Obtener("atencion",atencion);Exigir(S(a,"ubicacion")==S(u,"id")&&S(a,"fin")==""&&(e.Rol=="ADMIN"||N(a,"usuario")==actor.Id),"La atención debe estar abierta en esta ubicación y pertenecer a tu cuenta.",409);resultado["atencion"]=atencion;
     // Cierra la atención en la misma operación (evita atenciones olvidadas que siguen contando tiempo).
     if(B(input,"finalizarAtencion")){Exigir(e.Ahora>=DateTime.Parse(S(a,"inicio"),null,DateTimeStyles.RoundtripKind),"Fecha inconsistente.");a["fin"]=e.Ahora.ToString("O");resultado["atencionFinalizada"]=true;}}

    break;
   }
   case "ATENCION_INICIAR":
   {
    var u=e.Obtener("ubicacion",Texto(input,"ubicacion",100,true));var m=MaterialUbicacion(e,u);Puesto(e,m);Exigir(B(m,"activo",true),"Material inhabilitado.",409);Exigir(Activa(u),"La ubicación está desactivada.",409);
    Exigir(!e.Todos("atencion").Any(a=>S(a,"fin")==""&&S(a,"ubicacion")==S(u,"id")),"Ya hay una atención abierta para esta ubicación.",409);
    var a=new JsonObject{["id"]=Nuevo(),["material"]=S(m,"id"),["ubicacion"]=S(u,"id"),["puesto"]=S(m,"puesto"),["responsable"]=actor.Name,["usuario"]=actor.Id,["inicio"]=e.Ahora.ToString("O"),["fin"]=null,["turno"]=Turno(e.Ahora)};
    e.Poner("atencion",a);resultado["id"]=S(a,"id");break;
   }
   case "ATENCION_FINALIZAR":
   {
    var a=e.Obtener("atencion",Texto(input,"id",100,true));Puesto(e,e.Obtener("material",S(a,"material")));Exigir(e.Rol=="ADMIN"||N(a,"usuario")==actor.Id,"Atención de otra cuenta.",403);Exigir(S(a,"fin")=="","La atención ya terminó.",409);
    var inicio=DateTime.Parse(S(a,"inicio"),null,DateTimeStyles.RoundtripKind);Exigir(e.Ahora>=inicio,"Fecha inconsistente.");a["fin"]=e.Ahora.ToString("O");resultado["id"]=S(a,"id");break;
   }
   case "CALENDARIO":Admin(e);CalendarioV2.Validar(input);var cal=Copia(input);cal["id"]="config";cal.Remove("version");e.Poner("calendario",cal);break;
   default:throw new RuleException("Operación no admitida.",400);
  }
  ValidarIntegridad(e);
  var cambios=e.Datos.Where(p=>!original.Datos.TryGetValue(p.Key,out var old)||!JsonNode.DeepEquals(old,p.Value)).Select(p=>new V2Cambio(p.Key.Split(':',2)[0],S(p.Value,"id"),p.Value,original.Datos.GetValueOrDefault(p.Key))).Concat(original.Datos.Where(p=>!e.Datos.ContainsKey(p.Key)).Select(p=>new V2Cambio(p.Key.Split(':',2)[0],S(p.Value,"id"),null,p.Value))).ToArray();
  var result=await sql.QuerySingleAsync<string>("mes2.Guardar",new{usuario=actor.Id,linea,version=original.Version,id=key,huella,tipo,solicitud=input.ToJsonString(),cambios=JsonSerializer.Serialize(cambios,new JsonSerializerOptions{PropertyNamingPolicy=JsonNamingPolicy.CamelCase}),resultado=resultado.ToJsonString()},commandType:CommandType.StoredProcedure);
  return JsonSerializer.Deserialize<JsonElement>(result);
 }
 public async Task<object> VistaPrevia(Cuenta actor,int linea,JsonObject input)
 {
  var original=await Leer(actor,linea);Admin(original);Exigir(Entero(input,"version",0,int.MaxValue)==original.Version,"El catálogo cambió; actualiza la vista previa.",409);
  var e=new V2Estado(linea,original.Version,original.Apertura,original.Ahora,original.Rol,original.Puesto,original.Datos.ToDictionary(k=>k.Key,k=>Copia(k.Value)));
  var errores=new List<string>();try{Importar(e,input,new());ValidarIntegridad(e);}catch(RuleException ex){errores.Add(ex.Message);}
  return new{valida=errores.Count==0,errores,linea,version=original.Version,hoja=S(input,"hoja")};
 }
 static void Importar(V2Estado e,JsonObject input,JsonObject resultado)
 {
  Admin(e);var filas=input["filas"] as JsonArray??throw new RuleException("Faltan filas.",400);Exigir(filas.Count is >0 and <=500,"El lote debe contener entre 1 y 500 filas.");var vistos=new HashSet<string>(StringComparer.Ordinal);int altas=0,cambios=0,puestosNuevos=0;
  int fila=0;foreach(var node in filas){fila++;try{var f=Copia(node as JsonObject??throw new RuleException("Fila inválida.",400));var numero=Texto(f,"numeroParte",80,true);Exigir(!numero.StartsWith("[FORMULA",StringComparison.Ordinal),"No se aceptan fórmulas como número de parte.");Exigir(vistos.Add(numero),"Número de parte repetido en el lote: "+numero);
   if(f.ContainsKey("puestoNuevo")){
    Exigir(!f.ContainsKey("puesto"),"Indica puesto o puestoNuevo, no ambos.");var nombre=Texto(f,"puestoNuevo",80,true);
    Exigir(nombre!="[BORRAR]"&&!nombre.StartsWith("[FORMULA",StringComparison.Ordinal),"Puesto nuevo inválido; usa un nombre de texto.");
    var encontrados=e.Todos("puesto").Where(p=>S(p,"nombre")==nombre).ToArray();Exigir(encontrados.Length<=1,"Puesto ambiguo: "+nombre+". Usa el identificador exacto.");
    JsonObject puesto;if(encontrados.Length==1)puesto=encontrados[0];else{
     Exigir(B(input,"permitirPuestosNuevos"),"Autoriza crear el puesto faltante en esta línea: "+nombre);
     puesto=new(){["id"]=Nuevo(),["nombre"]=nombre};e.Poner("puesto",puesto);puestosNuevos++;
    }f["puesto"]=S(puesto,"id");f.Remove("puestoNuevo");
   }
   var id=Texto(f,"id",100);var m=id.Length>0?e.Obtener("material",id):e.Todos("material").SingleOrDefault(x=>S(x,"numeroParte")==numero);
   if(m is null){Exigir(B(input,"permitirAltas"),"Material nuevo requiere autorizar altas: "+numero);Exigir(!e.Todos("material").Any(x=>NormalCodigo(S(x,"numeroParte"))==NormalCodigo(numero))||B(input,"autorizarNomenclaturas"),"Código parecido a otro material: revisa y autoriza el código distinto.");Exigir(f["empaque"] is not null,"Material nuevo: indica piezas por caja; no se supone un empaque.");m=new(){["id"]=Nuevo(),["numeroParte"]=numero,["activo"]=true,["empaque"]=1,["minimo"]=0,["descripcion"]="",["variante"]="",["uso"]="",["revision"]="",["modelo"]=""};altas++;}
   else{Exigir(S(m,"numeroParte")==numero,"Renombra mediante la acción explícita; no por importación.",409);cambios++;}
   foreach(var campo in new[]{"descripcion","variante","uso","revision","modelo","puesto"})if(f.ContainsKey(campo)&&f[campo] is not null){
    var valor=Texto(f,campo,campo=="descripcion"?250:80);if(campo=="puesto"){e.Obtener("puesto",valor);Exigir(S(m,"puesto")==""||S(m,"puesto")==valor||!e.Todos("ubicacion").Any(u=>S(u,"material")==S(m,"id")),"No se puede reasignar un material con ubicaciones; conserva sus relaciones.",409);}
    if(campo is "variante" or "uso" or "revision" or "modelo")Exigir(valor.Length==0||e.Todos("material").Any(x=>S(x,campo)==valor)||B(input,"autorizarNomenclaturas"),"Autoriza el nuevo valor de "+campo+": "+valor);
    m[campo]=valor;
   }
   foreach(var campo in new[]{"empaque","minimo"})if(f.ContainsKey(campo)&&f[campo] is not null)m[campo]=Entero(f,campo,campo=="empaque"?1:0,campo=="empaque"?1000000:100000000);
   e.Obtener("puesto",S(m,"puesto"));e.Poner("material",m);
  }catch(RuleException ex){throw new RuleException("Hoja "+S(input,"hoja","seleccionada")+", fila "+(node is JsonObject f?N(f,"fila",fila):fila)+": "+ex.Message,ex.Status);}}resultado["puestosCreados"]=puestosNuevos;resultado["altas"]=altas;resultado["actualizados"]=cambios;resultado["filas"]=filas.DeepClone();
 }
 static string NormalCodigo(string s)=>new(s.Normalize(NormalizationForm.FormD).ToUpperInvariant().Where(char.IsLetterOrDigit).ToArray());
 internal static void ValidarIntegridad(V2Estado e)
 {
  Exigir(!e.Todos("material").GroupBy(m=>S(m,"numeroParte"),StringComparer.Ordinal).Any(g=>g.Count()>1),"Números de parte duplicados.",409);
  foreach(var m in e.Todos("material")){e.Obtener("puesto",S(m,"puesto"));Exigir(N(m,"empaque")>0&&N(m,"minimo")>=0,"Empaque o mínimo inválido.");}
  foreach(var u in e.Todos("ubicacion")){MaterialUbicacion(e,u);e.Obtener("estanteria",S(u,"estanteria"));Exigir(N(u,"cantidad")>=0,"Saldo negativo.");}
  foreach(var r in e.Todos("estanteria")){e.Obtener("estacion",S(r,"estacion"));Exigir(Ocupacion(e,S(r,"id"))<=N(r,"capacidad"),"El cambio excede la capacidad de una estantería.",409);}
 }
 public async Task<object> Historial(Cuenta actor,int linea,int pagina)
 {
  await Leer(actor,linea);Exigir(pagina is >0 and <=50000,"Página inválida.");await using var sql=await store.Open();var datos=await sql.QueryAsync("SELECT secuencia,id,tipo,fecha,cuenta,solicitud,resultado FROM mes2.EVENTO WHERE linea=@linea ORDER BY secuencia DESC OFFSET @offset ROWS FETCH NEXT 50 ROWS ONLY",new{linea,offset=(pagina-1)*50});return new{pagina,total=await sql.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM mes2.EVENTO WHERE linea=@linea",new{linea}),filas=datos};
 }
 public async Task<IResult> Imagen(Cuenta actor,int linea,string material)
 {
  var e=await Leer(actor,linea);Puesto(e,e.Obtener("material",material));await using var sql=await store.Open();var r=await sql.QuerySingleOrDefaultAsync("SELECT tipo,bytes FROM mes2.IMAGEN WHERE linea=@linea AND material=@material",new{linea,material});return r is null?Results.NotFound():Results.File((byte[])r.bytes,(string)r.tipo);
 }
 public async Task Foto(Cuenta actor,int linea,string material,HttpRequest request,bool eliminar)
 {
  var e=await Leer(actor,linea);Admin(e);e.Obtener("material",material);byte[]? bytes=null;var tipo="image/png";
  if(!eliminar){Exigir(request.ContentLength is >0 and <=5242880,"La imagen debe pesar hasta 5 MB.");using var ms=new MemoryStream();await request.Body.CopyToAsync(ms,request.HttpContext.RequestAborted);bytes=ms.ToArray();Exigir(bytes.Length is >0 and <=5242880,"La imagen debe pesar hasta 5 MB.");var png=bytes.Length>8&&bytes.AsSpan(0,8).SequenceEqual(new byte[]{137,80,78,71,13,10,26,10});var jpg=bytes.Length>3&&bytes[0]==255&&bytes[1]==216&&bytes[2]==255;Exigir(png||jpg,"Solo se aceptan PNG o JPG.");tipo=png?"image/png":"image/jpeg";}
  await using var sql=await store.Open();await sql.ExecuteAsync("mes2.Foto",new{usuario=actor.Id,linea,material,tipo,bytes},commandType:CommandType.StoredProcedure);
 }
 public static int Turno(DateTime utc){var h=TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utc,DateTimeKind.Utc),TimeZoneInfo.FindSystemTimeZoneById("America/Mexico_City")).Hour;return h is >=6 and <15?1:2;}
}
