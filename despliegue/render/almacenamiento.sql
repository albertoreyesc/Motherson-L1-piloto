SET XACT_ABORT ON;
IF DB_NAME()<>N'Motherson_L1' OR OBJECT_ID('mes.USUARIO_PILOTO') IS NULL OR DATABASE_PRINCIPAL_ID('mes_portal') IS NULL
 THROW 51100,'Se requiere un piloto instalado con cuentas personales.',1;
IF OBJECT_ID('mes.IMAGEN_PILOTO') IS NULL
 CREATE TABLE mes.IMAGEN_PILOTO(
 numero_parte nvarchar(100) NOT NULL PRIMARY KEY,
 contenido varbinary(max) NOT NULL CHECK(DATALENGTH(contenido)<=5242880),
 tipo varchar(10) NOT NULL CHECK(tipo IN ('image/png','image/jpeg')),
 version bigint NOT NULL DEFAULT 1);
IF OBJECT_ID('mes.CLAVE_PROTECCION_PILOTO') IS NULL
 CREATE TABLE mes.CLAVE_PROTECCION_PILOTO(
 nombre nvarchar(256) NOT NULL PRIMARY KEY,
 contenido nvarchar(max) NOT NULL CHECK(DATALENGTH(contenido)<=262144));
GO
CREATE OR ALTER PROCEDURE mes.GuardarImagenPiloto @parte nvarchar(100),@contenido varbinary(max),@tipo varchar(10)
AS
BEGIN
 SET NOCOUNT ON;SET XACT_ABORT ON;
 IF NOT EXISTS(SELECT 1 FROM dbo.MATERIAL WHERE numero_parte=@parte) THROW 51004,'Material inexistente.',1;
 DECLARE @v TABLE(version bigint);
 BEGIN TRANSACTION;
 UPDATE mes.IMAGEN_PILOTO WITH(UPDLOCK,SERIALIZABLE) SET contenido=@contenido,tipo=@tipo,version=version+1 OUTPUT inserted.version INTO @v WHERE numero_parte=@parte;
 IF @@ROWCOUNT=0 INSERT mes.IMAGEN_PILOTO(numero_parte,contenido,tipo) OUTPUT inserted.version INTO @v VALUES(@parte,@contenido,@tipo);
 COMMIT;
 SELECT version FROM @v;
END;
GO
CREATE OR ALTER PROCEDURE mes.EliminarImagenPiloto @parte nvarchar(100)
AS
BEGIN SET NOCOUNT ON;DELETE mes.IMAGEN_PILOTO WHERE numero_parte=@parte;END;
GO
CREATE OR ALTER PROCEDURE mes.GuardarClaveProteccionPiloto @nombre nvarchar(256),@contenido nvarchar(max)
AS
BEGIN
 SET NOCOUNT ON;SET XACT_ABORT ON;
 -- El repositorio de Data Protection solo acepta claves ya cifradas con el certificado privado.
 IF @contenido NOT LIKE '%encryptedSecret%' THROW 51100,'Se requiere una clave de protección cifrada.',1;
 BEGIN TRANSACTION;
 IF NOT EXISTS(SELECT 1 FROM mes.CLAVE_PROTECCION_PILOTO WITH(UPDLOCK,SERIALIZABLE) WHERE nombre=@nombre)
  INSERT mes.CLAVE_PROTECCION_PILOTO(nombre,contenido) VALUES(@nombre,@contenido);
 COMMIT;
END;
GO
GRANT SELECT ON mes.IMAGEN_PILOTO TO mes_portal;
GRANT SELECT ON mes.CLAVE_PROTECCION_PILOTO TO mes_portal;
GRANT EXECUTE ON mes.GuardarImagenPiloto TO mes_portal;
GRANT EXECUTE ON mes.EliminarImagenPiloto TO mes_portal;
GRANT EXECUTE ON mes.GuardarClaveProteccionPiloto TO mes_portal;
