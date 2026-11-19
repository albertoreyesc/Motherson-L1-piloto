"""Acceso local para conciliación del catálogo. No muestra credenciales."""
import json,subprocess
BASES=('Motherson_L1_MVP_Desarrollo','Motherson_L1_MVP_Pruebas')
def sql(base,consulta,contenedor="motherson-mvp-sql-1"):
 if base not in BASES:raise ValueError('Base no autorizada')
 cmd=['docker','exec','-i',contenedor,'sh','-c','SQLCMDPASSWORD="$MSSQL_SA_PASSWORD" /opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -d '+base+' -C -b -I -y 0 -w 65535']
 r=subprocess.run(cmd,input='SET NOCOUNT ON;\n'+consulta,encoding="utf-8",capture_output=True)
 if r.returncode:raise RuntimeError(r.stdout+r.stderr)
 return r.stdout.strip().replace("\n", "")
def datos(base):
 if sql(base,"SELECT CASE WHEN OBJECT_ID('reporting.vw_modelo_puestos','V') IS NULL THEN 0 ELSE 1 END") == "1":
  raise ValueError("Usa migrar_puestos.py auditar para el catálogo del modelo por puesto.")
 existe=sql(base,"SELECT COUNT(*) FROM sys.tables WHERE object_id=OBJECT_ID('dbo.COLOR')") == '1'
 consulta="""SELECT
 JSON_QUERY((SELECT m.*,c.nombre color FROM dbo.MATERIAL m LEFT JOIN dbo.COLOR c ON c.id_color=m.id_color ORDER BY numero_parte FOR JSON PATH,INCLUDE_NULL_VALUES)) materiales,
 JSON_QUERY((SELECT id_puesto,nombre,tipo FROM dbo.PUESTO_LOGISTICO ORDER BY id_puesto FOR JSON PATH,INCLUDE_NULL_VALUES)) puestos,
 JSON_QUERY((SELECT * FROM dbo.COLOR ORDER BY id_color FOR JSON PATH)) colores,
 (SELECT COUNT(*) FROM dbo.MATERIAL m LEFT JOIN dbo.COLOR c ON c.id_color=m.id_color WHERE m.id_color IS NOT NULL AND c.id_color IS NULL) colores_huerfanos,
 (SELECT COUNT(*) FROM (SELECT numero_parte FROM dbo.MATERIAL GROUP BY numero_parte HAVING COUNT(*)>1)x) partes_duplicadas,
 (SELECT CHECKSUM_AGG(BINARY_CHECKSUM(*)) FROM dbo.REGISTRO) huella_registros,
 (SELECT CHECKSUM_AGG(BINARY_CHECKSUM(*)) FROM archivo.EVENTO) huella_eventos,
 JSON_QUERY((SELECT id_ubicacion,cantidad,version_concurrencia FROM dbo.UBICACION ORDER BY id_ubicacion FOR JSON PATH,INCLUDE_NULL_VALUES)) inventario,
 JSON_QUERY((SELECT maestro,revision,corte_fuente,huella_fuentes,sellado FROM archivo.CORTE FOR JSON PATH)) corte
 FOR JSON PATH,WITHOUT_ARRAY_WRAPPER;"""
 if not existe:
  consulta=consulta.replace("m.*,c.nombre color", "m.*,CONVERT(int,NULL) id_color,CONVERT(nvarchar(100),NULL) color").replace(" LEFT JOIN dbo.COLOR c ON c.id_color=m.id_color", "").replace("(SELECT * FROM dbo.COLOR ORDER BY id_color FOR JSON PATH)", "(SELECT 1 AS id_color WHERE 1=0 FOR JSON PATH)").replace("(SELECT COUNT(*) FROM dbo.MATERIAL m LEFT JOIN dbo.COLOR c ON c.id_color=m.id_color WHERE m.id_color IS NOT NULL AND c.id_color IS NULL)", "0")
 consulta=consulta.replace("(SELECT COUNT(*) FROM dbo.MATERIAL m WHERE m.id_color IS NOT NULL AND c.id_color IS NULL)", "0") if not existe else consulta
 d=json.loads(sql(base,consulta))
 d.setdefault("colores",[]);return d
if __name__=='__main__':
 import argparse
 from pathlib import Path
 p=argparse.ArgumentParser();p.add_argument('--salida',type=Path,required=True);a=p.parse_args();d={b:datos(b) for b in BASES};a.salida.parent.mkdir(parents=True,exist_ok=True);a.salida.write_text(json.dumps(d,ensure_ascii=False,indent=2));print(json.dumps({b:{'materiales':len(x['materiales']),'usos':sum(bool(m['uso']) for m in x['materiales']),'colores':len(x['colores']),'puestos':len(x['puestos']),'tipos':sum(bool(v['tipo']) for v in x['puestos'])} for b,x in d.items()}))
