-- Instalación inicial autocontenida. Solo después de aprobación. No carga datos ni crea usuarios.
SET NOCOUNT ON;
SET XACT_ABORT ON;
IF DB_NAME() <> N'Motherson_L1' THROW 51100,'Destino incorrecto.',1;
IF EXISTS(SELECT 1 FROM sys.tables WHERE is_ms_shipped=0) THROW 51101,'La base ya tiene tablas. No se instaló nada.',1;
BEGIN TRY
 BEGIN TRANSACTION;
EXEC sys.sp_executesql N'-- =====================================================================
-- 01 · Las 8 tablas del ERD (Motherson L1 · Smart Material Replenishment)
--
-- Cómo correrlo: en VS Code, conectados a su base de Azure SQL con su usuario administrador.
--                Ejecuten el archivo completo una sola vez, sobre una base vacía.
-- Qué crea: ESTACION, COLOR, PUESTO_LOGISTICO, TURNO, ESTANTERIA, MATERIAL, UBICACION, REGISTRO,
--           con sus llaves, relaciones y reglas (CHECK / UNIQUE).
-- Nota: PUESTO_LOGISTICO incluye usuario y password (hash), que el portal de captura usa para
--       el inicio de sesión por puesto. Power BI no los lee.
-- =====================================================================

CREATE TABLE dbo.ESTACION (
    id_estacion      INT IDENTITY(1,1) NOT NULL,
    id_linea         INT               NOT NULL,
    nombre_estacion  VARCHAR(100)      NOT NULL,
    CONSTRAINT PK_ESTACION PRIMARY KEY (id_estacion)
);

CREATE TABLE dbo.COLOR (
    id_color  INT IDENTITY(1,1) NOT NULL,
    nombre    VARCHAR(50)       NOT NULL,
    CONSTRAINT PK_COLOR PRIMARY KEY (id_color),
    CONSTRAINT UQ_COLOR_nombre UNIQUE (nombre)
);

CREATE TABLE dbo.PUESTO_LOGISTICO (
    id_puesto  INT IDENTITY(1,1) NOT NULL,
    Nombre     VARCHAR(100)      NOT NULL,
    Tipo       VARCHAR(50)       NULL,
    usuario    NVARCHAR(80)      NULL,   -- acceso del portal de Equipo 1 (login por puesto)
    [password] NVARCHAR(500)     NULL,   -- hash de la contraseña, nunca la contraseña en claro
    CONSTRAINT PK_PUESTO_LOGISTICO PRIMARY KEY (id_puesto)
);

CREATE TABLE dbo.TURNO (
    id_turno      INT IDENTITY(1,1) NOT NULL,
    nombre_turno  VARCHAR(50)       NOT NULL,
    hora_inicio   TIME(0)           NOT NULL,
    hora_fin      TIME(0)           NOT NULL,
    CONSTRAINT PK_TURNO PRIMARY KEY (id_turno)
);

CREATE TABLE dbo.ESTANTERIA (
    id_estanteria     INT IDENTITY(1,1) NOT NULL,
    id_estacion       INT               NOT NULL,
    estanteria        VARCHAR(50)       NOT NULL,
    nivel             VARCHAR(20)       NULL,
    cajas_estanteria  INT               NOT NULL DEFAULT 0,
    CONSTRAINT PK_ESTANTERIA PRIMARY KEY (id_estanteria),
    CONSTRAINT FK_ESTANTERIA_ESTACION FOREIGN KEY (id_estacion)
        REFERENCES dbo.ESTACION (id_estacion),
    CONSTRAINT CK_ESTANTERIA_cajas CHECK (cajas_estanteria >= 0)
);

CREATE TABLE dbo.MATERIAL (
    numero_parte       VARCHAR(50)  NOT NULL,
    id_puesto          INT          NOT NULL,
    id_color           INT          NULL,
    descripcion        VARCHAR(255) NULL,
    uso                VARCHAR(100) NULL,
    cantidad_por_caja  INT          NOT NULL DEFAULT 1,
    stock              INT          NOT NULL DEFAULT 0,
    minimo             INT          NOT NULL DEFAULT 0,
    CONSTRAINT PK_MATERIAL PRIMARY KEY (numero_parte),
    CONSTRAINT FK_MATERIAL_PUESTO FOREIGN KEY (id_puesto)
        REFERENCES dbo.PUESTO_LOGISTICO (id_puesto),
    CONSTRAINT FK_MATERIAL_COLOR FOREIGN KEY (id_color)
        REFERENCES dbo.COLOR (id_color),
    CONSTRAINT CK_MATERIAL_cantidad CHECK (cantidad_por_caja > 0),
    CONSTRAINT CK_MATERIAL_stock    CHECK (stock >= 0),
    CONSTRAINT CK_MATERIAL_minimo   CHECK (minimo >= 0)
);

CREATE TABLE dbo.UBICACION (
    id_ubicacion   INT IDENTITY(1,1) NOT NULL,
    numero_parte   VARCHAR(50)       NOT NULL,
    id_estanteria  INT               NOT NULL,
    cantidad       INT               NOT NULL DEFAULT 0,
    CONSTRAINT PK_UBICACION PRIMARY KEY (id_ubicacion),
    CONSTRAINT FK_UBICACION_MATERIAL FOREIGN KEY (numero_parte)
        REFERENCES dbo.MATERIAL (numero_parte),
    CONSTRAINT FK_UBICACION_ESTANTERIA FOREIGN KEY (id_estanteria)
        REFERENCES dbo.ESTANTERIA (id_estanteria),
    CONSTRAINT UQ_UBICACION_parte_estanteria UNIQUE (numero_parte, id_estanteria),
    CONSTRAINT CK_UBICACION_cantidad CHECK (cantidad >= 0)
);

CREATE TABLE dbo.REGISTRO (
    id_registro        INT IDENTITY(1,1) NOT NULL,
    id_puesto          INT               NOT NULL,
    id_ubicacion       INT               NOT NULL,
    id_turno           INT               NOT NULL,
    fecha_hora_inicio  DATETIME2(0)      NOT NULL DEFAULT SYSDATETIME(),
    fecha_hora_fin     DATETIME2(0)      NULL,
    CONSTRAINT PK_REGISTRO PRIMARY KEY (id_registro),
    CONSTRAINT FK_REGISTRO_PUESTO FOREIGN KEY (id_puesto)
        REFERENCES dbo.PUESTO_LOGISTICO (id_puesto),
    CONSTRAINT FK_REGISTRO_UBICACION FOREIGN KEY (id_ubicacion)
        REFERENCES dbo.UBICACION (id_ubicacion),
    CONSTRAINT FK_REGISTRO_TURNO FOREIGN KEY (id_turno)
        REFERENCES dbo.TURNO (id_turno),
    CONSTRAINT CK_REGISTRO_fechas CHECK (fecha_hora_fin IS NULL OR fecha_hora_fin >= fecha_hora_inicio)
);
';
EXEC sys.sp_executesql N'-- =====================================================================
-- 02 · dbo.RegistrarMovimiento · entradas y salidas de material
--
-- El portal de captura lo llama en cada movimiento:
--     EXEC dbo.RegistrarMovimiento @ubicacion = 62, @piezas = 120;   -- entra material
--     EXEC dbo.RegistrarMovimiento @ubicacion = 62, @piezas = -30;   -- se consume
-- En una sola transacción suma @piezas a UBICACION.cantidad y recalcula MATERIAL.stock
-- (suma de las cantidades del material). Rechaza 0 piezas, ubicaciones inexistentes y salidas
-- que dejarían la cantidad en negativo. No crea ni cambia tablas.
--
-- Cómo correrlo: en VS Code, conectados con su usuario administrador. Ejecuten el archivo completo.
-- Córranlo ANTES de 03_objetos_portal.sql, porque la vista de inventario de ese archivo lo necesita.
-- =====================================================================

CREATE OR ALTER PROCEDURE dbo.RegistrarMovimiento
    @ubicacion int,
    @piezas int
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    IF @piezas IS NULL OR @piezas = 0
        THROW 51010, ''El movimiento debe tener piezas distintas de 0.'', 1;

    BEGIN TRANSACTION;

    DECLARE @parte varchar(50), @actual int;
    SELECT @parte = numero_parte, @actual = cantidad
    FROM dbo.UBICACION WITH (UPDLOCK, HOLDLOCK)
    WHERE id_ubicacion = @ubicacion;

    IF @parte IS NULL
        THROW 51011, ''La ubicación no existe.'', 1;
    IF @actual + @piezas < 0
        THROW 51012, ''No hay suficientes piezas en la ubicación para esa salida.'', 1;

    UPDATE dbo.UBICACION
    SET cantidad = cantidad + @piezas
    WHERE id_ubicacion = @ubicacion;

    UPDATE dbo.MATERIAL
    SET stock = (SELECT COALESCE(SUM(cantidad), 0) FROM dbo.UBICACION WHERE numero_parte = @parte)
    WHERE numero_parte = @parte;

    COMMIT;

    SELECT u.id_ubicacion, u.numero_parte, u.cantidad, m.stock
    FROM dbo.UBICACION u
    JOIN dbo.MATERIAL m ON m.numero_parte = u.numero_parte
    WHERE u.id_ubicacion = @ubicacion;
END;
';
EXEC sys.sp_executesql N'-- =====================================================================
-- 03 · Objetos que necesita el portal de captura (autoría: Equipo 1)
--
-- Parte A = su script 011: 3 vistas, la función TurnoDeAtencion y los procedimientos
--           GuardarAccesoPuesto, IniciarAtencion y FinalizarAtencion.
-- Parte B = su script 012: la vista de inventario lee UBICACION.cantidad.
-- No crean, borran ni cambian tablas, y no tocan datos. Se pueden correr más de una vez.
--
-- Cómo correrlo: en VS Code, conectados con su usuario administrador. Ejecuten el archivo completo.
-- Antes deben haber corrido 01_crear_tablas.sql y 02_procedimiento_movimiento.sql.
-- Qué deben ver: ningún mensaje en rojo, y la revisión final da 0 y 0.
-- =====================================================================

-- ============================ PARTE A · 011 ============================
/* =====================================================================
   Motherson L1 - Objetos que el portal necesita en la base de Azure
   Fecha: 10/11/2026

   Qué hace:
     - Crea 3 vistas, 1 función y 3 procedimientos en dbo.
     - NO crea, borra ni modifica tablas, y NO toca datos.
     - Se puede correr más de una vez (usa CREATE OR ALTER).

   Hora:
     - dbo.REGISTRO guarda hora de México (igual que el histórico de Azure).
     - La vista dbo.vw_kpi_atenciones es la que lee el portal. Entrega
       fecha_hora_inicio / fecha_hora_fin en UTC porque el portal así las
       espera, y además las columnas *_local en hora de México.
       Power BI debe seguir leyendo dbo.REGISTRO o las columnas *_local.

   NO correr en la base local Motherson_L1_MVP_Demo: sus atenciones están
   en UTC y quedarían mezcladas con hora de México.
   ===================================================================== */

SET NOCOUNT ON;

-- ---------------------------------------------------------------------
-- 0. Revisión previa: si falta alguna columna, avisa y no crea nada.
-- ---------------------------------------------------------------------
DECLARE @requeridas TABLE (tabla sysname, columna sysname);
INSERT @requeridas VALUES
 (''ESTACION'',''id_estacion''),(''ESTACION'',''nombre_estacion''),
 (''ESTANTERIA'',''id_estanteria''),(''ESTANTERIA'',''id_estacion''),(''ESTANTERIA'',''estanteria''),
 (''ESTANTERIA'',''nivel''),(''ESTANTERIA'',''cajas_estanteria''),
 (''PUESTO_LOGISTICO'',''id_puesto''),(''PUESTO_LOGISTICO'',''nombre''),
 (''PUESTO_LOGISTICO'',''usuario''),(''PUESTO_LOGISTICO'',''password''),
 (''COLOR'',''id_color''),(''COLOR'',''nombre''),
 (''MATERIAL'',''numero_parte''),(''MATERIAL'',''id_puesto''),(''MATERIAL'',''descripcion''),(''MATERIAL'',''uso''),
 (''MATERIAL'',''cantidad_por_caja''),(''MATERIAL'',''minimo''),(''MATERIAL'',''id_color''),(''MATERIAL'',''stock''),
 (''UBICACION'',''id_ubicacion''),(''UBICACION'',''numero_parte''),(''UBICACION'',''id_estanteria''),
 (''TURNO'',''id_turno''),(''TURNO'',''nombre_turno''),(''TURNO'',''hora_inicio''),(''TURNO'',''hora_fin''),
 (''REGISTRO'',''id_registro''),(''REGISTRO'',''id_puesto''),(''REGISTRO'',''id_ubicacion''),
 (''REGISTRO'',''fecha_hora_inicio''),(''REGISTRO'',''fecha_hora_fin''),(''REGISTRO'',''id_turno'');

DECLARE @faltan nvarchar(max) = (
    SELECT STRING_AGG(CONVERT(nvarchar(max), r.tabla + ''.'' + r.columna), '', '')
    FROM @requeridas r
    WHERE COL_LENGTH(''dbo.'' + r.tabla, r.columna) IS NULL);

IF @faltan IS NOT NULL
BEGIN
    RAISERROR(''No se creó nada. Faltan estas columnas en dbo: %s'', 16, 1, @faltan);
    SET NOEXEC ON;   -- el resto del script se compila pero no se ejecuta
END;
';
EXEC sys.sp_executesql N'
-- ---------------------------------------------------------------------
-- 1. Fecha del saldo inicial (MATERIAL.stock). El portal la compara con
--    su paquete histórico al arrancar. Valor en UTC; no cambiar.
-- ---------------------------------------------------------------------
CREATE OR ALTER VIEW dbo.vw_modelo_puestos AS
SELECT CONVERT(datetime2(7), ''2026-11-04T18:27:48.8981735'', 126) fecha_stock;
';
EXEC sys.sp_executesql N'
-- ---------------------------------------------------------------------
-- 2. Inventario al corte. version_concurrencia, es_simulado y
--    variante_revision son columnas calculadas de esta vista.
-- ---------------------------------------------------------------------
CREATE OR ALTER VIEW dbo.vw_kpi_inventario_actual AS
SELECT u.id_ubicacion,u.numero_parte,m.descripcion,m.uso,m.id_puesto,p.nombre puesto,m.cantidad_por_caja,m.minimo,
       CASE WHEN COUNT(*) OVER(PARTITION BY u.numero_parte)=1 THEN m.stock END cantidad,
       CONVERT(int,NULL) version_concurrencia,t.id_estanteria,t.estanteria,t.nivel,t.cajas_estanteria,
       s.id_estacion,s.nombre_estacion estacion,CONVERT(bigint,m.stock) stock_total_material,
       CONVERT(bit,1) es_simulado,c.nombre variante_revision, corte.fecha_stock
FROM dbo.UBICACION u JOIN dbo.MATERIAL m ON m.numero_parte=u.numero_parte
JOIN dbo.ESTANTERIA t ON t.id_estanteria=u.id_estanteria JOIN dbo.ESTACION s ON s.id_estacion=t.id_estacion
JOIN dbo.PUESTO_LOGISTICO p ON p.id_puesto=m.id_puesto
LEFT JOIN dbo.COLOR c ON c.id_color=m.id_color CROSS JOIN dbo.vw_modelo_puestos corte;
';
EXEC sys.sp_executesql N'
-- ---------------------------------------------------------------------
-- 3. Turno de una atención. Recibe la hora de México tal como se guarda
--    en REGISTRO.
-- ---------------------------------------------------------------------
CREATE OR ALTER FUNCTION dbo.TurnoDeAtencion(@inicio datetime2(7))
RETURNS int
AS
BEGIN
    DECLARE @hora time(7)=CONVERT(time(7),@inicio);
    DECLARE @turno int=(SELECT id_turno FROM dbo.TURNO WHERE @hora>=hora_inicio AND @hora<hora_fin);
    RETURN @turno;
END;
';
EXEC sys.sp_executesql N'
-- ---------------------------------------------------------------------
-- 4. Atenciones para el portal (UTC) y para análisis (*_local).
-- ---------------------------------------------------------------------
CREATE OR ALTER VIEW dbo.vw_kpi_atenciones AS
SELECT r.id_registro,r.id_puesto,p.nombre puesto,r.id_ubicacion,u.numero_parte,
       CONVERT(datetime2(7),r.fecha_hora_inicio AT TIME ZONE ''Central Standard Time (Mexico)''
                                                AT TIME ZONE ''UTC'') fecha_hora_inicio,
       CONVERT(datetime2(7),r.fecha_hora_fin    AT TIME ZONE ''Central Standard Time (Mexico)''
                                                AT TIME ZONE ''UTC'') fecha_hora_fin,
       DATEDIFF_BIG(millisecond,r.fecha_hora_inicio,r.fecha_hora_fin) duracion_ms,
       t.nombre_turno turno_inicio,CONVERT(bit,1) es_simulado,r.id_turno,
       r.fecha_hora_inicio fecha_hora_inicio_local,r.fecha_hora_fin fecha_hora_fin_local
FROM dbo.REGISTRO r
JOIN dbo.PUESTO_LOGISTICO p ON p.id_puesto=r.id_puesto
JOIN dbo.UBICACION u ON u.id_ubicacion=r.id_ubicacion
LEFT JOIN dbo.TURNO t ON t.id_turno=r.id_turno;
';
EXEC sys.sp_executesql N'
-- ---------------------------------------------------------------------
-- 5. Alta, cambio o baja del acceso de un puesto (usuario y hash).
-- ---------------------------------------------------------------------
CREATE OR ALTER PROCEDURE dbo.GuardarAccesoPuesto
    @puesto int, @usuario nvarchar(80), @hash nvarchar(500)
AS
BEGIN
    SET NOCOUNT ON;
    UPDATE dbo.PUESTO_LOGISTICO SET usuario=@usuario,[password]=@hash WHERE id_puesto=@puesto;
    IF @@ROWCOUNT=0 THROW 51004, ''El puesto no existe.'', 1;
END;
';
EXEC sys.sp_executesql N'
-- ---------------------------------------------------------------------
-- 6. Inicio de una atención: inserta en REGISTRO con hora de México.
-- ---------------------------------------------------------------------
CREATE OR ALTER PROCEDURE dbo.IniciarAtencion @puesto int, @ubicacion int
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;
    BEGIN TRANSACTION;
    IF NOT EXISTS (
        SELECT 1 FROM dbo.UBICACION u JOIN dbo.MATERIAL m ON m.numero_parte=u.numero_parte
        JOIN dbo.PUESTO_LOGISTICO p ON p.id_puesto=m.id_puesto
        WHERE u.id_ubicacion=@ubicacion AND m.id_puesto=@puesto AND p.usuario IS NOT NULL
    ) THROW 51003, ''La ubicación no pertenece a tu puesto.'', 1;
    DECLARE @id int;
    SELECT @id=id_registro FROM dbo.REGISTRO WITH(UPDLOCK,HOLDLOCK)
    WHERE id_puesto=@puesto AND id_ubicacion=@ubicacion AND fecha_hora_fin IS NULL;
    IF @id IS NULL
    BEGIN
        DECLARE @inicio datetime2(7)=CONVERT(datetime2(7),
            SYSDATETIMEOFFSET() AT TIME ZONE ''Central Standard Time (Mexico)'');
        INSERT dbo.REGISTRO(id_puesto,id_ubicacion,fecha_hora_inicio,id_turno)
        VALUES(@puesto,@ubicacion,@inicio,dbo.TurnoDeAtencion(@inicio));
        SET @id=CONVERT(int,SCOPE_IDENTITY());
    END;
    COMMIT;
    SELECT * FROM dbo.vw_kpi_atenciones WHERE id_registro=@id;
END;
';
EXEC sys.sp_executesql N'
-- ---------------------------------------------------------------------
-- 7. Fin de una atención: guarda la hora de México en fecha_hora_fin.
-- ---------------------------------------------------------------------
CREATE OR ALTER PROCEDURE dbo.FinalizarAtencion @puesto int, @registro int
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;
    UPDATE r SET fecha_hora_fin=COALESCE(r.fecha_hora_fin,CONVERT(datetime2(7),
        SYSDATETIMEOFFSET() AT TIME ZONE ''Central Standard Time (Mexico)''))
    FROM dbo.REGISTRO r JOIN dbo.PUESTO_LOGISTICO p ON p.id_puesto=r.id_puesto
    WHERE r.id_registro=@registro AND r.id_puesto=@puesto AND p.usuario IS NOT NULL;
    IF @@ROWCOUNT=0 THROW 51003, ''La atención no pertenece a tu puesto.'', 1;
    SELECT * FROM dbo.vw_kpi_atenciones WHERE id_registro=@registro;
END;
';
EXEC sys.sp_executesql N'
SET NOEXEC OFF;
';
EXEC sys.sp_executesql N'
-- ============================ PARTE B · 012 ============================
/* =====================================================================
   Motherson L1 - Inventario del portal leído de UBICACION.cantidad
   Fecha: 10/11/2026.  Solo para la base de Azure. Va DESPUÉS de la
   parte A de este mismo archivo.

   Por qué: dbo.RegistrarMovimiento cambia UBICACION.cantidad y deja
   MATERIAL.stock = suma de las cantidades del material. Para que el
   portal vea exactamente lo mismo, su vista de inventario debe leer
   UBICACION.cantidad (antes tomaba MATERIAL.stock).

   Solo reemplaza una vista. No toca tablas ni datos.
   ===================================================================== */
SET NOCOUNT ON;
IF COL_LENGTH(''dbo.UBICACION'',''cantidad'') IS NULL OR OBJECT_ID(''dbo.vw_modelo_puestos'',''V'') IS NULL
   OR OBJECT_ID(''dbo.RegistrarMovimiento'',''P'') IS NULL
BEGIN
    RAISERROR(''No se cambió nada. Falta UBICACION.cantidad, dbo.vw_modelo_puestos o dbo.RegistrarMovimiento.'',16,1);
    SET NOEXEC ON;
END;
';
EXEC sys.sp_executesql N'
CREATE OR ALTER VIEW dbo.vw_kpi_inventario_actual AS
SELECT u.id_ubicacion,u.numero_parte,m.descripcion,m.uso,m.id_puesto,p.nombre puesto,m.cantidad_por_caja,m.minimo,
       CONVERT(int,u.cantidad) cantidad,
       CONVERT(int,NULL) version_concurrencia,t.id_estanteria,t.estanteria,t.nivel,t.cajas_estanteria,
       s.id_estacion,s.nombre_estacion estacion,
       SUM(CONVERT(bigint,u.cantidad)) OVER(PARTITION BY u.numero_parte) stock_total_material,
       CONVERT(bit,1) es_simulado,c.nombre variante_revision, corte.fecha_stock
FROM dbo.UBICACION u JOIN dbo.MATERIAL m ON m.numero_parte=u.numero_parte
JOIN dbo.ESTANTERIA t ON t.id_estanteria=u.id_estanteria JOIN dbo.ESTACION s ON s.id_estacion=t.id_estacion
JOIN dbo.PUESTO_LOGISTICO p ON p.id_puesto=m.id_puesto
LEFT JOIN dbo.COLOR c ON c.id_color=m.id_color CROSS JOIN dbo.vw_modelo_puestos corte;
';
EXEC sys.sp_executesql N'
SET NOEXEC OFF;
';
EXEC sys.sp_executesql N'
-- Revisión opcional (una sola consulta): ubicaciones sin cantidad y materiales cuyo stock no es la suma.
-- El portal no permite capturar en ubicaciones con cantidad NULL.
SELECT (SELECT COUNT(*) FROM dbo.UBICACION WHERE cantidad IS NULL) ubicaciones_sin_cantidad,
       (SELECT COUNT(*) FROM dbo.MATERIAL m
         WHERE ISNULL(m.stock,-1) <> ISNULL((SELECT SUM(cantidad) FROM dbo.UBICACION u WHERE u.numero_parte=m.numero_parte),-1)
       ) materiales_con_stock_distinto_a_la_suma;
';
EXEC sys.sp_executesql N'-- =====================================================================
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
';
EXEC sys.sp_executesql N'
-- 0. Antes: cuánto lee la base para buscar atenciones abiertas (pestaña Messages, "logical reads") --
SET STATISTICS IO ON;
SELECT COUNT(*) AS antes_atenciones_abiertas FROM dbo.REGISTRO WHERE id_puesto = 1 AND id_ubicacion = 1 AND fecha_hora_fin IS NULL;
SET STATISTICS IO OFF;
';
EXEC sys.sp_executesql N'
-- 1. Índices -----------------------------------------------------------
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = ''IX_REGISTRO_abiertas'' AND object_id = OBJECT_ID(''dbo.REGISTRO''))
    CREATE NONCLUSTERED INDEX IX_REGISTRO_abiertas
        ON dbo.REGISTRO (id_puesto, id_ubicacion)
        WHERE fecha_hora_fin IS NULL;

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = ''IX_REGISTRO_ubicacion_inicio'' AND object_id = OBJECT_ID(''dbo.REGISTRO''))
    CREATE NONCLUSTERED INDEX IX_REGISTRO_ubicacion_inicio
        ON dbo.REGISTRO (id_ubicacion, fecha_hora_inicio)
        INCLUDE (id_puesto, id_turno, fecha_hora_fin);

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = ''IX_REGISTRO_inicio'' AND object_id = OBJECT_ID(''dbo.REGISTRO''))
    CREATE NONCLUSTERED INDEX IX_REGISTRO_inicio
        ON dbo.REGISTRO (fecha_hora_inicio)
        INCLUDE (fecha_hora_fin);
';
EXEC sys.sp_executesql N'
-- Después: la misma búsqueda; en Messages, "logical reads" debe bajar de cientos a muy pocas
SET STATISTICS IO ON;
SELECT COUNT(*) AS despues_atenciones_abiertas FROM dbo.REGISTRO WHERE id_puesto = 1 AND id_ubicacion = 1 AND fecha_hora_fin IS NULL;
SET STATISTICS IO OFF;
';
EXEC sys.sp_executesql N'
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
';
EXEC sys.sp_executesql N'
-- Power BI (powerbi_lector) y las personas de consulta leen la vista porque están en db_datareader.

-- 3. Revisión -------------------------------------------------------------
SELECT o.objeto,
       CASE WHEN o.existe = 1 THEN ''existe'' ELSE ''NO EXISTE'' END AS estado
FROM (VALUES
        (''IX_REGISTRO_abiertas'',         CASE WHEN EXISTS (SELECT 1 FROM sys.indexes WHERE name = ''IX_REGISTRO_abiertas''         AND object_id = OBJECT_ID(''dbo.REGISTRO'')) THEN 1 ELSE 0 END),
        (''IX_REGISTRO_ubicacion_inicio'', CASE WHEN EXISTS (SELECT 1 FROM sys.indexes WHERE name = ''IX_REGISTRO_ubicacion_inicio'' AND object_id = OBJECT_ID(''dbo.REGISTRO'')) THEN 1 ELSE 0 END),
        (''IX_REGISTRO_inicio'',           CASE WHEN EXISTS (SELECT 1 FROM sys.indexes WHERE name = ''IX_REGISTRO_inicio''           AND object_id = OBJECT_ID(''dbo.REGISTRO'')) THEN 1 ELSE 0 END),
        (''vw_registro_bi'',               CASE WHEN OBJECT_ID(''dbo.vw_registro_bi'', ''V'') IS NOT NULL THEN 1 ELSE 0 END)
     ) o(objeto, existe);

-- La vista debe tener las mismas filas que REGISTRO
SELECT (SELECT COUNT(*) FROM dbo.REGISTRO)       AS filas_registro,
       (SELECT COUNT(*) FROM dbo.vw_registro_bi) AS filas_vista;
';
EXEC sys.sp_executesql N'-- Diario transaccional: no sustituye ni elimina las ocho tablas operativas.
IF SCHEMA_ID(''mes'') IS NULL EXEC(''CREATE SCHEMA mes AUTHORIZATION dbo'');
';
EXEC sys.sp_executesql N'
ALTER TABLE dbo.UBICACION ADD version_concurrencia int NOT NULL CONSTRAINT DF_UBICACION_version DEFAULT 1;
CREATE UNIQUE INDEX UX_PUESTO_usuario ON dbo.PUESTO_LOGISTICO(usuario) WHERE usuario IS NOT NULL;
CREATE TABLE mes.CONFIGURACION (
 id int NOT NULL PRIMARY KEY CHECK(id=1), version_esquema int NOT NULL,
 es_simulado bit NOT NULL, creado_utc datetime2(7) NOT NULL DEFAULT SYSUTCDATETIME()
);
INSERT mes.CONFIGURACION(id,version_esquema,es_simulado) VALUES(1,1,1);
CREATE TABLE mes.BASE_INVENTARIO (
 id int NOT NULL PRIMARY KEY CHECK(id=1), fecha_utc datetime2(7) NOT NULL,
 filas_json nvarchar(max) NOT NULL CHECK(ISJSON(filas_json)=1)
);
CREATE TABLE mes.OPERACION (
 id_operacion uniqueidentifier NOT NULL PRIMARY KEY,
 secuencia bigint IDENTITY NOT NULL UNIQUE,
 tipo varchar(20) NOT NULL CHECK(tipo IN (''MOVIMIENTO'',''AJUSTE'')),
 cuenta nvarchar(80) NOT NULL, tipo_cuenta varchar(30) NOT NULL, puesto_cuenta int NULL,
 solicitud nvarchar(max) NOT NULL CHECK(ISJSON(solicitud)=1),
 respuesta nvarchar(max) NOT NULL CHECK(ISJSON(respuesta)=1),
 fecha_utc datetime2(7) NOT NULL DEFAULT SYSUTCDATETIME()
);
CREATE TABLE mes.MOVIMIENTO (
 id_operacion uniqueidentifier NOT NULL PRIMARY KEY REFERENCES mes.OPERACION(id_operacion),
 id_ubicacion int NOT NULL REFERENCES dbo.UBICACION(id_ubicacion),
 numero_parte varchar(50) NOT NULL, id_puesto int NOT NULL,
 tipo varchar(10) NOT NULL CHECK(tipo IN (''ENTRADA'',''SALIDA'')),
 cantidad_capturada int NOT NULL CHECK(cantidad_capturada>0),
 unidad_capturada varchar(10) NOT NULL CHECK(unidad_capturada=''CAJAS''),
 empaque_aplicado int NOT NULL CHECK(empaque_aplicado>0),
 piezas int NOT NULL CHECK(piezas>0), saldo_anterior int NOT NULL CHECK(saldo_anterior>=0),
 saldo_posterior int NOT NULL CHECK(saldo_posterior>=0), es_consumo bit NOT NULL,
 es_simulado bit NOT NULL,
 CONSTRAINT CK_MOVIMIENTO_saldo CHECK(saldo_posterior=saldo_anterior+CASE WHEN tipo=''ENTRADA'' THEN piezas ELSE -piezas END)
);
CREATE INDEX IX_MOVIMIENTO_ubicacion ON mes.MOVIMIENTO(id_ubicacion);
CREATE TABLE mes.AJUSTE (
 id_operacion uniqueidentifier NOT NULL REFERENCES mes.OPERACION(id_operacion),
 entidad varchar(20) NOT NULL, clave nvarchar(100) NOT NULL, campo varchar(30) NOT NULL,
 anterior nvarchar(250) NULL, nuevo nvarchar(250) NULL,
 PRIMARY KEY(id_operacion,entidad,clave,campo)
);
CREATE TABLE mes.ARCHIVO_LEGADO (
 sha256 char(64) NOT NULL PRIMARY KEY, archivo nvarchar(120) NOT NULL,
 filas_esperadas int NOT NULL CHECK(filas_esperadas>=0), completo bit NOT NULL DEFAULT 0,
 confirmado_utc datetime2(7) NULL
);
CREATE TABLE mes.HISTORICO_LEGADO (
 sha256 char(64) NOT NULL, linea int NOT NULL CHECK(linea>0), archivo nvarchar(120) NOT NULL,
 datos nvarchar(max) NOT NULL CHECK(ISJSON(datos)=1), importado_utc datetime2(7) NOT NULL DEFAULT SYSUTCDATETIME(),
 PRIMARY KEY(sha256,linea)
);
';
EXEC sys.sp_executesql N'-- Un bloqueo transaccional común serializa movimientos, ajustes y apertura del inventario.
-- La respuesta durable se devuelve también cuando se repite el UUID de una petición idéntica.
CREATE OR ALTER PROCEDURE mes.RegistrarMovimiento
 @operacion uniqueidentifier,@cuenta nvarchar(80),@tipo_cuenta varchar(30),@puesto_cuenta int=NULL,@solicitud nvarchar(max)
AS
BEGIN
 SET NOCOUNT ON; SET XACT_ABORT ON;
 IF @operacion IS NULL OR @operacion=''00000000-0000-0000-0000-000000000000'' OR ISJSON(@solicitud)<>1 OR @solicitud IS NULL
  THROW 51004,''Operación o captura inválida.'',1;
 DECLARE @ubicacion int=TRY_CONVERT(int,JSON_VALUE(@solicitud,''$.Ubicacion'')),
 @tipo varchar(10)=JSON_VALUE(@solicitud,''$.Tipo''),@cantidad int=TRY_CONVERT(int,JSON_VALUE(@solicitud,''$.Cantidad'')),
 @version int=TRY_CONVERT(int,JSON_VALUE(@solicitud,''$.Version'')),
 @consumo bit=CASE JSON_VALUE(@solicitud,''$.EsConsumo'') WHEN ''true'' THEN 1 WHEN ''false'' THEN 0 END;
 IF @ubicacion IS NULL OR @ubicacion<=0 OR @tipo IS NULL OR @tipo COLLATE Latin1_General_100_BIN2 NOT IN (''ENTRADA'',''SALIDA'') OR DATALENGTH(@tipo)<>LEN(@tipo) OR DATALENGTH(JSON_VALUE(@solicitud,''$.Tipo''))<>2*DATALENGTH(@tipo) OR @cantidad IS NULL OR @cantidad NOT BETWEEN 1 AND 1000000
  OR @version IS NULL OR @version<1 OR @consumo IS NULL OR (@tipo=''ENTRADA'' AND @consumo=1)
  THROW 51004,''Revisa tipo, cantidad, ubicación, versión y consumo.'',1;
 IF @cuenta IS NULL OR LEN(LTRIM(RTRIM(@cuenta)))=0 OR @tipo_cuenta IS NULL OR @tipo_cuenta NOT IN (''ADMIN_PUESTOS'',''PUESTO'')
  THROW 51003,''Cuenta no autorizada.'',1;
 BEGIN TRY
  BEGIN TRANSACTION;
  DECLARE @lock int;
  EXEC @lock=sys.sp_getapplock @Resource=''mes_inventario_v1'',@LockMode=''Exclusive'',@LockOwner=''Transaction'',@LockTimeout=10000;
  IF @lock<0 THROW 51009,''Inventario ocupado. Reintenta la misma operación.'',1;
  IF NOT EXISTS(SELECT 1 FROM mes.BASE_INVENTARIO WHERE id=1) THROW 51004,''Falta aprobar el saldo inicial.'',1;
  IF @tipo_cuenta=''PUESTO'' AND (@tipo<>''ENTRADA'' OR @puesto_cuenta IS NULL OR NOT EXISTS(
   SELECT 1 FROM dbo.PUESTO_LOGISTICO WHERE id_puesto=@puesto_cuenta AND usuario=@cuenta AND [password] IS NOT NULL))
   THROW 51003,''El puesto no puede realizar esta operación.'',1;
  IF EXISTS(SELECT 1 FROM mes.OPERACION WHERE id_operacion=@operacion)
  BEGIN
   IF NOT EXISTS(SELECT 1 FROM mes.OPERACION WHERE id_operacion=@operacion AND tipo=''MOVIMIENTO''
    AND cuenta COLLATE Latin1_General_100_BIN2=@cuenta COLLATE Latin1_General_100_BIN2 AND tipo_cuenta=@tipo_cuenta
    AND (puesto_cuenta=@puesto_cuenta OR (puesto_cuenta IS NULL AND @puesto_cuenta IS NULL))
    AND solicitud COLLATE Latin1_General_100_BIN2=@solicitud COLLATE Latin1_General_100_BIN2)
    THROW 51009,''El identificador ya pertenece a otra operación.'',1;
   COMMIT; SELECT respuesta FROM mes.OPERACION WHERE id_operacion=@operacion; RETURN;
  END;
  DECLARE @saldo int,@ppc int,@parte varchar(50),@rack int,@actual int,@puesto int,@capacidad int;
  SELECT @saldo=u.cantidad,@ppc=m.cantidad_por_caja,@parte=u.numero_parte,@rack=u.id_estanteria,
   @actual=u.version_concurrencia,@puesto=m.id_puesto,@capacidad=e.cajas_estanteria
   FROM dbo.UBICACION u JOIN dbo.MATERIAL m ON m.numero_parte=u.numero_parte JOIN dbo.ESTANTERIA e ON e.id_estanteria=u.id_estanteria
   WHERE u.id_ubicacion=@ubicacion;
  IF @saldo IS NULL OR @ppc IS NULL OR @ppc<=0 THROW 51004,''Ubicación o empaque inválidos.'',1;
  IF @tipo_cuenta=''PUESTO'' AND @puesto<>@puesto_cuenta THROW 51003,''La ubicación no pertenece a tu puesto.'',1;
  IF @actual<>@version THROW 51009,''Cambió la disponibilidad. Actualiza y confirma de nuevo.'',1;
  DECLARE @ocupadas bigint=(SELECT SUM((CONVERT(bigint,u.cantidad)+m.cantidad_por_caja-1)/m.cantidad_por_caja)
   FROM dbo.UBICACION u JOIN dbo.MATERIAL m ON m.numero_parte=u.numero_parte WHERE u.id_estanteria=@rack);
  IF @tipo=''ENTRADA'' AND (@capacidad IS NULL OR @cantidad>@capacidad-@ocupadas) THROW 51009,''No hay capacidad para esas cajas.'',1;
  IF @tipo=''SALIDA'' AND @cantidad>(CONVERT(bigint,@saldo)+@ppc-1)/@ppc THROW 51012,''No hay suficientes cajas para esa salida.'',1;
  DECLARE @piezas64 bigint=CONVERT(bigint,@cantidad)*@ppc;
  IF @tipo=''SALIDA'' AND @piezas64>@saldo SET @piezas64=@saldo;
  IF @piezas64<=0 OR @piezas64>2147483647 THROW 51004,''Cantidad fuera del límite permitido.'',1;
  DECLARE @piezas int=CONVERT(int,@piezas64),@posterior bigint=CONVERT(bigint,@saldo)+CASE WHEN @tipo=''ENTRADA'' THEN @piezas64 ELSE -@piezas64 END;
  IF @posterior NOT BETWEEN 0 AND 2147483647 THROW 51012,''Saldo fuera del límite permitido.'',1;
  UPDATE dbo.UBICACION SET cantidad=CONVERT(int,@posterior),version_concurrencia=version_concurrencia+1 WHERE id_ubicacion=@ubicacion;
  UPDATE dbo.MATERIAL SET stock=(SELECT SUM(CONVERT(bigint,cantidad)) FROM dbo.UBICACION WHERE numero_parte=@parte) WHERE numero_parte=@parte;
  DECLARE @fecha datetime2(7)=SYSUTCDATETIME(),@respuesta nvarchar(max);
  SELECT @respuesta=(SELECT @operacion id_operacion,''SQL:''+CONVERT(varchar(36),@operacion) id_movimiento,@tipo tipo_movimiento,
   @piezas cantidad_movimiento,@saldo saldo_anterior,CONVERT(int,@posterior) saldo_posterior,
   CONVERT(varchar(33),@fecha,126)+''Z'' fecha_movimiento,''CONTINUACION'' origen FOR JSON PATH,WITHOUT_ARRAY_WRAPPER);
  INSERT mes.OPERACION(id_operacion,tipo,cuenta,tipo_cuenta,puesto_cuenta,solicitud,respuesta,fecha_utc)
   VALUES(@operacion,''MOVIMIENTO'',@cuenta,@tipo_cuenta,@puesto_cuenta,@solicitud,@respuesta,@fecha);
  INSERT mes.MOVIMIENTO(id_operacion,id_ubicacion,numero_parte,id_puesto,tipo,cantidad_capturada,unidad_capturada,empaque_aplicado,piezas,saldo_anterior,saldo_posterior,es_consumo,es_simulado)
   SELECT @operacion,@ubicacion,@parte,@puesto,@tipo,@cantidad,''CAJAS'',@ppc,@piezas,@saldo,CONVERT(int,@posterior),@consumo,es_simulado FROM mes.CONFIGURACION WHERE id=1;
  COMMIT; SELECT @respuesta respuesta;
 END TRY
 BEGIN CATCH IF XACT_STATE()<>0 ROLLBACK; THROW; END CATCH;
END;
';
EXEC sys.sp_executesql N'
CREATE OR ALTER PROCEDURE mes.AjustarCatalogo
 @operacion uniqueidentifier,@cuenta nvarchar(80),@tipo_cuenta varchar(30),@puesto_cuenta int=NULL,@solicitud nvarchar(max)
AS
BEGIN
 SET NOCOUNT ON; SET XACT_ABORT ON;
 IF @operacion IS NULL OR @operacion=''00000000-0000-0000-0000-000000000000'' OR @solicitud IS NULL OR ISJSON(@solicitud)<>1
  THROW 51004,''Identificador o ajuste inválidos.'',1;
 IF @tipo_cuenta IS NULL OR @tipo_cuenta<>''ADMIN_PUESTOS'' OR @puesto_cuenta IS NOT NULL OR @cuenta IS NULL OR LEN(LTRIM(RTRIM(@cuenta)))=0
  THROW 51003,''Solo administración puede ajustar catálogos.'',1;
 DECLARE @c TABLE(rack int PRIMARY KEY,anterior int,nueva int);
 DECLARE @m TABLE(parte varchar(50) COLLATE Latin1_General_100_BIN2,campo varchar(30) COLLATE Latin1_General_100_BIN2,anterior nvarchar(250),nuevo nvarchar(250),PRIMARY KEY(parte,campo));
 -- Validar longitudes antes de convertir, para no truncar silenciosamente.
 IF EXISTS(SELECT 1 FROM OPENJSON(@solicitud,''$.Materiales'') WITH(Parte nvarchar(max),Campo nvarchar(max),Anterior nvarchar(max),Nuevo nvarchar(max))
  WHERE Parte IS NULL OR LEN(Parte)>50 OR Campo IS NULL OR LEN(Campo)>30 OR LEN(Anterior)>250 OR Nuevo IS NULL OR LEN(Nuevo)>250)
  THROW 51004,''El material o sus valores no son válidos.'',1;
 INSERT @c SELECT TRY_CONVERT(int,JSON_VALUE(value,''$.Estanteria'')),TRY_CONVERT(int,JSON_VALUE(value,''$.Anterior'')),TRY_CONVERT(int,JSON_VALUE(value,''$.Nueva'')) FROM OPENJSON(@solicitud,''$.Capacidades'');
 INSERT @m SELECT Parte,Campo,Anterior,Nuevo FROM OPENJSON(@solicitud,''$.Materiales'') WITH(Parte varchar(50),Campo varchar(30),Anterior nvarchar(250),Nuevo nvarchar(250));
 IF NOT EXISTS(SELECT 1 FROM @c) AND NOT EXISTS(SELECT 1 FROM @m) THROW 51004,''No hay cambios.'',1;
 IF EXISTS(SELECT 1 FROM @c WHERE rack<=0 OR anterior IS NULL OR nueva IS NULL OR nueva NOT BETWEEN 1 AND 10000 OR anterior=nueva)
  THROW 51004,''Capacidad inválida o sin cambios.'',1;
 IF EXISTS(SELECT 1 FROM @m WHERE campo NOT IN (''descripcion'',''uso'',''cantidad_por_caja'',''minimo'',''variante'')
  OR DATALENGTH(campo)<>LEN(campo) OR DATALENGTH(nuevo)<>DATALENGTH(LTRIM(RTRIM(nuevo))) OR nuevo='''' OR (nuevo=anterior AND anterior IS NOT NULL)
  OR (campo=''uso'' AND nuevo COLLATE Latin1_General_100_BIN2 NOT IN (''COMPARTIDO'',''EXCLUSIVO''))
  OR (campo=''variante'' AND LEN(nuevo)>10)
  OR (campo=''cantidad_por_caja'' AND (TRY_CONVERT(int,nuevo) IS NULL OR TRY_CONVERT(int,nuevo) NOT BETWEEN 1 AND 1000000))
  OR (campo=''minimo'' AND (TRY_CONVERT(int,nuevo) IS NULL OR TRY_CONVERT(int,nuevo) NOT BETWEEN 0 AND 100000000)))
  THROW 51004,''Campo o valor de material inválido.'',1;
 BEGIN TRY
  BEGIN TRANSACTION;
  DECLARE @lock int;
  EXEC @lock=sys.sp_getapplock @Resource=''mes_inventario_v1'',@LockMode=''Exclusive'',@LockOwner=''Transaction'',@LockTimeout=10000;
  IF @lock<0 THROW 51009,''Inventario ocupado. Reintenta la misma operación.'',1;
  IF NOT EXISTS(SELECT 1 FROM mes.BASE_INVENTARIO WHERE id=1) THROW 51004,''Falta aprobar el saldo inicial.'',1;
  IF EXISTS(SELECT 1 FROM mes.OPERACION WHERE id_operacion=@operacion)
  BEGIN
   IF NOT EXISTS(SELECT 1 FROM mes.OPERACION WHERE id_operacion=@operacion AND tipo=''AJUSTE''
    AND cuenta COLLATE Latin1_General_100_BIN2=@cuenta COLLATE Latin1_General_100_BIN2 AND tipo_cuenta=@tipo_cuenta AND puesto_cuenta IS NULL
    AND solicitud COLLATE Latin1_General_100_BIN2=@solicitud COLLATE Latin1_General_100_BIN2)
    THROW 51009,''El identificador ya pertenece a otra operación.'',1;
   COMMIT; SELECT respuesta FROM mes.OPERACION WHERE id_operacion=@operacion; RETURN;
  END;
  IF EXISTS(SELECT 1 FROM @c c LEFT JOIN dbo.ESTANTERIA e ON e.id_estanteria=c.rack WHERE e.id_estanteria IS NULL OR e.cajas_estanteria<>c.anterior)
   THROW 51009,''La capacidad cambió o no existe.'',1;
  IF EXISTS(SELECT 1 FROM @m c LEFT JOIN dbo.MATERIAL m ON m.numero_parte COLLATE Latin1_General_100_BIN2=c.parte
   LEFT JOIN dbo.COLOR k ON k.id_color=m.id_color
   WHERE m.numero_parte IS NULL OR EXISTS(SELECT
    CASE c.campo WHEN ''descripcion'' THEN CONVERT(nvarchar(250),m.descripcion) WHEN ''uso'' THEN CONVERT(nvarchar(250),m.uso)
    WHEN ''cantidad_por_caja'' THEN CONVERT(nvarchar(250),m.cantidad_por_caja) WHEN ''minimo'' THEN CONVERT(nvarchar(250),m.minimo)
    WHEN ''variante'' THEN CONVERT(nvarchar(250),k.nombre) END COLLATE Latin1_General_100_BIN2
    EXCEPT SELECT c.anterior COLLATE Latin1_General_100_BIN2))
   THROW 51009,''El material cambió o no existe. Actualiza la vista previa.'',1;
  IF EXISTS(SELECT 1 FROM @m c WHERE campo=''variante'' AND NOT EXISTS(SELECT 1 FROM dbo.COLOR k WHERE CONVERT(nvarchar(250),k.nombre) COLLATE Latin1_General_100_BIN2=c.nuevo COLLATE Latin1_General_100_BIN2))
   THROW 51004,''Una variante no existe en COLOR.'',1;
  UPDATE m SET descripcion=CASE WHEN d.parte IS NULL THEN m.descripcion ELSE d.nuevo END,
   uso=CASE WHEN u.parte IS NULL THEN m.uso ELSE u.nuevo END,
   cantidad_por_caja=CASE WHEN p.parte IS NULL THEN m.cantidad_por_caja ELSE CONVERT(int,p.nuevo) END,
   minimo=CASE WHEN n.parte IS NULL THEN m.minimo ELSE CONVERT(int,n.nuevo) END,
   id_color=CASE WHEN v.parte IS NULL THEN m.id_color ELSE k.id_color END
   FROM dbo.MATERIAL m
   LEFT JOIN @m d ON d.parte=m.numero_parte COLLATE Latin1_General_100_BIN2 AND d.campo=''descripcion''
   LEFT JOIN @m u ON u.parte=m.numero_parte COLLATE Latin1_General_100_BIN2 AND u.campo=''uso''
   LEFT JOIN @m p ON p.parte=m.numero_parte COLLATE Latin1_General_100_BIN2 AND p.campo=''cantidad_por_caja''
   LEFT JOIN @m n ON n.parte=m.numero_parte COLLATE Latin1_General_100_BIN2 AND n.campo=''minimo''
   LEFT JOIN @m v ON v.parte=m.numero_parte COLLATE Latin1_General_100_BIN2 AND v.campo=''variante''
   LEFT JOIN dbo.COLOR k ON CONVERT(nvarchar(250),k.nombre) COLLATE Latin1_General_100_BIN2=v.nuevo COLLATE Latin1_General_100_BIN2
   WHERE EXISTS(SELECT 1 FROM @m a WHERE a.parte=m.numero_parte COLLATE Latin1_General_100_BIN2);
  UPDATE e SET cajas_estanteria=c.nueva FROM dbo.ESTANTERIA e JOIN @c c ON c.rack=e.id_estanteria;
  IF EXISTS(SELECT 1 FROM dbo.ESTANTERIA e CROSS APPLY(
   SELECT SUM((CONVERT(bigint,u.cantidad)+m.cantidad_por_caja-1)/m.cantidad_por_caja) ocupadas
   FROM dbo.UBICACION u JOIN dbo.MATERIAL m ON m.numero_parte=u.numero_parte WHERE u.id_estanteria=e.id_estanteria) x
   WHERE x.ocupadas>e.cajas_estanteria AND (EXISTS(SELECT 1 FROM @c c WHERE c.rack=e.id_estanteria)
    OR EXISTS(SELECT 1 FROM dbo.UBICACION u JOIN @m m ON m.parte=u.numero_parte COLLATE Latin1_General_100_BIN2 WHERE u.id_estanteria=e.id_estanteria AND m.campo=''cantidad_por_caja'')))
   THROW 51009,''El ajuste dejaría menos capacidad que cajas ocupadas.'',1;
  UPDATE u SET version_concurrencia=version_concurrencia+1 FROM dbo.UBICACION u
   WHERE EXISTS(SELECT 1 FROM @c c WHERE c.rack=u.id_estanteria) OR EXISTS(SELECT 1 FROM @m m WHERE m.parte=u.numero_parte COLLATE Latin1_General_100_BIN2);
  DECLARE @fecha datetime2(7)=SYSUTCDATETIME(),@respuesta nvarchar(max),@rack int=(SELECT MIN(rack) FROM @c);
  SELECT @respuesta=(SELECT @operacion id_operacion,(SELECT COUNT(*) FROM @m) cambios,
   (SELECT COUNT(DISTINCT parte) FROM @m) materiales,@rack id_estanteria,
   (SELECT cajas_estanteria FROM dbo.ESTANTERIA WHERE id_estanteria=@rack) cajas_estanteria,
   (SELECT e.cajas_estanteria-COALESCE(SUM((CONVERT(bigint,u.cantidad)+m.cantidad_por_caja-1)/m.cantidad_por_caja),0)
    FROM dbo.ESTANTERIA e LEFT JOIN dbo.UBICACION u ON u.id_estanteria=e.id_estanteria LEFT JOIN dbo.MATERIAL m ON m.numero_parte=u.numero_parte
    WHERE e.id_estanteria=@rack GROUP BY e.cajas_estanteria) cajas_disponibles,
   CONVERT(varchar(33),@fecha,126)+''Z'' fecha FOR JSON PATH,WITHOUT_ARRAY_WRAPPER);
  INSERT mes.OPERACION(id_operacion,tipo,cuenta,tipo_cuenta,puesto_cuenta,solicitud,respuesta,fecha_utc)
   VALUES(@operacion,''AJUSTE'',@cuenta,@tipo_cuenta,NULL,@solicitud,@respuesta,@fecha);
  INSERT mes.AJUSTE SELECT @operacion,''CAPACIDAD'',CONVERT(nvarchar(100),rack),''cajas_estanteria'',CONVERT(nvarchar(250),anterior),CONVERT(nvarchar(250),nueva) FROM @c;
  INSERT mes.AJUSTE SELECT @operacion,''MATERIAL'',parte,campo,anterior,nuevo FROM @m;
  COMMIT; SELECT @respuesta respuesta;
 END TRY
 BEGIN CATCH IF XACT_STATE()<>0 ROLLBACK; THROW; END CATCH;
END;
';
EXEC sys.sp_executesql N'CREATE OR ALTER VIEW dbo.vw_modelo_puestos AS SELECT fecha_utc fecha_stock FROM mes.BASE_INVENTARIO WHERE id=1;
';
EXEC sys.sp_executesql N'
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
';
EXEC sys.sp_executesql N'
CREATE OR ALTER VIEW mes.vw_movimientos AS
SELECT ''SQL:''+CONVERT(varchar(36),o.id_operacion) id_movimiento,o.id_operacion,''CONTINUACION'' origen,''CONTINUACION'' espacio_identificadores,
 m.es_simulado,m.numero_parte,m.id_puesto,m.id_ubicacion,m.tipo tipo_movimiento,m.piezas cantidad_piezas,
 m.cantidad_capturada,m.unidad_capturada,m.empaque_aplicado cantidad_por_caja_aplicada,
 m.es_consumo es_consumo_produccion,m.saldo_anterior,m.saldo_posterior,o.fecha_utc fecha_movimiento,
 o.cuenta usuario_captura,CONVERT(nvarchar(80),NULL) responsable_simulado,''PORTAL_SQL_V1'' proceso_generador,
 s.nombre_estacion estacion,e.estanteria,e.nivel,CONVERT(int,NULL) id_turno
FROM mes.OPERACION o JOIN mes.MOVIMIENTO m ON m.id_operacion=o.id_operacion
JOIN dbo.UBICACION u ON u.id_ubicacion=m.id_ubicacion JOIN dbo.ESTANTERIA e ON e.id_estanteria=u.id_estanteria
JOIN dbo.ESTACION s ON s.id_estacion=e.id_estacion;
';
EXEC sys.sp_executesql N'
CREATE OR ALTER VIEW mes.vw_ajustes AS
SELECT o.id_operacion,o.fecha_utc,o.cuenta,o.tipo_cuenta,a.entidad,a.clave,a.campo,a.anterior,a.nuevo
FROM mes.OPERACION o JOIN mes.AJUSTE a ON a.id_operacion=o.id_operacion;
';
EXEC sys.sp_executesql N'
CREATE OR ALTER FUNCTION dbo.TurnoDeAtencion(@inicio datetime2(7)) RETURNS int AS
BEGIN
 DECLARE @hora time=CONVERT(time,@inicio),@turno int;
 SELECT @turno=CASE WHEN COUNT(*)=1 THEN MIN(id_turno) END FROM dbo.TURNO
 WHERE (hora_inicio<hora_fin AND @hora>=hora_inicio AND @hora<hora_fin)
  OR (hora_inicio>hora_fin AND (@hora>=hora_inicio OR @hora<hora_fin));
 RETURN @turno;
END;
';
EXEC sys.sp_executesql N'
CREATE OR ALTER PROCEDURE dbo.IniciarAtencion @puesto int,@ubicacion int AS
BEGIN
 SET NOCOUNT ON; SET XACT_ABORT ON;
 BEGIN TRY
  BEGIN TRANSACTION;
  IF NOT EXISTS(SELECT 1 FROM dbo.UBICACION u JOIN dbo.MATERIAL m ON m.numero_parte=u.numero_parte
   JOIN dbo.PUESTO_LOGISTICO p ON p.id_puesto=m.id_puesto WHERE u.id_ubicacion=@ubicacion AND m.id_puesto=@puesto AND p.usuario IS NOT NULL)
   THROW 51003,''La ubicación no pertenece a tu puesto.'',1;
  DECLARE @id int;
  SELECT @id=id_registro FROM dbo.REGISTRO WITH(UPDLOCK,HOLDLOCK) WHERE id_puesto=@puesto AND id_ubicacion=@ubicacion AND fecha_hora_fin IS NULL;
  IF @id IS NULL
  BEGIN
   DECLARE @inicio datetime2(7)=CONVERT(datetime2(7),SYSDATETIMEOFFSET() AT TIME ZONE ''Central Standard Time (Mexico)'');
   DECLARE @turno int=dbo.TurnoDeAtencion(@inicio);
   IF @turno IS NULL THROW 51004,''No hay un turno único para esta hora. Revisa el calendario de turnos.'',1;
   INSERT dbo.REGISTRO(id_puesto,id_ubicacion,id_turno,fecha_hora_inicio) VALUES(@puesto,@ubicacion,@turno,@inicio);
   SET @id=CONVERT(int,SCOPE_IDENTITY());
  END;
  COMMIT; SELECT * FROM dbo.vw_kpi_atenciones WHERE id_registro=@id;
 END TRY
 BEGIN CATCH IF XACT_STATE()<>0 ROLLBACK; THROW; END CATCH;
END;
';
EXEC sys.sp_executesql N'
CREATE OR ALTER VIEW dbo.vw_kpi_atenciones AS
SELECT r.id_registro,r.id_puesto,p.Nombre puesto,r.id_ubicacion,u.numero_parte,
 CONVERT(datetime2(7),r.fecha_hora_inicio AT TIME ZONE ''Central Standard Time (Mexico)'' AT TIME ZONE ''UTC'') fecha_hora_inicio,
 CONVERT(datetime2(7),r.fecha_hora_fin AT TIME ZONE ''Central Standard Time (Mexico)'' AT TIME ZONE ''UTC'') fecha_hora_fin,
 DATEDIFF_BIG(millisecond,r.fecha_hora_inicio,r.fecha_hora_fin) duracion_ms,t.nombre_turno turno_inicio,
 cfg.es_simulado,r.id_turno,r.fecha_hora_inicio fecha_hora_inicio_local,r.fecha_hora_fin fecha_hora_fin_local
FROM dbo.REGISTRO r JOIN dbo.PUESTO_LOGISTICO p ON p.id_puesto=r.id_puesto JOIN dbo.UBICACION u ON u.id_ubicacion=r.id_ubicacion
LEFT JOIN dbo.TURNO t ON t.id_turno=r.id_turno CROSS JOIN mes.CONFIGURACION cfg;
';
EXEC sys.sp_executesql N'
-- Solo aprovisionamiento; portal_app no recibe permiso para ejecutar esta apertura.
CREATE OR ALTER PROCEDURE mes.FijarSaldoInicial AS
BEGIN
 SET NOCOUNT ON; SET XACT_ABORT ON;
 BEGIN TRY
  BEGIN TRANSACTION;
  DECLARE @lock int; EXEC @lock=sys.sp_getapplock @Resource=''mes_inventario_v1'',@LockMode=''Exclusive'',@LockOwner=''Transaction'',@LockTimeout=10000;
  IF @lock<0 THROW 51009,''Inventario ocupado.'',1;
  IF EXISTS(SELECT 1 FROM mes.BASE_INVENTARIO) OR EXISTS(SELECT 1 FROM mes.OPERACION)
   THROW 51009,''Ya hay apertura u operaciones: no se reemplazó el saldo inicial.'',1;
  IF NOT EXISTS(SELECT 1 FROM dbo.UBICACION) THROW 51004,''Faltan datos iniciales autorizados.'',1;
  IF EXISTS(SELECT 1 FROM dbo.MATERIAL m WHERE m.stock<>COALESCE((SELECT SUM(CONVERT(bigint,cantidad)) FROM dbo.UBICACION WHERE numero_parte=m.numero_parte),0))
   THROW 51004,''El stock no coincide con las ubicaciones.'',1;
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
';
EXEC sys.sp_executesql N'
-- Consulta paginada: no descarga todo el legado para mostrar una página.
CREATE OR ALTER VIEW mes.vw_historial_json AS
SELECT (SELECT v.* FOR JSON PATH,INCLUDE_NULL_VALUES,WITHOUT_ARRAY_WRAPPER) datos,
 v.fecha_movimiento,v.numero_parte,v.id_ubicacion,v.espacio_identificadores,v.usuario_captura responsable,v.id_turno,v.id_movimiento
FROM mes.vw_movimientos v
UNION ALL
SELECT h.datos,TRY_CONVERT(datetime2(7),JSON_VALUE(h.datos,''$.fecha_movimiento''),127),
 CONVERT(varchar(50),JSON_VALUE(h.datos,''$.numero_parte'')),TRY_CONVERT(int,JSON_VALUE(h.datos,''$.id_ubicacion'')),
 CONVERT(varchar(30),JSON_VALUE(h.datos,''$.espacio_identificadores'')),
 COALESCE(JSON_VALUE(h.datos,''$.usuario_captura''),JSON_VALUE(h.datos,''$.responsable_simulado'')),
 TRY_CONVERT(int,JSON_VALUE(h.datos,''$.id_turno'')),JSON_VALUE(h.datos,''$.id_movimiento'')
FROM mes.HISTORICO_LEGADO h JOIN mes.ARCHIVO_LEGADO a ON a.sha256=h.sha256 AND a.completo=1 WHERE h.archivo IN (''movimientos.jsonl.gz'',''movimientos-diario.jsonl'');
';
EXEC sys.sp_executesql N'
CREATE INDEX IX_LEGADO_archivo ON mes.HISTORICO_LEGADO(archivo);
';
 COMMIT;
 SELECT 'INSTALACION_CONFIRMADA' resultado;
END TRY
BEGIN CATCH
 IF XACT_STATE()<>0 ROLLBACK;
 THROW;
END CATCH;
