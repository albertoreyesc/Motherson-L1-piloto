# Recuperación documental de Materiales y catálogos

> Antecedente conservado para trazabilidad. No es la guía de instalación ni de operación vigente. Consulta el [índice actual](../README.md).

Proyecto: Motherson L1 MVP, carpeta `motherson-l1-mvp-2026-11-02`.
Informe de entrega: 3 de noviembre de 2026. Datos simulados; atributos del catálogo actual.

## Resultado en Desarrollo

| Atributo | Antes | Después | Pendiente |
|---|---:|---:|---:|
| MATERIAL.uso | 0/63 | **62/63** | 1 referencia no equivalente |
| MATERIAL.id_color → COLOR.nombre | 0/63 | 0/63 | 63 sin color físico documentado |
| PUESTO_LOGISTICO.tipo | 0/6 | 0/6 | 6 sin clasificación definida |
| Código y nombre completo de puesto en el portal | Solo código | 6/6 | Ninguno para los nombres ya aprobados |

Los 62 usos son 14 `COMPARTIDO` y 48 `EXCLUSIVO`. Se actualizaron exclusivamente valores vacíos de `dbo.MATERIAL.uso`. No se insertaron materiales, colores ni puestos. No se agregaron tablas.

En Pruebas hay 68 materiales: los mismos 63 más cinco materiales de prueba preexistentes. Se actualizaron los mismos 62; los cinco materiales `LAB-*` conservan sus atributos sin documentar. Esta diferencia no representa nuevas referencias de planta.

## Diagnóstico y clasificación de causas

### USO: existe en la fuente y no se importó — causa (a)

`PFEP LOGISTICA - UPAEP (4).xlsx`, hoja **NumerosDeParte**, contiene:

- B: número de parte completo.
- I: `AUTO`, separado de uso.
- J: `USO`, con la clasificación literal `COMPARTIDO` / `EXCLUSIVO` en las referencias recuperadas.
- AB: línea.

Para este portal, **Uso PFEP** significa esa clasificación de utilización registrada en el documento. No se convirtió en modelo de vehículo, descripción de función o nombre de aplicación. El documento no define aquí el alcance de lo «compartido»; no se amplió su significado por inferencia.

Se comprobó la omisión en el código: `prepare_sources.py` del proyecto anterior prepara el catálogo sin extraer USO; `scripts/preparar_historico.py` del MVP prepara posiciones sin ese atributo; `MvpMigration.cs` inserta MATERIAL con parte, puesto, descripción, factor y mínimo, sin `uso`. Por tanto, en este campo sí hay evidencia de una omisión de preparación/carga. No se regeneró el histórico para resolverla.

La copia `PFEP LOGISTICA - UPAEP (1).xlsx` tiene **el mismo SHA-256** que `(4)`. Los nombres distintos no son dos fuentes independientes ni dos validaciones externas. Se verificó que ambas copias conservadas coinciden.

| Parte del MVP — se conserva su espaciado original | Uso recuperado | Hoja/celda PFEP |
|---|---|---|
| `11K 947 409` | COMPARTIDO | NumerosDeParte!J297 |
| `17A 800 375 CE` | EXCLUSIVO | NumerosDeParte!J132 |
| `17A 919 225 B  RDW` | EXCLUSIVO | NumerosDeParte!J143 |
| `5G0 857 173` | COMPARTIDO | NumerosDeParte!J32 |

**Pendiente confirmado por el usuario:** el MVP contiene `57N 858 065 A MEC`; el PFEP contiene `57N 858 065 MEC`, sin A. No se trataron como equivalentes. `uso` permanece NULL y el portal muestra **Pendiente de validación** hasta validarlo con Motherson. Tampoco se cambió el número de parte.

### COLOR: falta documentación explícita/equivalencia confirmada — causa (c)

COLOR existe con PK y relación opcional desde MATERIAL; antes de la corrección estaba vacío y todos los `id_color` eran NULL. La API hace el LEFT JOIN correcto. No había colores guardados que la consulta estuviera omitiendo.

En los Excel PFEP y UPAEP revisados no se identificó una columna explícita de color físico o una equivalencia confirmada de sufijos. El `color.csv` de la simulación anterior tiene 13 etiquetas, pero su origen es la función **color_de_parte** de `cargar_datos.py`: toma el último token de tres caracteres de la parte y usa «SIN COLOR» en los demás casos. Esto es una deducción del exportador, no evidencia suficiente para asignar un color físico.

No se cargaron `041`, `RDW`, `HUZ`, `82V` ni «SIN COLOR» como colores confirmados. «SIN COLOR» tampoco se convirtió en «No aplica». Los 63 materiales se muestran como **No documentado** al hacer visible la columna. Se conserva COLOR y su FK para futuros datos confirmados. Los colores de severidad de alarmas no participan en esta relación.

### TIPO del puesto: clasificación no definida — causa (d), sin fuente por puesto (c)

`PUESTO_LOGISTICO.tipo` existe, es opcional y estaba NULL en los seis puestos. La consulta de la API sí lo devuelve. La columna AA del PFEP **TIPO DE SURTIMIENTO** describe el manejo/surtimiento del material —por ejemplo, PATIN— y no define una clasificación del puesto. La hoja Puestos tampoco establece la taxonomía pedida para estos seis códigos.

No se inventaron categorías ni se repitió el nombre dentro de `tipo`. Se conserva la estructura y se oculta por defecto esa columna totalmente vacía; el control **Mostrar columnas sin datos** permite verla con «No documentado».

Los nombres completos proceden del catálogo aprobado de seis puestos conservado en `logistic_catalog.py`, definición `POSTS`. Se presentan como información documental del catálogo, separada del código guardado actualmente en `PUESTO_LOGISTICO.nombre`:

| Código | Nombre completo |
|---|---|
| DESENGANCHE L1 | Liberador de plataformas |
| K.E. | Kitting Eléctrico |
| MONTACARGAS | Montacargas de plataformas |
| SG | Secuenciado de guanteras |
| SZ01 | Surtidor Zona 01 |
| SZ02 | Surtidor Zona 02 |

No se renombró la columna SQL ni se cambiaron los identificadores de puestos o las relaciones históricas para mostrar sus nombres.

## Fuentes y alcance de la revisión

Las fuentes originales conservadas están en:

`/Users/nihhil/Documents/Codex/2026-10-20/referenced-chatgpt-conversation-this-is-an/outputs/motherson-l1-claro-2026-10-28/portal/sources/`

| Fuente consultada | Utilidad y decisión |
|---|---|
| `reviewed-originals/PFEP LOGISTICA - UPAEP (4).xlsx` | Fuente primaria de USO; B/J/AB y hoja NumerosDeParte. |
| `originals/PFEP LOGISTICA - UPAEP (1).xlsx` | Copia binaria idéntica; comprobación de conservación. |
| `reviewed-originals/UPAEP Update 08-10-2026 (5).xlsx` | Hojas Partes/Consumo; contraste de catálogo y referencias; sin color/uso confirmados adicionales. |
| `originals/UPAEP Update 08-10-2026 (3) - export.xlsx` | Exportación anterior identificada como tal en manifest; no reemplaza el PFEP para USO. |
| `Downloads/simulacion_planta/salida/UPAEP_Update_08-10-2026_con_tiempos.xlsx` | Libro derivado con timestamps simulados; no se usa para inventar atributos maestros. |
| `Downloads/simulacion_planta/salida/modelo/material.csv` | 174 materiales del modelo anterior, con uso derivado del PFEP. No se importan sus saldos, factores o mínimos. |
| `.../modelo/color.csv` y `cargar_datos.py` | 13 etiquetas generadas por sufijo; no se cargan como colores físicos confirmados. |
| `.../modelo/puesto_logistico.csv` | Cuatro puestos de un modelo anterior; no define tipo para los seis puestos actuales. |
| `.../modelo/registro.csv`, `.../bitacora_movimientos.csv` | 272 filas cada uno, derivados de simulación; no son fuente para color/uso/tipo. No se recargaron movimientos. |
| `simulation/fixtures/dataset.json` del proyecto anterior | Se revisó su estructura, sin presentarlo como catálogo original de planta. |
| Exportaciones `json/posiciones`, `configuraciones_posicion`, `responsables`, `turnos` | Revisión de atributos del histórico preservado. No se enriquecieron ni sobrescribieron. |
| `sources/manifest.json`, diccionario de datos y documentación relacional | Identificación de procedencia, nulabilidad y relaciones. |
| `prepare_sources.py`, `preparar_historico.py`, `MvpMigration.cs`, `MvpStore.cs`, `db/002_vistas.sql` | Seguimiento de la cadena de carga y lectura; la API/vista sí seleccionan uso y color. |

Las rutas exactas, cabeceras, ejemplos y SHA-256 quedan en `portal/evidencias/catalogo/fuentes-complementarias.json` y `diagnostico-catalogo.json`. No se modificó ningún Excel, CSV o JSON histórico de origen.

## Método de conciliación

1. Se tomó una instantánea de los tres catálogos y de los datos protegidos antes de escribir.
2. Se comparó por número de parte completo, normalizando solo espacios/guiones para el cruce. El identificador destino se conserva exactamente, incluidos sufijos y espacios.
3. Se rechazaron colisiones normalizadas en destino, referencias múltiples en origen, línea distinta, valores de uso no previstos o discrepancias entre las copias. No se eligió la primera fila.
4. Se detectaron cinco claves repetidas en PFEP, registradas con sus filas en el diagnóstico. Ninguna se utilizó para los 62 cruces inequívocos.
5. Se preparó un plan con parte, uso, archivo, hoja, fila/celda y SHA-256. Hay 132 filas de conciliación: 63 usos, 63 colores y seis tipos.
6. La aplicación verifica hashes de fuentes, valores anteriores y bases permitidas. Actualiza únicamente `MATERIAL.uso`, dentro de una transacción con el bloqueo de inventario ya existente. Si otro proceso cambia ese valor, la operación se cancela.
7. Un valor previo válido distinto al propuesto bloquea la carga; no se sobrescribe. Una segunda ejecución reconoce los valores ya aplicados y produce cero actualizaciones.

El archivo `portal/config/catalogo-documentado.json` contiene referencias documentales, estados de validación y nombres aprobados; no contiene existencias ni movimientos y no es un inventario alternativo. La API continúa leyendo los valores de MATERIAL, COLOR y PUESTO_LOGISTICO desde SQL. La documentación no sustituye esos valores.

## Cambios en el portal

- Catálogos e Inventario muestran el mismo Uso PFEP y los mismos nombres completos de puesto.
- Los valores recuperados tienen un desplegable **Fuente del dato** con archivo, hoja y celda.
- **No documentado** se usa cuando se comprobó la ausencia documental. **Pendiente de validación** se usa en la referencia ambigua y en datos sin evaluación documental, como las referencias LAB de Pruebas.
- El resumen indica uso, color y tipo completos/pendientes.
- Color y Tipo se ocultan por defecto si toda la columna está vacía; se pueden mostrar. SQL y sus relaciones se conservan.
- La documentación de atributos se identifica como del catálogo actual. Historial no incorpora estos atributos nuevos a eventos antiguos: la rama HISTORICO_21 de la vista sigue leyendo `archivo.EVENTO` sin unir MATERIAL actual.
- Consultar o actualizar las pantallas no genera movimientos, ajustes o alarmas persistidas.

## Aplicación, respaldo y reversión

Se usaron los mecanismos de instantáneas/evidencias del MVP. No se creó una tabla de auditoría adicional ni un movimiento ficticio para un cambio de metadatos.

Secuencia realizada:

1. Pruebas: 62 usos aplicados.
2. Pruebas: reversión selectiva de los 62; catálogo exactamente igual al inicial.
3. Pruebas: reaplicación de 62; segunda ejecución, cero cambios.
4. Navegador y API de Pruebas: procedencia, pendientes, nombres, consistencia e integridad visual aprobados.
5. Desarrollo: respaldo previo y 62 usos aplicados; segunda ejecución, cero cambios.
6. API/navegador de Desarrollo y auditoría SQL final aprobados.

Los respaldos lógicos completos de catálogos están en `portal/.local/respaldos-catalogo/`, con permiso 0600. El respaldo previo de Desarrollo es:

`Motherson_L1_MVP_Desarrollo-20261103T031614550338Z.json`

No contiene cuentas ni contraseñas. El script de reversión solo restaura los USO del plan, no el resto de esa instantánea. Rechaza un respaldo de otra base/plan o valores modificados posteriormente. No usa RESTORE DATABASE.

### Repetir la comprobación o aplicar el plan en una copia preparada

Desde `portal` del MVP, con Docker/SQL en funcionamiento:

```bash
python3 scripts/actualizar_catalogo.py \
  --base Motherson_L1_MVP_Pruebas \
  --plan evidencias/catalogo/plan-catalogo.json \
  --salida evidencias/catalogo/nueva-comprobacion-pruebas.json
```

El plan verifica los archivos PFEP por su ruta y hash; si se movieron, se debe regenerar la conciliación con `scripts/preparar_catalogo.py`, revisando las nuevas rutas. Este preparador requiere openpyxl y no cambia los libros. El aplicador usa Python estándar y el contenedor SQL existente; las credenciales se obtienen localmente sin mostrarlas.

### Reversión selectiva de la actualización de Desarrollo, si se solicita

```bash
python3 scripts/actualizar_catalogo.py \
  --base Motherson_L1_MVP_Desarrollo \
  --plan evidencias/catalogo/plan-catalogo.json \
  --revertir .local/respaldos-catalogo/Motherson_L1_MVP_Desarrollo-20261103T031614550338Z.json \
  --salida evidencias/catalogo/reversion-desarrollo.json
```

La reversión fue probada en Pruebas; no se ejecutó en Desarrollo porque conservar los 62 usos es el resultado solicitado.

## Validación y evidencias

| Comprobación | Resultado |
|---|---|
| Usos aplicados frente a PFEP y plan | 62 coincidencias; 14 COMPARTIDO, 48 EXCLUSIVO |
| Materiales o colores duplicados | 0; no hubo inserciones |
| Claves foráneas | Nueve FK habilitadas y confiables en cada base |
| DBCC CHECKCONSTRAINTS | Sin infracciones en ambas bases |
| Tablas operativas | Nueve antes y después |
| Movimientos MVP | Desarrollo 64; Pruebas 75; sin cambios |
| Eventos históricos originales | 709.042 por base; sin cambios |
| Saldos/versiones de ubicación, mínimos, capacidades y otros atributos | Sin cambios |
| Esquema, relaciones, módulos SQL y fuentes históricas archivadas | Huellas anteriores y posteriores coinciden |
| Segunda ejecución | Cero actualizaciones en cada base |
| Reversión selectiva | Probada en Pruebas; igualdad con instantánea inicial |
| Navegador | Ocho pruebas aprobadas; una prueba de movimientos omitida deliberadamente para no escribir movimientos |
| Consistencia API/Catálogos/Inventario | Misma parte completa, uso y color; procedencia visible |

Archivos para revisión:

- `portal/evidencias/catalogo/conciliacion-catalogo.csv` y `.json`: todos los campos solicitados, valor anterior/propuesto, fuente y resultado.
- `portal/evidencias/catalogo/diagnostico-catalogo.json`: causas, conteos y cruces rechazados.
- `portal/evidencias/catalogo/validacion-final.json`: invariantes SQL y conteos.
- `portal/evidencias/catalogo/aplicacion-*.json`, `reversion-pruebas.json`, `reaplicacion-pruebas.json`, `idempotencia-*.json`: resultado de cada paso y respaldo asociado.
- `portal/evidencias/catalogo-antes.json` y `catalogo-despues.json`: instantáneas de verificación sin cuentas/contraseñas.
- `portal/evidencias/pruebas-ui.json`: resultado del recorrido de navegador.
- `portal/evidencias/capturas-catalogo/desarrollo/`: catálogo, campos vacíos visibles, puestos completos, Inventario y vista móvil.

## Pendientes para validar con Motherson

1. Confirmar o rechazar la equivalencia de `57N 858 065 A MEC` frente a `57N 858 065 MEC`. Por indicación del usuario se mantiene pendiente.
2. Aportar color físico explícito o una tabla de equivalencia oficial por referencia/sufijo, con su procedencia y vigencia.
3. Definir si «tipo de puesto» es una clasificación útil y cuáles son sus valores autorizados. Mientras tanto, la columna permanece fuera de la vista principal y disponible mediante el control de columnas vacías.

Las nuevas confirmaciones podrán enriquecer el catálogo actual con su evidencia. No se presentarán como atributos que ya estaban documentados en la fecha de un evento original.
