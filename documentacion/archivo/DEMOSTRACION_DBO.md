# Motherson L1 · Base de demostración

> Antecedente conservado para trazabilidad. No es la guía de instalación ni de operación vigente. Consulta el [índice actual](../README.md).

`Motherson_L1_MVP_Demo` contiene las ocho tablas de usuario del modelo final en `dbo`. El [diagrama editable](../diagramas/mvp-puestos-fisico.erd), el [SVG](../diagramas/mvp-puestos-fisico.svg) y el [DBML](../diagramas/mvp-puestos-fisico.dbml) representan esas mismas relaciones. El diagrama automático de SQL Server se abre sobre **Demo**. En Pruebas seguirá incluyendo el archivo y las tablas de reversión.

| Información | Dónde se consulta |
| --- | --- |
| Estaciones, ubicaciones, variantes, materiales, puestos y turnos | Tablas de `dbo`. |
| Atenciones nuevas | `dbo.REGISTRO` y `dbo.vw_kpi_atenciones`. Cada atención conserva el turno de inicio. |
| Existencias al corte SQL | `dbo.vw_kpi_inventario_actual`. La fecha del saldo se conserva en `dbo.vw_modelo_puestos`. |
| Existencias efectivas del portal | Corte SQL más operaciones nuevas de `portal/.local/continuacion-movimientos/`, reconstruidas por el backend común. La vista SQL permanece en su corte original. |
| Histórico maestro y movimientos del MVP previo | `portal/.local/historico-demo/`, fuera de la carpeta pública. El portal lo consulta por medio del backend y muestra los movimientos filtrados y paginados. |

La base de referencia `Motherson_L1_MVP_Pruebas` mantiene su respaldo y los esquemas de la versión anterior. En Demo, las tres vistas y los procedimientos del portal pertenecen a `dbo`. El histórico original permanece sellado; la continuación cuantitativa se documenta en [CONTINUACION_MOVIMIENTOS.md](CONTINUACION_MOVIMIENTOS.md). El cambio del 05/11/2026 no modifica estructura ni datos SQL.

| Dependencia en Pruebas | Equivalencia en Demo |
| --- | --- |
| `archivo.FUENTE`, `archivo.EVENTO`, `archivo.REGISTRO_MVP` y `archivo.CUENTA_MVP` | Fuentes selladas, movimientos originales del MVP y proyección exportada con la atribución de origen. |
| `archivo.CORTE` | Corte y huella de las fuentes en el manifiesto histórico. |
| `respaldo_puestos.CORTE` | Fecha del saldo publicada por `dbo.vw_modelo_puestos`. |
| Otras tablas de `respaldo_puestos` | Reversión desde Pruebas y su respaldo. La configuración privada de administración se lleva por separado. |
| `reporting.vw_kpi_movimientos` | `movimientos.jsonl.gz` y exportación JSONL filtrada desde el portal. |
| `reporting.vw_kpi_inventario_actual` y `reporting.vw_kpi_atenciones` | Vistas del mismo nombre en `dbo`. |

## Arranque local

Abre Docker Desktop y sitúate en la carpeta `portal` de esta entrega. La base Demo y sus conexiones ya existen en esta Mac: **no vuelvas a prepararla ni migrarla para arrancar el portal**. Si el contenedor está detenido, ejecuta `docker start motherson-mvp-sql-1`.

```sh
python3 scripts/local.py --iniciar --database Motherson_L1_MVP_Demo --puerto 5089
```

Abre [el portal de demostración](http://127.0.0.1:5089/) y deja la Terminal abierta. Las credenciales vigentes no se han cambiado. No confundas la contraseña web con una contraseña SQL.

Después de editar el código, compila desde `portal` y reinicia únicamente su instancia:

```sh
npm --prefix frontend run build
dotnet build Motherson.slnx
```

Si el puerto está ocupado, comprueba la instancia existente y usa Ctrl+C en su propia Terminal antes de reiniciar. No inicies dos escritores del diario.

La preparación original `scripts/preparar_demo.py preparar` se conserva como antecedente de creación desde Pruebas; crea objetos SQL y no forma parte del arranque ni de la implementación de continuación.

## Conexión en Antigravity

Conecta con SQL Login a `127.0.0.1`, puerto `1433`, base `Motherson_L1_MVP_Demo`. Para el diagrama físico usa la cuenta SQL local `sa`, cifrado Mandatory y la opción Trust server certificate para este contenedor local. La cuenta SQL lectora generada para Demo tiene acceso a las dos vistas de análisis operativo.

## Histórico y exportaciones

El paquete contiene las diez fuentes comprimidas originales, los movimientos anteriores del MVP y la proyección que alimenta Historial de movimientos. El manifiesto registra el primer y último evento, los conteos, la procedencia, las huellas y los cortes independientes del histórico y del inventario. Conserva la base anual del 25/10/2025 al 24/10/2026 y la continuación existente. La preparación y el backend comprueban las huellas. Tras recuperar un paquete, reinicia el portal para verificarlo y reconstruir su índice de lectura.

Desde Historial de movimientos, la administración puede descargar en JSONL todos los registros que correspondan a los filtros seleccionados. El conjunto sellado `movimientos.jsonl.gz` contiene 230,976 movimientos e incluye los 75 del MVP anterior: no se vuelven a sumar. La descarga del portal incorpora además las operaciones nuevas, una vez por UUID. Las marcas de movimiento están en UTC. `espacio_identificadores` distingue `HISTORICO_21`, `MVP` y `CONTINUACION`. APERTURA representa un saldo inicial y queda fuera de los consumos. La base Demo ofrece por SQL las vistas de inventario al corte y de atenciones; las cantidades nuevas solo aparecen mediante el backend y su exportación.

## Traslado y restauración

Desde `portal`, exporta el respaldo SQL y su manifiesto.

```sh
python3 scripts/restaurar_puestos.py exportar --demo --bak .local/traslado/Demo.bak --contenedor motherson-mvp-sql-1
```

Detén antes el portal para copiar de forma coherente el diario completo y las fuentes. En la otra instalación coloca `historico-demo` y `continuacion-movimientos` completos en `portal/.local/` y conserva juntos el respaldo SQL, su manifiesto y esa copia. **El respaldo SQL no contiene las operaciones cuantitativas nuevas.** Transfiere por un canal privado la configuración `admin-puestos-Motherson_L1_MVP_Demo.json` y la contraseña web por separado. El respaldo SQL incluye los hashes de las cuentas por puesto. Indica una ruta de origen para el archivo privado de administración distinta de la que ocupará en la carpeta de destino. Si el nuevo inventario SQL no coincide exactamente con el ancla del diario, el portal bloquea la continuación: no borres el diario para forzar el arranque.

```sh
python3 scripts/restaurar_puestos.py comprobar --demo --bak RUTA/Demo.bak --contenedor CONTENEDOR
python3 scripts/restaurar_puestos.py restaurar --demo --bak RUTA/Demo.bak --admin RUTA/administracion.json --contenedor CONTENEDOR
```

La restauración exige que el nombre de Demo esté libre en la instancia de destino. El script comprueba el respaldo y el manifiesto histórico antes de crear conexiones locales. Después compila y arranca el portal con los comandos de arriba, omitiendo la preparación desde Pruebas.

## Verificación

```sh
python3 scripts/preparar_demo.py verificar
```

La prueba anterior `portal/scripts/test_demo.py` documenta la creación inicial y **escribe una atención temporal en SQL**; no forma parte de la comprobación de continuación. Para verificar esta entrega sin escrituras SQL se utilizaron `scripts/verificar_continuacion.py`, `pruebas-continuacion`, `pruebas-cobertura` y `frontend/pruebas-ui/continuacion-verificacion.mjs`, con un diario temporal eliminado al terminar. Evidencia actual en `portal/evidencias/continuacion/`.

La instalación desde una base vacía y su repetición conservaron el modelo y los saldos. También se restauró el respaldo en una base temporal de la instancia local y se comprobó con `DBCC CHECKDB`. En el portal, la primera consulta con el índice aún sin cargar tardó 1.24 segundos en esta Mac. La descarga completa devolvió 230,976 movimientos. Una copia ausente o con huella incorrecta produjo un mensaje explícito en el catálogo, el historial y la exportación.
