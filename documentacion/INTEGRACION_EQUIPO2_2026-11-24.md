# Integración del dashboard del equipo 2 · 24/11/2026

El portal y Power BI consultan el mismo catálogo y las mismas operaciones de Azure SQL piloto. No se requiere Power Apps para capturar: el portal escribe mediante su API autenticada. Power BI tiene un lector de solo consulta en las ocho vistas `mes2.bi_*`.

## Entrega incorporada

- Power BI: Crítico por Surtir, Desempeño semanal y Diagnóstico. Se conserva el identificador del informe, su acceso Microsoft y el modelo v2.
- KPIs del portal y descarga Excel: Hoja 1 Estado actual, Hoja 2 Actividad logística y Hoja 3 Diagnóstico. Comparten línea, corte, estación, material, turno y período. El inventario se calcula al corte; actividad y diagnóstico se calculan en el período.
- Hoja 3: tiempos persistidos de atenciones completadas, promedio, P90 inclusivo, objetivo de 4.5 minutos, agrupaciones por puesto/material/turno e inicios por hora. Sin observaciones: N/D. El ranking señala muestras menores de diez. Se excluyen fines anteriores al inicio; no se generan duraciones con movimientos.
- Dashboard del portal: panel de operación actual, catálogo buscable y consulta cada 30 segundos mientras la pestaña está visible. Puede pausarse o actualizarse manualmente. Mantiene permisos y separación por línea. Ante errores conserva el corte anterior y avisa.
- El informe incrustado conserva su propio corte. Recargar sus visuales no importa nuevas filas de SQL.

## Catálogo único

Las vistas incorporan modelo, revisión, variante, estado activo e identificador de línea. Las relaciones usan claves estables compuestas por línea e identificador, no coincidencias aproximadas de nomenclatura. Un dígito diferente sigue representando otro material. Un material preparado sin ubicación aparece como **Sin ubicación**, no como material agotado con existencia inventada. El modelo conserva materiales de las cuatro líneas; el portal solo incrusta el informe configurado de Línea 1 en esa línea.

No se ejecutaron los scripts iniciales de creación, borrado o carga de CSV de la entrega. No se recargó el histórico ni se reemplazó el catálogo vigente. Las vistas se aplicaron en transacción y conciliaron 11,465 piezas y 58,426 atenciones; el refresco del modelo terminó el 24/11/2026 a las 17:43:09 de Ciudad de México. Estos totales corresponden a ese corte y cambian con la operación.

## Correcciones al informe recibido

Se conservaron los diseños y 110 medidas con las correcciones anteriores de cobertura. El selector Modelo ahora usa el modelo del vehículo, separado de Uso PFEP. Se quitaron filtros de fecha colocados por error en dos selectores de número de parte. El selector de período de Diagnóstico ya no fija febrero-marzo de demostración y usa un grupo independiente. La página inicial es Crítico por Surtir. Se verificaron 141 referencias de campos/medidas. Los contadores de materiales y sus mínimos se agrupan por clave de material; tener dos ubicaciones ya no duplica el material ni su mínimo.

El PBIX y el TMDL originales se conservan como referencia en `powerbi/equipo2-2026-11-24/`; **no deben publicarse encima del piloto** porque todavía contienen la conexión/modelo de origen. La carpeta `report/` es el diseño adaptado publicado; `powerbi/administracion-v2/definition/` es el modelo activo. Los README originales se incluyen como antecedentes, no como instrucciones para reinstalar el piloto.

## Actualización y costos

El modelo actual utiliza Import. Su programación existente es lunes a viernes a las 06:00, 15:00 y 23:30, zona Ciudad de México. Se reactivó desde la cuenta propietaria después de resolver el bloqueo de red. El panel operativo de 30 segundos permite ver el catálogo reciente sin contratar Power Apps o capacidad adicional; consume las cuotas existentes de SQL y Render.

Esto no convierte el informe Power BI en tiempo real. Microsoft limita la actualización por licencia/capacidad: Pro admite hasta ocho refrescos programados diarios; la actualización automática de páginas requiere DirectQuery u otra fuente compatible. DirectQuery no obliga por sí solo a comprar Power Apps, pero consulta SQL y está sujeto a límites de servicio y licencia. La cuenta actual muestra una prueba de Power BI con 54 días restantes; no se contrató capacidad ni se introdujo un método de pago. La visualización privada compartida después de la prueba debe revisarse según licencias existentes. No se promete servicio ilimitado o gratuito permanente.

Cobertura temporal: el portal usa calendario y consumos de cada ubicación. El informe mantiene **Sin estimación** hasta incorporar esa misma fuente validada. La frecuencia de atenciones y el porcentaje de capacidad de estantería no representan minutos de cobertura. Las 85 ubicaciones sin estimación de la captura pueden deberse a falta de consumos suficientes aunque haya un calendario de referencia.

## Red y mantenimiento

Se corrigió SQL 40615: Microsoft cambió la salida de Power BI a 158.23.109.143. Se permiten los siete prefijos IPv4 oficiales de `PowerBI.MexicoCentral` (102 direcciones), versión Service Tags 20261123, changeNumber 428. No se habilitó acceso global a servicios Azure. El usuario SQL mantiene SELECT únicamente en las ocho vistas. Las reglas temporales de mantenimiento de la estación se retiraron al finalizar. Revisar semanalmente los prefijos oficiales y ante cambios de región/capacidad o nuevos errores; no existe una tarea automática creada para mantenerlos.

## Validación y recuperación

- 78 pruebas frontend; compilación TypeScript/Vite aprobada.
- 75 comprobaciones SQL locales aisladas, con autorización entre líneas, idempotencia, inventario y cálculo numérico: duraciones de 2 y 6 minutos producen media 4, P90 5.6 y 50% fuera de objetivo.
- TMDL validado: ocho tablas, ocho relaciones y 110 medidas.
- Actualización SQL y refresco Power BI terminados, conciliación de stock y atenciones con el portal.
- Respaldo completo previo del modelo y del informe en la carpeta privada `.local/v2/publicacion/`, fuera de Git. Para recuperar el diseño anterior, restaurar su definición de informe mediante Fabric conservando el mismo identificador y vínculo; para recuperar el modelo, restaurar su definición respaldada y refrescar. No restaurar saldos de datos de demostración.
- Para revertir solo el portal, redesplegar el commit anterior 67224c7. Las columnas adicionales de las vistas son compatibles y no cambian existencias. Conservar las nuevas reglas regionales para evitar volver al error de conexión.

Referencias: [actualización programada](https://learn.microsoft.com/en-us/power-bi/connect-data/refresh-scheduled-refresh), [actualización automática de páginas](https://learn.microsoft.com/en-us/power-bi/create-reports/desktop-automatic-page-refresh), [Service Tags Fabric](https://learn.microsoft.com/en-us/fabric/security/security-service-tags), [lista oficial de rangos](https://www.microsoft.com/en-us/download/details.aspx?id=56519).
