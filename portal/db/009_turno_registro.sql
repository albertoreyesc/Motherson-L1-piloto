-- Conserva el turno de inicio de cada atención mediante una clave foránea.
IF @@TRANCOUNT = 0 THROW 51000, 'Se requiere una transacción de migración.', 1;
IF OBJECT_ID('reporting.vw_modelo_puestos','V') IS NULL
    THROW 51000, 'Aplica primero el modelo por puesto.', 1;
IF EXISTS (SELECT 1 FROM dbo.TURNO WHERE hora_inicio >= hora_fin)
   OR EXISTS (SELECT 1 FROM dbo.TURNO a JOIN dbo.TURNO b ON a.id_turno < b.id_turno
              AND a.hora_inicio < b.hora_fin AND b.hora_inicio < a.hora_fin)
    THROW 51000, 'El horario de los turnos no es válido.', 1;
GO

CREATE OR ALTER FUNCTION dbo.TurnoDeAtencion(@inicio datetime2(7))
RETURNS int
AS
BEGIN
    DECLARE @hora time(7)=CONVERT(time(7),@inicio AT TIME ZONE 'UTC'
                                      AT TIME ZONE 'Central Standard Time (Mexico)');
    DECLARE @turno int=(SELECT id_turno FROM dbo.TURNO WHERE @hora>=hora_inicio AND @hora<hora_fin);
    RETURN @turno;
END;
GO

IF COL_LENGTH('dbo.REGISTRO','id_turno') IS NULL
BEGIN
    ALTER TABLE dbo.REGISTRO ADD id_turno int NULL;
    EXEC(N'UPDATE dbo.REGISTRO SET id_turno=dbo.TurnoDeAtencion(fecha_hora_inicio);');
    ALTER TABLE dbo.REGISTRO WITH CHECK ADD CONSTRAINT FK_REGISTRO_TURNO
        FOREIGN KEY(id_turno) REFERENCES dbo.TURNO(id_turno);
END;
GO

CREATE OR ALTER VIEW reporting.vw_kpi_atenciones AS
SELECT r.id_registro,r.id_puesto,p.nombre puesto,r.id_ubicacion,u.numero_parte,
       r.fecha_hora_inicio,r.fecha_hora_fin,
       DATEDIFF_BIG(millisecond,r.fecha_hora_inicio,r.fecha_hora_fin) duracion_ms,
       t.nombre_turno turno_inicio,CONVERT(bit,1) es_simulado,r.id_turno
FROM dbo.REGISTRO r
JOIN dbo.PUESTO_LOGISTICO p ON p.id_puesto=r.id_puesto
JOIN dbo.UBICACION u ON u.id_ubicacion=r.id_ubicacion
LEFT JOIN dbo.TURNO t ON t.id_turno=r.id_turno;
GO

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
        DECLARE @inicio datetime2(7)=SYSUTCDATETIME();
        INSERT dbo.REGISTRO(id_puesto,id_ubicacion,fecha_hora_inicio,id_turno)
        VALUES(@puesto,@ubicacion,@inicio,dbo.TurnoDeAtencion(@inicio));
        SET @id=CONVERT(int,SCOPE_IDENTITY());
    END;
    COMMIT;
    SELECT * FROM reporting.vw_kpi_atenciones WHERE id_registro=@id;
END;
GO
