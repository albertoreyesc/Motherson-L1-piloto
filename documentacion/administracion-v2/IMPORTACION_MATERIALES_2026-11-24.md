# Corrección de importación de materiales · 24/11/2026

La plantilla sí admitía materiales nuevos y números de parte distintos por un dígito. El bloqueo observado en Línea 2 era la referencia a puestos todavía inexistentes (`L2-P01`, `L2-P05`, `L2-P06`). La vista previa exigía crearlos previamente y deshabilitaba todo el lote. Además, el encabezado de la propia plantilla «Modelo / vehículo» no tenía alias reconocido.

## Flujo corregido

1. Seleccionar la línea e importar el archivo Excel o CSV.
2. Revisar hoja y columnas; «Modelo / vehículo» ya se reconoce automáticamente.
3. Autorizar materiales y valores nuevos cuando corresponda. Si faltan puestos, activar **Crear los puestos faltantes indicados en el archivo, solo en esta línea**.
4. Revisar la lista de puestos que se crearán y las filas marcadas como Alta. Aplicar el lote completo.
5. Asignar la estación, estantería y nivel reales en Estaciones y ubicaciones. El catálogo se puede preparar antes de recibir material; no se inventan capacidades ni cantidades. Registrar existencias mediante entradas.

Se reutiliza el puesto existente por identificador exacto o por nombre inequívoco. Un nombre ambiguo continúa rechazándose. Los puestos nuevos se crean una sola vez dentro de la misma operación transaccional que los materiales y requieren autorización explícita también en el servidor. Una fila inválida cancela toda la operación, incluidos los puestos propuestos. El comprobante informa `puestosCreados`.

Los números de parte mantienen identidad exacta: cambiar un dígito crea otro material; no se fusionan códigos ni se modifica una parte existente por semejanza. Los duplicados exactos dentro del archivo siguen rechazándose. La plantilla incluye una hoja adicional Puestos disponibles y las instrucciones actualizadas.

## Verificación

- 77 pruebas de frontend aprobadas, incluidas las columnas de plantilla, puestos nuevos/existentes/ambiguos y números distintos por un dígito.
- 71 comprobaciones SQL locales aprobadas: vista previa sin escritura, autorización de creación, lote atómico, reintento idempotente, relaciones y conservación de Línea 1.
- Compilación frontend y backend sin errores. No requiere migración de esquema.
- Prueba en navegador local: reproducir el bloqueo, activar la opción, importar L2-0001/L2-0002 y comprobar ambos materiales con su empaque/modelo y un solo puesto L2-P01. Datos de prueba exclusivamente locales; no se cargó este lote en Azure.

Evidencias: [vista previa](../evidencia-integracion/importacion-2026-11-24/vista-previa-local.jpg) y [confirmación local](../evidencia-integracion/importacion-2026-11-24/confirmacion-local.jpg).
