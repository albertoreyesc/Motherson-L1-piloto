"""Migración selectiva; Desarrollo requiere evidencias aprobadas de Pruebas."""
import argparse
import hashlib
import json
import os
import re
from datetime import datetime, timezone
from pathlib import Path
from catalogo_sql import sql

ROOT = Path(__file__).resolve().parents[1]
BASE = 'Motherson_L1_MVP_Pruebas'

def literal(value):
    return "N'" + value.replace("'", "''") + "'"

def identificador(value):
    return '[' + value.replace(']', ']]') + ']'

def privado(path, dato):
    path.parent.mkdir(parents=True, exist_ok=True)
    with os.fdopen(os.open(path, os.O_WRONLY | os.O_CREAT | os.O_EXCL, 0o600), 'w') as f:
        json.dump(dato, f, ensure_ascii=False, indent=2)

def estado():
    columnas = json.loads(sql(BASE, "SELECT name FROM sys.columns WHERE object_id=OBJECT_ID('dbo.REGISTRO') AND name<>'es_simulado' ORDER BY name FOR JSON PATH"))
    columnas_registro = ','.join(identificador(x['name']) for x in columnas)
    consulta = f"""SELECT
      JSON_QUERY((SELECT {columnas_registro} FROM dbo.REGISTRO ORDER BY id_registro FOR JSON PATH,INCLUDE_NULL_VALUES)) registros,
      JSON_QUERY((SELECT id_ubicacion,numero_parte,id_estanteria,cantidad,version_concurrencia FROM dbo.UBICACION ORDER BY id_ubicacion FOR JSON PATH,INCLUDE_NULL_VALUES)) ubicaciones,
      JSON_QUERY((SELECT numero_parte,id_puesto,descripcion,uso,cantidad_por_caja,minimo FROM dbo.MATERIAL ORDER BY numero_parte FOR JSON PATH,INCLUDE_NULL_VALUES)) materiales,
      JSON_QUERY((SELECT id_usuarios,nombre_usuario,nombre_visible,perfil,esta_activo FROM dbo.USUARIOS ORDER BY id_usuarios FOR JSON PATH,INCLUDE_NULL_VALUES)) usuarios,
      JSON_QUERY((SELECT id_puesto,nombre,tipo FROM dbo.PUESTO_LOGISTICO ORDER BY id_puesto FOR JSON PATH,INCLUDE_NULL_VALUES)) puestos,
      JSON_QUERY((SELECT * FROM dbo.ESTACION ORDER BY id_estacion FOR JSON PATH)) estaciones,
      JSON_QUERY((SELECT * FROM dbo.ESTANTERIA ORDER BY id_estanteria FOR JSON PATH)) estanterias,
      JSON_QUERY((SELECT * FROM dbo.TURNO ORDER BY id_turno FOR JSON PATH)) turnos,
      JSON_QUERY((SELECT * FROM archivo.CORTE FOR JSON PATH)) corte,
      JSON_QUERY((SELECT nombre,sha256,filas,CONVERT(varchar(64),HASHBYTES('SHA2_256',contenido_gzip),2) huella_binaria FROM archivo.FUENTE ORDER BY nombre FOR JSON PATH)) fuentes,
      (SELECT CHECKSUM_AGG(BINARY_CHECKSUM(*)) FROM archivo.EVENTO) checksum_eventos,
      (SELECT COUNT(*) FROM archivo.EVENTO) eventos
      FOR JSON PATH,WITHOUT_ARRAY_WRAPPER;"""
    return json.loads(sql(BASE, consulta))

def transaccion(sentencias):
    sql(BASE, """SET XACT_ABORT ON; BEGIN TRY BEGIN TRANSACTION;
      DECLARE @l int; EXEC @l=sys.sp_getapplock @Resource='MVP_INVENTARIO',@LockMode='Exclusive',@LockOwner='Transaction',@LockTimeout=10000;
      IF @l<0 THROW 51000,'Inventario ocupado',1;
      """ + '\n'.join(modulo(s) for s in sentencias) + "\nCOMMIT; END TRY BEGIN CATCH IF @@TRANCOUNT>0 ROLLBACK; THROW; END CATCH;")

def modulo(texto):
    return 'EXEC(' + literal(texto) + ');'

def aplicar():
    nuevo = int(sql(BASE, "SELECT COUNT(*) FROM sys.columns WHERE object_id=OBJECT_ID('dbo.USUARIOS') AND name='id_puesto'"))
    if nuevo:
        verificar()
        return {'base': BASE, 'cambios': 0, 'estado': 'Ya simplificada'}
    assert sql(BASE, 'SELECT COUNT(*) FROM dbo.COLOR') == '0', 'COLOR contiene información: detener'
    assert sql(BASE, 'SELECT COUNT(*) FROM dbo.MATERIAL WHERE id_color IS NOT NULL') == '0'
    assert sql(BASE, "SELECT COUNT(*) FROM dbo.REGISTRO WHERE origen NOT IN('CAPTURA_SIMULADA','APERTURA_SIMULADA','CAPTURA_REAL','APERTURA_REAL') OR es_simulado<>CASE WHEN origen IN('CAPTURA_SIMULADA','APERTURA_SIMULADA') THEN 1 ELSE 0 END") == '0'
    antes = estado()
    objetos = json.loads(sql(BASE, "SELECT SCHEMA_NAME(o.schema_id) esquema,o.name,m.definition FROM sys.sql_modules m JOIN sys.objects o ON o.object_id=m.object_id WHERE m.definition IS NOT NULL ORDER BY o.object_id FOR JSON PATH"))
    checks = json.loads(sql(BASE, "SELECT name,definition FROM sys.check_constraints WHERE parent_object_id=OBJECT_ID('dbo.REGISTRO') AND definition LIKE '%es_simulado%' FOR JSON PATH"))
    fk = json.loads(sql(BASE, "SELECT name FROM sys.foreign_keys WHERE parent_object_id=OBJECT_ID('dbo.MATERIAL') AND referenced_object_id=OBJECT_ID('dbo.COLOR') FOR JSON PATH"))
    backup = ROOT/'.local/simplificacion'/('antes-'+BASE+'-'+datetime.now(timezone.utc).strftime('%Y%m%dT%H%M%S%fZ')+'.json')
    privado(backup, {'base': BASE, 'estado': antes, 'modulos': objetos, 'checks': checks, 'fk_color': fk})
    pasos = [
        "IF EXISTS(SELECT 1 FROM dbo.COLOR) OR EXISTS(SELECT 1 FROM dbo.MATERIAL WHERE id_color IS NOT NULL) THROW 51000,'Apareció información de color',1;",
        "ALTER TABLE dbo.USUARIOS ADD id_puesto int NULL;",
        "ALTER TABLE dbo.USUARIOS WITH CHECK ADD CONSTRAINT FK_USUARIOS_PUESTO FOREIGN KEY(id_puesto) REFERENCES dbo.PUESTO_LOGISTICO(id_puesto);",
    ]
    pasos += ['ALTER TABLE dbo.MATERIAL DROP CONSTRAINT '+identificador(f['name'])+';' for f in fk]
    pasos += ['ALTER TABLE dbo.MATERIAL DROP COLUMN id_color;', 'DROP TABLE dbo.COLOR;']
    pasos += ['ALTER TABLE dbo.REGISTRO DROP CONSTRAINT '+identificador(c['name'])+';' for c in checks]
    pasos += ['ALTER TABLE dbo.REGISTRO DROP COLUMN es_simulado;']
    for archivo in ('002_vistas.sql', '003_movimientos.sql'):
        pasos += [modulo(batch) for batch in re.split(r'^GO\s*$', (ROOT/'db'/archivo).read_text(), flags=re.M|re.I) if batch.strip()]
    transaccion(pasos)
    assert estado() == antes, 'Cambió un dato protegido'
    verificar()
    return {'base': BASE, 'cambios': 1, 'respaldo': str(backup), 'datos_conservados': True}

def revertir(path):
    respaldo = json.loads(Path(path).read_text())
    assert respaldo['base'] == BASE
    # No borrar nuevas asignaciones ni comprobantes realizados después de migrar.
    assert estado() == respaldo['estado'], 'Hubo movimientos u otros cambios: preparar reversión específica'
    assert sql(BASE, 'SELECT COUNT(*) FROM dbo.USUARIOS WHERE id_puesto IS NOT NULL') == '0', 'Hay asignaciones nuevas: no se eliminan'
    pasos = [
        'ALTER TABLE dbo.USUARIOS DROP CONSTRAINT FK_USUARIOS_PUESTO;',
        'ALTER TABLE dbo.USUARIOS DROP COLUMN id_puesto;',
        'CREATE TABLE dbo.COLOR(id_color int IDENTITY PRIMARY KEY,nombre nvarchar(100) NOT NULL UNIQUE);',
        'ALTER TABLE dbo.MATERIAL ADD id_color int NULL;',
        'ALTER TABLE dbo.REGISTRO ADD es_simulado bit NULL;',
        'DISABLE TRIGGER dbo.TR_REGISTRO_INMUTABLE ON dbo.REGISTRO;',
        modulo("UPDATE dbo.REGISTRO SET es_simulado=CONVERT(bit,CASE WHEN origen IN('CAPTURA_SIMULADA','APERTURA_SIMULADA') THEN 1 ELSE 0 END);"),
        'ENABLE TRIGGER dbo.TR_REGISTRO_INMUTABLE ON dbo.REGISTRO;',
        'ALTER TABLE dbo.REGISTRO ALTER COLUMN es_simulado bit NOT NULL;',
    ]
    pasos += ['ALTER TABLE dbo.MATERIAL WITH CHECK ADD CONSTRAINT '+identificador(f['name'])+' FOREIGN KEY(id_color) REFERENCES dbo.COLOR(id_color);' for f in respaldo['fk_color']]
    pasos += ['ALTER TABLE dbo.REGISTRO WITH CHECK ADD CONSTRAINT '+identificador(c['name'])+' CHECK '+c['definition']+';' for c in respaldo['checks']]
    # Solo se restauran los módulos que cambió esta fase.
    nombres = {'vw_kpi_movimientos', 'vw_kpi_inventario_actual', 'vw_kpi_consumo_diario', 'vw_historico_eventos', 'vw_estado_fuentes', 'vw_archivos_originales', 'RegistrarMovimiento'}
    for obj in respaldo['modulos']:
        if obj['name'] in nombres:
            definicion = re.sub(r'CREATE\s+(?:OR\s+ALTER\s+)?(VIEW|PROCEDURE)', r'CREATE OR ALTER \1', obj['definition'], count=1, flags=re.I)
            pasos.append(modulo(definicion))
    transaccion(pasos)
    assert estado() == respaldo['estado']
    return {'base': BASE, 'revertida': True, 'datos_conservados': True}

def verificar():
    assert sql(BASE, "SELECT COUNT(*) FROM sys.tables WHERE schema_id=SCHEMA_ID('dbo')") == '8'
    assert sql(BASE, "SELECT COUNT(*) FROM sys.columns WHERE (object_id=OBJECT_ID('dbo.MATERIAL') AND name='id_color') OR(object_id=OBJECT_ID('dbo.REGISTRO') AND name='es_simulado')") == '0'
    assert sql(BASE, 'SELECT COUNT(*) FROM sys.foreign_keys WHERE is_disabled=1 OR is_not_trusted=1') == '0'
    assert not sql(BASE, 'DBCC CHECKCONSTRAINTS WITH ALL_CONSTRAINTS, NO_INFOMSGS;')
    assert sql(BASE, "SELECT COUNT(*) FROM reporting.vw_kpi_movimientos WHERE es_simulado IS NULL OR(es_simulado=0 AND origen NOT IN('CAPTURA_REAL','APERTURA_REAL'))") == '0'

if __name__ == '__main__':
    parser = argparse.ArgumentParser()
    parser.add_argument('--database', choices=['Motherson_L1_MVP_Pruebas','Motherson_L1_MVP_Desarrollo'], default='Motherson_L1_MVP_Pruebas')
    parser.add_argument('--revertir')
    parser.add_argument('--salida', required=True)
    args = parser.parse_args()
    BASE = args.database
    if BASE.endswith('_Desarrollo') and not args.revertir:
        evidencia = ROOT/'evidencias/simplificacion'
        permisos = json.loads((evidencia/'pruebas-permisos.json').read_text())
        assert permisos['base']=='Motherson_L1_MVP_Pruebas' and len(permisos['casos'])>=9
        assert all(c['resultado']=='APROBADO' for c in permisos['casos'])
        assert json.loads((evidencia/'reversion-pruebas.json').read_text())['revertida']
        assert json.loads((evidencia/'idempotencia-pruebas.json').read_text())['cambios']==0
    resultado = revertir(args.revertir) if args.revertir else aplicar()
    Path(args.salida).write_text(json.dumps(resultado, ensure_ascii=False, indent=2)+'\n')
    print(json.dumps(resultado, ensure_ascii=False))
