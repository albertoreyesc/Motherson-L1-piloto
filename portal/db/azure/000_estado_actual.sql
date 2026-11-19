-- SOLO LECTURA. Ejecutar con una cuenta que pueda ver la definición de objetos.
-- No inicia la instalación ni consulta contraseñas.
SET NOCOUNT ON;
SELECT DB_NAME() AS base_actual, SYSUTCDATETIME() AS consulta_utc;
SELECT s.name AS esquema, t.name AS tabla, SUM(p.rows) AS filas_estimadas
FROM sys.tables t JOIN sys.schemas s ON s.schema_id=t.schema_id
LEFT JOIN sys.partitions p ON p.object_id=t.object_id AND p.index_id IN (0,1)
WHERE t.is_ms_shipped=0 GROUP BY s.name,t.name ORDER BY s.name,t.name;
SELECT s.name AS esquema,o.name,o.type_desc
FROM sys.objects o JOIN sys.schemas s ON s.schema_id=o.schema_id
WHERE o.is_ms_shipped=0 AND o.type IN ('V','P','FN') ORDER BY s.name,o.name;
SELECT OBJECT_SCHEMA_NAME(p.object_id) AS esquema, OBJECT_NAME(p.object_id) AS procedimiento,
       p.name AS parametro,TYPE_NAME(p.user_type_id) AS tipo,p.max_length
FROM sys.parameters p WHERE p.object_id=OBJECT_ID('dbo.RegistrarMovimiento') ORDER BY p.parameter_id;
SELECT name,compatibility_level FROM sys.databases WHERE name=DB_NAME();
