# Histórico principal, responsables y cobertura

Actualización: 05/11/2026. Portal 5089, base Motherson_L1_MVP_Demo.

## Fuente y presentación

El histórico maestro de Línea 1, ejecución 4d577245a22d, es la consulta principal. Conserva los seis puestos y los doce responsables ficticios, con sus turnos PFEP T1 y PFEP T2. El periodo inicial del filtro es 25/10/2025–24/10/2026.

Los orígenes anteriores no son tres inventarios nuevos: identifican procedencias distintas de los hechos conservados. Las aperturas, pruebas del MVP y continuación quedan accesibles en la sección plegable de auditoría. No se borraron ni reasignaron eventos para simplificar la pantalla.

El maestro completo conserva 709,042 eventos. La proyección de movimientos disponible contiene 230,976 filas, incluidos 75 movimientos del MVP anterior. El filtro anual inicial devuelve 230,839 movimientos; este conteo no incluye eventos fuera de su intervalo. Los conteos corresponden a ámbitos distintos y no deben sumarse.

Historial permite seleccionar el número de parte completo, responsable ficticio y turno. Se conservan sufijos y espacios de la clave de origen. La exportación y la paginación utilizan los filtros de la consulta aplicada. Cuenta de acceso, responsable ficticio y proceso generador se presentan como conceptos diferentes.

## Atenciones: inicio y fin

REGISTRO conserva intervalos nuevos de atención por puesto, ubicación y turno de inicio. Su duración se calcula a partir del inicio y fin; un intervalo abierto todavía no tiene duración final. Esto no representa por sí mismo una entrada, salida ni cantidad de material.

La base consultada tiene cero atenciones nuevas. La pantalla explica esta condición y permite abrir el historial de movimientos. Administración consulta estos intervalos y gestiona accesos; no registra atenciones. No se convirtieron automáticamente movimientos históricos en intervalos ni se inventaron fechas de atención.

## Cobertura histórica en Resumen e Inventario

La presentación principal incorpora dos cortes reproducibles, calculados sobre saldos y consumos del mismo histórico y del mismo instante. Son observaciones de datos existentes, no escenarios regenerados para obtener colores.

| Corte en Ciudad de México | Agotado | Riesgo de paro | Crítico | Advertencia | Normal | Sin estimación |
| --- | ---: | ---: | ---: | ---: | ---: | ---: |
| 04/02/2026 09:45:43 | 0 | 2 | 0 | 3 | 1 | 95 |
| 06/04/2026 15:31:11 | 4 | 0 | 2 | 2 | 1 | 92 |

Cada corte evalúa 101 posiciones. Por ejemplo, el primer corte incluye riesgo para 5QM 419 502 AS; el segundo incluye agotamiento para 2Q0 953 254. Las tarjetas muestran la referencia que respalda la estimación.

La cobertura se obtiene del saldo conocido dividido entre la tasa de consumo de los últimos 60 minutos de trabajo programados hasta el corte. Se necesitan al menos dos consumos positivos en la ventana. No se utiliza la capacidad como existencia ni una tasa inventada.

- Normal: cobertura >= 60 minutos.
- Advertencia: >= 30 y < 60 minutos.
- Crítico: >= 15 y < 30 minutos.
- Riesgo de paro: > 0 y < 15 minutos.
- Agotado: saldo conocido igual a cero.
- Sin estimación: saldo desconocido, calendario no aplicable o muestra de consumo insuficiente.

La comparación utiliza el valor sin redondear. Los valores inferiores a 15 minutos no se presentan como 15 por redondeo. El rojo crítico utiliza #DA2128.

El calendario documental termina el 24/10/2026. La consulta de la continuación actual, fuera de ese calendario, conserva la explicación «Sin estimación». La cobertura histórica no se combina con el saldo actual para simular una tasa reciente. El mínimo por material sigue siendo un indicador independiente de la cobertura por posición.

## Conservación y validación

No se modificaron el esquema, las ocho tablas dbo, las ocho claves foráneas, saldos SQL, atenciones existentes ni fuentes selladas. El total SQL conservado es 10,435 piezas. El informe de integridad compara definiciones y huellas de filas y archivos antes y después.

La prueba integrada pasó 55 comprobaciones de API y navegador, incluidos filtros exactos, exportación, responsables y turnos, ambos cortes, permisos, ausencia de duplicados, persistencia de la continuación y consultas sin escritura. Los movimientos de la prueba se guardaron únicamente en un diario temporal eliminado al terminar; no son movimientos de la demostración.

Evidencias: [integridad antes](../portal/evidencias/historico-principal/integridad-antes.json), [integridad después](../portal/evidencias/historico-principal/integridad-despues.json), [pruebas](../portal/evidencias/historico-principal/pruebas-api-navegador.json), [cortes y filtros](../portal/evidencias/historico-principal/cortes-y-filtros.json).

Capturas: [corte con agotamientos](../portal/evidencias/historico-principal/capturas/09-corte-agotamiento.png), [filtro de material](../portal/evidencias/historico-principal/capturas/10-historial-material.png), [responsable y turno](../portal/evidencias/historico-principal/capturas/11-responsable-turno.png), [explicación de atenciones](../portal/evidencias/historico-principal/capturas/12-atenciones-explicacion.png).
