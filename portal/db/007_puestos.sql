-- El ejecutor valida el destino y mantiene una transacción para todos los lotes.
IF @@TRANCOUNT = 0 THROW 51000, 'Se requiere una transacción de migración.', 1;
IF OBJECT_ID('dbo.USUARIOS', 'U') IS NULL OR OBJECT_ID('dbo.COLOR', 'U') IS NOT NULL
    THROW 51000, 'El esquema de origen no coincide con la versión esperada.', 1;
IF EXISTS (SELECT numero_parte FROM dbo.UBICACION GROUP BY numero_parte HAVING COUNT(*) > 1)
    THROW 51000, 'Hay materiales con varias ubicaciones. Falta aprobar la regla de saldos.', 1;
IF EXISTS (SELECT 1 FROM dbo.TURNO a JOIN dbo.TURNO b ON a.id_turno < b.id_turno
           AND a.hora_inicio < b.hora_fin AND b.hora_inicio < a.hora_fin)
    THROW 51000, 'Hay horarios de turno que se superponen.', 1;

EXEC('CREATE SCHEMA respaldo_puestos AUTHORIZATION dbo');
SELECT id_ubicacion, cantidad, version_concurrencia INTO respaldo_puestos.SALDO FROM dbo.UBICACION;
SELECT id_puesto, tipo INTO respaldo_puestos.TIPO_PUESTO FROM dbo.PUESTO_LOGISTICO;
SELECT SYSUTCDATETIME() fecha_stock INTO respaldo_puestos.CORTE;
SELECT id_usuarios, nombre_usuario INTO archivo.CUENTA_MVP FROM dbo.USUARIOS;

DROP PROCEDURE IF EXISTS dbo.RegistrarMovimiento;
DROP TRIGGER IF EXISTS dbo.TR_REGISTRO_INMUTABLE;
DROP TRIGGER IF EXISTS dbo.TR_PRUEBA_FALLO_REGISTRO;
EXEC sp_rename 'dbo.REGISTRO', 'REGISTRO_MVP';
ALTER SCHEMA archivo TRANSFER dbo.REGISTRO_MVP;
-- La cuenta antigua se conserva exclusivamente para una reversión selectiva.
ALTER SCHEMA respaldo_puestos TRANSFER dbo.USUARIOS;

DECLARE @quitar nvarchar(max)=N'';
SELECT @quitar += N'ALTER TABLE dbo.UBICACION DROP CONSTRAINT '+QUOTENAME(name)+N';'
FROM sys.check_constraints
WHERE parent_object_id=OBJECT_ID('dbo.UBICACION')
  AND parent_column_id IN (COLUMNPROPERTY(parent_object_id,'cantidad','ColumnId'),
                           COLUMNPROPERTY(parent_object_id,'version_concurrencia','ColumnId'));
SELECT @quitar += N'ALTER TABLE dbo.UBICACION DROP CONSTRAINT '+QUOTENAME(name)+N';'
FROM sys.default_constraints
WHERE parent_object_id=OBJECT_ID('dbo.UBICACION')
  AND parent_column_id=COLUMNPROPERTY(parent_object_id,'version_concurrencia','ColumnId');
EXEC sys.sp_executesql @quitar;
ALTER TABLE dbo.UBICACION DROP COLUMN cantidad, version_concurrencia;
ALTER TABLE dbo.PUESTO_LOGISTICO DROP COLUMN tipo;
ALTER TABLE dbo.PUESTO_LOGISTICO ADD usuario nvarchar(80) NULL, [password] nvarchar(500) NULL;
GO
ALTER TABLE dbo.PUESTO_LOGISTICO ADD CONSTRAINT CK_PUESTO_ACCESO CHECK (
    (usuario IS NULL AND [password] IS NULL) OR
    (usuario IS NOT NULL AND [password] IS NOT NULL AND usuario <> N'admin.local'));
CREATE UNIQUE INDEX UX_PUESTO_USUARIO ON dbo.PUESTO_LOGISTICO(usuario) WHERE usuario IS NOT NULL;

CREATE TABLE dbo.COLOR (
    id_color int IDENTITY PRIMARY KEY,
    nombre varchar(10) COLLATE Latin1_General_100_BIN2 NOT NULL UNIQUE
);
ALTER TABLE dbo.MATERIAL ADD id_color int NULL REFERENCES dbo.COLOR(id_color),
    stock int NULL CONSTRAINT CK_MATERIAL_STOCK CHECK(stock >= 0);
GO
UPDATE m SET stock=s.cantidad
FROM dbo.MATERIAL m
JOIN dbo.UBICACION u ON u.numero_parte=m.numero_parte
JOIN respaldo_puestos.SALDO s ON s.id_ubicacion=u.id_ubicacion;

CREATE TABLE dbo.REGISTRO (
    id_registro int IDENTITY PRIMARY KEY,
    id_puesto int NOT NULL REFERENCES dbo.PUESTO_LOGISTICO(id_puesto),
    id_ubicacion int NOT NULL REFERENCES dbo.UBICACION(id_ubicacion),
    fecha_hora_inicio datetime2(7) NOT NULL,
    fecha_hora_fin datetime2(7) NULL,
    CONSTRAINT CK_REGISTRO_INTERVALO CHECK(fecha_hora_fin >= fecha_hora_inicio)
);
CREATE UNIQUE INDEX UX_REGISTRO_ATENCION_ABIERTA
ON dbo.REGISTRO(id_puesto,id_ubicacion) WHERE fecha_hora_fin IS NULL;
CREATE INDEX IX_REGISTRO_PUESTO_FECHA ON dbo.REGISTRO(id_puesto,fecha_hora_inicio DESC,id_registro DESC);
GO
CREATE OR ALTER TRIGGER archivo.TR_REGISTRO_MVP_CONSERVADO ON archivo.REGISTRO_MVP
INSTEAD OF INSERT, UPDATE, DELETE AS THROW 51009, 'Registro previo archivado.', 1;
GO
CREATE OR ALTER TRIGGER archivo.TR_CUENTA_MVP_CONSERVADA ON archivo.CUENTA_MVP
INSTEAD OF INSERT, UPDATE, DELETE AS THROW 51009, 'Atribución previa archivada.', 1;
GO
CREATE OR ALTER VIEW reporting.vw_modelo_puestos AS
SELECT fecha_stock FROM respaldo_puestos.CORTE;
GO
CREATE OR ALTER VIEW reporting.vw_kpi_atenciones AS
SELECT r.id_registro,r.id_puesto,p.nombre puesto,r.id_ubicacion,u.numero_parte,
       r.fecha_hora_inicio,r.fecha_hora_fin,
       DATEDIFF_BIG(millisecond,r.fecha_hora_inicio,r.fecha_hora_fin) duracion_ms,
       t.nombre_turno turno_inicio,CONVERT(bit,1) es_simulado
FROM dbo.REGISTRO r
JOIN dbo.PUESTO_LOGISTICO p ON p.id_puesto=r.id_puesto
JOIN dbo.UBICACION u ON u.id_ubicacion=r.id_ubicacion
CROSS APPLY (VALUES(CONVERT(time(7),r.fecha_hora_inicio AT TIME ZONE 'UTC'
                 AT TIME ZONE 'Central Standard Time (Mexico)'))) h(hora)
LEFT JOIN dbo.TURNO t ON h.hora>=t.hora_inicio AND h.hora<t.hora_fin;
GO
CREATE OR ALTER VIEW reporting.vw_kpi_inventario_actual AS
SELECT u.id_ubicacion,u.numero_parte,m.descripcion,m.uso,m.id_puesto,p.nombre puesto,m.cantidad_por_caja,m.minimo,
       CASE WHEN COUNT(*) OVER(PARTITION BY u.numero_parte)=1 THEN m.stock END cantidad,
       CONVERT(int,NULL) version_concurrencia,t.id_estanteria,t.estanteria,t.nivel,t.cajas_estanteria,
       s.id_estacion,s.nombre_estacion estacion,CONVERT(bigint,m.stock) stock_total_material,
       CONVERT(bit,1) es_simulado,c.nombre variante_revision, corte.fecha_stock
FROM dbo.UBICACION u JOIN dbo.MATERIAL m ON m.numero_parte=u.numero_parte
JOIN dbo.ESTANTERIA t ON t.id_estanteria=u.id_estanteria JOIN dbo.ESTACION s ON s.id_estacion=t.id_estacion
JOIN dbo.PUESTO_LOGISTICO p ON p.id_puesto=m.id_puesto
LEFT JOIN dbo.COLOR c ON c.id_color=m.id_color CROSS JOIN respaldo_puestos.CORTE corte;
GO
CREATE OR ALTER PROCEDURE dbo.GuardarAccesoPuesto
    @puesto int, @usuario nvarchar(80), @hash nvarchar(500)
AS
BEGIN
    SET NOCOUNT ON;
    UPDATE dbo.PUESTO_LOGISTICO SET usuario=@usuario,[password]=@hash WHERE id_puesto=@puesto;
    IF @@ROWCOUNT=0 THROW 51004, 'El puesto no existe.', 1;
END;
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
        INSERT dbo.REGISTRO(id_puesto,id_ubicacion,fecha_hora_inicio)
        VALUES(@puesto,@ubicacion,SYSUTCDATETIME());
        SET @id=CONVERT(int,SCOPE_IDENTITY());
    END;
    COMMIT;
    SELECT * FROM reporting.vw_kpi_atenciones WHERE id_registro=@id;
END;
GO
CREATE OR ALTER PROCEDURE dbo.FinalizarAtencion @puesto int, @registro int
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;
    UPDATE r SET fecha_hora_fin=COALESCE(r.fecha_hora_fin,SYSUTCDATETIME())
    FROM dbo.REGISTRO r JOIN dbo.PUESTO_LOGISTICO p ON p.id_puesto=r.id_puesto
    WHERE r.id_registro=@registro AND r.id_puesto=@puesto AND p.usuario IS NOT NULL;
    IF @@ROWCOUNT=0 THROW 51003, 'La atención no pertenece a tu puesto.', 1;
    SELECT * FROM reporting.vw_kpi_atenciones WHERE id_registro=@registro;
END;
GO
