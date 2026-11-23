# Manual de administración de líneas

## Seleccionar línea

El control **Línea de trabajo** muestra las líneas autorizadas para tu cuenta. Al cambiar, se limpian los datos y filtros de la línea anterior. Si tienes una captura sin confirmar, resuélvela o descártala antes de continuar. Si ya enviaste una operación cuyo resultado no conoces, utiliza **Comprobar operación pendiente**: conserva el mismo comprobante y evita duplicados, incluso después de recargar.

Las nuevas líneas empiezan vacías. Administración puede crear puestos y estaciones en **Configurar línea**, después estanterías con nivel/capacidad, importar materiales y asignarlos a estanterías. Una ubicación nueva empieza en cero; las existencias se registran mediante una entrada comprobable.

## Resumen y semáforos

Resumen muestra existencias actuales conocidas y una única vista principal de alarmas de la línea. **Consultar cobertura histórica** permite elegir otro corte sin cambiar el saldo actual. En Línea 1 hay una opción adicional para cortes anteriores a la ampliación.

Hay dos indicadores distintos:

| Indicador | Unidad y alcance | Regla |
| --- | --- | --- |
| Mínimo del material | Piezas del material en las ubicaciones consultadas | Agotado: cero. Crítico: saldo positivo hasta el mínimo, incluido. Advertencia: sobre mínimo hasta 1.2 veces el mínimo, incluido. Normal: por encima. Sin ubicaciones acreditadas: sin dato. |
| Cobertura temporal | Minutos estimados por ubicación | Valores iniciales: Normal ≥60; Advertencia ≥30 y <60; Crítico ≥15 y <30; riesgo estimado de paro >0 y <15. Saldo cero: agotado. Los límites se pueden configurar por línea. |

La cobertura divide el saldo por la tasa de consumo observada en los últimos **60 minutos laborables documentados**. Requiere al menos **dos salidas positivas marcadas como consumo** en esa ubicación, dentro de la ventana y después de la apertura. No se utilizan ediciones ni importaciones para inventar una tasa. Si falta calendario, vigencia, ventana suficiente o consumo, se muestra **Sin estimación** con su causa.

En **Configurar línea**, registra periodo, días laborables, turnos en hora de Ciudad de México y excepciones. El día de un turno nocturno corresponde al día de inicio. Los turnos no pueden solaparse. Indica la fuente o autorización y umbrales con `0 < crítico < advertencia < normal`. Guardar queda auditado; los cortes anteriores conservan la configuración que existía entonces.

La cobertura es una estimación sin nuevas reposiciones; no confirma un paro real. La etiqueta **SIMULADO** sigue vigente en este piloto. Un material puede estar sobre su mínimo y, aun así, no tener información para calcular minutos de cobertura.

## Importar Excel o CSV

1. Selecciona la línea y entra en **Materiales y catálogos**.
2. Descarga la plantilla o carga un archivo `.xlsx` o `.csv`, hasta 10 MB. Elige la hoja y la fila de encabezados.
3. Revisa el mapeo. Se reconocen encabezados equivalentes aunque tengan espacios, acentos o mayúsculas diferentes. Uso PFEP, variante/color, revisión, modelo/vehículo y puesto son campos diferentes.
4. Revisa altas, modificaciones, filas sin cambios, errores y avisos. Autoriza explícitamente los materiales nuevos y las nomenclaturas o códigos distintos propuestos. Los puestos deben existir; primero configúralos si faltan.
5. Los materiales nuevos necesitan número de parte, puesto y piezas por caja. Mínimo omitido en un alta: cero. No se supone un empaque para completar un dato faltante. Los campos de texto opcionales pueden permanecer vacíos.
6. Pulsa **Aplicar lote completo**. El servidor vuelve a validar y confirma todo en una transacción; un error impide aplicar el lote. Si el catálogo cambió mientras preparabas el archivo, actualiza y revisa otra vez.
7. Descarga la vista previa y el resultado con el comprobante. Si la conexión falla después del envío, comprueba la operación pendiente antes de volver a capturar.

La identidad se compara de forma exacta, conservando ceros y sufijos; no se fusionan códigos parecidos. Guarda el número de parte como texto en Excel. Un dato numérico ya redondeado por Excel no puede recuperarse. Se rechazan fórmulas como entradas del catálogo: conviértelas a valores antes de importar. El formato numérico de ceros simples se conserva al leer el archivo.

Una celda vacía conserva el valor existente. Para vaciar intencionalmente descripción, variante, revisión, modelo o uso, escribe **[BORRAR]**. No se pueden borrar así el puesto, número de parte, empaque o mínimo. Un lote contiene hasta 500 filas. El importador no cambia saldos, ni crea atenciones.

## Materiales activos e inactivos

En el catálogo puedes filtrar activos, inactivos o todos. Administración puede inhabilitar o reactivar indicando un motivo. Se conservan movimientos, imágenes, relaciones y comprobantes.

Un material inactivo no admite entradas ordinarias ni nuevas asignaciones o atenciones. Administración puede retirar su remanente mediante una salida autorizada. El cierre de una atención ya iniciada sigue permitido. Reactivar permite volver a operarlo sin crear otro material.

**Renombrar** es la acción explícita para cambiar el número de parte manteniendo el identificador y las relaciones. Excel no realiza este cambio de identidad.

**Eliminar** solo funciona para un registro sin ubicaciones, imágenes ni atenciones asociadas. Si tiene uso o dependencias, el servidor lo rechaza: utiliza inhabilitación. El icono para quitar una fotografía solo afecta la fotografía, no al material.

## Entradas, salidas y atenciones

Entradas y salidas vuelve a utilizar bloques de material. Busca por número de parte, descripción o ubicación y filtra por estación. Cada bloque muestra foto disponible, saldo, cajas equivalentes y espacio libre. Una caja equivalente puede ser fraccionaria; la captura continúa en cajas enteras.

1. Elige **Entrada · cajas** o **Salida · cajas**, según los permisos de tu cuenta.
2. Selecciona el bloque. Se conserva el mismo material y ubicación para el movimiento y su atención.
3. Si comienza un surtido, pulsa **Iniciar atención**. El servidor registra el inicio; no modifica piezas.
4. Elige las cajas y, para una salida, marca **Consumo de producción** solo cuando corresponda. Pulsa **Revisar entrada/salida**: muestra piezas y saldo previsto antes de confirmar.
5. Confirma una vez. El servidor verifica versión, saldo y capacidad, y devuelve el comprobante. Si la respuesta queda pendiente, usa **Comprobar operación pendiente**; no vuelvas a capturarla.
6. Pulsa **Finalizar atención** al terminar realmente el surtido. El tiempo se conserva en Hoja 2. Si el movimiento se confirma con una atención propia abierta en esa ubicación, conserva su referencia. No se inventa una atención al guardar una entrada.

Las tarjetas de materiales inhabilitados permiten consultar restricciones; solo administración puede retirar remanentes. El surtidor mantiene el acceso a su puesto y sus líneas autorizadas.

## KPIs y descarga

**Hoja 1 · Estado actual** es el inventario al corte. **Hoja 2 · Actividad logística** cuenta atenciones, no hojas del archivo de importación.

- Completados: fin dentro del periodo y hasta el corte.
- En curso: iniciados y todavía abiertos al corte; pueden haber comenzado antes del periodo.
- Repetidos: atenciones adicionales a la primera del mismo material, estación y día de inicio.
- Incompletos: registros sin responsable o referencia válida dentro del alcance; los nuevos formularios exigen esos datos.
- Intervalo promedio: horas entre inicios consecutivos comparables del mismo material y estación en el periodo. Sin dos inicios comparables muestra **N/D**.

El periodo afecta la actividad; el saldo se calcula al corte. Estación y material acotan ambos resultados. Turno filtra la actividad por hora de inicio: grupo 1 de 06:00 a 14:59; grupo 2 de 15:00 a 05:59, en Ciudad de México. Estos grupos de análisis no sustituyen el calendario laboral configurado.

El período inicial son los últimos siete días. **Seleccionar período histórico disponible** muestra y selecciona explícitamente el rango con atenciones autorizadas para esa cuenta; después pulsa **Calcular indicadores**. Los filtros de estación, material y turno permanecen visibles. Cero en un período vacío no significa que falte el histórico completo. Si cambias un filtro después de calcular, aparece un aviso de consulta pendiente de actualizar.

Pulsa **Calcular indicadores** para consultar. **Descargar Excel de esta consulta** exporta la misma respuesta, sus filtros, corte y tablas de detalle en dos hojas. Cambiar filtros sin recalcular no cambia el archivo de la consulta anterior.

Cero significa que no hay operaciones contables en ese alcance; N/D significa que no existe información suficiente para ese promedio. Importar catálogos o editar descripciones no genera reabastecimientos.

## Dashboard Power BI

Solo aparece un informe para una línea cuando está configurado. El informe actual corresponde a Línea 1. Power BI sigue comprobando permisos y licencias Microsoft aparte del acceso al portal. Abrir o recargar el informe no importa datos nuevos desde SQL.

Esta ampliación prepara fuentes SQL por línea, pero el modelo del informe publicado necesita revisión independiente para utilizarlas. Hasta esa revisión, no debe interpretarse el informe anterior como una vista actualizada de las nuevas operaciones v2.

## Calendario de referencia del piloto, 22/11/2026

Por indicación del usuario se configuró Línea 1 con una referencia **simulada**, vigente del 22/11/2026 al 31/12/2026, lunes a viernes, con los turnos del histórico 06:00–15:00 y 15:00–23:30. El 25 de diciembre se marca como descanso. No se presume que ese horario describa la operación real de Motherson ni se inventan descansos internos. Administración debe validarlos y renovarlos antes de la fecha final.

La referencia de días obligatorios es el [artículo 74 de la LFT, texto oficial](https://www.diputados.gob.mx/LeyesBiblio/pdf/LFT.pdf). El horario de turnos es un supuesto del piloto tomado del histórico, no una prescripción de esa ley ni un calendario corporativo de Puebla. Jornadas electorales o paros internos requieren las excepciones autorizadas que correspondan.

En Configurar línea se puede ver la semana de referencia, añadir descansos dentro de cada turno y conservar la marca **Calendario de referencia simulado**. Los descansos no cuentan como minutos laborables y pueden cruzar medianoche dentro de un turno nocturno. El último turno nocturno puede terminar al día siguiente del último día de inicio vigente.

En Inventario, abre **Ver cálculo de esta ubicación** para consultar saldo, piezas consumidas, número de observaciones, tasa, último consumo, corte e intervalos laborables. Fuera del turno se utiliza la ventana laborable de referencia y se muestra su fecha: los minutos estimados no son una cuenta regresiva de reloj hasta un paro. Sin una ventana completa desde la apertura o sin dos consumos válidos, se conserva **Sin estimación**.
