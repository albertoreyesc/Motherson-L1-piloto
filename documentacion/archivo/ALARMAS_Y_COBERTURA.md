# Alarmas informativas y cobertura temporal

> Antecedente conservado para trazabilidad. No es la guía de instalación ni de operación vigente. Consulta el [índice actual](../README.md).

Implementación 02/11/2026 · política COBERTURA_60_30_15_V1. Incorporación en Resumen e Inventario, sin pestaña adicional. Los datos y resultados de esta instalación son SIMULADOS.

## Fuente única y ausencia de escrituras

El saldo procede de UBICACION.cantidad a través de la consulta existente de inventario. No se usa la capacidad como saldo ni se inventa consumo para liberar espacio. No se creó ni modificó tabla, columna, vista, procedimiento o trigger SQL. Los nueve objetos operativos y los archivos históricos permanecen en su estructura original.

Se amplió el GET existente con una opción compatible: `/api/inventario?alarmas=true`. Sin esa opción continúa devolviendo el arreglo anterior. Con ella devuelve saldo, avisos, mínimos y corte en un mismo documento. No hay otro endpoint de alarmas ni otra fuente de inventario.

La lectura usa una transacción breve y un bloqueo compartido sobre el recurso MVP_INVENTARIO que ya usa RegistrarMovimiento. El corte se toma de SYSUTCDATETIME después de adquirir el bloqueo; saldo y consumos se leen antes de liberarlo. No se crean filas de auditoría, avisos ni solicitudes. Consultas simultáneas pueden compartir el bloqueo; una captura puede esperar hasta que termine la lectura. Por ello «sin afectar la base» significa sin modificar estructura o datos, no ausencia absoluta de trabajo de lectura o de bloqueos.

La consulta mantiene el permiso view y la autenticación existentes. Los permisos de escritura y los logins SQL no cambian. No se amplió el contrato de vistas SQL del Equipo 2 ni se añadieron nuevas migraciones.

## Calendario encontrado y límite actual

La fuente documental es `portal/sources/annual/master/reporte-anual.json` de la entrega original motherson-l1-claro-2026-10-28, maestro 4d577245a22d.

SHA-256 del reporte original:
`e0f33dedbd3eeb391ce8a947beac0b7ab77ec2a2e1d9dcdfa5fa2231d2b9698d`

Se extrajeron únicamente los 365 días explícitos y sus turnos, sin copiar inventarios o resultados, a `portal/config/calendario-historico.json`. Se conservan periodo, zona, origen y huella. No se ejecuta el simulador ni se recalculan festivos mediante una norma actual. Se respetan los días cerrados que ya declara esa fuente simulada; no acreditan el calendario colectivo real de Motherson.

Vigencia: **25/10/2025–24/10/2026**. El calendario no cubre noviembre ni la apertura nueva del MVP. Por tanto, al consultar el inventario actual de esta instalación, una ubicación con saldo positivo muestra **SIN_DATOS / Sin estimación**, causa FUERA_CALENDARIO_DOCUMENTADO. No se utiliza una tasa antigua de octubre con el saldo actual. Una ubicación con saldo conocido cero sigue siendo AGOTADO; un saldo NULL sigue siendo desconocido.

Los avisos por mínimo funcionan independientemente y permiten identificar materiales bajo mínimo aunque no se estime cobertura. Para obtener minutos actuales se necesita primero evidencia de calendario vigente y consumos aplicados que cubran la ventana. Este cambio no inventa esa evidencia ni incorpora una edición administrativa para fabricarla.

No se incorporó selector de corte histórico: el contexto de esta pantalla es inventario actual. El historial original sigue consultable en su sección y en las vistas existentes. No se reconstruye un saldo pasado mediante la suma incompleta de eventos de prueba.

## Cálculo determinista

1. Identificar el día del corte en America/Mexico_City. Si queda fuera del periodo publicado, informar SIN_DATOS.
2. Recorrer hacia atrás los turnos de los días explícitamente laborables hasta reunir exactamente 60 minutos programados. Se excluyen noches, fines de semana y cierres declarados. Un día sin información impide la estimación.
3. La apertura de la ubicación debe existir antes o al inicio de toda la ventana. Si se abrió después, no hay observación completa.
4. Seleccionar solo SALIDA, cantidad positiva, naturaleza simulada, misma parte y misma ubicación del espacio MVP, dentro de los intervalos `[inicio, fin)`. Los consumos en el instante del corte quedan para la siguiente consulta.
5. Los IDs repetidos idénticos se cuentan una sola vez. Un ID contradictorio impide estimar. Entradas, aperturas, ajustes, filas de demanda sin descuento y otros espacios de identificadores no se incluyen.
6. Se requieren al menos dos observaciones de consumo aplicado. Es un criterio conservador técnico explícito, pendiente de validación operativa; una sola observación muestra MUESTRA_INSUFICIENTE. No se considera que el silencio de eventos fuera de la ventana produzca cobertura infinita.
7. Tasa pz/h = piezas aplicadas / minutos laborables observados × 60.
8. Cobertura min = saldo de la ubicación / tasa pz/h × 60.

Los intervalos se calculan con ticks y aritmética decimal para conservar los minutos. Los umbrales se comparan antes del formato visual. No se utiliza el ciclo de 102 segundos: falta evidencia de su aplicabilidad y de piezas de material por ciclo. La fórmula alternativa no está habilitada ni se usa como respaldo automático.

## Clasificación

| Estado | Condición | Presentación |
| --- | --- | --- |
| SIN_DATOS | Saldo desconocido, calendario/ventana/tasa insuficiente | Gris, «Sin estimación», causa explícita |
| AGOTADO | Saldo conocido cero; prevalece aunque falte tasa | Rojo #DA2128, prioridad alta/crítica |
| RIESGO_PARO | Cobertura >0 y <15 min | Rojo #DA2128, riesgo estimado, prioridad alta/crítica |
| CRITICO | Cobertura ≥15 y <30 min | Rojo #DA2128, prioridad crítica |
| ADVERTENCIA | Cobertura ≥30 y <60 min | Amarillo, prioridad media |
| NORMAL | Cobertura ≥60 min | Verde, prioridad normal |

14.999 minutos sigue siendo RIESGO_PARO. El redondeo de presentación no cambia la clasificación. Texto e icono acompañan todos los colores. No se afirma un paro confirmado de línea ni producción garantizada.

## Mínimo por material

MATERIAL.minimo corresponde al material completo. Se suma su cantidad por ubicaciones una sola vez, sin sumar de nuevo stock_total_material en cada fila. Si alguna ubicación tiene saldo desconocido, el total completo es desconocido.

- Total cero: agotado.
- Total positivo y mínimo conocido, total ≤ mínimo: bajo mínimo.
- Total > mínimo: sobre mínimo, sin promesa de cobertura temporal.
- Falta saldo completo o mínimo: sin datos para comparar.

Un material con varias ubicaciones puede tener una ubicación agotada y total sobre mínimo. No se confunden esos indicadores. Resumen cuenta ubicaciones para cobertura y materiales únicos para mínimos.

## Recorrido visual

Resumen muestra seis conteos de cobertura por ubicación, explicación del calendario, lista priorizada de atención y materiales bajo mínimo como indicador independiente. Los conteos abren Inventario filtrado; el enlace de material conserva el número de parte.

Inventario muestra saldo, cobertura, severidad y causa cerca del inicio de la tabla. El detalle desplegable expone corte, ventana, tasa, observaciones, piezas aplicadas y método. Los filtros permiten consultar severidad, parte, estación y nivel, y limpiar la selección.

Ambas pantallas reutilizan la misma respuesta guardada en memoria React. Cambiar de pantalla no consulta otro corte ni guarda un inventario en el navegador. Actualizar obtiene un nuevo corte y reemplaza los resultados juntos. Después de una captura válida se vuelve a leer ese mismo contexto.

No se implementaron reconocimiento, cierre, asignación, notificaciones externas, reservas ni solicitudes urgentes. Las prioridades son clasificaciones visuales y desaparecen o cambian al variar los datos de origen, sin borrar registros porque nunca se persistieron como alarmas.

## Verificación reproducible

Desde la raíz del proyecto:

```sh
dotnet run --project portal/pruebas-cobertura -- portal/config/calendario-historico.json
npm --prefix portal/frontend test
npm --prefix portal/frontend run test:ui
```

Desde portal, con 5087/5088 activos:

```sh
python3 scripts/test_alarmas.py
```

La opción explícita `--movimientos` realiza una entrada de una caja de 24 piezas y una salida de 24 en LAB-CONC-SAL, únicamente en la base de Pruebas. Son dos movimientos trazables, no un reset del inventario ni movimientos para fabricar severidades. El caso LAB-MAT-001 se conserva en 105.

El script auditar_alarmas.py es de solo lectura: compara huellas de columnas, objetos SQL, relaciones, fuentes originales e inventario y conteos de registros. No ejecuta DDL ni migraciones. Sus resultados antes y después permiten demostrar que consultar alarmas no altera datos.

Las pruebas unitarias del motor usan calendarios y consumos sintéticos identificados como pruebas; no se cargan en Desarrollo ni se presentan como mediciones de planta. Las capturas reales muestran las estimaciones no disponibles para la fecha actual.
