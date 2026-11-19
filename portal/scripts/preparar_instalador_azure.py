"""Genera una variante autocontenida para Azure Query editor. No conecta con SQL."""
from pathlib import Path
import re
folder=Path(__file__).resolve().parents[1]/'db/azure'
lines=(folder/'instalar.sql').read_text().splitlines()
files=[folder/line[3:] for line in lines if line.startswith(':r ')]
statements=[]
for path in files:
 for batch in re.split(r'^\s*GO\s*$',path.read_text(),flags=re.M|re.I):
  if batch.strip():statements.append("EXEC sys.sp_executesql N'"+batch.replace("'","''")+"';")
script="""-- Instalación inicial autocontenida. Solo después de aprobación. No carga datos ni crea usuarios.
SET NOCOUNT ON;
SET XACT_ABORT ON;
IF DB_NAME() <> N'Motherson_L1' THROW 51100,'Destino incorrecto.',1;
IF EXISTS(SELECT 1 FROM sys.tables WHERE is_ms_shipped=0) THROW 51101,'La base ya tiene tablas. No se instaló nada.',1;
BEGIN TRY
 BEGIN TRANSACTION;
"""+'\n'.join(statements)+"""
 COMMIT;
 SELECT 'INSTALACION_CONFIRMADA' resultado;
END TRY
BEGIN CATCH
 IF XACT_STATE()<>0 ROLLBACK;
 THROW;
END CATCH;
"""
(folder/'instalar_portal.sql').write_text(script)
print('Preparado:',folder/'instalar_portal.sql','Sin ejecución SQL.')
