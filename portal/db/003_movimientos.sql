CREATE OR ALTER PROCEDURE dbo.RegistrarMovimiento @usuario int,@ubicacion int,@tipo varchar(10),@cantidad int,@version int,@operacion uniqueidentifier,@huella char(64)
AS
BEGIN
 SET NOCOUNT ON; SET XACT_ABORT ON;
 DECLARE @inicio datetime2(7)=SYSUTCDATETIME(),@perfil varchar(20),@anterior int,@factor int,@capacidad int,@nivel int,@puesto int,@puesto_cuenta int,@saldo bigint,@piezas bigint,@ocupacion bigint,@turno int,@id int,@lock int;
 BEGIN TRY
 BEGIN TRANSACTION;
 EXEC @lock=sp_getapplock @Resource='MVP_INVENTARIO',@LockMode='Exclusive',@LockOwner='Transaction',@LockTimeout=10000;
 IF @lock<0 THROW 51009,'Inventario ocupado. Reintenta la misma operación.',1;
 SELECT @perfil=perfil,@puesto_cuenta=id_puesto FROM dbo.USUARIOS WITH(UPDLOCK,HOLDLOCK) WHERE id_usuarios=@usuario AND esta_activo=1;
 IF @perfil IS NULL THROW 51001,'Sesión no válida.',1;
 IF EXISTS(SELECT 1 FROM dbo.REGISTRO WHERE id_operacion=@operacion) BEGIN
  IF NOT EXISTS(SELECT 1 FROM dbo.REGISTRO WHERE id_operacion=@operacion AND id_usuarios=@usuario AND huella_peticion=@huella) THROW 51009,'El identificador ya pertenece a otra petición.',1;
  COMMIT;SELECT * FROM dbo.REGISTRO WHERE id_operacion=@operacion;RETURN;
 END;
 IF @tipo NOT IN('ENTRADA','SALIDA') OR @cantidad<=0 OR @cantidad>1000000 THROW 51004,'Cantidad o movimiento inválido.',1;
 IF (@tipo='ENTRADA' AND @perfil NOT IN('ENTRADAS','MOVIMIENTOS','ADMINISTRADOR')) OR(@tipo='SALIDA' AND @perfil<>'ADMINISTRADOR') THROW 51003,'Operación no autorizada.',1;
 SELECT @anterior=u.cantidad,@factor=m.cantidad_por_caja,@capacidad=t.cajas_estanteria,@nivel=t.id_estanteria,@puesto=m.id_puesto FROM dbo.UBICACION u WITH(UPDLOCK,HOLDLOCK) JOIN dbo.MATERIAL m ON m.numero_parte=u.numero_parte JOIN dbo.ESTANTERIA t ON t.id_estanteria=u.id_estanteria WHERE u.id_ubicacion=@ubicacion AND u.version_concurrencia=@version;
 IF @nivel IS NULL THROW 51009,'La ubicación o su versión cambió. Actualiza el inventario.',1;
 IF @perfil<>'ADMINISTRADOR' AND (@puesto_cuenta IS NULL OR @puesto_cuenta<>@puesto) THROW 51003,'El material no pertenece a tu puesto o falta tu asignación.',1;
 IF @anterior IS NULL THROW 51009,'Falta una apertura conciliada.',1;
 IF @tipo='ENTRADA' BEGIN
  IF @factor IS NULL OR @capacidad IS NULL THROW 51009,'Falta validar empaque o capacidad.',1;
  IF EXISTS(SELECT 1 FROM dbo.UBICACION u JOIN dbo.MATERIAL m ON m.numero_parte=u.numero_parte WHERE u.id_estanteria=@nivel AND(u.cantidad IS NULL OR m.cantidad_por_caja IS NULL)) THROW 51009,'Hay existencias o empaques pendientes en el nivel.',1;
  SELECT @ocupacion=SUM((CONVERT(bigint,u.cantidad)+m.cantidad_por_caja-1)/m.cantidad_por_caja) FROM dbo.UBICACION u JOIN dbo.MATERIAL m ON m.numero_parte=u.numero_parte WHERE u.id_estanteria=@nivel;
  IF @ocupacion+@cantidad>@capacidad THROW 51009,'Sin espacio para esas cajas completas.',1;
 END;
 SET @piezas=CASE WHEN @tipo='ENTRADA' THEN CONVERT(bigint,@cantidad)*@factor ELSE @cantidad END;
 SET @saldo=CONVERT(bigint,@anterior)+CASE WHEN @tipo='ENTRADA' THEN @piezas ELSE -@piezas END;
 IF @saldo<0 THROW 51009,'Existencias insuficientes. No se descontó material.',1;
 IF @saldo>2147483647 OR @piezas>2147483647 THROW 51004,'Cantidad fuera del rango permitido.',1;
 DECLARE @ahora datetime2(7)=SYSUTCDATETIME(),@hora time=CONVERT(time,SYSUTCDATETIME() AT TIME ZONE 'UTC' AT TIME ZONE 'Central Standard Time (Mexico)');
 SELECT @turno=id_turno FROM dbo.TURNO WHERE @hora>=hora_inicio AND @hora<hora_fin;
 UPDATE dbo.UBICACION SET cantidad=CONVERT(int,@saldo),version_concurrencia=version_concurrencia+1 WHERE id_ubicacion=@ubicacion;
 INSERT dbo.REGISTRO(id_usuarios,id_ubicacion,id_turno,id_puesto,tipo_movimiento,cantidad_movimiento,cantidad_cajas,cantidad_por_caja_aplicada,saldo_anterior,saldo_posterior,fecha_movimiento,fecha_hora_inicio,fecha_hora_fin,id_operacion,huella_peticion,origen)
 VALUES(@usuario,@ubicacion,@turno,@puesto,@tipo,CONVERT(int,@piezas),CASE WHEN @tipo='ENTRADA' THEN @cantidad END,CASE WHEN @tipo='ENTRADA' THEN @factor END,@anterior,CONVERT(int,@saldo),@ahora,@inicio,@ahora,@operacion,@huella,'CAPTURA_SIMULADA');
 SET @id=CONVERT(int,SCOPE_IDENTITY());COMMIT;SELECT * FROM dbo.REGISTRO WHERE id_registro=@id;
 END TRY BEGIN CATCH IF @@TRANCOUNT>0 ROLLBACK;THROW;END CATCH
END;
GO
CREATE OR ALTER TRIGGER dbo.TR_REGISTRO_INMUTABLE ON dbo.REGISTRO INSTEAD OF UPDATE,DELETE AS THROW 51009,'Un comprobante confirmado no se modifica.',1;
GO
CREATE OR ALTER TRIGGER dbo.TR_UBICACION_IDENTIDAD ON dbo.UBICACION AFTER UPDATE AS
 IF EXISTS(SELECT id_ubicacion,numero_parte,id_estanteria FROM deleted EXCEPT SELECT id_ubicacion,numero_parte,id_estanteria FROM inserted) THROW 51009,'La identidad de una ubicación no se reasigna.',1;
GO
CREATE OR ALTER TRIGGER dbo.TR_ESTACION_IDENTIDAD ON dbo.ESTACION INSTEAD OF UPDATE,DELETE AS THROW 51009,'La identidad histórica de estación es inmutable.',1;
GO
CREATE OR ALTER TRIGGER dbo.TR_ESTANTERIA_IDENTIDAD ON dbo.ESTANTERIA AFTER UPDATE AS
 IF EXISTS(SELECT id_estanteria,id_estacion,estanteria,nivel FROM deleted EXCEPT SELECT id_estanteria,id_estacion,estanteria,nivel FROM inserted) THROW 51009,'La identidad histórica de nivel es inmutable.',1;
GO
CREATE OR ALTER TRIGGER archivo.TR_EVENTO_INMUTABLE ON archivo.EVENTO INSTEAD OF UPDATE,DELETE AS THROW 51009,'Archivo de solo lectura.',1;
GO
CREATE OR ALTER TRIGGER archivo.TR_FUENTE_INMUTABLE ON archivo.FUENTE INSTEAD OF UPDATE,DELETE AS THROW 51009,'Archivo de solo lectura.',1;
GO
CREATE OR ALTER TRIGGER archivo.TR_EVENTO_SELLADO ON archivo.EVENTO AFTER INSERT AS
 IF EXISTS(SELECT 1 FROM archivo.CORTE WHERE sellado=1) THROW 51009,'Archivo cerrado: no admite más filas.',1;
GO
CREATE OR ALTER TRIGGER archivo.TR_FUENTE_SELLADO ON archivo.FUENTE AFTER INSERT AS
 IF EXISTS(SELECT 1 FROM archivo.CORTE WHERE sellado=1) THROW 51009,'Archivo cerrado: no admite más fuentes.',1;
GO
CREATE OR ALTER TRIGGER archivo.TR_CORTE_SELLADO ON archivo.CORTE AFTER UPDATE,DELETE AS
 IF EXISTS(SELECT 1 FROM deleted WHERE sellado=1) THROW 51009,'El corte sellado no se modifica.',1;
