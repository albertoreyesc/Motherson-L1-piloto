# Entradas, salidas y cobertura · Motherson L1

Implementación del 05/11/2026 sobre la carpeta existente `Motherson-L1-5089-Antigravity`. Portal: **http://127.0.0.1:5089/**. Base efectiva: **Motherson_L1_MVP_Demo**. Todos los resultados se identifican como **SIMULADO · L1**.

## Diagnóstico y alcance

El modelo vigente tiene ocho tablas y ocho claves foráneas activas en `dbo`. `REGISTRO` conserva puesto, ubicación, turno de inicio e inicio/fin de una atención. No dispone de los hechos necesarios para reconstruir cantidades nuevas de material. El tiempo de atención no permite deducir una entrada, una salida ni piezas consumidas.

SQL contiene un saldo al corte. `MATERIAL.stock` es un total por material; la vista existente solo permite atribuirlo a una ubicación cuando la correspondencia es inequívoca. La continuación no reparte automáticamente un total entre varias ubicaciones. Las columnas técnicas recibidas de esa vista se usan para calcular disponibilidad; no se añaden columnas a las tablas.

La comprobación inicial encontró **68 materiales y 68 ubicaciones**, con **10,435 piezas conocidas**, 67 materiales con saldo conocido y uno sin saldo documentado. Son las filas efectivas de Demo al comenzar este trabajo, no las 63 de capturas anteriores. Ya existían referencias `LAB-…`; no se crean, corrigen ni eliminan aquí. `LAB-PENDIENTE`, ubicación 106, tiene saldo desconocido y no admite capturas cuantitativas.

El corte SQL es `2026-11-04T18:27:48.8981735Z`, mostrado en hora de México como 04/11/2026, 12:27:48. Las fuentes maestras conservan otro corte: maestro `4d577245a22d`, revisión 210, corte fuente `2026-10-27T15:30:51.3900000Z`. Esas fechas se mantienen diferenciadas en el portal.

El manifiesto privado verifica doce archivos comprimidos. El maestro conserva **709,042 eventos** y el MVP anterior **75 movimientos**. La proyección consultable contiene **230,976 movimientos** e incluye esos 75. No se suman 709,042 + 75 + 230,976: son representaciones y alcances distintos de la misma procedencia.

No se modifican datos ni estructura SQL, fuentes selladas, diagramas, capacidades ni mínimos. No se ejecuta la simulación ni se incorpora MES/SAP. Las pruebas usan un diario temporal y lecturas SQL; no insertan atenciones.

## Hechos y cálculos

| Dato | Origen o tratamiento |
| --- | --- |
| Identificador | UUID estable de la operación, conservado durante los reintentos. |
| Tipo de movimiento | Elección explícita ENTRADA o SALIDA autorizada por el servidor. |
| Cantidad capturada | Entero positivo: cajas completas en entrada, piezas en salida. |
| Material, ubicación y puesto | Catálogo SQL inicial verificado y permisos efectivos de la cuenta. |
| Piezas por caja aplicadas | Valor documental conservado al confirmar la entrada; no se atribuye el empaque actual a movimientos antiguos. |
| Cuenta y proceso | Cuenta autenticada, tipo de cuenta, puesto de acceso y proceso `PORTAL_CONTINUACION_V1`; no se confunden con responsables ficticios históricos. |
| Fecha del movimiento | Confirmación UTC del servidor, sin convertir un intervalo en una fecha inventada. |
| Consumo de producción | Declaración explícita administrativa en una salida; por defecto no está marcada. Describe una operación simulada, no una medición industrial. |
| Piezas de entrada | Cajas capturadas × piezas por caja aplicadas. |
| Piezas de salida | Cantidad capturada en piezas. |
| Saldo anterior/posterior | Reconstruidos desde el saldo inicial y los hechos anteriores; no se almacenan duplicados por operación. |
| Inicio, fin y duración | Se mantienen en el módulo de atenciones. Un movimiento cuantitativo nuevo devuelve inicio/fin nulos; no inventa un intervalo. |

La fórmula común es `saldo efectivo = saldo inicial SQL + entradas nuevas − salidas nuevas`. No se vuelven a aplicar los movimientos originales sobre un saldo que ya los incorpora. La cabecera del diario conserva el saldo inicial como ancla; no constituye un segundo inventario mutable.

## Diario duradero y límites

La ruta por defecto es `portal/.local/continuacion-movimientos/`, resuelta desde `MVP_ROOT`. Puede indicarse otra carpeta privada mediante `MVP_DIARIO_PATH`; se rechaza la carpeta pública `wwwroot`. El directorio tiene permisos 0700 y el archivo de eventos 0600 en macOS/Linux. No contiene contraseñas, hashes de acceso ni cadenas de conexión.

- `eventos.jsonl`: primera línea BASE con inventario inicial, fecha de corte, identidad de Demo, huella SHA-256 del manifiesto e inicio de observación. Las líneas siguientes son exclusivamente hechos nuevos MOVIMIENTO.
- Cada línea incluye versión de formato, huella SHA-256 de contenido y vínculo con la huella anterior. Se valida la cadena completa al abrir, la secuencia, los UUID y las reglas de reconstrucción.
- `escritor.lock`: bloqueo exclusivo mantenido durante la vida del portal. Solo se admite un proceso escritor por diario.
- Dentro de un bloqueo de operación se releen autorización y base SQL, se valida versión de disponibilidad, cantidad, saldo y capacidad, y se escribe. El servidor confirma únicamente después de `Flush(true)`.
- Un UUID con la misma cuenta y captura devuelve el comprobante original, incluso tras reiniciar. Reutilizarlo con otro contenido se rechaza con 409.
- El navegador conserva la captura pendiente y su UUID durante una respuesta perdida. No cambia la cantidad de esa operación pendiente antes de resolver el reintento.
- Si SQL cambia respecto del ancla, el reloj retrocede, el archivo cambia de tamaño externamente o hay una escritura incompleta, la captura se bloquea y explica la causa. No se descartan líneas silenciosamente.

La cadena detecta daños accidentales; no es una firma digital frente a alguien que controle el sistema de archivos. Se verifica todo el contenido al abrir; durante una sesión se controla el acceso exclusivo y el tamaño del archivo. No se anuncia soporte distribuido, varios servidores ni alta disponibilidad. Los cálculos de esta demostración se reconstruyen en memoria; el volumen de la continuación requiere reevaluación si deja de ser pequeño.

## Capacidad, permisos y pantallas

Las cajas admisibles consideran la ocupación de **todos** los materiales en la misma estantería y nivel, incluso de otros puestos: se redondean hacia arriba las cajas ocupadas por cada saldo parcial. Una capacidad, empaque u ocupación desconocida impide afirmar que hay espacio. No se muestran reservas ficticias.

Las cuentas de puesto solo ingresan cajas de sus materiales autorizados. Administración puede registrar entradas y salidas cuantitativas; conserva la prohibición de registrar atenciones por un puesto. Se valida en el backend además de la interfaz, con la autenticación, la revocación de acceso y la protección antifalsificación existentes.

Entradas y salidas recupera los bloques Ubicación → Material → Confirmar, búsqueda, posiciones disponibles primero, rojo corporativo `#DA2128`, controles táctiles y comprobante. Tras una operación, el comprobante muestra el saldo confirmado; la tarjeta siguiente puede mostrar otra previsión y bloquearla si ya no cabe otra caja. Un conflicto concurrente actualiza la disponibilidad y exige una nueva confirmación.

Resumen e Inventario usan el mismo servicio de saldo efectivo y distinguen existencias conocidas de saldos pendientes. Historial y su descarga combinan la proyección sellada con los hechos nuevos una sola vez. Los nuevos identificadores se publican como `CONTINUACION:UUID`; no se unen identificadores numéricos del histórico con ubicaciones SQL. Las atenciones no se exportan como movimientos cuantitativos.

## Alarmas informativas

Se muestran en Resumen e Inventario, sin otra pestaña y sin guardar alarmas ni generar solicitudes. Consultar, recargar o exportar no añade movimientos. La consulta histórica utiliza sus propias posiciones, configuraciones y corte, sin cambiar el inventario actual.

`tasa = piezas de consumo acreditado / minutos laborables de referencia`; `cobertura = saldo conocido / tasa en piezas por minuto`. Se usa una ventana de 60 minutos de trabajo programados, excluyendo tiempo fuera de jornada y cierres del calendario. Se excluyen aperturas, reposiciones, ajustes y salidas de procedencia no acreditada como consumo. Para el maestro se admiten los procesos documentados `SIMULADOR_ANUAL` y `SIMULADOR_CONTINUACION`; para hechos nuevos se requiere la declaración explícita de consumo.

Se conserva el criterio del motor existente de **al menos dos observaciones positivas**, además de ventana y calendario completos y observación suficiente. No se sustituye por una tasa fija de 102 segundos ni se usa la duración de una atención. Los límites se comparan antes de formatear decimales:

| Estado | Condición | Presentación |
| --- | --- | --- |
| AGOTADO | Saldo conocido = 0, aun sin tasa | Rojo, Alta / Crítica |
| RIESGO_PARO | 0 < cobertura < 15 min | Rojo, Alta / Crítica |
| CRÍTICO | 15 ≤ cobertura < 30 min | Rojo, Crítica |
| ADVERTENCIA | 30 ≤ cobertura < 60 min | Amarillo, Media |
| NORMAL | Cobertura ≥ 60 min | Verde, Normal |
| SIN ESTIMACIÓN | Saldo, calendario, ventana o evidencia insuficientes | Gris y motivo explícito |

El calendario documental termina el **24/10/2026**. Al corte actual de noviembre no existe calendario aplicable: se muestra «Sin estimación» donde el saldo no sea cero. No se prolonga el calendario ni se usa consumo antiguo como reciente. El detalle de consulta histórica permite elegir otro corte cubierto por las fuentes, con hora de México explícita.

En el corte de comprobación 19/08/2026 15:00 se reconstruyen 101 posiciones; 33 tienen una sola observación de consumo y el resto ninguna en su ventana. Por ello tampoco hay estimación suficiente en ese ejemplo. Esto no prueba ausencia de riesgo; se expone el motivo. Las pruebas sintéticas validan todos los colores y límites sin incorporarse a la demostración.

El indicador bajo mínimo se calcula por material, sumando sus saldos conocidos solo cuando corresponde y comparando el mínimo una vez. No se confunde con cobertura temporal. No hay una solicitud nueva porque una tarjeta sea roja. La cobertura es una proyección condicionada al ritmo observado y a que no haya reposición; no certifica un paro de toda la línea.

## Respaldo y recuperación

El respaldo SQL por sí solo **no** incluye las operaciones nuevas. Para una copia coherente:

1. Detén el portal en su propia Terminal y espera su cierre. No detengas ni borres Docker, bases o volúmenes.
2. Copia `continuacion-movimientos/` completo a una ubicación privada de respaldo. Conserva una huella SHA-256 de `eventos.jsonl` y la fecha de copia. No edites, recortes ni sustituyas líneas.
3. Conserva los doce archivos y el manifiesto de `historico-demo/`, y un respaldo SQL correspondiente al mismo ancla de inventario y catálogo. Las credenciales van por un canal privado separado.
4. Para recuperar, conserva primero una copia del archivo averiado. Restaura una copia íntegra y verificada del diario con sus fuentes y su base coincidente; mantén permisos privados.
5. Arranca un único portal. El backend verifica huellas, ancla, secuencia y operaciones antes de permitir capturas. Conciliar el último comprobante/UUID y el inventario efectivo antes de continuar.

No borres un diario para que el portal cree otro vacío: se perdería la continuación. Si falta una copia válida o SQL ya no coincide, conserva los archivos y resuelve la divergencia; el sistema no realiza una reparación automática. Una copia de diario anterior puede omitir operaciones confirmadas después de ella: esa pérdida debe reconciliarse, no ocultarse.

## Pruebas ejecutadas y evidencia

Compilación .NET y TypeScript/Vite correctas. Diez pruebas frontend existentes, **34 comprobaciones de diario**, **19 de cobertura** y **45 de API/navegador** correctas. Se comprueban conversión, cantidades inválidas, saldo negativo, capacidad compartida, permisos, antifalsificación, concurrencia de dos operaciones distintas y repetidas, respuesta perdida, reinicio, archivo dañado/incompleto, cero y límites exactos 15/30/60, exportación y ausencia de escrituras al consultar.

El escenario temporal pasó de 10,435 a 10,452 piezas tras cuatro operaciones nuevas. Se eliminó su diario al terminar: **no son movimientos de la demostración**. No se cambiaron la contraseña real ni los accesos SQL para ejecutar la prueba.

En `portal/evidencias/continuacion/` se conservan:

- `integridad-antes.json` e `integridad-despues.json`: conteos y SHA-256 del contenido de las ocho tablas, definición de objetos/columnas SQL y las doce fuentes comprimidas; comparación de conservación.
- `pruebas-diario.json`, `pruebas-cobertura.json`, `pruebas-api-navegador.json`: casos y resultados.
- `capturas/01-resumen.png` a `08-bloques-tablet.png`: pantallas del escenario temporal, incluyendo comprobante, entrada, salida, cobertura histórica e inventario.

Comprobación de integridad de solo lectura, desde `portal`:

```sh
python3 scripts/verificar_continuacion.py --salida evidencias/continuacion/integridad-despues.json --comparar evidencias/continuacion/integridad-antes.json
```

Prueba integrada con diario temporal, desde `portal/frontend`:

```sh
node pruebas-ui/continuacion-verificacion.mjs
```

Las pruebas de navegador usan el puerto 5091 temporal y su propia administración efímera; no requieren compartir la contraseña real.

## Arranque vigente

Desde `portal`, con Docker y el contenedor SQL existentes iniciados:

```sh
python3 scripts/local.py --iniciar --database Motherson_L1_MVP_Demo --puerto 5089
```

Deja la Terminal abierta. No ejecutes scripts de preparación o migración para abrir esta instalación. Los comandos completos y la compilación están en [ABRIR-EN-ANTIGRAVITY.md](../ABRIR-EN-ANTIGRAVITY.md).
