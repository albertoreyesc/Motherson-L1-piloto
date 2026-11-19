using Motherson;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Identity;

// Hash efímero para validar HTTP sin cambiar credenciales ni cuentas SQL.
if (args.Length == 2 && args[0] == "--hash-temporal")
{ Console.Write(new PasswordHasher<string>().HashPassword("", args[1])); return; }

var raiz = Path.Combine(Path.GetTempPath(), "motherson-diario-pruebas-" + Guid.NewGuid());
Directory.CreateDirectory(raiz);
var pasadas = new List<string>();
void Bien(bool valor, string caso) { if (!valor) throw new Exception(caso); pasadas.Add(caso); }
async Task Rechaza(Func<Task> accion, int estado, string caso)
{ try { await accion(); } catch (RuleException e) when(e.Status == estado) { pasadas.Add(caso); return; } throw new Exception(caso); }
void NoAbre(Action accion, string caso)
{ try { accion(); } catch (RuleException e) when (e.Status == 503) { pasadas.Add(caso); return; } throw new Exception(caso); }
var fecha = new DateTime(2026, 8, 19, 18, 0, 0, DateTimeKind.Utc);
InventarioAlarma Fila(int id, string parte, int puesto, int rack, int saldo, int caja=10, int capacidad=4) => new() {
 id_ubicacion=id, numero_parte=parte, id_puesto=puesto, puesto="P"+puesto, id_estanteria=rack,
 cantidad=saldo, stock_total_material=saldo, cantidad_por_caja=caja, cajas_estanteria=capacidad,
 fecha_stock=fecha, minimo=5, estacion="AL01", estanteria="E1", nivel="A", es_simulado=true };
BaseMovimientos NuevaBase() => new("PRUEBA_SIN_SQL", "HUELLA_DOCUMENTAL", fecha,
 [Fila(1,"000 PARTE A",1,1,10), Fila(2,"OTRA PARTE B",2,1,10), Fila(3,"PARTE C",1,2,30)]);
Cuenta admin = new(0,"admin.temporal",null,"ADMINISTRADOR","ADMIN_PUESTOS",null);
Cuenta puesto = new(1,"puesto.temporal",null,"PUESTO","PUESTO",1);
var origen = NuevaBase(); var actor=admin; var reloj=fecha.AddHours(1);
Task<(Cuenta Actor,BaseMovimientos Base)> Verificar() => Task.FromResult((actor,origen));
string Carpeta(string nombre) => Path.Combine(raiz,nombre);
// Lee el diario mientras el portal lo tiene abierto. En Windows File.ReadAllBytes falla
// porque pide que nadie más escriba el archivo; aquí se comparte la escritura.
byte[] LeerCompartido(string ruta)
{ using var f=new FileStream(ruta,FileMode.Open,FileAccess.Read,FileShare.ReadWrite); using var m=new MemoryStream(); f.CopyTo(m); return m.ToArray(); }

try
{
 ResultadoMovimiento entrada;
 var operacion=Guid.NewGuid();
 using(var d=new DiarioMovimientos(Carpeta("principal"),origen,()=>reloj))
 {
  Bien((await d.Leer()).Filas.Sum(f=>f.cantidad)==50,"La base se lee sin repetir el histórico");
  var antes=LeerCompartido(Path.Combine(Carpeta("principal"),"eventos.jsonl"));
  await d.Leer(); await d.Leer();
  Bien(antes.SequenceEqual(LeerCompartido(Path.Combine(Carpeta("principal"),"eventos.jsonl"))),"Consultar no escribe operaciones");
  entrada=await d.Registrar(new(1,"ENTRADA",2,1),admin,operacion,Verificar);
  Bien(entrada.CantidadMovimiento==20&&entrada.SaldoAnterior==10&&entrada.SaldoPosterior==30,"Entrada: dos cajas por diez piezas");
  Bien((await d.Leer()).Filas.First(f=>f.id_ubicacion==2).cajas_disponibles==0,"La capacidad considera otros materiales y puestos del rack");
  await Rechaza(()=>d.Registrar(new(2,"ENTRADA",1,1),admin,Guid.NewGuid(),Verificar),409,"Rechaza sobrellenar la capacidad compartida");
  var salida=await d.Registrar(new(1,"SALIDA",1,2,true),admin,Guid.NewGuid(),Verificar);
  Bien(salida.CantidadMovimiento==10&&salida.SaldoAnterior==30&&salida.SaldoPosterior==20,"Salida: una caja descuenta sus diez piezas");
  var reintento=await d.Registrar(new(1,"ENTRADA",2,1),admin,operacion,Verificar);
  Bien(reintento==entrada&&(await d.Leer()).Hechos.Length==2,"Respuesta perdida: reintento idéntico devuelve el mismo comprobante");
  await Rechaza(()=>d.Registrar(new(1,"ENTRADA",1,1),admin,operacion,Verificar),409,"UUID reutilizado con otro contenido se rechaza");
  await Rechaza(()=>d.Registrar(new(1,"SALIDA",1,1),admin,Guid.NewGuid(),Verificar),409,"Versión obsoleta se rechaza");
  await Rechaza(()=>d.Registrar(new(1,"SALIDA",3,3),admin,Guid.NewGuid(),Verificar),409,"Rechaza sacar más cajas de las que hay");
  foreach(var n in new[]{0,-1,1000001}) await Rechaza(()=>d.Registrar(new(1,"SALIDA",n,3),admin,Guid.NewGuid(),Verificar),400,"Rechaza cantidad inválida "+n);
  await Rechaza(()=>d.Registrar(new(1,"ENTRADA",1,3,true),admin,Guid.NewGuid(),Verificar),400,"Una reposición no es consumo");
  await Rechaza(()=>d.Registrar(new(1,"AJUSTE",1,3),admin,Guid.NewGuid(),Verificar),400,"Rechaza tipos no habilitados");
  await Rechaza(()=>d.Registrar(new(1,"SALIDA",1,3),admin,Guid.Empty,Verificar),400,"Rechaza UUID vacío");
  actor=puesto;
  await Rechaza(()=>d.Registrar(new(2,"ENTRADA",1,1),puesto,Guid.NewGuid(),Verificar),403,"Puesto no captura material de otro puesto");
  await Rechaza(()=>d.Registrar(new(1,"SALIDA",1,3),puesto,Guid.NewGuid(),Verificar),403,"Puesto no registra salidas");
  var propia=await d.Registrar(new(3,"ENTRADA",1,1),puesto,Guid.NewGuid(),Verificar);
  Bien(propia.SaldoPosterior==40,"El puesto registra entradas en su ámbito");
  await Rechaza(()=>d.Registrar(new(3,"ENTRADA",1,2),puesto,Guid.NewGuid(),()=>throw new RuleException("Acceso revocado",401)),401,"Se vuelve a comprobar la revocación dentro del bloqueo");
  actor=admin;
  reloj=fecha;
  await Rechaza(()=>d.Registrar(new(1,"SALIDA",1,3),admin,Guid.NewGuid(),Verificar),503,"Reloj que retrocede bloquea captura");
  reloj=fecha.AddHours(2);
  NoAbre(()=>new DiarioMovimientos(Carpeta("principal"),origen),"Segundo escritor del mismo diario se rechaza");
 }
 using(var d=new DiarioMovimientos(Carpeta("principal"),origen,()=>reloj))
 {
  Bien((await d.Leer()).Filas.Sum(f=>f.cantidad)==70,"Reinicio reconstruye saldos y todas las operaciones");
  Bien(await d.Registrar(new(1,"ENTRADA",2,1),admin,operacion,Verificar)==entrada,"La idempotencia persiste al reiniciar");
  var paralelo=await Task.WhenAll(Enumerable.Range(0,2).Select(async _=>{
   try { await d.Registrar(new(1,"SALIDA",1,3),admin,Guid.NewGuid(),Verificar);return true; }
   catch(RuleException e) when(e.Status==409){return false;}
  }));
  Bien(paralelo.Count(x=>x)==1&&(await d.Leer()).Filas.First(f=>f.id_ubicacion==1).cantidad==10,"Dos confirmaciones con la misma versión: solo una aplica");
  var misma=Guid.NewGuid();var cuerpo=new MovimientoNuevo(1,"SALIDA",1,4);
  var dobles=await Task.WhenAll(d.Registrar(cuerpo,admin,misma,Verificar),d.Registrar(cuerpo,admin,misma,Verificar));
  Bien(dobles[0]==dobles[1]&&(await d.Leer()).Filas.First(f=>f.id_ubicacion==1).cantidad==0,"Dos confirmaciones del mismo UUID no duplican descuento");
  var estado=await d.Leer();
  Bien(estado.Hechos.Count(h=>h.Captura.EsConsumo)==1,"Los retiros administrativos no se convierten en consumo");
  var lineas=Encoding.UTF8.GetString(LeerCompartido(Path.Combine(Carpeta("principal"),"eventos.jsonl"))).Split('\n',StringSplitOptions.RemoveEmptyEntries);
  Bien(!string.Join('\n',lineas).Contains("saldoAnterior")&&!string.Join('\n',lineas).Contains("saldoPosterior"),"El diario no duplica saldos calculados por operación");
  var cambio=origen with{HuellaHistorico="OTRA"};
  try{d.VerificarBase(cambio);throw new Exception("Base cambiada");}catch(RuleException e)when(e.Status==503){pasadas.Add("Cambio de fuente bloquea sin mezclar saldos");}
 }
 var fuente=Path.Combine(Carpeta("principal"),"eventos.jsonl");
 Directory.CreateDirectory(Carpeta("truncado"));File.Copy(fuente,Path.Combine(Carpeta("truncado"),"eventos.jsonl"));
 using(var s=new FileStream(Path.Combine(Carpeta("truncado"),"eventos.jsonl"),FileMode.Open,FileAccess.Write))s.SetLength(s.Length-1);
 NoAbre(()=>new DiarioMovimientos(Carpeta("truncado"),origen),"Última línea incompleta bloquea la reapertura");
 Directory.CreateDirectory(Carpeta("alterado"));File.WriteAllText(Path.Combine(Carpeta("alterado"),"eventos.jsonl"),File.ReadAllText(fuente).Replace("PORTAL_CONTINUACION_V1","PORTAL_CONTINUACION_V2"));
 NoAbre(()=>new DiarioMovimientos(Carpeta("alterado"),origen),"Contenido modificado falla por SHA-256");
 var desconocida=NuevaBase();desconocida.Filas[0].cantidad=null;
 using(var d=new DiarioMovimientos(Carpeta("sin-saldo"),desconocida,()=>reloj))
  await Rechaza(()=>d.Registrar(new(1,"ENTRADA",1,1),admin,Guid.NewGuid(),()=>Task.FromResult((admin,desconocida))),409,"No reparte automáticamente un saldo desconocido");
 var sinEmpaque=NuevaBase();sinEmpaque.Filas[0].cantidad_por_caja=null;
 using(var d=new DiarioMovimientos(Carpeta("sin-empaque"),sinEmpaque,()=>reloj))
  await Rechaza(()=>d.Registrar(new(1,"ENTRADA",1,1),admin,Guid.NewGuid(),()=>Task.FromResult((admin,sinEmpaque))),400,"No ingresa cajas sin empaque documentado");
 var sinCapacidad=NuevaBase();sinCapacidad.Filas[0].cajas_estanteria=null;
 using(var d=new DiarioMovimientos(Carpeta("sin-capacidad"),sinCapacidad,()=>reloj))
  await Rechaza(()=>d.Registrar(new(1,"ENTRADA",1,1),admin,Guid.NewGuid(),()=>Task.FromResult((admin,sinCapacidad))),409,"Capacidad compartida desconocida no habilita entrada");

 // Caja incompleta y ocupación discreta (regla de caja completa, 17/11/2026).
 {
  var parcial=NuevaBase(); parcial.Filas[2].cantidad=25; parcial.Filas[2].stock_total_material=25; parcial.Filas[2].cajas_estanteria=3;
  Task<(Cuenta Actor,BaseMovimientos Base)> VerParcial()=>Task.FromResult((admin,parcial));
  using var d=new DiarioMovimientos(Carpeta("caja-incompleta"),parcial,()=>reloj);
  Bien((await d.Leer()).Filas.First(f=>f.id_ubicacion==3).cajas_disponibles==0,"Una caja con piezas sueltas ocupa el lugar de una caja completa");
  await Rechaza(()=>d.Registrar(new(3,"ENTRADA",1,1),admin,Guid.NewGuid(),VerParcial),409,"Sin espacio libre se rechaza la caja nueva aunque sobren piezas sueltas");
  await Rechaza(()=>d.Registrar(new(3,"SALIDA",4,1),admin,Guid.NewGuid(),VerParcial),409,"25 piezas son 3 cajas: no se pueden sacar 4");
  var una=await d.Registrar(new(3,"SALIDA",1,1),admin,Guid.NewGuid(),VerParcial);
  Bien(una.CantidadMovimiento==10&&una.SaldoPosterior==15,"Sale primero una caja cerrada completa");
  Bien((await d.Leer()).Filas.First(f=>f.id_ubicacion==3).cajas_disponibles==1,"Al salir la caja se libera su lugar");
  var resto=await d.Registrar(new(3,"SALIDA",2,2),admin,Guid.NewGuid(),VerParcial);
  Bien(resto.CantidadMovimiento==15&&resto.SaldoPosterior==0,"La última caja abierta sale con sus piezas sueltas, sin saldo negativo");
  await Rechaza(()=>d.Registrar(new(3,"SALIDA",1,3),admin,Guid.NewGuid(),VerParcial),409,"Sin existencias no hay cajas que sacar");
 }

 // Compatibilidad: una salida guardada antes del 17/11/2026 (en piezas y sin empaque) se sigue leyendo en piezas.
 {
  var vieja=NuevaBase(); vieja.Filas[2].cantidad=30; vieja.Filas[2].stock_total_material=30;
  using(var d=new DiarioMovimientos(Carpeta("salida-anterior"),vieja,()=>reloj))
   await d.Registrar(new(3,"SALIDA",2,1),admin,Guid.NewGuid(),()=>Task.FromResult((admin,vieja)));
  var ruta=Path.Combine(Carpeta("salida-anterior"),"eventos.jsonl"); var anterior=new string('0',64); var nuevas=new List<string>();
  foreach(var linea in File.ReadAllLines(ruta)){
   var sobre=JsonNode.Parse(linea)!.AsObject(); var datos=sobre["datos"]!.AsObject(); var tipo=(string)sobre["tipo"]!;
   if(tipo=="MOVIMIENTO"){ datos["piezasPorCaja"]=null; datos.Remove("enCajas"); datos["unidad"]="PIEZAS"; }
   var texto=datos.ToJsonString(); var huella=Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(tipo+"\n"+anterior+"\n"+texto)));
   nuevas.Add("{\"version\":1,\"tipo\":\""+tipo+"\",\"datos\":"+texto+",\"anterior\":\""+anterior+"\",\"huella\":\""+huella+"\"}"); anterior=huella; }
  File.WriteAllText(ruta,string.Join("\n",nuevas)+"\n");
  using(var d=new DiarioMovimientos(Carpeta("salida-anterior"),vieja,()=>reloj)){
   var e=await d.Leer();
   Bien(e.Filas.First(f=>f.id_ubicacion==3).cantidad==28&&e.Hechos[0].Unidad=="PIEZAS"&&e.Piezas(e.Hechos[0])==2,"Una salida anterior en piezas se reconstruye igual que antes");
  }
 }

 // Base compartida (espejo): SQL recibe cada movimiento y debe coincidir con saldo inicial + diario.
 {
  var sql=NuevaBase(); var llamadas=0; var fallar=false;
  BaseMovimientos Sql()=>JsonSerializer.Deserialize<BaseMovimientos>(JsonSerializer.Serialize(sql))!;
  Task<(Cuenta Actor,BaseMovimientos Base)> VerSql()=>Task.FromResult((admin,Sql()));
  // Imita dbo.RegistrarMovimiento: suma a la ubicación y recalcula el total del material.
  Task Aplicar(int ubicacion,int piezas){
   if(fallar) throw new RuleException("SQL no disponible",503);
   llamadas++; var f=sql.Filas.Single(x=>x.id_ubicacion==ubicacion); f.cantidad+=piezas;
   foreach(var m in sql.Filas.Where(x=>x.numero_parte==f.numero_parte)) m.stock_total_material=sql.Filas.Where(x=>x.numero_parte==f.numero_parte).Sum(x=>(long)x.cantidad!.Value);
   return Task.CompletedTask; }
  var relojEspejo=fecha.AddHours(2); var op=Guid.NewGuid();
  using(var d=new DiarioMovimientos(Carpeta("espejo"),Sql(),()=>relojEspejo,espejo:true))
  {
   var r=await d.Registrar(new(1,"ENTRADA",2,1),admin,op,VerSql,Aplicar);
   Bien(r.SaldoPosterior==30&&sql.Filas[0].cantidad==30&&sql.Filas[0].stock_total_material==30&&llamadas==1,"Espejo: la entrada se aplica una vez en SQL");
   await d.Registrar(new(1,"ENTRADA",2,1),admin,op,VerSql,Aplicar);
   Bien(llamadas==1,"Espejo: el reintento con el mismo UUID no repite el movimiento en SQL");
   var s=await d.Registrar(new(1,"SALIDA",1,2),admin,Guid.NewGuid(),VerSql,Aplicar);
   Bien(s.SaldoPosterior==20&&sql.Filas[0].cantidad==20&&llamadas==2,"Espejo: la salida en cajas resta sus piezas en SQL");
   d.VerificarBase(Sql()); pasadas.Add("Espejo: SQL coincide con saldo inicial más diario");
   fallar=true;
   await Rechaza(()=>d.Registrar(new(1,"SALIDA",1,3),admin,Guid.NewGuid(),VerSql,Aplicar),503,"Espejo: si SQL falla no se registra el movimiento");
   fallar=false;
   Bien((await d.Leer()).Hechos.Length==2&&sql.Filas[0].cantidad==20,"Espejo: el fallo de SQL no deja rastro en el diario");
   sql.Filas[2].cantidad=999;
   await Rechaza(()=>d.Registrar(new(1,"SALIDA",1,3),admin,Guid.NewGuid(),VerSql,Aplicar),503,"Espejo: un cambio externo en SQL bloquea la captura");
   sql.Filas[2].cantidad=30;
  }
  using(var d=new DiarioMovimientos(Carpeta("espejo"),Sql(),()=>relojEspejo,espejo:true))
   Bien((await d.Leer()).Filas[0].cantidad==20,"Espejo: reabre cuando SQL ya contiene los movimientos");
  NoAbre(()=>new DiarioMovimientos(Carpeta("espejo"),NuevaBase(),()=>relojEspejo,espejo:true),"Espejo: no reabre si SQL no contiene los movimientos del diario");
 }
 // Ajuste de capacidad (+ / −) registrado en el diario, 17/11/2026.
 {
  var cap=NuevaBase(); Task<(Cuenta Actor,BaseMovimientos Base)> VerCap()=>Task.FromResult((admin,cap));
  CambioCapacidad[] Cap(int anterior,int nueva)=>[new(1,anterior,nueva)];
  var idAjuste=Guid.NewGuid();
  using(var d=new DiarioMovimientos(Carpeta("capacidad"),cap,()=>reloj))
  {
   Bien((await d.Leer()).Filas.First(f=>f.id_ubicacion==1).cajas_disponibles==2,"Capacidad: 4 cajas con 2 ocupadas dejan 2 libres");
   var a=await d.RegistrarAjuste(Cap(4,6),admin,idAjuste,VerCap);
   var e=await d.Leer();
   Bien(e.Filas.Where(f=>f.id_estanteria==1).All(f=>f.cajas_estanteria==6&&f.cajas_disponibles==4)&&e.Ajustes.Length==1,"Capacidad: subir a 6 cajas deja 4 libres en toda la estantería");
   Bien(await d.RegistrarAjuste(Cap(4,6),admin,idAjuste,VerCap)==a&&(await d.Leer()).Ajustes.Length==1,"Capacidad: el reintento con el mismo UUID no duplica el ajuste");
   await Rechaza(()=>d.RegistrarAjuste(Cap(4,5),admin,Guid.NewGuid(),VerCap),409,"Capacidad: se rechaza si cambió mientras se editaba");
   await Rechaza(()=>d.RegistrarAjuste(Cap(6,1),admin,Guid.NewGuid(),VerCap),409,"Capacidad: no puede quedar menor que las cajas ocupadas");
   await Rechaza(()=>d.RegistrarAjuste(Cap(6,0),admin,Guid.NewGuid(),VerCap),400,"Capacidad: rechaza cero");
   await Rechaza(()=>d.RegistrarAjuste([new(99,4,5)],admin,Guid.NewGuid(),VerCap),400,"Capacidad: rechaza una estantería inexistente");
   await Rechaza(()=>d.RegistrarAjuste(Cap(6,7),puesto,Guid.NewGuid(),()=>Task.FromResult((puesto,cap))),403,"Capacidad: un puesto no puede ajustar el catálogo");
   var entra=await d.Registrar(new(1,"ENTRADA",4,1),admin,Guid.NewGuid(),VerCap);
   Bien(entra.SaldoPosterior==50,"Capacidad: con el espacio nuevo entran 4 cajas que antes no cabían");
   await Rechaza(()=>d.RegistrarAjuste(Cap(6,5),admin,Guid.NewGuid(),VerCap),409,"Capacidad: con 6 cajas ocupadas no se puede bajar a 5");
  }
  using(var d=new DiarioMovimientos(Carpeta("capacidad"),cap,()=>reloj))
  {
   var e=await d.Leer();
   Bien(e.Filas.First(f=>f.id_ubicacion==1).cajas_estanteria==6&&e.Filas.First(f=>f.id_ubicacion==1).cantidad==50&&e.Ajustes.Length==1,"Capacidad: el ajuste y los movimientos se reconstruyen al reiniciar");
   await Rechaza(()=>d.Registrar(new(1,"ENTRADA",1,1),admin,idAjuste,VerCap),409,"Capacidad: un UUID de ajuste no se reutiliza para un movimiento");
  }
  // Base compartida: el ajuste también se aplica en SQL y SQL debe coincidir.
  var sql=NuevaBase(); BaseMovimientos Sql()=>JsonSerializer.Deserialize<BaseMovimientos>(JsonSerializer.Serialize(sql))!;
  Task<(Cuenta Actor,BaseMovimientos Base)> VerSql()=>Task.FromResult((admin,Sql()));
  var aplicados=0;
  Task Aplicar(AjusteCatalogo a,bool revertir){aplicados++;foreach(var c in a.Capacidades)foreach(var f in sql.Filas.Where(f=>f.id_estanteria==c.Estanteria))f.cajas_estanteria=revertir?c.Anterior:c.Nueva;return Task.CompletedTask;}
  using(var d=new DiarioMovimientos(Carpeta("capacidad-espejo"),Sql(),()=>reloj,espejo:true))
  {
   await d.RegistrarAjuste(Cap(4,6),admin,Guid.NewGuid(),VerSql,Aplicar);
   Bien(aplicados==1&&sql.Filas[0].cajas_estanteria==6,"Capacidad espejo: el ajuste se aplica una vez en SQL");
   d.VerificarBase(Sql()); pasadas.Add("Capacidad espejo: SQL coincide con el diario después del ajuste");
   sql.Filas[0].cajas_estanteria=9; sql.Filas[1].cajas_estanteria=9;
   await Rechaza(()=>d.Registrar(new(1,"ENTRADA",1,1),admin,Guid.NewGuid(),VerSql),503,"Capacidad espejo: un cambio directo en SQL bloquea la captura");
   sql.Filas[0].cajas_estanteria=6; sql.Filas[1].cajas_estanteria=6;
  }
  using(var d=new DiarioMovimientos(Carpeta("capacidad-espejo"),Sql(),()=>reloj,espejo:true))
   Bien((await d.Leer()).Filas[0].cajas_estanteria==6,"Capacidad espejo: reabre cuando SQL ya tiene la capacidad nueva");
 }

 // Importación de Excel: datos de material registrados en el diario, 17/11/2026.
 {
  var mat=NuevaBase(); Task<(Cuenta Actor,BaseMovimientos Base)> VerMat()=>Task.FromResult((admin,mat));
  const string A="000 PARTE A";
  CambioMaterial M(string campo,string? antes,string? despues,string parte=A)=>new(parte,campo,antes,despues);
  using(var d=new DiarioMovimientos(Carpeta("materiales"),mat,()=>reloj))
  {
   await d.Registrar(new(1,"ENTRADA",1,1),admin,Guid.NewGuid(),VerMat);
   var imp=await d.RegistrarAjuste([],[M("minimo","5","8"),M("descripcion",null,"TAPA SENSOR"),M("uso",null,"EXCLUSIVO"),M("cantidad_por_caja","10","20")],admin,Guid.NewGuid(),VerMat);
   var f=(await d.Leer()).Filas.First(x=>x.id_ubicacion==1);
   Bien(f.minimo==8&&f.descripcion=="TAPA SENSOR"&&f.uso=="EXCLUSIVO"&&f.cantidad_por_caja==20&&f.cantidad==20,"Importación: cambia mínimo, descripción, uso y empaque sin tocar existencias");
   Bien((await d.Leer()).Filas.First(x=>x.id_ubicacion==1).cajas_disponibles==2,"Importación: el empaque nuevo recalcula las cajas ocupadas");
   await Rechaza(()=>d.RegistrarAjuste([],[M("minimo","5","9")],admin,Guid.NewGuid(),VerMat),409,"Importación: rechaza si el valor cambió desde que se preparó el archivo");
   await Rechaza(()=>d.RegistrarAjuste([],[M("uso","EXCLUSIVO","OTRO")],admin,Guid.NewGuid(),VerMat),400,"Importación: uso solo COMPARTIDO o EXCLUSIVO");
   await Rechaza(()=>d.RegistrarAjuste([],[M("minimo","8","-1")],admin,Guid.NewGuid(),VerMat),400,"Importación: rechaza mínimo negativo");
   await Rechaza(()=>d.RegistrarAjuste([],[M("cantidad_por_caja","20","0")],admin,Guid.NewGuid(),VerMat),400,"Importación: rechaza empaque cero");
   await Rechaza(()=>d.RegistrarAjuste([],[M("minimo",null,"3","NO EXISTE")],admin,Guid.NewGuid(),VerMat),400,"Importación: rechaza materiales que no están en el portal");
   await Rechaza(()=>d.RegistrarAjuste([],[M("stock","20","999")],admin,Guid.NewGuid(),VerMat),400,"Importación: no permite cambiar existencias");
   await Rechaza(()=>d.RegistrarAjuste([],[M("minimo","8","10"),M("uso","EXCLUSIVO","OTRO")],admin,Guid.NewGuid(),VerMat),400,"Importación: todo o nada, un error cancela el archivo completo");
   Bien((await d.Leer()).Filas.First(x=>x.id_ubicacion==1).minimo==8,"Importación: tras el rechazo no quedó ningún cambio a medias");
   await Rechaza(()=>d.RegistrarAjuste([],[M("minimo","8","10")],puesto,Guid.NewGuid(),()=>Task.FromResult((puesto,mat))),403,"Importación: un puesto no puede importar");
   var entradaNueva=await d.Registrar(new(1,"ENTRADA",1,2),admin,Guid.NewGuid(),VerMat);
   Bien(entradaNueva.CantidadMovimiento==20&&entradaNueva.SaldoPosterior==40,"Importación: después del cambio, una caja ya son 20 piezas");
   Bien((await d.Leer()).Ajustes[0].Materiales!.Length==4&&imp.Materiales!.Length==4,"Importación: el diario guarda cada cambio con su valor anterior");
  }
  using(var d=new DiarioMovimientos(Carpeta("materiales"),mat,()=>reloj))
  {
   var f=(await d.Leer()).Filas.First(x=>x.id_ubicacion==1);
   Bien(f.cantidad==40&&f.cantidad_por_caja==20&&f.minimo==8,"Importación: al reiniciar se reconstruye en orden (entrada con 10, cambio a 20, entrada con 20)");
  }
  var sqlM=NuevaBase(); BaseMovimientos SqlM()=>JsonSerializer.Deserialize<BaseMovimientos>(JsonSerializer.Serialize(sqlM))!;
  Task AplicarM(AjusteCatalogo a,bool revertir){if(!revertir)DiarioMovimientos.AplicarAjuste(sqlM.Filas,a);return Task.CompletedTask;}
  using(var d=new DiarioMovimientos(Carpeta("materiales-espejo"),SqlM(),()=>reloj,espejo:true))
  {
   await d.RegistrarAjuste([],[M("minimo","5","12")],admin,Guid.NewGuid(),()=>Task.FromResult((admin,SqlM())),AplicarM);
   d.VerificarBase(SqlM()); pasadas.Add("Importación espejo: SQL coincide con el diario después de importar");
  }
 }

 // Imágenes de la pieza: se identifica el tipo por la firma del archivo, no por su nombre.
 Bien(Store.TipoImagen([0x89,0x50,0x4E,0x47,0x0D,0x0A,0x1A,0x0A,0,0])==".png","Imagen: reconoce un PNG por su firma");
 Bien(Store.TipoImagen([0xFF,0xD8,0xFF,0xE0,0,0])==".jpg","Imagen: reconoce un JPG por su firma");
 Bien(Store.TipoImagen(Encoding.ASCII.GetBytes("GIF89a......"))==null&&Store.TipoImagen(Encoding.ASCII.GetBytes("<svg></svg>"))==null&&Store.TipoImagen([])==null,"Imagen: rechaza GIF, SVG y archivos vacíos");
 Console.WriteLine(JsonSerializer.Serialize(new{resultado="CORRECTO",total=pasadas.Count,casos=pasadas},new JsonSerializerOptions{WriteIndented=true}));
}
finally { Directory.Delete(raiz,true); }
