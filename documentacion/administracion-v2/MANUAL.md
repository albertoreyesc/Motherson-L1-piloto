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

Los movimientos se registran en cajas. El servidor convierte según las piezas por caja y verifica saldo, capacidad compartida de la estantería, estado del material y permisos. Al retirar la última caja incompleta solo salen las piezas existentes. Marca **Consumo** únicamente cuando la salida represente consumo para calcular cobertura.

Una atención logística tiene otro propósito:

1. Selecciona la ubicación y pulsa **Iniciar atención** cuando realmente comience el surtido.
2. Registra el movimiento correspondiente cuando ocurra. Iniciar la atención no cambia las existencias.
3. Pulsa **Finalizar** al concluir la atención. El servidor registra las horas; no se pide inventar ni retroceder tiempos.

No puede haber dos atenciones abiertas en una misma ubicación. El operador finaliza sus propias atenciones del puesto autorizado; administración puede cerrar una atención de esa línea. Los dos eventos quedan persistidos y auditados.

## KPIs y descarga

**Hoja 1 · Estado actual** es el inventario al corte. **Hoja 2 · Actividad logística** cuenta atenciones, no hojas del archivo de importación.

- Completados: fin dentro del periodo y hasta el corte.
- En curso: iniciados y todavía abiertos al corte; pueden haber comenzado antes del periodo.
- Repetidos: atenciones adicionales a la primera del mismo material, estación y día de inicio.
- Incompletos: registros sin responsable o referencia válida dentro del alcance; los nuevos formularios exigen esos datos.
- Intervalo promedio: horas entre inicios consecutivos comparables del mismo material y estación en el periodo. Sin dos inicios comparables muestra **N/D**.

El periodo afecta la actividad; el saldo se calcula al corte. Estación y material acotan ambos resultados. Turno filtra la actividad por hora de inicio: grupo 1 de 06:00 a 14:59; grupo 2 de 15:00 a 05:59, en Ciudad de México. Estos grupos de análisis no sustituyen el calendario laboral configurado.

Pulsa **Calcular indicadores** para consultar. **Descargar Excel de esta consulta** exporta la misma respuesta, sus filtros, corte y tablas de detalle en dos hojas. Cambiar filtros sin recalcular no cambia el archivo de la consulta anterior.

Cero significa que no hay operaciones contables en ese alcance; N/D significa que no existe información suficiente para ese promedio. Importar catálogos o editar descripciones no genera reabastecimientos.

## Dashboard Power BI

Solo aparece un informe para una línea cuando está configurado. El informe actual corresponde a Línea 1. Power BI sigue comprobando permisos y licencias Microsoft aparte del acceso al portal. Abrir o recargar el informe no importa datos nuevos desde SQL.

Esta ampliación prepara fuentes SQL por línea, pero el modelo del informe publicado necesita revisión independiente para utilizarlas. Hasta esa revisión, no debe interpretarse el informe anterior como una vista actualizada de las nuevas operaciones v2.
