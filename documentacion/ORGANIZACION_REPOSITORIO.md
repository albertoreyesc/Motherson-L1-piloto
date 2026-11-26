# Organización del repositorio · 26/11/2026

Base revisada: **v3, commit `8c25f64`**. GitHub identifica `piloto/preparacion-gratuita-2026-11-19` como rama predeterminada. Render confirmó ese mismo commit como **Live**, servicio `motherson-l1-piloto`, rama conectada coincidente. Se trabajó en una rama independiente; esta organización se entrega como PR, sin integrar ni desplegar.

## Inventario y criterio

El [inventario CSV](inventario-repositorio.csv) clasifica los **729 archivos de la base** individualmente con ruta, función, evidencia, decisión y destino. La clasificación agrupa la evidencia cuando varios archivos comparten compilación, consumidor o finalidad. No atribuye uso operativo actual a una evidencia fechada ni a una herramienta histórica.

| Ruta original | Función | Evidencia | Decisión / destino |
| --- | --- | --- | --- |
| `portal/frontend/`, API y proyectos .NET | Aplicación/pruebas | Imports, csproj, Vite y workflow | Conservar rutas y comportamiento |
| `portal/backend/Motherson.Api/wwwroot/` (11 archivos) | Estáticos generados | Vite y Docker los regeneran; 11 archivos idénticos byte por byte tras compilar | Dejar de versionar; originales en frontend, licencias conservadas |
| `portal/frontend/tsconfig.tsbuildinfo` | Caché | Salida de tsc -b | Dejar de versionar e ignorar |
| `archivo-codigo/` (50 archivos) | Módulos sustituidos | Excluidos del Compile Include explícito; interfaz y API compilan sin ellos | Retirar del árbol; recuperables en Git, sin duplicar archivo histórico |
| `powerbi/Motherson.pbix` | Duplicado | SHA-256 idéntico al original del equipo 2 | Conservar solo `powerbi/equipo2-2026-11-24/Motherson.pbix` |
| `powerbi/modelo_completo.tmdl` | Modelo previo | Fuente del entorno original, distinta de las vistas mes2 activas | Mover a `powerbi/archivo/modelo_previo_2026-11-18.tmdl` |
| `powerbi/administracion-v2/`, `equipo2-2026-11-24/report/` | Modelo activo e informe adaptado | Definiciones de la integración publicada | Conservar sin modificar el servicio |
| Documentos anteriores y guías 5087/5089 | Antecedentes | Contenido de fases anteriores, sustituido por manuales v3 | Mover a `documentacion/archivo/` con aviso y referencias actualizadas |
| `portal/db/`, `despliegue/`, `render.yaml` | Instalación/actualización/hosting | Leídos por consolas y Docker; ramas y rutas verificadas | Conservar; nueva guía distingue instalación y migración |
| `datos/`, recursos frontend y calendarios | Fuentes requeridas | DemoData.Load, runners, copia de config y fuentes públicas | Conservar rutas y ocho CSV originales |
| `portal/evidencias/`, `documentacion/evidencia-integracion/` | Pruebas históricas | Citadas por documentos/scripts, algunos runners escriben ahí | Conservar con índices y alcance temporal |
| Herramientas heredadas de `portal/scripts/`, ETL y consolas Azure | Utilidad dependiente del entorno | Algunos consumidores requieren fuentes privadas o instalaciones originales | Conservar e identificar; no eliminar por fecha |

**Resultado:** 28 archivos movidos y 63 retirados del versionado/árbol actual. Se evita duplicar aproximadamente 4.88 MiB en el árbol vigente. El historial Git y el tamaño histórico del repositorio no se reducen por esta operación.

## Movimientos exactos

| Antes | Después |
| --- | --- |
| `documentacion/ACTUALIZACION_HISTORICO_Y_POWERBI_2026-11-19.md` | [documentacion/archivo/ACTUALIZACION_HISTORICO_Y_POWERBI_2026-11-19.md](archivo/ACTUALIZACION_HISTORICO_Y_POWERBI_2026-11-19.md) |
| `documentacion/ALARMAS_Y_COBERTURA.md` | [documentacion/archivo/ALARMAS_Y_COBERTURA.md](archivo/ALARMAS_Y_COBERTURA.md) |
| `documentacion/APROBACION_CARGA_HISTORICA.md` | [documentacion/archivo/APROBACION_CARGA_HISTORICA.md](archivo/APROBACION_CARGA_HISTORICA.md) |
| `documentacion/APROBACION_INSTALACION_AZURE.md` | [documentacion/archivo/APROBACION_INSTALACION_AZURE.md](archivo/APROBACION_INSTALACION_AZURE.md) |
| `documentacion/BASE_COMPARTIDA_AZURE.md` | [documentacion/archivo/BASE_COMPARTIDA_AZURE.md](archivo/BASE_COMPARTIDA_AZURE.md) |
| `documentacion/CONCILIACION_CATALOGO.md` | [documentacion/archivo/CONCILIACION_CATALOGO.md](archivo/CONCILIACION_CATALOGO.md) |
| `documentacion/CONTINUACION_MOVIMIENTOS.md` | [documentacion/archivo/CONTINUACION_MOVIMIENTOS.md](archivo/CONTINUACION_MOVIMIENTOS.md) |
| `documentacion/CONTRATO_DATOS_EQUIPO2.md` | [documentacion/archivo/CONTRATO_DATOS_EQUIPO2.md](archivo/CONTRATO_DATOS_EQUIPO2.md) |
| `documentacion/DEMOSTRACION_DBO.md` | [documentacion/archivo/DEMOSTRACION_DBO.md](archivo/DEMOSTRACION_DBO.md) |
| `documentacion/HISTORICO_PRINCIPAL.md` | [documentacion/archivo/HISTORICO_PRINCIPAL.md](archivo/HISTORICO_PRINCIPAL.md) |
| `documentacion/INDICADORES_KPI.md` | [documentacion/archivo/INDICADORES_KPI.md](archivo/INDICADORES_KPI.md) |
| `documentacion/INSTALACION_AZURE_EJECUTADA_2026-11-19.md` | [documentacion/archivo/INSTALACION_AZURE_EJECUTADA_2026-11-19.md](archivo/INSTALACION_AZURE_EJECUTADA_2026-11-19.md) |
| `documentacion/INTEGRACION_AZURE_PROPIA.md` | [documentacion/archivo/INTEGRACION_AZURE_PROPIA.md](archivo/INTEGRACION_AZURE_PROPIA.md) |
| `documentacion/MODELO_POR_PUESTOS.md` | [documentacion/archivo/MODELO_POR_PUESTOS.md](archivo/MODELO_POR_PUESTOS.md) |
| `documentacion/MODELO_RELACIONAL_ACTUALIZADO.md` | [documentacion/archivo/MODELO_RELACIONAL_ACTUALIZADO.md](archivo/MODELO_RELACIONAL_ACTUALIZADO.md) |
| `documentacion/ORIGEN_INTEGRACION_AZURE.json` | [documentacion/archivo/ORIGEN_INTEGRACION_AZURE.json](archivo/ORIGEN_INTEGRACION_AZURE.json) |
| `documentacion/PLAN_MIGRACION.md` | [documentacion/archivo/PLAN_MIGRACION.md](archivo/PLAN_MIGRACION.md) |
| `documentacion/POWERBI_ACCESO_PLANTA.md` | [documentacion/archivo/POWERBI_ACCESO_PLANTA.md](archivo/POWERBI_ACCESO_PLANTA.md) |
| `documentacion/POWERBI_PUBLICACION_PROPIA.md` | [documentacion/archivo/POWERBI_PUBLICACION_PROPIA.md](archivo/POWERBI_PUBLICACION_PROPIA.md) |
| `documentacion/REDISENO_ENTRADAS_Y_SALIDAS.md` | [documentacion/archivo/REDISENO_ENTRADAS_Y_SALIDAS.md](archivo/REDISENO_ENTRADAS_Y_SALIDAS.md) |
| `documentacion/RESULTADOS_ALARMAS.md` | [documentacion/archivo/RESULTADOS_ALARMAS.md](archivo/RESULTADOS_ALARMAS.md) |
| `documentacion/RESULTADOS_PRUEBAS.md` | [documentacion/archivo/RESULTADOS_PRUEBAS.md](archivo/RESULTADOS_PRUEBAS.md) |
| `documentacion/SIMPLIFICACION_MODELO.md` | [documentacion/archivo/SIMPLIFICACION_MODELO.md](archivo/SIMPLIFICACION_MODELO.md) |
| `documentacion/VALIDACION_AZURE_TRANSACCIONAL.md` | [documentacion/archivo/VALIDACION_AZURE_TRANSACCIONAL.md](archivo/VALIDACION_AZURE_TRANSACCIONAL.md) |
| `documentacion/VALIDACION_HISTORICO.md` | [documentacion/archivo/VALIDACION_HISTORICO.md](archivo/VALIDACION_HISTORICO.md) |
| `ORIGEN.md` | [documentacion/archivo/ORIGEN.md](archivo/ORIGEN.md) |
| `ABRIR-EN-ANTIGRAVITY.md` | [documentacion/archivo/ABRIR-EN-ANTIGRAVITY.md](archivo/ABRIR-EN-ANTIGRAVITY.md) |
| `powerbi/modelo_completo.tmdl` | [powerbi/archivo/modelo_previo_2026-11-18.tmdl](../powerbi/archivo/modelo_previo_2026-11-18.tmdl) |

## Recuperación de lo retirado

Los 50 módulos sustituidos y los archivos generados anteriores están disponibles en el [árbol base](https://github.com/albertoreyesc/Motherson-L1-piloto/tree/8c25f64). La lista exacta se filtra en el CSV por las decisiones «Dejar de versionar» y «Eliminar duplicado/código sustituido». No se reescribió historia ni se ejecutó force push.

## Guías e índices incorporados

- README principal con estado vigente, mapa, manuales, compilación y límites.
- CONTRIBUTING y plantilla de PR con validación y diferencia entre PR y despliegue.
- Índices de SQL, configuración, scripts, Power BI, archivo y evidencias.
- Ejemplo público de configuración piloto con marcadores, sin valores secretos.
- `CHANGELOG.md` con hitos comprobables, sin inventar políticas corporativas.
- `portal/scripts/verificar_repositorio.py`: comprueba enlaces locales, rutas requeridas y huella del PBIX, sin conectar a servicios ni modificar datos.

## Validación

- Frontend: **91 pruebas aprobadas** y compilación TypeScript/Vite correcta.
- Los **11 estáticos regenerados son idénticos byte por byte** a los de la base v3. Eliminar su copia versionada no cambia la interfaz producida.
- .NET: compilación y publicación Release correctas; **25 comprobaciones locales** de destino, cifrado y Power BI simulado aprobadas, sin SQL.
- Integración SQL: no ejecutada; no se cambiaron reglas, procedimientos, migraciones ni rutas de datos.
- **202 enlaces locales y 12 rutas requeridas comprobadas**, sin destinos inexistentes; huella del PBIX original conservada.
- Conservación byte a byte: 34 archivos fuente/proyecto de API, 43 fuentes frontend, 30 SQL, ocho CSV, 13 archivos del modelo BI y 139 del diseño PBIR. Dockerfile, arranque, render.yaml y workflow no cambian.
- Docker ARM64 local: la descarga de índices de Ubuntu falló con `Hash Sum mismatch`, ajeno a las rutas del proyecto. Se inició una comprobación Linux/AMD64, arquitectura de Render; resultado pendiente al abrir el PR.
- No se ejecutaron migraciones, scripts de carga ni pruebas que modifiquen SQL.

## Configuración privada y revisión de archivos sensibles

No se copiaron archivos de operación `.local`, `.env`, certificados, sesiones ni credenciales a esta rama. Se ampliaron exclusiones para salidas generadas y archivos privados. La revisión por patrones de claves privadas, tokens GitHub y claves Azure Storage no encontró coincidencias de alta confianza en el árbol base; esta revisión no certifica todo el historial ni sustituye una auditoría de secretos. Los valores ficticios de pruebas y marcadores de ejemplos se conservan.

## Pendientes y límites conservados

- Los comandos de algunas herramientas anteriores dependen de rutas personales, fuentes privadas o bases previas. Se mantienen clasificados; su necesidad completa requiere validar esos entornos. No se ejecutaron como limpieza.
- `render.yaml` conserva un aviso `PowerBI__DataNotice` de una fase anterior. No se cambió configuración de hosting en esta organización; revisar ese valor contra la configuración real en una intervención separada. La guía vigente describe la integración actual.
- Las evidencias históricas y diagramas no acreditan saldos, licencias o infraestructura actuales. Los nuevos índices delimitan su alcance.
- La cobertura temporal equivalente en Power BI, la validación del calendario por planta y las condiciones de hosting/licencia siguen siendo pendientes de producto/operación.
- No se establecieron CODEOWNERS, licencia de distribución ni reglas corporativas de aprobación sin decisión del propietario.

## Revisión e integración

La entrega está en `organizacion/repositorio-ingenieria-2026-11-26`. Revisar el PR antes de integrar: la rama base publica automáticamente en Render. Esta tarea no cambió existencias, usuarios, permisos, informes Power BI ni infraestructura. Revertir los commits de organización restaura el árbol anterior; no requiere restaurar una base de datos.
