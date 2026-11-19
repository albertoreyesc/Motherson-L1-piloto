-- SOLO LECTURA. No presupone los conteos de una simulación.
SET NOCOUNT ON;
SELECT DB_NAME() AS base_actual;
SELECT esperado.nombre,esperado.tipo,
       CASE WHEN OBJECT_ID('dbo.'+esperado.nombre,esperado.tipo) IS NULL THEN 'FALTA' ELSE 'OK' END AS estado
FROM (VALUES ('ESTACION','U'),('COLOR','U'),('PUESTO_LOGISTICO','U'),('TURNO','U'),
 ('ESTANTERIA','U'),('MATERIAL','U'),('UBICACION','U'),('REGISTRO','U'),
 ('vw_modelo_puestos','V'),('vw_kpi_inventario_actual','V'),('vw_kpi_atenciones','V'),('vw_registro_bi','V'),
 ('TurnoDeAtencion','FN'),('RegistrarMovimiento','P'),('IniciarAtencion','P'),('FinalizarAtencion','P'),
 ('GuardarAccesoPuesto','P'),('AjustarCapacidadEstanteria','P'),('AjustarMateriales','P')) esperado(nombre,tipo);
-- Solo consultar datos si existen las tablas; sp_executesql evita compilar nombres inexistentes.
IF OBJECT_ID('dbo.MATERIAL','U') IS NOT NULL AND OBJECT_ID('dbo.UBICACION','U') IS NOT NULL
 EXEC sys.sp_executesql N'
 SELECT COUNT(*) AS ubicaciones,SUM(CONVERT(bigint,cantidad)) AS piezas FROM dbo.UBICACION;
 SELECT m.numero_parte,m.stock,COALESCE(u.total,0) AS total_ubicaciones
 FROM dbo.MATERIAL m LEFT JOIN
 (SELECT numero_parte,SUM(CONVERT(bigint,cantidad)) AS total FROM dbo.UBICACION GROUP BY numero_parte) u
 ON u.numero_parte=m.numero_parte WHERE m.stock<>COALESCE(u.total,0);';
SELECT r.name AS rol,u.name AS miembro FROM sys.database_role_members m
JOIN sys.database_principals r ON r.principal_id=m.role_principal_id
JOIN sys.database_principals u ON u.principal_id=m.member_principal_id
WHERE u.name IN ('portal_app','powerbi_lector');
