/* =====================================================================
   Motherson L1 - Inventario del portal leído de UBICACION.cantidad
   Fecha: 10/11/2026.  Solo para la base de Azure. Correr DESPUÉS de
   011_azure_objetos_portal.sql.

   Por qué: dbo.RegistrarMovimiento cambia UBICACION.cantidad y deja
   MATERIAL.stock = suma de las cantidades del material. Para que el
   portal vea exactamente lo mismo, su vista de inventario debe leer
   UBICACION.cantidad (antes tomaba MATERIAL.stock).

   Solo reemplaza una vista. No toca tablas ni datos.
   ===================================================================== */
SET NOCOUNT ON;
IF COL_LENGTH('dbo.UBICACION','cantidad') IS NULL OR OBJECT_ID('dbo.vw_modelo_puestos','V') IS NULL
   OR OBJECT_ID('dbo.RegistrarMovimiento','P') IS NULL
BEGIN
    RAISERROR('No se cambió nada. Falta UBICACION.cantidad, dbo.vw_modelo_puestos o dbo.RegistrarMovimiento.',16,1);
    SET NOEXEC ON;
END;
GO
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
GO
SET NOEXEC OFF;
GO

-- Revisión opcional (una sola consulta): ubicaciones sin cantidad y materiales cuyo stock no es la suma.
-- El portal no permite capturar en ubicaciones con cantidad NULL.
SELECT (SELECT COUNT(*) FROM dbo.UBICACION WHERE cantidad IS NULL) ubicaciones_sin_cantidad,
       (SELECT COUNT(*) FROM dbo.MATERIAL m
         WHERE ISNULL(m.stock,-1) <> ISNULL((SELECT SUM(cantidad) FROM dbo.UBICACION u WHERE u.numero_parte=m.numero_parte),-1)
       ) materiales_con_stock_distinto_a_la_suma;
GO
