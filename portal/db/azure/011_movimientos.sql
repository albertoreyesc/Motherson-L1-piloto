-- =====================================================================
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
        THROW 51010, 'El movimiento debe tener piezas distintas de 0.', 1;

    BEGIN TRANSACTION;

    DECLARE @parte varchar(50), @actual int;
    SELECT @parte = numero_parte, @actual = cantidad
    FROM dbo.UBICACION WITH (UPDLOCK, HOLDLOCK)
    WHERE id_ubicacion = @ubicacion;

    IF @parte IS NULL
        THROW 51011, 'La ubicación no existe.', 1;
    IF @actual + @piezas < 0
        THROW 51012, 'No hay suficientes piezas en la ubicación para esa salida.', 1;

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
GO
