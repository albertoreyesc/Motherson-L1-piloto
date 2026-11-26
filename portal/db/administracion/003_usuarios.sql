-- Usuarios desde el portal (pantalla "Usuarios", solo ADMIN). Aditivo e idempotente: se puede volver a ejecutar.
-- No borra cuentas ni historial. Las contraseñas llegan ya cifradas (PasswordHasher del portal); nunca en texto.
SET XACT_ABORT ON;
GO
-- En administración v2 el puesto de una cuenta PUESTO se guarda por línea en mes2.ACCESO (identificador de texto),
-- así que id_puesto (catálogo anterior) deja de ser obligatorio para el rol PUESTO.
DECLARE @ck sysname,@sql nvarchar(400);
SELECT @ck=cc.name FROM sys.check_constraints cc
 WHERE cc.parent_object_id=OBJECT_ID('mes.USUARIO_PILOTO') AND cc.definition LIKE '%id_puesto%' AND cc.name<>N'CK_USUARIO_PILOTO_PUESTO_V2';
IF @ck IS NOT NULL BEGIN SET @sql=N'ALTER TABLE mes.USUARIO_PILOTO DROP CONSTRAINT '+QUOTENAME(@ck);EXEC(@sql);END;
IF NOT EXISTS(SELECT 1 FROM sys.check_constraints WHERE name=N'CK_USUARIO_PILOTO_PUESTO_V2' AND parent_object_id=OBJECT_ID('mes.USUARIO_PILOTO'))
 ALTER TABLE mes.USUARIO_PILOTO ADD CONSTRAINT CK_USUARIO_PILOTO_PUESTO_V2 CHECK(rol='PUESTO' OR id_puesto IS NULL);
GO
CREATE OR ALTER PROCEDURE mes2.UsuarioValidarAccesos @rol varchar(10),@accesos nvarchar(max)
AS
BEGIN
 SET NOCOUNT ON;
 IF ISJSON(@accesos)<>1 OR NOT EXISTS(SELECT 1 FROM OPENJSON(@accesos)) THROW 51004,N'Indica al menos una línea.',1;
 IF EXISTS(SELECT 1 FROM OPENJSON(@accesos) WITH(linea int) j WHERE NOT EXISTS(SELECT 1 FROM mes2.LINEA l WHERE l.id=j.linea)) THROW 51004,N'Línea inválida.',1;
 IF EXISTS(SELECT linea FROM OPENJSON(@accesos) WITH(linea int) GROUP BY linea HAVING COUNT(*)>1) THROW 51004,N'Línea repetida.',1;
 IF @rol='PUESTO' AND EXISTS(SELECT 1 FROM OPENJSON(@accesos) WITH(linea int,puesto nvarchar(100)) j
   WHERE j.puesto IS NULL OR NOT EXISTS(SELECT 1 FROM mes2.ENTIDAD e WHERE e.linea=j.linea AND e.tipo='puesto' AND e.id=j.puesto COLLATE Latin1_General_100_BIN2))
  THROW 51004,N'Elige un puesto existente en cada línea.',1;
END;
GO
CREATE OR ALTER PROCEDURE mes2.UsuarioCrear @admin int,@usuario nvarchar(80),@nombre nvarchar(120),@rol varchar(10),@hash nvarchar(512),@accesos nvarchar(max)
WITH EXECUTE AS OWNER
AS
BEGIN
 SET XACT_ABORT ON;SET NOCOUNT ON;
 DECLARE @cuenta nvarchar(80)=(SELECT usuario FROM mes.USUARIO_PILOTO WHERE id_usuario=@admin AND rol='ADMIN' AND activo=1);
 IF @cuenta IS NULL THROW 51003,N'Solo administración puede crear usuarios.',1;
 IF @rol IS NULL OR @rol NOT IN('ADMIN','PUESTO','LECTOR') THROW 51004,N'Rol inválido.',1;
 IF LEN(LTRIM(RTRIM(ISNULL(@usuario,''))))<3 OR LEN(LTRIM(RTRIM(ISNULL(@nombre,''))))<1 OR LEN(ISNULL(@hash,''))<20 THROW 51004,N'Revisa usuario, nombre y contraseña.',1;
 EXEC mes2.UsuarioValidarAccesos @rol,@accesos;
 BEGIN TRAN;
 IF EXISTS(SELECT 1 FROM mes.USUARIO_PILOTO WITH(UPDLOCK,HOLDLOCK) WHERE usuario=@usuario) THROW 51009,N'Ese usuario ya existe. Elige otro nombre de usuario.',1;
 INSERT mes.USUARIO_PILOTO(usuario,nombre,rol,id_puesto,password_hash) VALUES(@usuario,@nombre,@rol,NULL,@hash);
 DECLARE @id int=CONVERT(int,SCOPE_IDENTITY());
 INSERT mes2.ACCESO(usuario,linea,puesto) SELECT @id,j.linea,CASE WHEN @rol='PUESTO' THEN j.puesto END FROM OPENJSON(@accesos) WITH(linea int,puesto nvarchar(100)) j;
 -- Trazabilidad en el historial de cada línea (sin contraseña).
 INSERT mes2.EVENTO(id,linea,usuario,cuenta,tipo,huella,solicitud,cambios,resultado)
 SELECT NEWID(),j.linea,@admin,@cuenta,'USUARIO_CREAR','',
  (SELECT @usuario usuario,@nombre nombre,@rol rol FOR JSON PATH,WITHOUT_ARRAY_WRAPPER),N'[]',
  (SELECT @id id,@usuario usuario,@rol rol,JSON_QUERY(@accesos) accesos FOR JSON PATH,WITHOUT_ARRAY_WRAPPER)
 FROM OPENJSON(@accesos) WITH(linea int) j;
 COMMIT;
 SELECT @id;
END;
GO
CREATE OR ALTER PROCEDURE mes2.UsuarioActualizar @admin int,@id int,@nombre nvarchar(120),@rol varchar(10),@activo bit,@hash nvarchar(512)=NULL,@accesos nvarchar(max)
WITH EXECUTE AS OWNER
AS
BEGIN
 SET XACT_ABORT ON;SET NOCOUNT ON;
 DECLARE @cuenta nvarchar(80)=(SELECT usuario FROM mes.USUARIO_PILOTO WHERE id_usuario=@admin AND rol='ADMIN' AND activo=1);
 IF @cuenta IS NULL THROW 51003,N'Solo administración puede modificar usuarios.',1;
 IF @rol IS NULL OR @rol NOT IN('ADMIN','PUESTO','LECTOR') OR @activo IS NULL OR LEN(LTRIM(RTRIM(ISNULL(@nombre,''))))<1 THROW 51004,N'Revisa nombre, rol y estado.',1;
 IF @hash IS NOT NULL AND LEN(@hash)<20 THROW 51004,N'Contraseña inválida.',1;
 IF @id=@admin AND (@rol<>'ADMIN' OR @activo=0) THROW 51004,N'No puedes quitarte el rol de administración ni desactivar tu propia cuenta.',1;
 EXEC mes2.UsuarioValidarAccesos @rol,@accesos;
 BEGIN TRAN;
 DECLARE @usuario nvarchar(80)=(SELECT usuario FROM mes.USUARIO_PILOTO WITH(UPDLOCK,HOLDLOCK) WHERE id_usuario=@id);
 IF @usuario IS NULL THROW 51004,N'Usuario no encontrado.',1;
 IF (@rol<>'ADMIN' OR @activo=0) AND NOT EXISTS(SELECT 1 FROM mes.USUARIO_PILOTO WHERE rol='ADMIN' AND activo=1 AND id_usuario<>@id)
  THROW 51004,N'Debe quedar al menos una cuenta de administración activa.',1;
 -- Cambiar rol, contraseña o desactivar invalida las sesiones abiertas de esa cuenta en la siguiente petición.
 UPDATE mes.USUARIO_PILOTO SET nombre=@nombre,rol=@rol,activo=@activo,password_hash=COALESCE(@hash,password_hash),
  id_puesto=CASE WHEN @rol='PUESTO' THEN id_puesto END WHERE id_usuario=@id;
 DELETE mes2.ACCESO WHERE usuario=@id;
 INSERT mes2.ACCESO(usuario,linea,puesto) SELECT @id,j.linea,CASE WHEN @rol='PUESTO' THEN j.puesto END FROM OPENJSON(@accesos) WITH(linea int,puesto nvarchar(100)) j;
 INSERT mes2.EVENTO(id,linea,usuario,cuenta,tipo,huella,solicitud,cambios,resultado)
 SELECT NEWID(),j.linea,@admin,@cuenta,'USUARIO_ACTUALIZAR','',
  (SELECT @usuario usuario,@nombre nombre,@rol rol,@activo activo,CASE WHEN @hash IS NULL THEN CAST(0 AS bit) ELSE CAST(1 AS bit) END cambioContrasena FOR JSON PATH,WITHOUT_ARRAY_WRAPPER),N'[]',
  (SELECT @id id,@usuario usuario,@rol rol,@activo activo,JSON_QUERY(@accesos) accesos FOR JSON PATH,WITHOUT_ARRAY_WRAPPER)
 FROM OPENJSON(@accesos) WITH(linea int) j;
 COMMIT;
END;
GO
IF DATABASE_PRINCIPAL_ID('portal_app') IS NOT NULL
 GRANT EXECUTE ON SCHEMA::mes2 TO portal_app;
