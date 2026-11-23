using Microsoft.AnalysisServices.Tabular;
var db=TmdlSerializer.DeserializeDatabaseFromFolder(Path.GetFullPath(args[0]));
if(db.Model.Tables.Count!=8||db.Model.Relationships.Count!=8)throw new Exception("Se deben conservar las ocho tablas y relaciones originales.");
foreach(var table in db.Model.Tables){if(table.Partitions.Count!=1||table.Partitions[0].Source is not MPartitionSource m||!m.Expression.Contains("Schema = \"mes2\""))throw new Exception("Fuente pendiente: "+table.Name);}
foreach(var r in db.Model.Relationships.OfType<SingleColumnRelationship>()){if(r.FromColumn.DataType!=r.ToColumn.DataType)throw new Exception("Tipo de relación incompatible: "+r.Name);}
if(!db.Model.Expressions["ServidorSQL"].Expression.Contains("motherson-piloto-l1-20261119"))throw new Exception("Servidor no es el piloto.");
Console.WriteLine($"TMDL válido: {db.Model.Tables.Count} tablas, {db.Model.Relationships.Count} relaciones, {db.Model.Tables.Sum(t=>t.Measures.Count)} medidas conservadas. Falta refresco y conciliación en Power BI.");
