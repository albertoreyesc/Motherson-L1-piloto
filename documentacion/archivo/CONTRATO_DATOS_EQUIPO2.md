# Contrato de datos — Equipo 2 / Motherson L1

> Antecedente conservado para trazabilidad. No es la guía de instalación ni de operación vigente. Consulta el [índice actual](../README.md).

Versión 2 · 12/11/2026 · Sustituye a la versión 1 (02/11/2026), que queda en el historial de Git.

## 1. Qué cambió respecto a la versión 1

La versión 1 describía un acceso de solo lectura a las vistas `reporting` de la base local `Motherson_L1_MVP_Desarrollo` y la recuperación de los archivos originales `.jsonl.gz`. Ese ya no es el camino de intercambio con el Equipo 2.

Desde el 10/11/2026 hay una sola fuente compartida: la base de Azure del Equipo 2, con las 8 tablas del ERD. El portal escribe en ella y el dashboard de Power BI lee de ella. Los datos iniciales de esa base son 8 archivos CSV, uno por tabla.

| | Versión 1 | Versión 2 |
| --- | --- | --- |
| Fuente compartida | Base local, vistas `reporting` | Base de Azure del Equipo 2, tablas `dbo` |
| Datos de carga | 10 archivos `.jsonl.gz` del modelo anterior | 8 CSV con la forma del ERD |
| Hora en `REGISTRO` | UTC | Hora de México |
| Cuentas | Tabla `USUARIOS` | `usuario` y `password` en `PUESTO_LOGISTICO` |

## 2. Fuente de verdad

- **Base compartida:** servidor `mothersonbdd.database.windows.net`, base `db`. La administra el Equipo 2.
- **Estructura:** `01_crear_tablas.sql` (carpeta `1. BDD` del Equipo 2). Los tipos y restricciones de cada columna se leen de ese archivo.
- **Datos iniciales:** carpeta `2. Datos` del Equipo 2, con `datos/*.csv`, `cargar_azure.py` y `requirements.txt`.

Las contraseñas no forman parte de este contrato ni del repositorio. La conexión del portal vive en `portal/.local/conexiones-compartida.json`, que no se sube a Git.

## 3. Los 8 CSV

Codificación UTF-8 con BOM, separados por comas, con encabezado. Un valor vacío significa NULL. Las columnas van en el mismo orden que en `01_crear_tablas.sql`.

| Archivo | Filas | Columnas |
| --- | ---: | --- |
| `ESTACION.csv` | 27 | id_estacion, id_linea, nombre_estacion |
| `COLOR.csv` | 15 | id_color, nombre |
| `PUESTO_LOGISTICO.csv` | 6 | id_puesto, Nombre, Tipo, usuario, password |
| `TURNO.csv` | 2 | id_turno, nombre_turno, hora_inicio, hora_fin |
| `ESTANTERIA.csv` | 85 | id_estanteria, id_estacion, estanteria, nivel, cajas_estanteria |
| `MATERIAL.csv` | 85 | numero_parte, id_puesto, id_color, descripcion, uso, cantidad_por_caja, stock, minimo |
| `UBICACION.csv` | 85 | id_ubicacion, numero_parte, id_estanteria, cantidad |
| `REGISTRO.csv` | 58,422 | id_registro, id_puesto, id_ubicacion, id_turno, fecha_hora_inicio, fecha_hora_fin |

Valores de referencia del paquete (datos simulados al 23/10/2026): 85 ubicaciones, 10,474 piezas en total, 58,422 registros entre el 27/10/2025 y el 23/10/2026.

## 4. Significado y reglas

- **`REGISTRO`** es una atención: un puesto atendió una ubicación, con hora de inicio y de fin. No guarda cantidades ni tipo de movimiento.
- **Hora.** `fecha_hora_inicio` y `fecha_hora_fin` están en hora de México, con formato `AAAA-MM-DD HH:MM:SS`. No convertir desde UTC.
- **Existencias.** `UBICACION.cantidad` es la existencia en piezas de esa ubicación. `MATERIAL.stock` es la suma de las cantidades del material en todas sus ubicaciones; no es un dato independiente.
- **Capacidad.** `ESTANTERIA.cajas_estanteria` es capacidad en cajas, no inventario. `MATERIAL.cantidad_por_caja` convierte cajas a piezas.
- **Mínimo.** `MATERIAL.minimo` está en piezas.
- **Color.** `COLOR.nombre` guarda el código (por ejemplo `82V`). Un material sin color lleva `id_color` vacío; 57 de los 85 no tienen.
- **Turnos.** `id_turno` 2 = PFEP T1 (06:00–15:00) y 3 = PFEP T2 (15:00–23:30).
- **Accesos.** `PUESTO_LOGISTICO.usuario` y `password` van vacíos en el CSV. Se dan de alta desde el portal y `password` guarda un hash, nunca la contraseña.
- **Número de parte.** Es texto: conservar espacios y sufijos tal como vienen.

## 5. Qué escribe el portal en la base compartida

| Captura en el portal | Efecto en Azure | Mediante |
| --- | --- | --- |
| Inicio de una atención | Fila nueva en `REGISTRO` | `dbo.IniciarAtencion` |
| Fin de una atención | `REGISTRO.fecha_hora_fin` | `dbo.FinalizarAtencion` |
| Entrada o salida de material | `UBICACION.cantidad` y `MATERIAL.stock` | `dbo.RegistrarMovimiento` (del Equipo 2) |
| Alta de acceso de un puesto | `PUESTO_LOGISTICO.usuario` y `password` | `dbo.GuardarAccesoPuesto` |

El portal no crea ni modifica tablas. Sus objetos en Azure están en `portal/db/011_azure_objetos_portal.sql` y `portal/db/012_azure_inventario_por_ubicacion.sql`: las vistas `vw_kpi_atenciones`, `vw_kpi_inventario_actual` y `vw_modelo_puestos`, la función `TurnoDeAtencion` y los procedimientos de la tabla anterior.

`vw_kpi_atenciones` entrega las fechas en UTC para el portal y, además, `fecha_hora_inicio_local` y `fecha_hora_fin_local` en hora de México. Power BI puede leer `REGISTRO` directo o esas dos columnas.

El detalle de cada entrada o salida (quién, cuándo, cajas, saldo anterior y posterior) no está en Azure, porque el modelo de 8 tablas no tiene dónde guardarlo. Queda en el diario local del portal.

## 6. Reglas de convivencia

- **Avisar antes de recargar.** `cargar_azure.py --reiniciar` borra las 8 tablas y las vuelve a cargar desde los CSV. Conserva los accesos de puesto, pero se pierden las atenciones capturadas desde el portal y las existencias regresan a las del CSV.
- **No cambiar existencias ni catálogo directo en Azure.** El portal comprueba que el inventario de Azure sea igual a su saldo inicial más los movimientos que registró. Si no coincide, bloquea las capturas. Para reanudar: detener el portal, renombrar `portal/.local/continuacion-movimientos-compartida/` y arrancar de nuevo.
- **Cuota gratuita.** El portal se conecta a Azure solo para pruebas integradas y demostraciones, y se cierra al terminar.
- **Cambios al modelo.** Agregar, quitar o renombrar columnas en las 8 tablas se avisa antes al otro equipo, porque los objetos del portal dependen de ellas.

## 7. Archivos que no forman parte del contrato

Los `.jsonl.gz` del modelo anterior (`corte_demo/inventario` e `historico/…`) no se entregan al Equipo 2 ni se cargan en Azure. Se conservan únicamente porque el portal los lee en su modo local de demostración (`HistoricoDemo.cs`, `HistoricoCobertura.cs`, `HistoricoKpis.cs`). Las vistas `reporting` de `portal/db/002_vistas.sql` pertenecen a ese mismo modo local.

## 8. Pendientes

- `REGISTRO.csv` no trae los `id_registro` 58385, 58387 y 58417 (los identificadores llegan a 58,425 con 58,422 filas). Confirmar con el Equipo 2 si se retiraron a propósito.
- 8 materiales no tienen `descripcion` ni `uso`: `17A 880 204 Q`, `17A 880 204 R`, `1EE 953 507 DK ASS`, `1EE 953 507 DP  ASS`, `571 035 876 K`, `571 035 877 B`, `572 035 876 H` y `57N 858 065 A MEC`.
- `PUESTO_LOGISTICO.Tipo` está vacío en los 6 puestos, sin clasificación confirmada.
- Confirmar que el script 012 quedó aplicado en Azure.

Todos los datos de este paquete son simulados. No acreditan conteos físicos ni tiempos reales de planta.
