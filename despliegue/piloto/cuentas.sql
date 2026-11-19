-- SOLO base nueva del piloto. Aplicar mediante --piloto-inicializar, que valida servidor independiente.
CREATE TABLE mes.USUARIO_PILOTO(
 id_usuario int IDENTITY PRIMARY KEY,
 usuario nvarchar(80) NOT NULL UNIQUE,
 nombre nvarchar(120) NOT NULL,
 rol varchar(10) NOT NULL CHECK(rol IN ('ADMIN','PUESTO','LECTOR')),
 id_puesto int NULL REFERENCES dbo.PUESTO_LOGISTICO(id_puesto),
 password_hash nvarchar(512) NOT NULL,
 activo bit NOT NULL DEFAULT 1,
 CHECK((rol='PUESTO' AND id_puesto IS NOT NULL) OR (rol<>'PUESTO' AND id_puesto IS NULL))
);
GO
-- Un bloqueo transaccional común serializa movimientos, ajustes y apertura del inventario.
-- La respuesta durable se devuelve también cuando se repite el UUID de una petición idéntica.
CREATE OR ALTER PROCEDURE mes.RegistrarMovimiento
 @operacion uniqueidentifier,@cuenta nvarchar(80),@tipo_cuenta varchar(30),@puesto_cuenta int=NULL,@solicitud nvarchar(max)
AS
BEGIN
 SET NOCOUNT ON; SET XACT_ABORT ON;
 IF @tipo_cuenta='ADMIN_PUESTOS' AND NOT EXISTS(SELECT 1 FROM mes.USUARIO_PILOTO WHERE usuario=@cuenta AND rol='ADMIN' AND activo=1)
  THROW 51003,'Cuenta administrativa no autorizada.',1;
 IF @operacion IS NULL OR @operacion='00000000-0000-0000-0000-000000000000' OR ISJSON(@solicitud)<>1 OR @solicitud IS NULL
  THROW 51004,'Operación o captura inválida.',1;
 DECLARE @ubicacion int=TRY_CONVERT(int,JSON_VALUE(@solicitud,'$.Ubicacion')),
 @tipo varchar(10)=JSON_VALUE(@solicitud,'$.Tipo'),@cantidad int=TRY_CONVERT(int,JSON_VALUE(@solicitud,'$.Cantidad')),
 @version int=TRY_CONVERT(int,JSON_VALUE(@solicitud,'$.Version')),
 @consumo bit=CASE JSON_VALUE(@solicitud,'$.EsConsumo') WHEN 'true' THEN 1 WHEN 'false' THEN 0 END;
 IF @ubicacion IS NULL OR @ubicacion<=0 OR @tipo IS NULL OR @tipo COLLATE Latin1_General_100_BIN2 NOT IN ('ENTRADA','SALIDA') OR DATALENGTH(@tipo)<>LEN(@tipo) OR DATALENGTH(JSON_VALUE(@solicitud,'$.Tipo'))<>2*DATALENGTH(@tipo) OR @cantidad IS NULL OR @cantidad NOT BETWEEN 1 AND 1000000
  OR @version IS NULL OR @version<1 OR @consumo IS NULL OR (@tipo='ENTRADA' AND @consumo=1)
  THROW 51004,'Revisa tipo, cantidad, ubicación, versión y consumo.',1;
 IF @cuenta IS NULL OR LEN(LTRIM(RTRIM(@cuenta)))=0 OR @tipo_cuenta IS NULL OR @tipo_cuenta NOT IN ('ADMIN_PUESTOS','PUESTO')
  THROW 51003,'Cuenta no autorizada.',1;
 BEGIN TRY
  BEGIN TRANSACTION;
  DECLARE @lock int;
  EXEC @lock=sys.sp_getapplock @Resource='mes_inventario_v1',@LockMode='Exclusive',@LockOwner='Transaction',@LockTimeout=10000;
  IF @lock<0 THROW 51009,'Inventario ocupado. Reintenta la misma operación.',1;
  IF NOT EXISTS(SELECT 1 FROM mes.BASE_INVENTARIO WHERE id=1) THROW 51004,'Falta aprobar el saldo inicial.',1;
  IF @tipo_cuenta='PUESTO' AND (@tipo<>'ENTRADA' OR @puesto_cuenta IS NULL OR NOT EXISTS(
   SELECT 1 FROM mes.USUARIO_PILOTO WHERE id_puesto=@puesto_cuenta AND usuario=@cuenta AND rol='PUESTO' AND activo=1))
   THROW 51003,'El puesto no puede realizar esta operación.',1;
  IF EXISTS(SELECT 1 FROM mes.OPERACION WHERE id_operacion=@operacion)
  BEGIN
   IF NOT EXISTS(SELECT 1 FROM mes.OPERACION WHERE id_operacion=@operacion AND tipo='MOVIMIENTO'
    AND cuenta COLLATE Latin1_General_100_BIN2=@cuenta COLLATE Latin1_General_100_BIN2 AND tipo_cuenta=@tipo_cuenta
    AND (puesto_cuenta=@puesto_cuenta OR (puesto_cuenta IS NULL AND @puesto_cuenta IS NULL))
    AND solicitud COLLATE Latin1_General_100_BIN2=@solicitud COLLATE Latin1_General_100_BIN2)
    THROW 51009,'El identificador ya pertenece a otra operación.',1;
   COMMIT; SELECT respuesta FROM mes.OPERACION WHERE id_operacion=@operacion; RETURN;
  END;
  DECLARE @saldo int,@ppc int,@parte varchar(50),@rack int,@actual int,@puesto int,@capacidad int;
  SELECT @saldo=u.cantidad,@ppc=m.cantidad_por_caja,@parte=u.numero_parte,@rack=u.id_estanteria,
   @actual=u.version_concurrencia,@puesto=m.id_puesto,@capacidad=e.cajas_estanteria
   FROM dbo.UBICACION u JOIN dbo.MATERIAL m ON m.numero_parte=u.numero_parte JOIN dbo.ESTANTERIA e ON e.id_estanteria=u.id_estanteria
   WHERE u.id_ubicacion=@ubicacion;
  IF @saldo IS NULL OR @ppc IS NULL OR @ppc<=0 THROW 51004,'Ubicación o empaque inválidos.',1;
  IF @tipo_cuenta='PUESTO' AND @puesto<>@puesto_cuenta THROW 51003,'La ubicación no pertenece a tu puesto.',1;
  IF @actual<>@version THROW 51009,'Cambió la disponibilidad. Actualiza y confirma de nuevo.',1;
  DECLARE @ocupadas bigint=(SELECT SUM((CONVERT(bigint,u.cantidad)+m.cantidad_por_caja-1)/m.cantidad_por_caja)
   FROM dbo.UBICACION u JOIN dbo.MATERIAL m ON m.numero_parte=u.numero_parte WHERE u.id_estanteria=@rack);
  IF @tipo='ENTRADA' AND (@capacidad IS NULL OR @cantidad>@capacidad-@ocupadas) THROW 51009,'No hay capacidad para esas cajas.',1;
  IF @tipo='SALIDA' AND @cantidad>(CONVERT(bigint,@saldo)+@ppc-1)/@ppc THROW 51012,'No hay suficientes cajas para esa salida.',1;
  DECLARE @piezas64 bigint=CONVERT(bigint,@cantidad)*@ppc;
  IF @tipo='SALIDA' AND @piezas64>@saldo SET @piezas64=@saldo;
  IF @piezas64<=0 OR @piezas64>2147483647 THROW 51004,'Cantidad fuera del límite permitido.',1;
  DECLARE @piezas int=CONVERT(int,@piezas64),@posterior bigint=CONVERT(bigint,@saldo)+CASE WHEN @tipo='ENTRADA' THEN @piezas64 ELSE -@piezas64 END;
  IF @posterior NOT BETWEEN 0 AND 2147483647 THROW 51012,'Saldo fuera del límite permitido.',1;
  UPDATE dbo.UBICACION SET cantidad=CONVERT(int,@posterior),version_concurrencia=version_concurrencia+1 WHERE id_ubicacion=@ubicacion;
  UPDATE dbo.MATERIAL SET stock=(SELECT SUM(CONVERT(bigint,cantidad)) FROM dbo.UBICACION WHERE numero_parte=@parte) WHERE numero_parte=@parte;
  DECLARE @fecha datetime2(7)=SYSUTCDATETIME(),@respuesta nvarchar(max);
  SELECT @respuesta=(SELECT @operacion id_operacion,'SQL:'+CONVERT(varchar(36),@operacion) id_movimiento,@tipo tipo_movimiento,
   @piezas cantidad_movimiento,@saldo saldo_anterior,CONVERT(int,@posterior) saldo_posterior,
   CONVERT(varchar(33),@fecha,126)+'Z' fecha_movimiento,'CONTINUACION' origen FOR JSON PATH,WITHOUT_ARRAY_WRAPPER);
  INSERT mes.OPERACION(id_operacion,tipo,cuenta,tipo_cuenta,puesto_cuenta,solicitud,respuesta,fecha_utc)
   VALUES(@operacion,'MOVIMIENTO',@cuenta,@tipo_cuenta,@puesto_cuenta,@solicitud,@respuesta,@fecha);
  INSERT mes.MOVIMIENTO(id_operacion,id_ubicacion,numero_parte,id_puesto,tipo,cantidad_capturada,unidad_capturada,empaque_aplicado,piezas,saldo_anterior,saldo_posterior,es_consumo,es_simulado)
   SELECT @operacion,@ubicacion,@parte,@puesto,@tipo,@cantidad,'CAJAS',@ppc,@piezas,@saldo,CONVERT(int,@posterior),@consumo,es_simulado FROM mes.CONFIGURACION WHERE id=1;
  COMMIT; SELECT @respuesta respuesta;
 END TRY
 BEGIN CATCH IF XACT_STATE()<>0 ROLLBACK; THROW; END CATCH;
END;
GO
CREATE OR ALTER PROCEDURE mes.AjustarCatalogo
 @operacion uniqueidentifier,@cuenta nvarchar(80),@tipo_cuenta varchar(30),@puesto_cuenta int=NULL,@solicitud nvarchar(max)
AS
BEGIN
 SET NOCOUNT ON; SET XACT_ABORT ON;
 IF @tipo_cuenta='ADMIN_PUESTOS' AND NOT EXISTS(SELECT 1 FROM mes.USUARIO_PILOTO WHERE usuario=@cuenta AND rol='ADMIN' AND activo=1)
  THROW 51003,'Cuenta administrativa no autorizada.',1;
 IF @operacion IS NULL OR @operacion='00000000-0000-0000-0000-000000000000' OR @solicitud IS NULL OR ISJSON(@solicitud)<>1
  THROW 51004,'Identificador o ajuste inválidos.',1;
 IF @tipo_cuenta IS NULL OR @tipo_cuenta<>'ADMIN_PUESTOS' OR @puesto_cuenta IS NOT NULL OR @cuenta IS NULL OR LEN(LTRIM(RTRIM(@cuenta)))=0
  THROW 51003,'Solo administración puede ajustar catálogos.',1;
 DECLARE @c TABLE(rack int PRIMARY KEY,anterior int,nueva int);
 DECLARE @m TABLE(parte varchar(50) COLLATE Latin1_General_100_BIN2,campo varchar(30) COLLATE Latin1_General_100_BIN2,anterior nvarchar(250),nuevo nvarchar(250),PRIMARY KEY(parte,campo));
 -- Validar longitudes antes de convertir, para no truncar silenciosamente.
 IF EXISTS(SELECT 1 FROM OPENJSON(@solicitud,'$.Materiales') WITH(Parte nvarchar(max),Campo nvarchar(max),Anterior nvarchar(max),Nuevo nvarchar(max))
  WHERE Parte IS NULL OR LEN(Parte)>50 OR Campo IS NULL OR LEN(Campo)>30 OR LEN(Anterior)>250 OR Nuevo IS NULL OR LEN(Nuevo)>250)
  THROW 51004,'El material o sus valores no son válidos.',1;
 INSERT @c SELECT TRY_CONVERT(int,JSON_VALUE(value,'$.Estanteria')),TRY_CONVERT(int,JSON_VALUE(value,'$.Anterior')),TRY_CONVERT(int,JSON_VALUE(value,'$.Nueva')) FROM OPENJSON(@solicitud,'$.Capacidades');
 INSERT @m SELECT Parte,Campo,Anterior,Nuevo FROM OPENJSON(@solicitud,'$.Materiales') WITH(Parte varchar(50),Campo varchar(30),Anterior nvarchar(250),Nuevo nvarchar(250));
 IF NOT EXISTS(SELECT 1 FROM @c) AND NOT EXISTS(SELECT 1 FROM @m) THROW 51004,'No hay cambios.',1;
 IF EXISTS(SELECT 1 FROM @c WHERE rack<=0 OR anterior IS NULL OR nueva IS NULL OR nueva NOT BETWEEN 1 AND 10000 OR anterior=nueva)
  THROW 51004,'Capacidad inválida o sin cambios.',1;
 IF EXISTS(SELECT 1 FROM @m WHERE campo NOT IN ('descripcion','uso','cantidad_por_caja','minimo','variante')
  OR DATALENGTH(campo)<>LEN(campo) OR DATALENGTH(nuevo)<>DATALENGTH(LTRIM(RTRIM(nuevo))) OR nuevo='' OR (nuevo=anterior AND anterior IS NOT NULL)
  OR (campo='uso' AND nuevo COLLATE Latin1_General_100_BIN2 NOT IN ('COMPARTIDO','EXCLUSIVO'))
  OR (campo='variante' AND LEN(nuevo)>10)
  OR (campo='cantidad_por_caja' AND (TRY_CONVERT(int,nuevo) IS NULL OR TRY_CONVERT(int,nuevo) NOT BETWEEN 1 AND 1000000))
  OR (campo='minimo' AND (TRY_CONVERT(int,nuevo) IS NULL OR TRY_CONVERT(int,nuevo) NOT BETWEEN 0 AND 100000000)))
  THROW 51004,'Campo o valor de material inválido.',1;
 BEGIN TRY
  BEGIN TRANSACTION;
  DECLARE @lock int;
  EXEC @lock=sys.sp_getapplock @Resource='mes_inventario_v1',@LockMode='Exclusive',@LockOwner='Transaction',@LockTimeout=10000;
  IF @lock<0 THROW 51009,'Inventario ocupado. Reintenta la misma operación.',1;
  IF NOT EXISTS(SELECT 1 FROM mes.BASE_INVENTARIO WHERE id=1) THROW 51004,'Falta aprobar el saldo inicial.',1;
  IF EXISTS(SELECT 1 FROM mes.OPERACION WHERE id_operacion=@operacion)
  BEGIN
   IF NOT EXISTS(SELECT 1 FROM mes.OPERACION WHERE id_operacion=@operacion AND tipo='AJUSTE'
    AND cuenta COLLATE Latin1_General_100_BIN2=@cuenta COLLATE Latin1_General_100_BIN2 AND tipo_cuenta=@tipo_cuenta AND puesto_cuenta IS NULL
    AND solicitud COLLATE Latin1_General_100_BIN2=@solicitud COLLATE Latin1_General_100_BIN2)
    THROW 51009,'El identificador ya pertenece a otra operación.',1;
   COMMIT; SELECT respuesta FROM mes.OPERACION WHERE id_operacion=@operacion; RETURN;
  END;
  IF EXISTS(SELECT 1 FROM @c c LEFT JOIN dbo.ESTANTERIA e ON e.id_estanteria=c.rack WHERE e.id_estanteria IS NULL OR e.cajas_estanteria<>c.anterior)
   THROW 51009,'La capacidad cambió o no existe.',1;
  IF EXISTS(SELECT 1 FROM @m c LEFT JOIN dbo.MATERIAL m ON m.numero_parte COLLATE Latin1_General_100_BIN2=c.parte
   LEFT JOIN dbo.COLOR k ON k.id_color=m.id_color
   WHERE m.numero_parte IS NULL OR EXISTS(SELECT
    CASE c.campo WHEN 'descripcion' THEN CONVERT(nvarchar(250),m.descripcion) WHEN 'uso' THEN CONVERT(nvarchar(250),m.uso)
    WHEN 'cantidad_por_caja' THEN CONVERT(nvarchar(250),m.cantidad_por_caja) WHEN 'minimo' THEN CONVERT(nvarchar(250),m.minimo)
    WHEN 'variante' THEN CONVERT(nvarchar(250),k.nombre) END COLLATE Latin1_General_100_BIN2
    EXCEPT SELECT c.anterior COLLATE Latin1_General_100_BIN2))
   THROW 51009,'El material cambió o no existe. Actualiza la vista previa.',1;
  IF EXISTS(SELECT 1 FROM @m c WHERE campo='variante' AND NOT EXISTS(SELECT 1 FROM dbo.COLOR k WHERE CONVERT(nvarchar(250),k.nombre) COLLATE Latin1_General_100_BIN2=c.nuevo COLLATE Latin1_General_100_BIN2))
   THROW 51004,'Una variante no existe en COLOR.',1;
  UPDATE m SET descripcion=CASE WHEN d.parte IS NULL THEN m.descripcion ELSE d.nuevo END,
   uso=CASE WHEN u.parte IS NULL THEN m.uso ELSE u.nuevo END,
   cantidad_por_caja=CASE WHEN p.parte IS NULL THEN m.cantidad_por_caja ELSE CONVERT(int,p.nuevo) END,
   minimo=CASE WHEN n.parte IS NULL THEN m.minimo ELSE CONVERT(int,n.nuevo) END,
   id_color=CASE WHEN v.parte IS NULL THEN m.id_color ELSE k.id_color END
   FROM dbo.MATERIAL m
   LEFT JOIN @m d ON d.parte=m.numero_parte COLLATE Latin1_General_100_BIN2 AND d.campo='descripcion'
   LEFT JOIN @m u ON u.parte=m.numero_parte COLLATE Latin1_General_100_BIN2 AND u.campo='uso'
   LEFT JOIN @m p ON p.parte=m.numero_parte COLLATE Latin1_General_100_BIN2 AND p.campo='cantidad_por_caja'
   LEFT JOIN @m n ON n.parte=m.numero_parte COLLATE Latin1_General_100_BIN2 AND n.campo='minimo'
   LEFT JOIN @m v ON v.parte=m.numero_parte COLLATE Latin1_General_100_BIN2 AND v.campo='variante'
   LEFT JOIN dbo.COLOR k ON CONVERT(nvarchar(250),k.nombre) COLLATE Latin1_General_100_BIN2=v.nuevo COLLATE Latin1_General_100_BIN2
   WHERE EXISTS(SELECT 1 FROM @m a WHERE a.parte=m.numero_parte COLLATE Latin1_General_100_BIN2);
  UPDATE e SET cajas_estanteria=c.nueva FROM dbo.ESTANTERIA e JOIN @c c ON c.rack=e.id_estanteria;
  IF EXISTS(SELECT 1 FROM dbo.ESTANTERIA e CROSS APPLY(
   SELECT SUM((CONVERT(bigint,u.cantidad)+m.cantidad_por_caja-1)/m.cantidad_por_caja) ocupadas
   FROM dbo.UBICACION u JOIN dbo.MATERIAL m ON m.numero_parte=u.numero_parte WHERE u.id_estanteria=e.id_estanteria) x
   WHERE x.ocupadas>e.cajas_estanteria AND (EXISTS(SELECT 1 FROM @c c WHERE c.rack=e.id_estanteria)
    OR EXISTS(SELECT 1 FROM dbo.UBICACION u JOIN @m m ON m.parte=u.numero_parte COLLATE Latin1_General_100_BIN2 WHERE u.id_estanteria=e.id_estanteria AND m.campo='cantidad_por_caja')))
   THROW 51009,'El ajuste dejaría menos capacidad que cajas ocupadas.',1;
  UPDATE u SET version_concurrencia=version_concurrencia+1 FROM dbo.UBICACION u
   WHERE EXISTS(SELECT 1 FROM @c c WHERE c.rack=u.id_estanteria) OR EXISTS(SELECT 1 FROM @m m WHERE m.parte=u.numero_parte COLLATE Latin1_General_100_BIN2);
  DECLARE @fecha datetime2(7)=SYSUTCDATETIME(),@respuesta nvarchar(max),@rack int=(SELECT MIN(rack) FROM @c);
  SELECT @respuesta=(SELECT @operacion id_operacion,(SELECT COUNT(*) FROM @m) cambios,
   (SELECT COUNT(DISTINCT parte) FROM @m) materiales,@rack id_estanteria,
   (SELECT cajas_estanteria FROM dbo.ESTANTERIA WHERE id_estanteria=@rack) cajas_estanteria,
   (SELECT e.cajas_estanteria-COALESCE(SUM((CONVERT(bigint,u.cantidad)+m.cantidad_por_caja-1)/m.cantidad_por_caja),0)
    FROM dbo.ESTANTERIA e LEFT JOIN dbo.UBICACION u ON u.id_estanteria=e.id_estanteria LEFT JOIN dbo.MATERIAL m ON m.numero_parte=u.numero_parte
    WHERE e.id_estanteria=@rack GROUP BY e.cajas_estanteria) cajas_disponibles,
   CONVERT(varchar(33),@fecha,126)+'Z' fecha FOR JSON PATH,WITHOUT_ARRAY_WRAPPER);
  INSERT mes.OPERACION(id_operacion,tipo,cuenta,tipo_cuenta,puesto_cuenta,solicitud,respuesta,fecha_utc)
   VALUES(@operacion,'AJUSTE',@cuenta,@tipo_cuenta,NULL,@solicitud,@respuesta,@fecha);
  INSERT mes.AJUSTE SELECT @operacion,'CAPACIDAD',CONVERT(nvarchar(100),rack),'cajas_estanteria',CONVERT(nvarchar(250),anterior),CONVERT(nvarchar(250),nueva) FROM @c;
  INSERT mes.AJUSTE SELECT @operacion,'MATERIAL',parte,campo,anterior,nuevo FROM @m;
  COMMIT; SELECT @respuesta respuesta;
 END TRY
 BEGIN CATCH IF XACT_STATE()<>0 ROLLBACK; THROW; END CATCH;
END;
GO
