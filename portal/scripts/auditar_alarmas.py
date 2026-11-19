"""Huellas de solo lectura. No ejecuta migraciones ni modifica SQL."""
import argparse,hashlib,json,subprocess
from pathlib import Path
QUERY="""
SET NOCOUNT ON;
SELECT
 (SELECT SCHEMA_NAME(t.schema_id) esquema,t.name tabla,c.name columna,c.column_id,c.system_type_id,c.max_length,c.is_nullable FROM sys.tables t JOIN sys.columns c ON c.object_id=t.object_id ORDER BY esquema,tabla,c.column_id FOR JSON PATH) columnas,
 (SELECT SCHEMA_NAME(o.schema_id) esquema,o.name,o.type,m.definition FROM sys.objects o LEFT JOIN sys.sql_modules m ON m.object_id=o.object_id WHERE o.is_ms_shipped=0 ORDER BY esquema,o.name,o.type FOR JSON PATH) objetos,
 (SELECT name,parent_object_id,referenced_object_id FROM sys.foreign_keys ORDER BY name FOR JSON PATH) relaciones,
 (SELECT COUNT(*) FROM dbo.REGISTRO) registros,
 (SELECT id_ubicacion,cantidad,version_concurrencia FROM dbo.UBICACION ORDER BY id_ubicacion FOR JSON PATH) inventario,
 (SELECT COUNT(*) FROM archivo.EVENTO) eventos,
 (SELECT nombre,sha256,CONVERT(varchar(64),HASHBYTES('SHA2_256',contenido_gzip),2) sha_real FROM archivo.FUENTE ORDER BY nombre FOR JSON PATH) fuentes,
 (SELECT COUNT(*) FROM sys.tables WHERE schema_id=SCHEMA_ID('dbo')) tablas_dbo
FOR JSON PATH,WITHOUT_ARRAY_WRAPPER;
"""
def auditar(db):
 if db not in ('Motherson_L1_MVP_Desarrollo','Motherson_L1_MVP_Pruebas'):raise ValueError('Base no permitida')
 cmd=['docker','exec','-i','motherson-mvp-sql-1','sh','-c','SQLCMDPASSWORD="$MSSQL_SA_PASSWORD" /opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -d '+db+' -C -b -y 0 -w 65535']
 raw=subprocess.check_output(cmd,input=QUERY,text=True);d=json.loads(raw[raw.index('{'):raw.rindex('}')+1].replace('\n',''));result={'base':db,'tablas_dbo':d['tablas_dbo'],'registros':d['registros'],'eventos':d['eventos']}
 for k in ('columnas','objetos','relaciones','inventario','fuentes'):
  v=d[k];parsed=json.loads(v) if isinstance(v,str) else v
  result['huella_'+k]=hashlib.sha256(json.dumps(parsed,sort_keys=True,separators=(',',':')).encode()).hexdigest()
 return result
if __name__=='__main__':
 p=argparse.ArgumentParser();p.add_argument('--salida',required=True);args=p.parse_args();r=[auditar(d) for d in ['Motherson_L1_MVP_Desarrollo','Motherson_L1_MVP_Pruebas']];Path(args.salida).write_text(json.dumps(r,indent=2));print(json.dumps(r))
