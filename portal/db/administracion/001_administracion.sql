-- Ampliación aditiva. No borra ni reabre el inventario existente.
SET XACT_ABORT ON;
IF SCHEMA_ID('mes2') IS NULL EXEC('CREATE SCHEMA mes2 AUTHORIZATION dbo');
GO
BEGIN TRAN;
IF OBJECT_ID('mes2.LINEA') IS NULL
BEGIN
 CREATE TABLE mes2.LINEA(id int NOT NULL PRIMARY KEY CHECK(id BETWEEN 1 AND 4),nombre nvarchar(80) NOT NULL,version int NOT NULL DEFAULT 0,apertura datetime2(7) NULL);
 INSERT mes2.LINEA(id,nombre) VALUES(1,N'Línea 1'),(2,N'Línea 2'),(3,N'Línea 3'),(4,N'Línea 4');
 CREATE TABLE mes2.CONFIG(id int NOT NULL PRIMARY KEY CHECK(id=1),activo bit NOT NULL DEFAULT 0,solo_lectura bit NOT NULL DEFAULT 0);
 INSERT mes2.CONFIG(id) VALUES(1);
 CREATE TABLE mes2.ACCESO(usuario int NOT NULL REFERENCES mes.USUARIO_PILOTO(id_usuario),linea int NOT NULL REFERENCES mes2.LINEA(id),puesto nvarchar(100) NULL,PRIMARY KEY(usuario,linea));
 INSERT mes2.ACCESO(usuario,linea,puesto) SELECT u.id_usuario,l.id,CASE WHEN u.rol='PUESTO' THEN CONVERT(nvarchar(100),u.id_puesto) END FROM mes.USUARIO_PILOTO u CROSS JOIN mes2.LINEA l WHERE u.rol='ADMIN' OR l.id=1;
 CREATE TABLE mes2.ENTIDAD(linea int NOT NULL REFERENCES mes2.LINEA(id),tipo varchar(20) NOT NULL CHECK(tipo IN('material','puesto','estacion','estanteria','ubicacion','atencion','calendario')),id nvarchar(100) COLLATE Latin1_General_100_BIN2 NOT NULL,datos nvarchar(max) NOT NULL CHECK(ISJSON(datos)=1),PRIMARY KEY(linea,tipo,id));
 CREATE TABLE mes2.EVENTO(secuencia bigint IDENTITY PRIMARY KEY,id uniqueidentifier NOT NULL UNIQUE,linea int NOT NULL REFERENCES mes2.LINEA(id),usuario int NULL,cuenta nvarchar(120) NOT NULL,tipo varchar(40) NOT NULL,fecha datetime2(7) NOT NULL DEFAULT SYSUTCDATETIME(),huella varchar(64) NOT NULL,solicitud nvarchar(max) NOT NULL CHECK(ISJSON(solicitud)=1),cambios nvarchar(max) NOT NULL CHECK(ISJSON(cambios)=1),resultado nvarchar(max) NOT NULL CHECK(ISJSON(resultado)=1));
 CREATE INDEX IX_mes2_evento_linea ON mes2.EVENTO(linea,fecha,secuencia);
 CREATE TABLE mes2.IMAGEN(linea int NOT NULL REFERENCES mes2.LINEA(id),material nvarchar(100) COLLATE Latin1_General_100_BIN2 NOT NULL,tipo varchar(30) NOT NULL,bytes varbinary(max) NOT NULL CHECK(DATALENGTH(bytes)<=5242880),version int NOT NULL DEFAULT 1,PRIMARY KEY(linea,material));
END;
COMMIT;
GO
CREATE OR ALTER PROCEDURE mes2.Leer @usuario int,@linea int,@corte datetime2(7)=NULL
AS
BEGIN
 SET NOCOUNT ON;
 IF NOT EXISTS(SELECT 1 FROM mes2.ACCESO a JOIN mes.USUARIO_PILOTO u ON u.id_usuario=a.usuario WHERE a.usuario=@usuario AND a.linea=@linea AND u.activo=1) THROW 51003,N'Línea no autorizada.',1;
 SELECT l.*,SYSUTCDATETIME() ahora,u.rol,a.puesto FROM mes2.LINEA l JOIN mes2.ACCESO a ON a.linea=l.id JOIN mes.USUARIO_PILOTO u ON u.id_usuario=a.usuario WHERE l.id=@linea AND a.usuario=@usuario;
 IF @corte IS NULL SELECT tipo,id,datos FROM mes2.ENTIDAD WHERE linea=@linea;
 ELSE BEGIN
  IF EXISTS(SELECT 1 FROM mes2.LINEA WHERE id=@linea AND apertura>@corte) THROW 51004,N'No hay apertura acreditada para ese corte.',1;
  WITH versiones AS(SELECT j.tipo,j.id,j.datos,ROW_NUMBER() OVER(PARTITION BY j.tipo,j.id ORDER BY e.secuencia DESC) rn FROM mes2.EVENTO e CROSS APPLY OPENJSON(e.cambios) WITH(tipo varchar(20),id nvarchar(100),datos nvarchar(max) AS JSON) j WHERE e.linea=@linea AND e.fecha<=@corte)
  SELECT tipo,id,datos FROM versiones WHERE rn=1 AND datos IS NOT NULL;
 END;
END;
GO
CREATE OR ALTER PROCEDURE mes2.Resultado @usuario int,@linea int,@id uniqueidentifier,@huella varchar(64)
AS
BEGIN
 IF NOT EXISTS(SELECT 1 FROM mes2.ACCESO a JOIN mes.USUARIO_PILOTO u ON u.id_usuario=a.usuario WHERE a.usuario=@usuario AND a.linea=@linea AND u.activo=1) THROW 51003,N'Línea no autorizada.',1;
 IF EXISTS(SELECT 1 FROM mes2.EVENTO WHERE id=@id AND (usuario<>@usuario OR linea<>@linea OR huella<>@huella)) THROW 51009,N'Identificador utilizado con otra solicitud.',1;
 SELECT resultado FROM mes2.EVENTO WHERE id=@id;
END;
GO
CREATE OR ALTER PROCEDURE mes2.Guardar @usuario int,@linea int,@version int,@id uniqueidentifier,@huella varchar(64),@tipo varchar(40),@solicitud nvarchar(max),@cambios nvarchar(max),@resultado nvarchar(max)
WITH EXECUTE AS OWNER
AS
BEGIN
 SET XACT_ABORT ON;SET NOCOUNT ON;
 IF ISJSON(@solicitud)<>1 OR ISJSON(@cambios)<>1 OR ISJSON(@resultado)<>1 THROW 51004,N'Contenido inválido.',1;
 DECLARE @rol varchar(10),@cuenta nvarchar(120);
 SELECT @rol=u.rol,@cuenta=u.usuario FROM mes2.ACCESO a JOIN mes.USUARIO_PILOTO u ON u.id_usuario=a.usuario WHERE a.usuario=@usuario AND a.linea=@linea AND u.activo=1;
 IF @rol IS NULL OR @rol='LECTOR' OR (@rol='PUESTO' AND @tipo NOT IN('ENTRADA','ATENCION_INICIAR','ATENCION_FINALIZAR')) THROW 51003,N'Operación no autorizada.',1;
 BEGIN TRAN;
 DECLARE @lock int,@recurso nvarchar(100)=N'mes2_linea_'+CONVERT(nvarchar(10),@linea);
 EXEC @lock=sys.sp_getapplock @Resource=@recurso,@LockMode='Exclusive',@LockOwner='Transaction',@LockTimeout=10000;
 IF @lock<0 THROW 51009,N'Línea ocupada. Actualiza y vuelve a intentar.',1;
 IF EXISTS(SELECT 1 FROM mes2.EVENTO WHERE id=@id AND (usuario<>@usuario OR linea<>@linea OR huella<>@huella)) THROW 51009,N'Identificador utilizado con otra solicitud.',1;
 IF EXISTS(SELECT 1 FROM mes2.EVENTO WHERE id=@id) BEGIN SELECT resultado FROM mes2.EVENTO WHERE id=@id;COMMIT;RETURN;END;
 IF EXISTS(SELECT 1 FROM mes2.CONFIG WHERE activo=0 OR solo_lectura=1) THROW 51004,N'Administración en modo de solo lectura.',1;
 UPDATE mes2.LINEA SET version=version+1,apertura=COALESCE(apertura,SYSUTCDATETIME()) WHERE id=@linea AND version=@version;
 IF @@ROWCOUNT<>1 THROW 51009,N'Los datos cambiaron. Actualiza y revisa la operación.',1;
 IF (SELECT COUNT(*) FROM OPENJSON(@cambios))>2000 THROW 51004,N'Lote demasiado grande.',1;
 DELETE e FROM mes2.ENTIDAD e JOIN OPENJSON(@cambios) WITH(tipo varchar(20),id nvarchar(100),datos nvarchar(max) AS JSON) j ON e.linea=@linea AND e.tipo=j.tipo AND e.id=j.id COLLATE Latin1_General_100_BIN2 WHERE j.datos IS NULL;
 UPDATE e SET datos=j.datos FROM mes2.ENTIDAD e JOIN OPENJSON(@cambios) WITH(tipo varchar(20),id nvarchar(100),datos nvarchar(max) AS JSON) j ON e.linea=@linea AND e.tipo=j.tipo AND e.id=j.id COLLATE Latin1_General_100_BIN2 WHERE j.datos IS NOT NULL;
 INSERT mes2.ENTIDAD(linea,tipo,id,datos) SELECT @linea,j.tipo,j.id,j.datos FROM OPENJSON(@cambios) WITH(tipo varchar(20),id nvarchar(100),datos nvarchar(max) AS JSON) j WHERE j.datos IS NOT NULL AND NOT EXISTS(SELECT 1 FROM mes2.ENTIDAD e WHERE e.linea=@linea AND e.tipo=j.tipo AND e.id=j.id COLLATE Latin1_General_100_BIN2);
 SET @resultado=JSON_MODIFY(@resultado,'$.version',@version+1);
 INSERT mes2.EVENTO(id,linea,usuario,cuenta,tipo,huella,solicitud,cambios,resultado) VALUES(@id,@linea,@usuario,@cuenta,@tipo,@huella,@solicitud,@cambios,@resultado);
 COMMIT;SELECT @resultado;
END;
GO
CREATE OR ALTER PROCEDURE mes2.Foto @usuario int,@linea int,@material nvarchar(100),@tipo varchar(30),@bytes varbinary(max)=NULL
WITH EXECUTE AS OWNER
AS
BEGIN
 SET XACT_ABORT ON;SET NOCOUNT ON;
 IF NOT EXISTS(SELECT 1 FROM mes2.ACCESO a JOIN mes.USUARIO_PILOTO u ON u.id_usuario=a.usuario WHERE a.usuario=@usuario AND a.linea=@linea AND u.activo=1 AND u.rol='ADMIN') THROW 51003,N'Operación no autorizada.',1;
 BEGIN TRAN;
 DECLARE @lock int,@recurso nvarchar(100)=N'mes2_linea_'+CONVERT(nvarchar(10),@linea);
 EXEC @lock=sys.sp_getapplock @Resource=@recurso,@LockMode='Exclusive',@LockOwner='Transaction',@LockTimeout=10000;
 IF @lock<0 THROW 51009,N'Línea ocupada.',1;
 IF EXISTS(SELECT 1 FROM mes2.CONFIG WHERE activo=0 OR solo_lectura=1) THROW 51004,N'Administración en modo de solo lectura.',1;
 IF NOT EXISTS(SELECT 1 FROM mes2.ENTIDAD WHERE linea=@linea AND tipo='material' AND id=@material) THROW 51004,N'Material no encontrado en la línea.',1;
 UPDATE mes2.LINEA SET version=version+1 WHERE id=@linea;
 IF @bytes IS NULL DELETE mes2.IMAGEN WHERE linea=@linea AND material=@material;
 ELSE BEGIN
  UPDATE mes2.IMAGEN WITH(UPDLOCK,SERIALIZABLE) SET bytes=@bytes,tipo=@tipo,version=version+1 WHERE linea=@linea AND material=@material;
  IF @@ROWCOUNT=0 INSERT mes2.IMAGEN(linea,material,tipo,bytes) VALUES(@linea,@material,@tipo,@bytes);
 END;
 INSERT mes2.EVENTO(id,linea,usuario,cuenta,tipo,huella,solicitud,cambios,resultado) SELECT NEWID(),@linea,@usuario,usuario,'IMAGEN','',JSON_OBJECT('material':@material),N'[]',JSON_OBJECT('material':@material,'eliminada':CASE WHEN @bytes IS NULL THEN 1 ELSE 0 END) FROM mes.USUARIO_PILOTO WHERE id_usuario=@usuario;
 COMMIT;
END;
GO
-- El usuario SQL de ejecución no puede cambiar tablas directamente.
IF DATABASE_PRINCIPAL_ID('portal_app') IS NOT NULL
BEGIN
 GRANT SELECT ON SCHEMA::mes2 TO portal_app;
 GRANT EXECUTE ON SCHEMA::mes2 TO portal_app;
 DENY INSERT,UPDATE,DELETE ON SCHEMA::mes2 TO portal_app;
END;
GO
-- Tras activar v2, un binario viejo no podrá escribir saldos en las tablas anteriores.
CREATE OR ALTER TRIGGER dbo.Mes2_BloquearUbicacion ON dbo.UBICACION AFTER INSERT,UPDATE,DELETE AS
BEGIN
 IF EXISTS(SELECT 1 FROM mes2.CONFIG WHERE activo=1) THROW 51004,N'Usa el servicio de administración v2; datos anteriores conservados en lectura.',1;
END;
GO
CREATE OR ALTER TRIGGER dbo.Mes2_BloquearMaterial ON dbo.MATERIAL AFTER INSERT,UPDATE,DELETE AS
BEGIN
 IF EXISTS(SELECT 1 FROM mes2.CONFIG WHERE activo=1) THROW 51004,N'Usa el servicio de administración v2; catálogo anterior conservado en lectura.',1;
END;
GO
CREATE OR ALTER TRIGGER dbo.Mes2_BloquearRegistro ON dbo.REGISTRO AFTER INSERT,UPDATE,DELETE AS
BEGIN
 IF EXISTS(SELECT 1 FROM mes2.CONFIG WHERE activo=1) THROW 51004,N'Usa el servicio de administración v2; atenciones anteriores conservadas en lectura.',1;
END;
GO
-- Fuentes explícitas para una futura revisión del modelo de Power BI.
-- No reemplazan silenciosamente las tablas ni el informe publicado.
CREATE OR ALTER VIEW mes2.vw_inventario_bi AS
SELECT u.linea,JSON_VALUE(u.datos,'$.id') id_ubicacion,m.id id_material,
 JSON_VALUE(m.datos,'$.numeroParte') numero_parte,JSON_VALUE(m.datos,'$.descripcion') descripcion,
 TRY_CONVERT(int,JSON_VALUE(u.datos,'$.cantidad')) cantidad,
 TRY_CONVERT(int,JSON_VALUE(m.datos,'$.minimo')) minimo,
 TRY_CONVERT(int,JSON_VALUE(m.datos,'$.empaque')) cantidad_por_caja,
 JSON_VALUE(m.datos,'$.activo') activo,JSON_VALUE(m.datos,'$.puesto') id_puesto,
 JSON_VALUE(r.datos,'$.estacion') id_estacion,JSON_VALUE(u.datos,'$.estanteria') id_estanteria
FROM mes2.ENTIDAD u JOIN mes2.ENTIDAD m ON m.linea=u.linea AND m.tipo='material' AND m.id=JSON_VALUE(u.datos,'$.material') COLLATE Latin1_General_100_BIN2
JOIN mes2.ENTIDAD r ON r.linea=u.linea AND r.tipo='estanteria' AND r.id=JSON_VALUE(u.datos,'$.estanteria') COLLATE Latin1_General_100_BIN2
WHERE u.tipo='ubicacion';
GO
CREATE OR ALTER VIEW mes2.vw_atenciones_bi AS
SELECT a.linea,a.id id_atencion,JSON_VALUE(a.datos,'$.material') COLLATE Latin1_General_100_BIN2 id_material,
 JSON_VALUE(a.datos,'$.ubicacion') id_ubicacion,JSON_VALUE(a.datos,'$.responsable') responsable,
 TRY_CONVERT(datetimeoffset,JSON_VALUE(a.datos,'$.inicio')) inicio_utc,
 TRY_CONVERT(datetimeoffset,JSON_VALUE(a.datos,'$.fin')) fin_utc,
 TRY_CONVERT(int,JSON_VALUE(a.datos,'$.turno')) turno_inicio
FROM mes2.ENTIDAD a WHERE a.tipo='atencion'
UNION ALL
SELECT 1,N'legado-'+CONVERT(nvarchar(80),r.id_registro),m.id,CONVERT(nvarchar(100),r.id_ubicacion),r.puesto,
 TODATETIMEOFFSET(r.fecha_hora_inicio,'+00:00'),TODATETIMEOFFSET(r.fecha_hora_fin,'+00:00'),
 CASE WHEN DATEPART(hour,r.fecha_hora_inicio_local)>=6 AND DATEPART(hour,r.fecha_hora_inicio_local)<15 THEN 1 ELSE 2 END
FROM dbo.vw_kpi_atenciones r JOIN mes2.ENTIDAD m ON m.linea=1 AND m.tipo='material'
 AND JSON_VALUE(m.datos,'$.numeroParteLegado') COLLATE Latin1_General_100_BIN2=r.numero_parte COLLATE Latin1_General_100_BIN2
WHERE r.fecha_hora_fin IS NOT NULL;
GO
IF DATABASE_PRINCIPAL_ID('mes_lectura') IS NOT NULL
BEGIN
 GRANT SELECT ON mes2.vw_inventario_bi TO mes_lectura;
 GRANT SELECT ON mes2.vw_atenciones_bi TO mes_lectura;
END;
