"""Evidencia de solo lectura: compara SQL y fuentes antes/después de la continuación."""
import argparse
import gzip
import hashlib
import json
import subprocess
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]


def consulta(texto):
    result = subprocess.run([
        'docker', 'exec', '-i', 'motherson-mvp-sql-1', 'sh', '-c',
        'SQLCMDPASSWORD="$MSSQL_SA_PASSWORD" /opt/mssql-tools18/bin/sqlcmd '
        '-S localhost -U sa -d Motherson_L1_MVP_Demo -C -b -y 0',
    ], input='SET NOCOUNT ON;\n'+texto+';\n', text=True, capture_output=True, check=True)
    # sqlcmd divide el JSON largo en bloques de 2033 caracteres.
    return json.loads(''.join(result.stdout.strip().splitlines()))


def snapshot():
    tablas = consulta("SELECT SCHEMA_NAME(t.schema_id) esquema,t.name tabla,"
        "(SELECT c.name columna FROM sys.indexes i JOIN sys.index_columns ic "
        "ON ic.object_id=i.object_id AND ic.index_id=i.index_id JOIN sys.columns c "
        "ON c.object_id=ic.object_id AND c.column_id=ic.column_id "
        "WHERE i.object_id=t.object_id AND i.is_primary_key=1 "
        "ORDER BY ic.key_ordinal FOR JSON PATH) pk "
        "FROM sys.tables t WHERE t.is_ms_shipped=0 ORDER BY esquema,tabla FOR JSON PATH")
    esquema = consulta("SELECT o.type tipo,SCHEMA_NAME(o.schema_id) esquema,o.name nombre,"
        "OBJECT_DEFINITION(o.object_id) definicion,"
        "(SELECT c.name columna,TYPE_NAME(c.user_type_id) tipo,c.max_length longitud,"
        "c.precision,c.scale,c.is_nullable FROM sys.columns c WHERE c.object_id=o.object_id "
        "ORDER BY c.column_id FOR JSON PATH) columnas "
        "FROM sys.objects o WHERE o.is_ms_shipped=0 AND o.type IN ('U','V','P','FN','IF','TF','D','F','PK','UQ','C','TR') "
        "ORDER BY esquema,nombre,tipo FOR JSON PATH, INCLUDE_NULL_VALUES")
    datos = []
    for tabla in tablas:
        nombre = '['+tabla['esquema'].replace(']', ']]')+'].['+tabla['tabla'].replace(']', ']]')+']'
        claves = ','.join('['+p['columna'].replace(']', ']]')+']' for p in tabla['pk'])
        if not claves:
            raise RuntimeError('No existe orden primario para comprobar '+nombre)
        resultado = consulta("SELECT COUNT_BIG(*) filas,CONVERT(varchar(64),HASHBYTES('SHA2_256',"
            "CONVERT(varbinary(max),(SELECT * FROM "+nombre+" ORDER BY "+claves+
            " FOR JSON PATH, INCLUDE_NULL_VALUES))),2) sha256 FROM "+nombre+" FOR JSON PATH")[0]
        datos.append(dict(esquema=tabla['esquema'], tabla=tabla['tabla'], **resultado))
    integridad = consulta("SELECT (SELECT COUNT(*) FROM sys.foreign_keys WHERE is_disabled=0 AND is_not_trusted=0) fk_activas,"
        "(SELECT SUM(CONVERT(bigint,stock)) FROM dbo.MATERIAL) piezas,"
        "(SELECT COUNT(*) FROM dbo.REGISTRO) atenciones FOR JSON PATH")[0]
    carpeta = ROOT/'.local/historico-demo'
    manifiesto = json.loads((carpeta/'manifiesto.json').read_text())
    archivos = []
    for entrada in manifiesto['archivos']:
        path = carpeta/entrada['nombre']
        huella = hashlib.sha256(path.read_bytes()).hexdigest()
        if huella.lower() != entrada['sha256'].lower():
            raise RuntimeError('Fuente no coincide: '+path.name)
        with gzip.open(path, 'rb') as stream:
            cantidad = sum(1 for _ in stream)
        archivos.append(dict(nombre=path.name, filas=cantidad, sha256=huella))
    return dict(base='Motherson_L1_MVP_Demo', tablas=datos, integridad=integridad,
        esquema_sha256=hashlib.sha256(json.dumps(esquema,sort_keys=True,ensure_ascii=False).encode()).hexdigest(),
        manifiesto_sha256=hashlib.sha256((carpeta/'manifiesto.json').read_bytes()).hexdigest(), archivos=archivos)


if __name__ == '__main__':
    parser = argparse.ArgumentParser()
    parser.add_argument('--salida', required=True, type=Path)
    parser.add_argument('--comparar', type=Path)
    args = parser.parse_args()
    resultado = snapshot()
    args.salida.parent.mkdir(parents=True,exist_ok=True)
    args.salida.write_text(json.dumps(resultado,indent=2,ensure_ascii=False)+'\n')
    if args.comparar and resultado != json.loads(args.comparar.read_text()):
        raise SystemExit('Diferencias encontradas. Revisa ambas evidencias; no se modificó SQL.')
    print('SQL y fuentes verificados: '+str(len(resultado['tablas']))+' tablas, '+str(resultado['integridad']['fk_activas'])+' FK, '+str(resultado['integridad']['piezas'])+' piezas.'+(" Sin cambios respecto de la evidencia anterior." if args.comparar else ''))
