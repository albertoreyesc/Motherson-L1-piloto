# Modelo candidato para las operaciones v2

Preparado desde la definición **real del modelo publicado**, recuperada por Fabric el 22/11/2026, no desde un PBIX supuesto. Conserva las 8 tablas, 8 relaciones y 110 medidas existentes. No se cambió el diseño de las tres páginas del informe.

Estado: **fuentes SQL instaladas y conciliadas; candidato validado como TMDL, pendiente de publicar, actualizar y conciliar en Power BI**. El informe publicado todavía utiliza `motherson-l1-equipo.database.windows.net`; su actualización programada está desactivada. La aplicación pudo recuperar el modelo, pero recibió HTTP 401 al consultar la conexión SQL administrada. No se sustituyó el modelo en uso por uno sin credenciales de conexión verificadas.

## Archivos y cambios

- `definition/`: candidato TMDL completo basado en el servicio. Parámetro ServidorSQL apunta exclusivamente al servidor piloto `motherson-piloto-l1-20261119.database.windows.net`, base `Motherson_L1`.
- `../../portal/db/administracion/002_fuentes_powerbi.sql`: vistas de compatibilidad `mes2.bi_*`, ya aplicadas al piloto. No escriben en las tablas originales ni reconstruyen saldos.
- Claves de ubicaciones, puestos y estanterías incluyen línea e identificador estable. Se añadió `clave_material` para relacionar sin fusionar números de parte iguales entre líneas. Las etiquetas visibles conservan el número de parte.
- REGISTRO combina el histórico cerrado y las nuevas atenciones sin copiarlos dos veces, convierte las fechas a Ciudad de México y calcula intervalos por material y estación.
- Los identificadores de turno del informe son los grupos de análisis 1/2, no los identificadores físicos 2/3 del catálogo anterior.
- `Cobertura (min)` se deja en blanco en el candidato: la fórmula anterior infería ritmo de las atenciones y no equivale al cálculo por consumos y calendario del portal. Hasta incorporar una fuente equivalente no debe presentar minutos como cobertura temporal actual. Existencias y mínimos siguen siendo indicadores distintos.

## Validación y publicación pendiente

1. Iniciar sesión como propietario del modelo y configurar una conexión Azure SQL al **servidor piloto**, con el lector SQL autorizado. No usar credenciales del portal ni guardarlas en Git/TMDL.
2. Mantener un respaldo privado del modelo actual. El original ya se recuperó en la carpeta privada de operación, excluida de Git.
3. Abrir/validar el candidato en una copia del informe o desplegarlo con la API soportada de Fabric, conservando el informe y sus identificadores. La estructura se validó con `Microsoft.AnalysisServices` 19.114.12; esto no ejecuta expresiones DAX ni una actualización de datos.
4. Actualizar y comparar 10,464 piezas y 58,422 atenciones del piloto al corte de instalación. Si hay operaciones posteriores, comparar el corte vigente con las mismas fuentes; no forzar esas cantidades como valores permanentes. Para 03/02/2026–31/03/2026 deben conciliar las 9,254 atenciones históricas comprobadas en portal, respetando fecha de fin y filtros.
5. Verificar las tres páginas, relaciones y filtros por línea. Las líneas 2–4 están vacías en el piloto y no deben mostrar datos de Línea 1.
6. Configurar la frecuencia de actualización disponible con las licencias existentes y verificar una ejecución terminada correctamente. Recargar el iframe no sustituye la actualización del modelo. No contratar capacidad ni licencias para este paso sin decisión de Motherson.
7. Solo después de esa conciliación, actualizar el aviso del portal para declarar que el informe incorpora operaciones v2.

Prueba de estructura: `dotnet build portal/pruebas-powerbi` y ejecutar su binario con `powerbi/administracion-v2/definition` como argumento. Las vistas se prueban en SQL local junto con `portal/pruebas-administracion`.

Referencias oficiales: [definición de modelo Fabric](https://learn.microsoft.com/en-us/rest/api/fabric/semanticmodel/items/get-semantic-model-definition), [actualizar definición](https://learn.microsoft.com/en-us/rest/api/fabric/semanticmodel/items/update-semantic-model-definition), [TMDL](https://learn.microsoft.com/en-us/analysis-services/tmdl/tmdl-overview), [actualización programada](https://learn.microsoft.com/en-us/power-bi/connect-data/refresh-scheduled-refresh).
