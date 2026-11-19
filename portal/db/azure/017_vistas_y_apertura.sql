CREATE OR ALTER VIEW dbo.vw_modelo_puestos AS SELECT fecha_utc fecha_stock FROM mes.BASE_INVENTARIO WHERE id=1;
GO
CREATE OR ALTER VIEW dbo.vw_kpi_inventario_actual AS
SELECT u.id_ubicacion,u.numero_parte,m.descripcion,m.uso,m.id_puesto,p.Nombre puesto,m.cantidad_por_caja,m.minimo,
 u.cantidad,u.version_concurrencia,e.id_estanteria,e.estanteria,e.nivel,e.cajas_estanteria,
 s.id_estacion,s.nombre_estacion estacion,SUM(CONVERT(bigint,u.cantidad)) OVER(PARTITION BY u.numero_parte) stock_total_material,
 cfg.es_simulado,c.nombre variante_revision,b.fecha_utc fecha_stock,
 CASE WHEN e.cajas_estanteria-x.ocupadas<0 THEN 0 ELSE e.cajas_estanteria-x.ocupadas END cajas_disponibles
FROM dbo.UBICACION u JOIN dbo.MATERIAL m ON m.numero_parte=u.numero_parte JOIN dbo.PUESTO_LOGISTICO p ON p.id_puesto=m.id_puesto
JOIN dbo.ESTANTERIA e ON e.id_estanteria=u.id_estanteria JOIN dbo.ESTACION s ON s.id_estacion=e.id_estacion
LEFT JOIN dbo.COLOR c ON c.id_color=m.id_color CROSS JOIN mes.CONFIGURACION cfg CROSS JOIN mes.BASE_INVENTARIO b
OUTER APPLY(SELECT SUM((CONVERT(bigint,u2.cantidad)+m2.cantidad_por_caja-1)/m2.cantidad_por_caja) ocupadas
 FROM dbo.UBICACION u2 JOIN dbo.MATERIAL m2 ON m2.numero_parte=u2.numero_parte WHERE u2.id_estanteria=e.id_estanteria) x;
GO
CREATE OR ALTER VIEW mes.vw_movimientos AS
SELECT 'SQL:'+CONVERT(varchar(36),o.id_operacion) id_movimiento,o.id_operacion,'CONTINUACION' origen,'CONTINUACION' espacio_identificadores,
 m.es_simulado,m.numero_parte,m.id_puesto,m.id_ubicacion,m.tipo tipo_movimiento,m.piezas cantidad_piezas,
 m.cantidad_capturada,m.unidad_capturada,m.empaque_aplicado cantidad_por_caja_aplicada,
 m.es_consumo es_consumo_produccion,m.saldo_anterior,m.saldo_posterior,o.fecha_utc fecha_movimiento,
 o.cuenta usuario_captura,CONVERT(nvarchar(80),NULL) responsable_simulado,'PORTAL_SQL_V1' proceso_generador,
 s.nombre_estacion estacion,e.estanteria,e.nivel,CONVERT(int,NULL) id_turno
FROM mes.OPERACION o JOIN mes.MOVIMIENTO m ON m.id_operacion=o.id_operacion
JOIN dbo.UBICACION u ON u.id_ubicacion=m.id_ubicacion JOIN dbo.ESTANTERIA e ON e.id_estanteria=u.id_estanteria
JOIN dbo.ESTACION s ON s.id_estacion=e.id_estacion;
GO
CREATE OR ALTER VIEW mes.vw_ajustes AS
SELECT o.id_operacion,o.fecha_utc,o.cuenta,o.tipo_cuenta,a.entidad,a.clave,a.campo,a.anterior,a.nuevo
FROM mes.OPERACION o JOIN mes.AJUSTE a ON a.id_operacion=o.id_operacion;
GO
CREATE OR ALTER FUNCTION dbo.TurnoDeAtencion(@inicio datetime2(7)) RETURNS int AS
BEGIN
 DECLARE @hora time=CONVERT(time,@inicio),@turno int;
 SELECT @turno=CASE WHEN COUNT(*)=1 THEN MIN(id_turno) END FROM dbo.TURNO
 WHERE (hora_inicio<hora_fin AND @hora>=hora_inicio AND @hora<hora_fin)
  OR (hora_inicio>hora_fin AND (@hora>=hora_inicio OR @hora<hora_fin));
 RETURN @turno;
END;
GO
CREATE OR ALTER PROCEDURE dbo.IniciarAtencion @puesto int,@ubicacion int AS
BEGIN
 SET NOCOUNT ON; SET XACT_ABORT ON;
 BEGIN TRY
  BEGIN TRANSACTION;
  IF NOT EXISTS(SELECT 1 FROM dbo.UBICACION u JOIN dbo.MATERIAL m ON m.numero_parte=u.numero_parte
   JOIN dbo.PUESTO_LOGISTICO p ON p.id_puesto=m.id_puesto WHERE u.id_ubicacion=@ubicacion AND m.id_puesto=@puesto AND p.usuario IS NOT NULL)
   THROW 51003,'La ubicación no pertenece a tu puesto.',1;
  DECLARE @id int;
  SELECT @id=id_registro FROM dbo.REGISTRO WITH(UPDLOCK,HOLDLOCK) WHERE id_puesto=@puesto AND id_ubicacion=@ubicacion AND fecha_hora_fin IS NULL;
  IF @id IS NULL
  BEGIN
   DECLARE @inicio datetime2(7)=CONVERT(datetime2(7),SYSDATETIMEOFFSET() AT TIME ZONE 'Central Standard Time (Mexico)');
   DECLARE @turno int=dbo.TurnoDeAtencion(@inicio);
   IF @turno IS NULL THROW 51004,'No hay un turno único para esta hora. Revisa el calendario de turnos.',1;
   INSERT dbo.REGISTRO(id_puesto,id_ubicacion,id_turno,fecha_hora_inicio) VALUES(@puesto,@ubicacion,@turno,@inicio);
   SET @id=CONVERT(int,SCOPE_IDENTITY());
  END;
  COMMIT; SELECT * FROM dbo.vw_kpi_atenciones WHERE id_registro=@id;
 END TRY
 BEGIN CATCH IF XACT_STATE()<>0 ROLLBACK; THROW; END CATCH;
END;
GO
CREATE OR ALTER VIEW dbo.vw_kpi_atenciones AS
SELECT r.id_registro,r.id_puesto,p.Nombre puesto,r.id_ubicacion,u.numero_parte,
 CONVERT(datetime2(7),r.fecha_hora_inicio AT TIME ZONE 'Central Standard Time (Mexico)' AT TIME ZONE 'UTC') fecha_hora_inicio,
 CONVERT(datetime2(7),r.fecha_hora_fin AT TIME ZONE 'Central Standard Time (Mexico)' AT TIME ZONE 'UTC') fecha_hora_fin,
 DATEDIFF_BIG(millisecond,r.fecha_hora_inicio,r.fecha_hora_fin) duracion_ms,t.nombre_turno turno_inicio,
 cfg.es_simulado,r.id_turno,r.fecha_hora_inicio fecha_hora_inicio_local,r.fecha_hora_fin fecha_hora_fin_local
FROM dbo.REGISTRO r JOIN dbo.PUESTO_LOGISTICO p ON p.id_puesto=r.id_puesto JOIN dbo.UBICACION u ON u.id_ubicacion=r.id_ubicacion
LEFT JOIN dbo.TURNO t ON t.id_turno=r.id_turno CROSS JOIN mes.CONFIGURACION cfg;
GO
-- Solo aprovisionamiento; portal_app no recibe permiso para ejecutar esta apertura.
CREATE OR ALTER PROCEDURE mes.FijarSaldoInicial AS
BEGIN
 SET NOCOUNT ON; SET XACT_ABORT ON;
 BEGIN TRY
  BEGIN TRANSACTION;
  DECLARE @lock int; EXEC @lock=sys.sp_getapplock @Resource='mes_inventario_v1',@LockMode='Exclusive',@LockOwner='Transaction',@LockTimeout=10000;
  IF @lock<0 THROW 51009,'Inventario ocupado.',1;
  IF EXISTS(SELECT 1 FROM mes.BASE_INVENTARIO) OR EXISTS(SELECT 1 FROM mes.OPERACION)
   THROW 51009,'Ya hay apertura u operaciones: no se reemplazó el saldo inicial.',1;
  IF NOT EXISTS(SELECT 1 FROM dbo.UBICACION) THROW 51004,'Faltan datos iniciales autorizados.',1;
  IF EXISTS(SELECT 1 FROM dbo.MATERIAL m WHERE m.stock<>COALESCE((SELECT SUM(CONVERT(bigint,cantidad)) FROM dbo.UBICACION WHERE numero_parte=m.numero_parte),0))
   THROW 51004,'El stock no coincide con las ubicaciones.',1;
  DECLARE @fecha datetime2(7)=SYSUTCDATETIME(),@filas nvarchar(max);
  SELECT @filas=(SELECT u.id_ubicacion,u.numero_parte,m.descripcion,m.uso,m.id_puesto,p.Nombre puesto,m.cantidad_por_caja,m.minimo,
   u.cantidad,u.version_concurrencia,e.id_estanteria,e.estanteria,e.nivel,e.cajas_estanteria,s.id_estacion,s.nombre_estacion estacion,
   SUM(CONVERT(bigint,u.cantidad)) OVER(PARTITION BY u.numero_parte) stock_total_material,c.nombre variante_revision,
   cfg.es_simulado,@fecha fecha_stock FROM dbo.UBICACION u JOIN dbo.MATERIAL m ON m.numero_parte=u.numero_parte
   JOIN dbo.PUESTO_LOGISTICO p ON p.id_puesto=m.id_puesto JOIN dbo.ESTANTERIA e ON e.id_estanteria=u.id_estanteria
   JOIN dbo.ESTACION s ON s.id_estacion=e.id_estacion LEFT JOIN dbo.COLOR c ON c.id_color=m.id_color CROSS JOIN mes.CONFIGURACION cfg
   FOR JSON PATH,INCLUDE_NULL_VALUES);
  INSERT mes.BASE_INVENTARIO(id,fecha_utc,filas_json) VALUES(1,@fecha,@filas);
  COMMIT;
 END TRY
 BEGIN CATCH IF XACT_STATE()<>0 ROLLBACK; THROW; END CATCH;
END;
GO
-- Consulta paginada: no descarga todo el legado para mostrar una página.
CREATE OR ALTER VIEW mes.vw_historial_json AS
SELECT (SELECT v.* FOR JSON PATH,INCLUDE_NULL_VALUES,WITHOUT_ARRAY_WRAPPER) datos,
 v.fecha_movimiento,v.numero_parte,v.id_ubicacion,v.espacio_identificadores,v.usuario_captura responsable,v.id_turno,v.id_movimiento
FROM mes.vw_movimientos v
UNION ALL
SELECT h.datos,TRY_CONVERT(datetime2(7),JSON_VALUE(h.datos,'$.fecha_movimiento'),127),
 CONVERT(varchar(50),JSON_VALUE(h.datos,'$.numero_parte')),TRY_CONVERT(int,JSON_VALUE(h.datos,'$.id_ubicacion')),
 CONVERT(varchar(30),JSON_VALUE(h.datos,'$.espacio_identificadores')),
 COALESCE(JSON_VALUE(h.datos,'$.usuario_captura'),JSON_VALUE(h.datos,'$.responsable_simulado')),
 TRY_CONVERT(int,JSON_VALUE(h.datos,'$.id_turno')),JSON_VALUE(h.datos,'$.id_movimiento')
FROM mes.HISTORICO_LEGADO h JOIN mes.ARCHIVO_LEGADO a ON a.sha256=h.sha256 AND a.completo=1 WHERE h.archivo IN ('movimientos.jsonl.gz','movimientos-diario.jsonl');
GO
CREATE INDEX IX_LEGADO_archivo ON mes.HISTORICO_LEGADO(archivo);
GO
