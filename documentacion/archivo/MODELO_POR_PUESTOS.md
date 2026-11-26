# Motherson L1 · Modelo por puesto

> Antecedente conservado para trazabilidad. No es la guía de instalación ni de operación vigente. Consulta el [índice actual](../README.md).

Validado el 4 de noviembre de 2026 en `Motherson_L1_MVP_Pruebas`, con portal local en el puerto 5089. Desarrollo, en 5087, corresponde a la versión anterior. El alcance de datos es SIMULADO · HISTÓRICO de Línea 1.

## Cambios del modelo

| Elemento | Modelo anterior | Modelo por puesto |
| --- | --- | --- |
| Identidad operativa | Cuentas individuales en `dbo.USUARIOS` | Una cuenta compartida por puesto en `PUESTO_LOGISTICO`. El código, el nombre y el identificador de cada puesto se conservan. |
| Captura | Entradas y salidas con cantidades y saldos | `REGISTRO` guarda identificador, puesto, ubicación, inicio UTC, fin UTC opcional y turno de inicio. |
| Turno | FK del movimiento a `TURNO` | `REGISTRO.id_turno` referencia a `TURNO` y guarda el turno de inicio en Ciudad de México. T1 abarca [06:00, 15:00) y T2 [15:00, 23:30). Fuera de esos rangos, el campo queda vacío. |
| Inventario | Saldo y versión por ubicación | `MATERIAL.stock` guarda piezas conocidas al corte. Se conservó cada saldo, incluidos los desconocidos. `UBICACION` conserva su identificador y sus dos FK. |
| Variante | Sin catálogo de códigos | `COLOR.nombre` contiene el código documental. La FK `MATERIAL.id_color` es opcional. |
| Administración | Cuenta de `USUARIOS` | `admin.local` independiente, con configuración privada del servidor. Consulta todas las atenciones y administra los accesos. |

El esquema operativo tiene **8 tablas y 8 claves foráneas**. Los inicios y cierres usan `datetime2(7)` con instantes del servidor. Un cierre anterior al inicio es inválido. El operador abre el intervalo al empezar la atención y lo cierra al terminarla. La duración mide ese intervalo. Un turno agrupa muchos registros. Cada registro pertenece a un turno como máximo y conserva esa asignación si termina en el turno siguiente. En Atenciones se puede filtrar por PFEP T1, PFEP T2 o Fuera de turno.

Las cuentas de puesto pueden consultar su catálogo e inventario y registrar atenciones únicamente en ubicaciones del puesto. Cada cuenta compartida identifica al puesto. La consulta de historial cuantitativo queda disponible para administración. El hash de cada cuenta de puesto se guarda en `password`. Revocar un acceso limpia usuario y hash sin borrar el puesto. La rotación y la revocación invalidan las sesiones anteriores.

Un índice único filtrado evita dos intervalos abiertos para un mismo puesto y ubicación. La transacción de inicio reutiliza el intervalo que sigue abierto. El cierre repetido devuelve el mismo fin. Si ya se cerró un intervalo y vuelve a llegar una orden de inicio, se trata como un nuevo inicio. El registro carece de una clave de solicitud separada que permita distinguir ese reintento.

## Conservación e indicadores

En Pruebas se conciliaron 68 materiales y 68 ubicaciones. El saldo de cada ubicación coincidió con el saldo trasladado al material correspondiente. La migración aborta si encuentra más de una ubicación para un material. Desarrollo se auditó por separado con 63 materiales y 63 ubicaciones.

El maestro mantiene los **709 042 eventos** y la tabla de movimientos del MVP anterior mantiene sus **75 filas de Pruebas**. Los movimientos de Desarrollo eran 64 al corte de la auditoría. Los registros anteriores mantienen los identificadores, tiempos, cantidades, saldos y cuentas de origen. `archivo.REGISTRO_MVP` conserva las filas previas. `archivo.CUENTA_MVP` contiene la atribución de las cuentas originales. El esquema restringido `respaldo_puestos` retiene la estructura previa para la reversión selectiva. Los consumidores de datos consultan las vistas de `reporting`.

| Vista | Alcance |
| --- | --- |
| `reporting.vw_kpi_atenciones` | Nuevos intervalos por puesto y ubicación, duración en milisegundos para los cerrados y turno al inicio. |
| `reporting.vw_kpi_movimientos` | Movimientos compatibles del maestro y los movimientos archivados del MVP previo. Conserva el origen y el espacio de identificadores. |
| `reporting.vw_kpi_inventario_actual` | Saldo en piezas al corte de la migración, variante, empaque, mínimo y capacidad. |
| `reporting.vw_kpi_consumo_diario` | Conserva el filtro de procedencia del modelo anterior. Las atenciones quedan fuera de esa vista. |

Las nuevas atenciones carecen de cantidades, movimientos de stock y saldos antes y después. Por eso el portal presenta el inventario al corte y el mínimo por material, y señala que la cobertura de las atenciones nuevas no está disponible. Las consultas históricas siguen utilizando los datos originales.

## Fuente de las variantes

Se cruzó `UPAEP Update 08-10-2026 (5).xlsx`, hoja `Partes`, encabezado `PUESTO LOGÍSTICO + MATERIAL` en B2 y contraste con la columna C. El cruce usa el número de parte completo y admite únicamente una normalización inequívoca de espacios. El código del puesto aporta contexto al informe de conciliación.

En Pruebas se asignaron **25 referencias y 11 códigos únicos**. Las **43 referencias restantes** mantienen `id_color` sin valor. `17A 800 375 CE` conserva el código `CE`. `5G0 857 173` no tiene sufijo. `5WA 937 084 AN`, `AL`, `AM` y `5QS 907 530 H` se reconocieron en el Excel y quedan fuera del catálogo operativo. `5TB 880 841 F` quedó pendiente por discrepancia entre las columnas. El uso de `57N 858 065 A MEC` sigue pendiente de validación con Motherson. Los códigos se muestran como **Variante / revisión**.

## Ejecución y traslado a Windows

Desde `portal`, con el contenedor SQL iniciado, instala las dependencias y compila con .NET SDK 10, Node.js y Python 3.11 o posterior. Usa la contraseña de `admin.local` correspondiente a Pruebas. En la instalación original se conserva en el archivo privado de acceso inicial.

```sh
python3 scripts/migrar_puestos.py turnos
npm --prefix frontend ci
npm --prefix frontend run build
dotnet build Motherson.slnx
python3 scripts/local.py --iniciar --database Motherson_L1_MVP_Pruebas --puerto 5089
```

Para trasladar el estado completo a otra computadora, exporta el respaldo y su manifiesto. Los dos archivos deben viajar juntos. El respaldo se entrega por canal privado porque incluye información de autenticación de la base local.

```sh
python3 scripts/restaurar_puestos.py exportar --bak .local/traslado/Pruebas.bak --contenedor motherson-mvp-sql-1
```

En Windows, desde `portal`, usa un contenedor local de SQL Server en Docker Desktop con el puerto publicado. Sustituye `NOMBRE_DEL_CONTENEDOR` por su nombre real. La instancia de destino necesita tener libre el nombre de la base de Pruebas. El restaurador verifica el SHA-256 y el checksum SQL y prepara conexiones locales de aplicación y lectura. La contraseña web de administración sigue siendo la de Pruebas y debe comunicarse por separado.

```powershell
python scripts\restaurar_puestos.py comprobar --bak C:\ruta\Pruebas.bak --contenedor NOMBRE_DEL_CONTENEDOR
python scripts\restaurar_puestos.py restaurar --bak C:\ruta\Pruebas.bak --contenedor NOMBRE_DEL_CONTENEDOR
python scripts\migrar_puestos.py turnos --contenedor NOMBRE_DEL_CONTENEDOR
npm --prefix frontend ci
npm --prefix frontend run build
dotnet build Motherson.slnx
python scripts\local.py --iniciar --database Motherson_L1_MVP_Pruebas --puerto 5089
```

La migración `turnos` prepara un respaldo en la primera ejecución y asigna el turno a las atenciones existentes según su inicio. Una segunda ejecución conserva las asignaciones ya guardadas. El cruce requiere `openpyxl` (`python3 -m pip install openpyxl` en un entorno virtual). La migración selectiva se ejecuta con `python3 scripts/migrar_puestos.py auditar --excel RUTA --informe RUTA`. Usa `aplicar` después de revisar el informe o `revertir` para recuperar la estructura previa. Puedes indicar `--contenedor` y `--solo-pruebas` en una instalación aislada. El modo de escritura está limitado a Pruebas. La reversión se detiene si hay nuevas atenciones. La exportación y la comprobación del respaldo se ejecutaron en macOS con SQL Server en Docker. Falta ejecutar una restauración completa en Windows.

## Evidencia

- Diagramas [físico en SVG](../diagramas/mvp-puestos-fisico.svg), [editable en ERD](../diagramas/mvp-puestos-fisico.erd) y [DBML](../diagramas/mvp-puestos-fisico.dbml). Las [versiones anteriores](../diagramas/anteriores/README.md) están archivadas. [Diccionario de datos](../DICCIONARIO_PUESTOS.csv).
- [Cruce de variantes](../../portal/evidencias/puestos/auditoria.json), [verificación SQL](../../portal/evidencias/puestos/validacion-sql.json) y [pruebas API](../../portal/evidencias/puestos/pruebas-api.json).
- [Reaplicación](../../portal/evidencias/puestos/reaplicacion.json) y [reversión verificada](../../portal/evidencias/puestos/reversion.json).
- [Migración de turnos](../../portal/evidencias/puestos/turnos-aplicacion.json), [límites de horario](../../portal/evidencias/puestos/turnos-limites.json) y [filtros del portal](../../portal/evidencias/puestos/turnos-ui.json).
- Capturas de [catálogo](../../portal/evidencias/puestos/catalogo.png), [administración](../../portal/evidencias/puestos/administracion.png) y [atenciones](../../portal/evidencias/puestos/atenciones.png).
