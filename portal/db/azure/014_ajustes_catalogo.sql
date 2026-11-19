/* =====================================================================
   Motherson L1 - Ajustes de catálogo desde el portal
   Fecha: 17/11/2026.  Solo para la base de Azure. Correr DESPUÉS de
   011_azure_objetos_portal.sql y 012_azure_inventario_por_ubicacion.sql.

   Por qué: el portal compara Azure contra su saldo inicial más lo que
   él mismo registró. Si alguien cambia la capacidad de una estantería
   directo en Azure, el portal bloquea las capturas. Con este
   procedimiento el portal hace el cambio Y lo registra en su diario,
   así las dos cosas siguen cuadrando.

   Qué hace: crea (o reemplaza) dos procedimientos:
     - dbo.AjustarCapacidadEstanteria  (botón + / − de capacidad)
     - dbo.AjustarMateriales           (importación de Excel de materiales)
   No crea ni modifica tablas, columnas ni datos. Se puede correr más
   de una vez.
   ===================================================================== */
SET NOCOUNT ON;
IF COL_LENGTH('dbo.ESTANTERIA','cajas_estanteria') IS NULL OR OBJECT_ID('dbo.vw_modelo_puestos','V') IS NULL
   OR COL_LENGTH('dbo.MATERIAL','cantidad_por_caja') IS NULL OR COL_LENGTH('dbo.MATERIAL','id_color') IS NULL
   OR OBJECT_ID('dbo.COLOR','U') IS NULL
BEGIN
    RAISERROR('No se cambió nada. Faltan columnas de ESTANTERIA/MATERIAL/COLOR o dbo.vw_modelo_puestos (corre antes 011).',16,1);
    SET NOEXEC ON;
END;
GO

-- ---------------------------------------------------------------------
-- Capacidad en cajas de una o varias estanterías, todo o nada.
-- @cambios: JSON, por ejemplo [{"estanteria":12,"cajas":4}]
-- ---------------------------------------------------------------------
CREATE OR ALTER PROCEDURE dbo.AjustarCapacidadEstanteria @cambios nvarchar(max)
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;
    IF @cambios IS NULL OR ISJSON(@cambios)<>1
        THROW 51004, 'Los cambios de capacidad no son válidos.', 1;

    DECLARE @c TABLE(id_estanteria int NOT NULL PRIMARY KEY, cajas int NULL);
    INSERT @c(id_estanteria,cajas)
    SELECT id_estanteria,cajas
    FROM OPENJSON(@cambios) WITH(id_estanteria int '$.estanteria', cajas int '$.cajas');

    IF NOT EXISTS(SELECT 1 FROM @c)
        THROW 51004, 'No hay cambios de capacidad.', 1;
    IF EXISTS(SELECT 1 FROM @c c LEFT JOIN dbo.ESTANTERIA e ON e.id_estanteria=c.id_estanteria WHERE e.id_estanteria IS NULL)
        THROW 51004, 'Una estantería no existe.', 1;
    IF EXISTS(SELECT 1 FROM @c WHERE cajas IS NOT NULL AND cajas NOT BETWEEN 1 AND 10000)
        THROW 51004, 'La capacidad debe estar entre 1 y 10,000 cajas.', 1;

    BEGIN TRANSACTION;
    UPDATE e SET cajas_estanteria=c.cajas
    FROM dbo.ESTANTERIA e JOIN @c c ON c.id_estanteria=e.id_estanteria;
    COMMIT;
END;
GO

-- ---------------------------------------------------------------------
-- Datos de material desde la importación de Excel, todo o nada.
-- @cambios: JSON, por ejemplo
--   [{"parte":"5G0 857 173","campo":"minimo","valor":"40"},
--    {"parte":"5G0 857 173","campo":"variante","valor":"82V"}]
-- campo: descripcion | uso | cantidad_por_caja | minimo | variante
-- variante se busca por COLOR.nombre; si no existe, no se aplica nada.
-- No toca existencias (UBICACION.cantidad / MATERIAL.stock) ni el puesto.
-- ---------------------------------------------------------------------
CREATE OR ALTER PROCEDURE dbo.AjustarMateriales @cambios nvarchar(max)
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;
    IF @cambios IS NULL OR ISJSON(@cambios)<>1
        THROW 51004, 'Los cambios de materiales no son válidos.', 1;

    DECLARE @c TABLE(numero_parte varchar(100) COLLATE Latin1_General_100_BIN2 NOT NULL,
                     campo varchar(30) NOT NULL, valor nvarchar(250) NULL,
                     PRIMARY KEY(numero_parte,campo));
    INSERT @c(numero_parte,campo,valor)
    SELECT parte,campo,valor
    FROM OPENJSON(@cambios) WITH(parte varchar(100) '$.parte', campo varchar(30) '$.campo', valor nvarchar(250) '$.valor');

    IF NOT EXISTS(SELECT 1 FROM @c)
        THROW 51004, 'No hay cambios de materiales.', 1;
    IF EXISTS(SELECT 1 FROM @c WHERE campo NOT IN ('descripcion','uso','cantidad_por_caja','minimo','variante'))
        THROW 51004, 'Campo de material no permitido.', 1;
    IF EXISTS(SELECT 1 FROM @c c LEFT JOIN dbo.MATERIAL m ON m.numero_parte=c.numero_parte WHERE m.numero_parte IS NULL)
        THROW 51004, 'Un material no existe.', 1;
    IF EXISTS(SELECT 1 FROM @c WHERE campo IN ('cantidad_por_caja','minimo') AND valor IS NOT NULL AND TRY_CONVERT(int,valor) IS NULL)
        THROW 51004, 'Piezas por caja y mínimo deben ser números enteros.', 1;
    IF EXISTS(SELECT 1 FROM @c c LEFT JOIN dbo.COLOR k ON k.nombre=CONVERT(varchar(10),c.valor)
              WHERE c.campo='variante' AND c.valor IS NOT NULL AND k.id_color IS NULL)
        THROW 51004, 'Una variante no existe en COLOR.', 1;

    BEGIN TRANSACTION;
    UPDATE m SET
        descripcion=CASE WHEN d.numero_parte IS NULL THEN m.descripcion ELSE d.valor END,
        uso=CASE WHEN u.numero_parte IS NULL THEN m.uso ELSE u.valor END,
        cantidad_por_caja=CASE WHEN p.numero_parte IS NULL THEN m.cantidad_por_caja ELSE TRY_CONVERT(int,p.valor) END,
        minimo=CASE WHEN n.numero_parte IS NULL THEN m.minimo ELSE TRY_CONVERT(int,n.valor) END,
        id_color=CASE WHEN v.numero_parte IS NULL THEN m.id_color ELSE k.id_color END
    FROM dbo.MATERIAL m
    LEFT JOIN @c d ON d.numero_parte=m.numero_parte AND d.campo='descripcion'
    LEFT JOIN @c u ON u.numero_parte=m.numero_parte AND u.campo='uso'
    LEFT JOIN @c p ON p.numero_parte=m.numero_parte AND p.campo='cantidad_por_caja'
    LEFT JOIN @c n ON n.numero_parte=m.numero_parte AND n.campo='minimo'
    LEFT JOIN @c v ON v.numero_parte=m.numero_parte AND v.campo='variante'
    LEFT JOIN dbo.COLOR k ON k.nombre=CONVERT(varchar(10),v.valor)
    WHERE m.numero_parte IN (SELECT numero_parte FROM @c);
    COMMIT;
END;
GO

SET NOEXEC OFF;
GO
