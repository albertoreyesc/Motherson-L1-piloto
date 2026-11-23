# Smart Material Replenishment · Motherson

Portal del piloto: **https://motherson-l1-piloto.onrender.com/**.

Ampliación de administración de cuatro líneas, 22/11/2026: [manual](documentacion/administracion-v2/MANUAL.md), [pruebas](documentacion/administracion-v2/PRUEBAS.md) y [migración y recuperación](documentacion/administracion-v2/ACTUALIZACION_Y_RECUPERACION.md). [Estado de publicación y activación](documentacion/administracion-v2/PUBLICACION_2026-11-22.md): migraciones SQL aplicadas y administración de cuatro líneas activa en Render.

Para ubicar el código, preparar el entorno y colaborar mediante ramas, consulta **[CONTRIBUTING.md](CONTRIBUTING.md)**. El repositorio del piloto es privado; cada compañero requiere acceso con su propia cuenta GitHub.

La configuración de Render está en `despliegue/render/`; las migraciones nuevas están en `portal/db/administracion/`. No ejecutar inicializadores históricos para actualizar una base con movimientos existentes.

---

## Antecedentes del proyecto

Las notas que siguen corresponden a distintas fechas, bases y entornos. Sus cantidades y estados de instalación no sustituyen la documentación de la versión vigente.

> **Piloto gratuito publicado (19/11/2026):** https://motherson-l1-piloto.onrender.com/ . Solo cuentas personales y datos simulados. Alojamiento Render Free con SQL Azure piloto separado. El informe original de Power BI ya se muestra con sesión Microsoft individual; licencias de participantes y conexión del informe a la base piloto siguen pendientes. Consulta [acceso de planta](documentacion/POWERBI_ACCESO_PLANTA.md). Guía vigente: [despliegue/render/README.md](despliegue/render/README.md); acceso de participantes: [PARTICIPANTES.md](despliegue/render/PARTICIPANTES.md). Las notas anteriores de App Service F1 describen una ruta bloqueada por cuota, no el alojamiento actual.

# Preparación del piloto de planta — 19/11/2026

Perfil gratuito aislado para PC y tabletas; aproximadamente 80 participantes previstos. **Todavía no desplegado.** Consulta [la guía vigente del piloto](despliegue/piloto/README.md), límites, evidencias, cuentas personales y pasos sujetos a aprobación. La instalación Azure actual no se modifica. Las notas siguientes conservan fases históricas.

---

Estado actual: ZIP (3) integrado, histórico completo cargado en Azure y Dashboard/KPIs actualizados. [Resultado, pruebas y bloqueo de publicación Power BI](documentacion/ACTUALIZACION_HISTORICO_Y_POWERBI_2026-11-19.md).

Estado 19/11/2026: instalación propia aplicada en Azure SQL con demostración y portal local conectado. [Resultado y pendientes](documentacion/INSTALACION_AZURE_EJECUTADA_2026-11-19.md).

# Integración con Azure propio · Motherson L1

Trabajo de integración iniciado el 18/11/2026. Destino: `motherson-l1-equipo.database.windows.net` / `Motherson_L1`.

**Instalación y carga en Azure pendientes de aprobación; probadas en SQL local.** Consulta [el estado y la guía vigente](documentacion/INTEGRACION_AZURE_PROPIA.md).
El diagnóstico y los scripts de instalación están separados de los scripts históricos de desarrollo.

---

# Motherson L1 · Demo 5089

**Versión vigente, 05/11/2026:** base `Motherson_L1_MVP_Demo`, ocho tablas y ocho claves foráneas en `dbo`. Entradas y salidas conservan sus hechos en una continuación privada JSONL y calculan los saldos en el backend, sin actualizar SQL. Las alarmas se consultan en Resumen e Inventario; `REGISTRO` conserva exclusivamente las atenciones.

**17/11/2026 · Importar Excel de materiales:** en Materiales y catálogos, la cuenta administrativa descarga una plantilla (PARTE, PUESTO LOGÍSTICO, DESCRIPCION, USO, CANTIDAD X CAJA, MINIMO (PZ), VARIANTE / COLOR) con los datos actuales, la edita y la vuelve a subir (.xlsx o .csv). También reconoce los encabezados de las hojas *Partes* (UPAEP Update) y *NumerosDeParte* (PFEP). Antes de aplicar muestra una vista previa con cada cambio (antes → después), los materiales no encontrados y las filas con error. Solo actualiza materiales que ya existen; no cambia existencias ni puestos, y las celdas vacías no borran datos. Se aplica todo o nada y queda en el diario como `AJUSTE`. En la base compartida usa `dbo.AjustarMateriales` (mismo script `013_azure_ajustes_catalogo.sql`).

**17/11/2026 · Capacidad + / −:** en Estaciones y ubicaciones e Inventario, la cuenta administrativa ajusta la capacidad en cajas de cada estantería con + / − y Guardar. Nunca puede quedar menor que las cajas ocupadas (una caja abierta cuenta como completa). Cada ajuste queda en el diario como `AJUSTE` junto a los movimientos, con su cuenta y hora, así que el portal sigue cuadrando con SQL. En la base compartida, el ajuste además se aplica con `dbo.AjustarCapacidadEstanteria`: el Equipo 2 debe correr una vez `portal/db/013_azure_ajustes_catalogo.sql` (solo crea ese procedimiento; no toca tablas ni datos). En Demo local no hace falta el script.

**17/11/2026 · Imágenes:** en Materiales y catálogos cada material puede tener la foto de su pieza (solo PNG o JPG, hasta 5 MB). La cuenta administrativa la sube, cambia o quita; las cuentas de puesto solo ven las de sus materiales. Se valida la firma real del archivo, no solo la extensión. Las fotos se guardan en `portal/.local/imagenes-materiales/` (fuera de Git y de la base de Azure), así que al trasladar el portal hay que copiar esa carpeta junto con el diario.

**17/11/2026:** las salidas se capturan en cajas completas, igual que las entradas. Una salida de N cajas descuenta N × `cantidad_por_caja` piezas; si la última caja está abierta, sale con las piezas que le queden, sin dejar saldo negativo. Una caja con piezas sueltas sigue ocupando el lugar de una caja completa en la estantería. Las salidas registradas antes de este cambio se conservan en piezas. Solo la cuenta administrativa registra salidas; las cuentas de puesto (materialistas) solo registran entradas.

**06/11/2026:** sección Indicadores con los 10 KPIs del Equipo 2 y descarga en Excel; ver [INDICADORES_KPI.md](documentacion/INDICADORES_KPI.md).

Consulta [los comandos de arranque](ABRIR-EN-ANTIGRAVITY.md), [la implementación y sus pruebas](documentacion/CONTINUACION_MOVIMIENTOS.md) y [el traslado de Demo](documentacion/DEMOSTRACION_DBO.md). El portal se abre en [http://127.0.0.1:5089/](http://127.0.0.1:5089/).


Abre esta carpeta en Antigravity y sigue la [guía de arranque del portal 5089](ABRIR-EN-ANTIGRAVITY.md). El [modelo por puesto](documentacion/MODELO_POR_PUESTOS.md) documenta los cambios de esta versión. [Diagrama físico final](documentacion/diagramas/mvp-puestos-fisico.svg).

---

**Referencia histórica:** toda la sección siguiente corresponde a la versión anterior de Desarrollo (5087). Sus cuentas, cantidades, tablas de archivo y comandos no describen la instalación vigente. Utiliza las guías enlazadas arriba para operar Demo.

# Motherson L1 — MVP e histórico consultable

**Actualización aplicada el 03/11/2026:** [ocho tablas y cuentas individuales por puesto](documentacion/SIMPLIFICACION_MODELO.md), validadas en Pruebas (5088) y promovidas con autorización a Desarrollo (5087). Las contraseñas existentes y el histórico se conservaron. Los diagramas y permisos actuales están en esa documentación; el contenido siguiente describe la instalación inicial.

Fase C implementada el 02/11/2026. Versión de desarrollo independiente del portal anterior. Rama Git: fase-c/mvp-historico-kpis.

**Abrir en esta máquina: http://127.0.0.1:5087**. Usuario inicial: admin.local. La contraseña nueva está únicamente en portal/.local/acceso-inicial-Motherson_L1_MVP_Desarrollo.json; abrir ese archivo localmente, no usar la contraseña SQL ni la del portal anterior.

Ocho tablas operativas, tres tablas de archivo y seis vistas de lectura. Se conservan 709,042 eventos originales y una consulta unificada de 230,901 movimientos históricos compatibles más comprobantes nuevos. El inventario activo comienza con 63 ubicaciones documentales y 10,116 piezas **simuladas**. No hay datos de operación real acreditados en esta entrega.

## Iniciar el portal preparado

Desde portal, con el contenedor SQL motherson-mvp-sql-1 iniciado:

```sh
bash scripts/start.sh
```

Esperar Now listening on: http://127.0.0.1:5087 y mantener esa Terminal abierta. El portal anterior continúa en 5080. El puerto 5088 corresponde a pruebas aisladas; no usarlo para la presentación. El detalle de preparación, dependencias y pruebas está en documentacion/README.md.

## Pantallas del MVP

| Pantalla | Función |
| --- | --- |
| Resumen | Existencias por ubicación, conteos y acceso al registro |
| Materiales y catálogos | Partes, empaque, mínimo, uso documentado y puestos |
| Estaciones y ubicaciones | Estación, estantería, nivel y relación con el material |
| Inventario | Saldo actual y total calculado por material, sin duplicar stock |
| Entradas y salidas | Bloques visuales, cajas completas de entrada y piezas de salida, confirmación y comprobante |
| Historial de movimientos | Archivo y movimientos nuevos, filtros por parte, fecha, ubicación y origen |

Se retiraron las rutas activas de alarmas, solicitudes, reservas y simulaciones complejas. El código anterior de esos módulos permanece en archivo-codigo, fuera de la compilación. El histórico conserva sus eventos para análisis.

## Documentación de entrega

- documentacion/SIMPLIFICACION_MODELO.md — ocho tablas y nueve relaciones vigentes; MODELO_RELACIONAL_ACTUALIZADO.md conserva la versión inicial de nueve tablas.
- documentacion/PLAN_MIGRACION.md — corte, apertura, carga repetible y recuperación.
- documentacion/VALIDACION_HISTORICO.md — conteos, huellas, conciliación y exclusiones.
- documentacion/CONTRATO_DATOS_EQUIPO2.md — vistas, tipos, claves, ejemplos y acceso lector.
- documentacion/RESULTADOS_PRUEBAS.md — evidencia ejecutada, reinicio y limitaciones.
- documentacion/README.md — uso y preparación técnica.
- portal/db — DDL, vistas, procedimiento y validaciones SQL.
- portal/evidencias — resultados JSON y capturas de las pantallas.

Para Equipo 2, usar reporting con el login mvp_desarrollo_lectura y el perfil privado portal/.local/acceso-equipo2-Desarrollo.json. El archivo no contiene la cuenta de aplicación y no debe publicarse. Nota histórica del perfil local: 127.0.0.1 solo sirve en esa máquina. El piloto remoto vigente y su Dashboard con sesión Microsoft están descritos al inicio de este documento.

Las aperturas no son consumo ni surtimiento. reporting.vw_kpi_consumo_diario excluye simulaciones y por ello está vacía; los indicadores de demostración deben filtrar explícitamente es_simulado=1 en la vista de movimientos.

Se verificaron el caso 72→120→105, concurrencia, reversión de fallos, permisos y navegador. El reinicio autorizado de SQL conservó inventario, comprobantes e histórico. No se borraron bases ni volúmenes del proyecto anterior.

## Alarmas calculadas incorporadas

Resumen e Inventario incluyen alarmas informativas y cobertura temporal, sin una pestaña nueva ni cambios de esquema SQL. Las reglas y el calendario están en [ALARMAS_Y_COBERTURA.md](documentacion/ALARMAS_Y_COBERTURA.md); la evidencia está en [RESULTADOS_ALARMAS.md](documentacion/RESULTADOS_ALARMAS.md). El calendario publicado termina el 24/10/2026: el inventario actual conserva «Sin estimación» donde falta calendario vigente, mientras los mínimos se comparan por material.
