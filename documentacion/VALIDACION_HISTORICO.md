# Validación del histórico — Fase C

02/11/2026. Resultado: histórico preservado y consultable en SQL; apertura conciliada en el MVP. Toda la información de esta entrega es SIMULADA.

## Evidencia de origen

| Dato | Resultado comprobado |
| --- | --- |
| Maestro / revisión | 4d577245a22d / 210 |
| Cobertura base | 25/10/2025–24/10/2026 |
| Primera fecha de evento declarada UTC | 2025-10-25T06:00:00 |
| Última fecha de evento declarada UTC | 2026-10-27T15:30:51.389889; incluye continuación |
| Fuentes originales gzip | 10, conservadas binarias y consultables |
| Eventos de registros.jsonl.gz | 709,042 |
| IDs de evento repetidos | 0 |
| IDs de versión repetidos | 0 |
| Referencias no nulas a posiciones inexistentes | 0 |
| Diferencias entre último saldo conocido y saldo exportado | 0 |
| Huella conjunta de las diez fuentes | 204630df3c25698750693d09a7ccdbaf47e025a6d6793a18d68730d993e4f873 |
| Archivos de código fuente original comparados | 113; ninguna diferencia frente a huellas A/B |

El informe máquina portal/evidencias/validacion-historico.json contiene el SHA-256 y conteo de cada archivo, tipos de evento y lista de exclusiones. La prueba SQL recalculó los diez SHA sobre contenido_gzip almacenado. Archivo.FUENTE contiene las exportaciones originales completas, no solo su nombre/ruta. El lector puede recuperarlas mediante reporting.vw_archivos_originales.

Los 506 CAMBIO_TURNO sin posición son eventos globales válidos. No se crean ubicaciones ficticias para forzarlos a una FK del MVP. Los campos de identidad desconocidos se preservan como NULL.

## Clasificación para movimientos

| Clase | Filas | Criterio |
| --- | ---: | --- |
| SALIDA | 172,338 | CONSUMO con delta negativo aplicado |
| ENTRADA | 58,462 | ENTRADA_CONFIRMADA con delta positivo |
| APERTURA histórica | 86 | INVENTARIO_INICIAL con delta no negativo |
| AJUSTE histórico | 15 | AJUSTE_AUDITADO con delta explícito distinto de cero |
| Evento conservado que no es movimiento | 478,141 | Solicitud, alarma, etapa, cierre, prueba u otro evento sin delta operativo compatible |
| **Total** | **709,042** | Todos conservados |

La proyección de movimientos históricos tiene 230,901 filas. El resto permanece consultable como evento y en las exportaciones originales; no se eliminó por no encajar en REGISTRO. Los 199 CONSUMO sin delta negativo no se contabilizan como consumo aplicado. Los cambios de saldo de escenarios de prueba no se transforman en salidas inventadas.

Cuando entrada/salida dispone de ambos saldos, se comprobó saldo_anterior + delta = saldo_posterior. Para conciliar apertura se usaron los últimos saldos explícitos por posición y se contrastaron con saldos_actuales.jsonl.gz; no se sumaron todas las fotos de inventario.

Por ese motivo, la vista mínima de movimientos permite analizar flujos compatibles, pero no basta para reconstruir toda la historia de saldos de los escenarios anteriores. Revisar los eventos de prueba, cierres y fuentes completas para ese análisis.

## Apertura del MVP

| Población fuente | Cantidad |
| --- | ---: |
| Posiciones del esquema 21 | 102 |
| Documentales | 79 |
| Lógicas simuladas | 23 |
| Con saldo conocido / operables en origen | 86 |
| No operables, sin saldo habilitado | 16 |
| Elegibles documentales para apertura nueva | 63 |
| Excluidas del inventario activo nuevo | 39 |
| Piezas simuladas de apertura | 10,116 |

Las 39 exclusiones son las 23 posiciones lógicas pendientes de validación y las 16 no operables. Los motivos individuales y las referencias se conservaron. La apertura no prueba un conteo físico ni que toda la planta esté representada en las ubicaciones activas del MVP.

Los catálogos cargados tienen 63 materiales, 63 ubicaciones documentales y seis códigos de puesto. Que un puesto exista en el catálogo no implica que todas sus posiciones simuladas se hayan habilitado como ubicaciones físicas. COLOR queda vacío y sus referencias NULL porque no hay fuente de color validada.

Cada ubicación elegible recibió una sola APERTURA_SIMULADA, de cero al saldo conciliado, realizada por la cuenta inicial de importación. El importador identifica al ejecutor de la carga, no al responsable de los eventos históricos. La suma se comprobó antes del sello y después de confirmar la transacción.

Se volvió a ejecutar la carga en Desarrollo: reconoció la huella existente y conservó 63 aperturas y 10,116 piezas. Evidencia: portal/evidencias/recarga-idempotente.txt. La base de Pruebas incluye además cuatro aperturas LAB de 72 piezas: total 10,404; es dato de prueba aislado, no apertura de planta.

## Fecha, contexto y limitaciones

El corte inclusivo SQL existente es 2026-10-27T15:30:51.3900000. El máximo evento sigue en .389889, sin cambio; la diferencia corresponde únicamente al redondeo del metadato inicial. El límite incluye todos los eventos fuente. La fecha de cada evento original se conserva sin desplazarla para coincidir con PFEP T1/T2. La calibración del reloj fuente contra planta no está demostrada.

El histórico conserva IDs, nombres y códigos de la exportación 21. Sus identidades de posición se trataron como inmutables según las restricciones de ese esquema; no se reconstruye el pasado usando los catálogos reducidos actuales. Las uniones de reporting requieren el espacio de identificadores del origen.

Muchos eventos pertenecen a un proceso generador y carecen de cuenta humana. No se inventó usuario de captura. Las personas ficticias, sus actuaciones y las cuentas SQL/portal se mantienen conceptualmente separadas.

Las diez exportaciones no sustituyen por sí solas el respaldo completo de las 35 tablas. Las columnas que nunca estuvieron en esas exportaciones no se fabricaron: el respaldo y la entrega original se preservan como evidencia complementaria. Esta fase verificó y cargó los archivos disponibles, no una nueva restauración completa del esquema anterior.

## Integridad y acceso posteriores

En SQL se comprobaron: diez fuentes con SHA exacto, 709,042 eventos, ausencia de IDs globales repetidos, corte inclusivo válido, una apertura por ubicación y saldos de UBICACION coincidentes con el último comprobante nuevo. Las vistas de archivo están selladas; consultar no escribe.

El login real de Equipo 2 pudo consultar reporting y recibió denegación al intentar escribir en inventario, borrar eventos, ejecutar movimientos o leer hashes de cuentas. La prueba de reinicio del contenedor compartido preservó los 230,901 movimientos históricos y el inventario del caso LAB, sin reconstrucción desde Python al arrancar.
