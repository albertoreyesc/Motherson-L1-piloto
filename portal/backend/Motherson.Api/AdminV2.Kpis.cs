using Dapper;
using System.Globalization;
using System.Text.Json.Nodes;
namespace Motherson;
public sealed partial class AdminV2
{
 public async Task<object> ActividadDisponible(Cuenta actor,int linea)
 {
  var e=await Leer(actor,linea);var ms=e.Todos("material").Where(m=>e.Puesto is null||S(m,"puesto")==e.Puesto).ToArray();var ids=ms.Select(m=>S(m,"id")).ToHashSet();var us=e.Todos("ubicacion").Where(u=>ids.Contains(S(u,"material"))).Select(u=>int.TryParse(S(u,"id"),out var id)?id:-1).Where(id=>id>=0).ToArray();
  var fechas=new List<DateTime>();var atenciones=e.Todos("atencion").Where(a=>ids.Contains(S(a,"material"))).ToArray();
  foreach(var a in atenciones){fechas.Add(ParseDate(S(a,"inicio")));if(S(a,"fin")!="")fechas.Add(ParseDate(S(a,"fin")));}
  long legado=0;var partes=ms.Select(m=>S(m,"numeroParteLegado")).Where(p=>p!="").ToArray();
  if(linea==1&&partes.Length>0&&us.Length>0){await using var sql=await store.Open();var r=await sql.QuerySingleAsync("SELECT COUNT_BIG(*) cantidad,MIN(fecha_hora_inicio) inicio,MAX(fecha_hora_fin) fin FROM dbo.vw_kpi_atenciones WHERE fecha_hora_fin IS NOT NULL AND numero_parte IN @partes AND id_ubicacion IN @us",new{partes,us});legado=(long)r.cantidad;if(r.inicio is DateTime start)fechas.Add(DateTime.SpecifyKind(start,DateTimeKind.Utc));if(r.fin is DateTime end)fechas.Add(DateTime.SpecifyKind(end,DateTimeKind.Utc));}
  return new{linea,total=legado+atenciones.Length,legado,nuevas=atenciones.Length,desde=fechas.Count>0?KpisMotor.Dia(fechas.Min()):null,hasta=fechas.Count>0?KpisMotor.Dia(fechas.Max()):null,zona="America/Mexico_City"};
 }
 static DateTime ParseDate(string text)=>DateTime.Parse(text,null,DateTimeStyles.RoundtripKind);
 public async Task<object> Kpis(Cuenta actor,int linea,IQueryCollection q)
 {
  DateTime corte,desde,hasta;try{
  corte=string.IsNullOrWhiteSpace(q["corte"])?DateTime.UtcNow:KpisMotor.FechaLocal(q["corte"].ToString());
  desde=KpisMotor.FechaLocal(q["desde"].ToString());hasta=KpisMotor.FechaLocal(q["hasta"].ToString()).AddDays(1);
  }catch(FormatException){throw new RuleException("Fechas inválidas.",400);}
  Exigir(corte<=DateTime.UtcNow.AddSeconds(2)&&hasta>desde&&(hasta-desde).TotalDays<=732,"Revisa corte y periodo (hasta dos años).");
  var e=await Leer(actor,linea,corte);var parte=q["material"].ToString();var estacion=q["estacion"].ToString();var turno=q["turno"].ToString();
  Exigir(turno is "" or "1" or "2","Turno inválido.");
  var ms=e.Todos("material").Where(m=>(e.Puesto is null||S(m,"puesto")==e.Puesto)&&(parte==""||S(m,"id")==parte)).ToArray();var ids=ms.Select(m=>S(m,"id")).ToHashSet();
  var us=e.Todos("ubicacion").Where(u=>ids.Contains(S(u,"material"))&&(estacion==""||S(e.Obtener("estanteria",S(u,"estanteria")),"estacion")==estacion)).ToArray();var uids=us.Select(u=>S(u,"id")).ToHashSet();
  var materiales=ms.Select(m=>{var sitios=us.Where(u=>S(u,"material")==S(m,"id")).ToArray();var saldo=sitios.Sum(u=>(long)N(u,"cantidad"));var minimo=N(m,"minimo");var estado=sitios.Length==0?"SIN_DATOS":saldo==0?"AGOTADO":saldo<=minimo?"CRITICO":saldo<=1.2m*minimo?"ADVERTENCIA":"NORMAL";return new{id=S(m,"id"),numeroParte=S(m,"numeroParte"),activo=B(m,"activo",true),saldo=sitios.Length==0?(long?)null:saldo,minimo,estado,ubicaciones=sitios.Length};}).ToArray();
  var todas=e.Todos("atencion").Where(a=>uids.Contains(S(a,"ubicacion"))&&(turno==""||N(a,"turno").ToString()==turno)).ToList();
  // El legado cerrado permanece en sus tablas y se consulta por periodo, no en cada apertura del portal.
  if(linea==1){
   var porLegado=ms.Where(m=>S(m,"numeroParteLegado")!="").ToDictionary(m=>S(m,"numeroParteLegado"),m=>S(m,"id"),StringComparer.Ordinal);
   if(porLegado.Count>0){await using var legacySql=await store.Open();
    var rows=await legacySql.QueryAsync("SELECT id_registro,numero_parte,id_ubicacion,id_puesto,puesto,fecha_hora_inicio,fecha_hora_fin FROM dbo.vw_kpi_atenciones WHERE fecha_hora_fin IS NOT NULL AND fecha_hora_inicio<=@corte AND fecha_hora_inicio<@hasta AND fecha_hora_fin>=@desde AND numero_parte IN @partes",new{corte,hasta,desde,partes=porLegado.Keys.ToArray()});
    foreach(var row in rows){var idUbicacion=((int)row.id_ubicacion).ToString();var inicio=DateTime.SpecifyKind((DateTime)row.fecha_hora_inicio,DateTimeKind.Utc);var turnoLegado=Turno(inicio);if(!uids.Contains(idUbicacion)||(turno!=""&&turnoLegado.ToString()!=turno))continue;
     todas.Add(new JsonObject{["id"]="legado-"+(int)row.id_registro,["material"]=porLegado[(string)row.numero_parte],["ubicacion"]=idUbicacion,["puesto"]=((int)row.id_puesto).ToString(),["responsable"]=(string)row.puesto,["usuario"]=0,["inicio"]=inicio.ToString("O"),["fin"]=DateTime.SpecifyKind((DateTime)row.fecha_hora_fin,DateTimeKind.Utc).ToString("O"),["turno"]=turnoLegado,["origen"]="REGISTRO_ORIGINAL"});
    }
   }
  }
  var ops=todas.Where(a=>ParseDate(S(a,"inicio"))<hasta&&(ParseDate(S(a,"inicio"))>=desde||S(a,"fin")==""||ParseDate(S(a,"fin"))>=desde)).ToArray();
  var iniciadas=ops.Where(a=>ParseDate(S(a,"inicio"))>=desde&&ParseDate(S(a,"inicio"))<hasta&&ParseDate(S(a,"inicio"))<=corte).ToArray();
  var completadas=ops.Where(a=>S(a,"fin")!=""&&ParseDate(S(a,"fin"))>=desde&&ParseDate(S(a,"fin"))<hasta&&ParseDate(S(a,"fin"))<=corte).ToArray();
  var abiertas=ops.Where(a=>ParseDate(S(a,"inicio"))<=corte&&(S(a,"fin")==""||ParseDate(S(a,"fin"))>corte)).ToArray();
  var intervalos=new List<double>();foreach(var g in iniciadas.GroupBy(a=>(S(a,"material"),S(e.Obtener("estanteria",S(e.Obtener("ubicacion",S(a,"ubicacion")),"estanteria")),"estacion")))){
   var sorted=g.Select(a=>ParseDate(S(a,"inicio"))).OrderBy(t=>t).ToArray();for(int i=1;i<sorted.Length;i++)intervalos.Add((sorted[i]-sorted[i-1]).TotalHours);
  }
  string Estacion(JsonObject a)=>S(e.Obtener("estacion",S(e.Obtener("estanteria",S(e.Obtener("ubicacion",S(a,"ubicacion")),"estanteria")),"estacion")),"nombre");
  var repeats=iniciadas.GroupBy(a=>new{dia=KpisMotor.Dia(ParseDate(S(a,"inicio"))),material=S(a,"material"),estacion=Estacion(a)}).Where(g=>g.Count()>1).Select(g=>new{g.Key.dia,g.Key.material,g.Key.estacion,cantidad=g.Count(),adicionales=g.Count()-1}).ToArray();
  return new{contexto=new{linea,corte,desde=q["desde"].ToString(),hasta=q["hasta"].ToString(),estacion,material=parte,turno,zona="America/Mexico_City",simulado=true,nota="Hoja 1: saldo al corte. Hoja 2: atenciones con inicio y fin reales; una importación o un movimiento no crea una atención."},
   estadoActual=new{inventario=us.Sum(u=>(long)N(u,"cantidad")),cajasEquivalentes=us.Sum(u=>(decimal)N(u,"cantidad")/N(e.Obtener("material",S(u,"material")),"empaque")),materiales,ubicaciones=us.Length,agotados=materiales.Count(m=>m.estado=="AGOTADO"),criticos=materiales.Count(m=>m.estado=="CRITICO"),advertencia=materiales.Count(m=>m.estado=="ADVERTENCIA"),sinDatos=materiales.Count(m=>m.estado=="SIN_DATOS")},
   actividad=new{completados=completadas.Length,enCurso=abiertas.Length,repetidos=repeats.Sum(r=>r.adicionales),incompletos=ops.Count(a=>S(a,"responsable")==""||!uids.Contains(S(a,"ubicacion"))),intervaloPromedioHoras=intervalos.Count>0?(double?)intervalos.Average():null,intervalosEvaluados=intervalos.Count,
    porDia=completadas.GroupBy(a=>KpisMotor.Dia(ParseDate(S(a,"fin")))).Select(g=>new{dia=g.Key,cantidad=g.Count()}),repeticiones=repeats,operaciones=ops,nota=ops.Length==0?"No hay atenciones registradas en el alcance. Inicia y finaliza una atención desde Entradas y salidas; editar materiales no genera actividad.":"Los tiempos provienen de las atenciones persistidas."}};
 }
}
