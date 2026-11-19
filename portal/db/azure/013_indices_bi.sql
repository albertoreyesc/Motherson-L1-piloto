-- =====================================================================
-- 05 · Índices y vista para Power BI
--
-- Qué hace:
--   - 3 índices en REGISTRO, para que el portal y Power BI no tengan que recorrer la tabla completa:
--       IX_REGISTRO_abiertas         atenciones sin terminar; lo usa IniciarAtencion en cada atención.
--       IX_REGISTRO_ubicacion_inicio surtidos por ubicación en orden de fecha; lo usa la vista de abajo.
--       IX_REGISTRO_inicio           búsquedas por fecha (semanas, periodos, actualización incremental).
--   - La vista dbo.vw_registro_bi, que es lo que Power BI lee como tabla REGISTRO: las mismas columnas
--     de REGISTRO más intervalo_h (horas desde el surtido anterior del mismo material en la misma estación).
-- No crea tablas, no cambia columnas y no toca datos. Se puede correr más de una vez.
--
-- Cómo correrlo: en VS Code, conectados con su usuario administrador. Ejecuten el archivo completo.
-- Antes deben haber corrido 01 a 04. Si la base ya está en uso con el portal, avísenle antes a Equipo 1.
-- Qué deben ver: al final, los 3 índices y la vista en "existe", y las mismas filas en REGISTRO y en la vista.
-- En la pestaña Messages, "logical reads" de la búsqueda de atenciones abiertas baja entre el antes y el después.
-- =====================================================================
SET NOCOUNT ON;
GO

-- 0. Antes: cuánto lee la base para buscar atenciones abiertas (pestaña Messages, "logical reads") --
SET STATISTICS IO ON;
SELECT COUNT(*) AS antes_atenciones_abiertas FROM dbo.REGISTRO WHERE id_puesto = 1 AND id_ubicacion = 1 AND fecha_hora_fin IS NULL;
SET STATISTICS IO OFF;
GO

-- 1. Índices -----------------------------------------------------------
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_REGISTRO_abiertas' AND object_id = OBJECT_ID('dbo.REGISTRO'))
    CREATE NONCLUSTERED INDEX IX_REGISTRO_abiertas
        ON dbo.REGISTRO (id_puesto, id_ubicacion)
        WHERE fecha_hora_fin IS NULL;

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_REGISTRO_ubicacion_inicio' AND object_id = OBJECT_ID('dbo.REGISTRO'))
    CREATE NONCLUSTERED INDEX IX_REGISTRO_ubicacion_inicio
        ON dbo.REGISTRO (id_ubicacion, fecha_hora_inicio)
        INCLUDE (id_puesto, id_turno, fecha_hora_fin);

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_REGISTRO_inicio' AND object_id = OBJECT_ID('dbo.REGISTRO'))
    CREATE NONCLUSTERED INDEX IX_REGISTRO_inicio
        ON dbo.REGISTRO (fecha_hora_inicio)
        INCLUDE (fecha_hora_fin);
GO

-- Después: la misma búsqueda; en Messages, "logical reads" debe bajar de cientos a muy pocas
SET STATISTICS IO ON;
SELECT COUNT(*) AS despues_atenciones_abiertas FROM dbo.REGISTRO WHERE id_puesto = 1 AND id_ubicacion = 1 AND fecha_hora_fin IS NULL;
SET STATISTICS IO OFF;
GO

-- 2. Vista que lee Power BI ----------------------------------------------
-- intervalo_h = horas entre este surtido y el anterior del mismo material en la misma estación.
-- Antes era una consulta escrita dentro de Power BI; aquí da exactamente el mismo resultado.
CREATE OR ALTER VIEW dbo.vw_registro_bi AS
SELECT r.id_registro, r.id_puesto, r.id_ubicacion, r.id_turno, r.fecha_hora_inicio, r.fecha_hora_fin,
       CAST(DATEDIFF_BIG(second,
                LAG(r.fecha_hora_inicio) OVER (PARTITION BY u.numero_parte, s.id_estacion
                                               ORDER BY r.fecha_hora_inicio, r.id_registro),
                r.fecha_hora_inicio) AS FLOAT) / 3600.0 AS intervalo_h
FROM dbo.REGISTRO r
JOIN dbo.UBICACION u  ON u.id_ubicacion  = r.id_ubicacion
JOIN dbo.ESTANTERIA s ON s.id_estanteria = u.id_estanteria;
GO

-- Power BI (powerbi_lector) y las personas de consulta leen la vista porque están en db_datareader.

-- 3. Revisión -------------------------------------------------------------
SELECT o.objeto,
       CASE WHEN o.existe = 1 THEN 'existe' ELSE 'NO EXISTE' END AS estado
FROM (VALUES
        ('IX_REGISTRO_abiertas',         CASE WHEN EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_REGISTRO_abiertas'         AND object_id = OBJECT_ID('dbo.REGISTRO')) THEN 1 ELSE 0 END),
        ('IX_REGISTRO_ubicacion_inicio', CASE WHEN EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_REGISTRO_ubicacion_inicio' AND object_id = OBJECT_ID('dbo.REGISTRO')) THEN 1 ELSE 0 END),
        ('IX_REGISTRO_inicio',           CASE WHEN EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_REGISTRO_inicio'           AND object_id = OBJECT_ID('dbo.REGISTRO')) THEN 1 ELSE 0 END),
        ('vw_registro_bi',               CASE WHEN OBJECT_ID('dbo.vw_registro_bi', 'V') IS NOT NULL THEN 1 ELSE 0 END)
     ) o(objeto, existe);

-- La vista debe tener las mismas filas que REGISTRO
SELECT (SELECT COUNT(*) FROM dbo.REGISTRO)       AS filas_registro,
       (SELECT COUNT(*) FROM dbo.vw_registro_bi) AS filas_vista;
GO
