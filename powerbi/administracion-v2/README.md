# Modelo publicado para las operaciones v2

Preparado desde la definición **real del modelo publicado**, recuperada por Fabric el 22/11/2026, no desde un PBIX supuesto. Conserva las 8 tablas, 8 relaciones y 110 medidas existentes. No se cambió el diseño de las tres páginas del informe.

Estado: **publicado, actualizado y conciliado**. El informe Motherson conserva su identificador y sus tres diseños; ahora utiliza el modelo `Motherson piloto v2` y exclusivamente las vistas de la base piloto. Último refresco comprobado: 22/11/2026 21:05:49 Ciudad de México (23/11/2026 03:05:49 UTC). Datos conciliados con DAX: 10,464 piezas, 58,422 atenciones y 9,254 completados del 03/02/2026 al 31/03/2026.

Actualización programada verificada por API: lunes a viernes, 06:00, 15:00 y 23:30, zona `Central Standard Time (Mexico)`. La cuenta propietaria es Power BI Motherson. El lector SQL dedicado solo dispone de SELECT en las ocho vistas BI; su secreto permanece fuera del repositorio. Los permisos de lectura existentes de Administración de planta se conservan.

## Archivos y cambios

- `definition/`: TMDL completo basado en el servicio. Parámetro ServidorSQL apunta exclusivamente al servidor piloto `motherson-piloto-l1-20261119.database.windows.net`, base `Motherson_L1`.
- `../../portal/db/administracion/002_fuentes_powerbi.sql`: vistas de compatibilidad `mes2.bi_*`, ya aplicadas al piloto. No escriben en las tablas originales ni reconstruyen saldos.
- Claves de ubicaciones, puestos y estanterías incluyen línea e identificador estable. Se añadió `clave_material` para relacionar sin fusionar números de parte iguales entre líneas. Las etiquetas visibles conservan el número de parte.
- REGISTRO combina el histórico cerrado y las nuevas atenciones sin copiarlos dos veces, convierte las fechas a Ciudad de México y calcula intervalos por material y estación.
- Los identificadores de turno del informe son los grupos de análisis 1/2, no los identificadores físicos 2/3 del catálogo anterior.
- `Cobertura (min)` se deja en blanco en el modelo: la fórmula anterior infería ritmo de las atenciones y no equivale al cálculo por consumos y calendario del portal. Hasta incorporar una fuente equivalente no debe presentar minutos como cobertura temporal actual. Existencias y mínimos siguen siendo indicadores distintos.

## Publicación y recuperación

- Workspace: `851cabfa-9a0e-43ef-a5b0-88576f9201b7`.
- Informe conservado: `561bace1-3a3f-4671-b8e3-9eaf4b90a00c`.
- Modelo activo: `bb20ed94-f540-4005-b88e-3e9745e0bede`.
- Modelo anterior conservado para recuperación: `22d01542-205e-48da-a3a3-2d5988621954`. El respaldo de su definición y vínculo anterior se guarda en la carpeta privada de operación. Para recuperar, volver a vincular el mismo informe a ese modelo; ello vuelve a mostrar la fuente anterior, no operaciones v2.
- Se desplegó primero el modelo separado, se configuraron credenciales, se completó un refresco real y se conciliaron los totales antes de vincular el informe mediante Rebind. El último ajuste de medidas también tuvo refresco terminado y conciliación.
- La fecha de actualización procede de la captura del modelo en Ciudad de México, no del último registro histórico. El diagnóstico distingue mínimos de cobertura temporal.
- Las líneas 2–4 siguen vacías; no se generaron operaciones de demostración en el piloto. El modelo incluye claves por línea y no fusiona materiales que compartan número de parte.
- La cobertura temporal del informe permanece sin estimación: el cálculo válido por ubicación, calendario y consumos se consulta en Inventario del portal. Incorporar ese cálculo al modelo BI sigue pendiente; no usar frecuencia de atenciones como consumo.
- Conexión SQL cifrada. Reglas de Power BI limitadas a las direcciones observadas `158.23.200.149` (validación) y `158.23.200.135` (refresco). No se habilitó acceso global a servicios Azure. Si Microsoft cambia su dirección de salida, revisar el error concreto y la configuración de red antes de ampliar acceso.
- Se mantuvieron las licencias existentes; no se contrató capacidad. La cuenta propietaria muestra una prueba vigente. La continuidad del acceso depende de las licencias/permisos correspondientes.

Prueba de estructura: `dotnet build portal/pruebas-powerbi` y ejecutar su binario con `powerbi/administracion-v2/definition` como argumento. Las vistas se prueban en SQL local junto con `portal/pruebas-administracion`.

Referencias oficiales: [definición de modelo Fabric](https://learn.microsoft.com/en-us/rest/api/fabric/semanticmodel/items/get-semantic-model-definition), [actualizar definición](https://learn.microsoft.com/en-us/rest/api/fabric/semanticmodel/items/update-semantic-model-definition), [TMDL](https://learn.microsoft.com/en-us/analysis-services/tmdl/tmdl-overview), [actualización programada](https://learn.microsoft.com/en-us/power-bi/connect-data/refresh-scheduled-refresh).
