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
 ('ESTACION','id_estacion'),('ESTACION','nombre_estacion'),
 ('ESTANTERIA','id_estanteria'),('ESTANTERIA','id_estacion'),('ESTANTERIA','estanteria'),
 ('ESTANTERIA','nivel'),('ESTANTERIA','cajas_estanteria'),
 ('PUESTO_LOGISTICO','id_puesto'),('PUESTO_LOGISTICO','nombre'),
 ('PUESTO_LOGISTICO','usuario'),('PUESTO_LOGISTICO','password'),
 ('COLOR','id_color'),('COLOR','nombre'),
 ('MATERIAL','numero_parte'),('MATERIAL','id_puesto'),('MATERIAL','descripcion'),('MATERIAL','uso'),
 ('MATERIAL','cantidad_por_caja'),('MATERIAL','minimo'),('MATERIAL','id_color'),('MATERIAL','stock'),
 ('UBICACION','id_ubicacion'),('UBICACION','numero_parte'),('UBICACION','id_estanteria'),
 ('TURNO','id_turno'),('TURNO','nombre_turno'),('TURNO','hora_inicio'),('TURNO','hora_fin'),
 ('REGISTRO','id_registro'),('REGISTRO','id_puesto'),('REGISTRO','id_ubicacion'),
 ('REGISTRO','fecha_hora_inicio'),('REGISTRO','fecha_hora_fin'),('REGISTRO','id_turno');

DECLARE @faltan nvarchar(max) = (
    SELECT STRING_AGG(CONVERT(nvarchar(max), r.tabla + '.' + r.columna), ', ')
    FROM @requeridas r
    WHERE COL_LENGTH('dbo.' + r.tabla, r.columna) IS NULL);

IF @faltan IS NOT NULL
BEGIN
    RAISERROR('No se creó nada. Faltan estas columnas en dbo: %s', 16, 1, @faltan);
    SET NOEXEC ON;   -- el resto del script se compila pero no se ejecuta
END;
GO

-- ---------------------------------------------------------------------
-- 1. Fecha del saldo inicial (MATERIAL.stock). El portal la compara con
--    su paquete histórico al arrancar. Valor en UTC; no cambiar.
-- ---------------------------------------------------------------------
CREATE OR ALTER VIEW dbo.vw_modelo_puestos AS
SELECT CONVERT(datetime2(7), '2026-11-04T18:27:48.8981735', 126) fecha_stock;
GO

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
GO

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
GO

-- ---------------------------------------------------------------------
-- 4. Atenciones para el portal (UTC) y para análisis (*_local).
-- ---------------------------------------------------------------------
CREATE OR ALTER VIEW dbo.vw_kpi_atenciones AS
SELECT r.id_registro,r.id_puesto,p.nombre puesto,r.id_ubicacion,u.numero_parte,
       CONVERT(datetime2(7),r.fecha_hora_inicio AT TIME ZONE 'Central Standard Time (Mexico)'
                                                AT TIME ZONE 'UTC') fecha_hora_inicio,
       CONVERT(datetime2(7),r.fecha_hora_fin    AT TIME ZONE 'Central Standard Time (Mexico)'
                                                AT TIME ZONE 'UTC') fecha_hora_fin,
       DATEDIFF_BIG(millisecond,r.fecha_hora_inicio,r.fecha_hora_fin) duracion_ms,
       t.nombre_turno turno_inicio,CONVERT(bit,1) es_simulado,r.id_turno,
       r.fecha_hora_inicio fecha_hora_inicio_local,r.fecha_hora_fin fecha_hora_fin_local
FROM dbo.REGISTRO r
JOIN dbo.PUESTO_LOGISTICO p ON p.id_puesto=r.id_puesto
JOIN dbo.UBICACION u ON u.id_ubicacion=r.id_ubicacion
LEFT JOIN dbo.TURNO t ON t.id_turno=r.id_turno;
GO

-- ---------------------------------------------------------------------
-- 5. Alta, cambio o baja del acceso de un puesto (usuario y hash).
-- ---------------------------------------------------------------------
CREATE OR ALTER PROCEDURE dbo.GuardarAccesoPuesto
    @puesto int, @usuario nvarchar(80), @hash nvarchar(500)
AS
BEGIN
    SET NOCOUNT ON;
    UPDATE dbo.PUESTO_LOGISTICO SET usuario=@usuario,[password]=@hash WHERE id_puesto=@puesto;
    IF @@ROWCOUNT=0 THROW 51004, 'El puesto no existe.', 1;
END;
GO

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
    ) THROW 51003, 'La ubicación no pertenece a tu puesto.', 1;
    DECLARE @id int;
    SELECT @id=id_registro FROM dbo.REGISTRO WITH(UPDLOCK,HOLDLOCK)
    WHERE id_puesto=@puesto AND id_ubicacion=@ubicacion AND fecha_hora_fin IS NULL;
    IF @id IS NULL
    BEGIN
        DECLARE @inicio datetime2(7)=CONVERT(datetime2(7),
            SYSDATETIMEOFFSET() AT TIME ZONE 'Central Standard Time (Mexico)');
        INSERT dbo.REGISTRO(id_puesto,id_ubicacion,fecha_hora_inicio,id_turno)
        VALUES(@puesto,@ubicacion,@inicio,dbo.TurnoDeAtencion(@inicio));
        SET @id=CONVERT(int,SCOPE_IDENTITY());
    END;
    COMMIT;
    SELECT * FROM dbo.vw_kpi_atenciones WHERE id_registro=@id;
END;
GO

-- ---------------------------------------------------------------------
-- 7. Fin de una atención: guarda la hora de México en fecha_hora_fin.
-- ---------------------------------------------------------------------
CREATE OR ALTER PROCEDURE dbo.FinalizarAtencion @puesto int, @registro int
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;
    UPDATE r SET fecha_hora_fin=COALESCE(r.fecha_hora_fin,CONVERT(datetime2(7),
        SYSDATETIMEOFFSET() AT TIME ZONE 'Central Standard Time (Mexico)'))
    FROM dbo.REGISTRO r JOIN dbo.PUESTO_LOGISTICO p ON p.id_puesto=r.id_puesto
    WHERE r.id_registro=@registro AND r.id_puesto=@puesto AND p.usuario IS NOT NULL;
    IF @@ROWCOUNT=0 THROW 51003, 'La atención no pertenece a tu puesto.', 1;
    SELECT * FROM dbo.vw_kpi_atenciones WHERE id_registro=@registro;
END;
GO

SET NOEXEC OFF;
GO
