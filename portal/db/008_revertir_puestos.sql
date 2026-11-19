-- Reversión selectiva. Las atenciones capturadas impiden una reversión con pérdida.
IF @@TRANCOUNT = 0 THROW 51000, 'Se requiere una transacción de reversión.', 1;
IF OBJECT_ID('respaldo_puestos.USUARIOS','U') IS NULL
    THROW 51000, 'No se encontró el respaldo selectivo.', 1;
IF EXISTS (SELECT 1 FROM dbo.REGISTRO)
    THROW 51000, 'La base contiene atenciones nuevas. Conserva esos datos antes de revertir.', 1;

DROP PROCEDURE dbo.IniciarAtencion, dbo.FinalizarAtencion, dbo.GuardarAccesoPuesto;
DROP VIEW reporting.vw_kpi_atenciones, reporting.vw_modelo_puestos;
DROP FUNCTION IF EXISTS dbo.TurnoDeAtencion;
DROP TRIGGER archivo.TR_REGISTRO_MVP_CONSERVADO, archivo.TR_CUENTA_MVP_CONSERVADA;
DROP TABLE dbo.REGISTRO, archivo.CUENTA_MVP;

ALTER SCHEMA dbo TRANSFER archivo.REGISTRO_MVP;
EXEC sp_rename 'dbo.REGISTRO_MVP', 'REGISTRO';
ALTER SCHEMA dbo TRANSFER respaldo_puestos.USUARIOS;

ALTER TABLE dbo.UBICACION ADD cantidad int NULL CONSTRAINT CK_UBICACION_CANTIDAD CHECK(cantidad>=0),
    version_concurrencia int NOT NULL CONSTRAINT DF_UBICACION_VERSION DEFAULT 1
    CONSTRAINT CK_UBICACION_VERSION CHECK(version_concurrencia>0);
ALTER TABLE dbo.PUESTO_LOGISTICO ADD tipo nvarchar(60) NULL;
GO
UPDATE u SET cantidad=s.cantidad,version_concurrencia=s.version_concurrencia
FROM dbo.UBICACION u JOIN respaldo_puestos.SALDO s ON s.id_ubicacion=u.id_ubicacion;
UPDATE p SET tipo=t.tipo
FROM dbo.PUESTO_LOGISTICO p JOIN respaldo_puestos.TIPO_PUESTO t ON t.id_puesto=p.id_puesto;
DROP INDEX UX_PUESTO_USUARIO ON dbo.PUESTO_LOGISTICO;
ALTER TABLE dbo.PUESTO_LOGISTICO DROP CONSTRAINT CK_PUESTO_ACCESO;
ALTER TABLE dbo.PUESTO_LOGISTICO DROP COLUMN usuario, [password];

DECLARE @fk sysname = (
    SELECT name FROM sys.foreign_keys WHERE parent_object_id=OBJECT_ID('dbo.MATERIAL')
    AND referenced_object_id=OBJECT_ID('dbo.COLOR')
);
EXEC ('ALTER TABLE dbo.MATERIAL DROP CONSTRAINT '+@fk);
ALTER TABLE dbo.MATERIAL DROP CONSTRAINT CK_MATERIAL_STOCK;
ALTER TABLE dbo.MATERIAL DROP COLUMN id_color,stock;
DROP TABLE dbo.COLOR;
DROP TABLE respaldo_puestos.SALDO, respaldo_puestos.TIPO_PUESTO, respaldo_puestos.CORTE;
DROP SCHEMA respaldo_puestos;
GO
