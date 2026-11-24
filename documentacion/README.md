# Documentación del proyecto

Para la ampliación de cuatro líneas del 22/11/2026 consulta:

- [Manual de usuario y administración](administracion-v2/MANUAL.md).
- [Pruebas y alcance de la validación](administracion-v2/PRUEBAS.md).
- [Actualización y recuperación](administracion-v2/ACTUALIZACION_Y_RECUPERACION.md).
- [Mapa del repositorio y colaboración](../CONTRIBUTING.md).

Entrega a Motherson (24/11/2026):

- [Arquitectura y herramientas](ARQUITECTURA_Y_HERRAMIENTAS.md).
- [Guía para correr el sistema de forma local](GUIA_INSTALACION_LOCAL.md).

## Antecedentes

Las guías siguientes conservan su contexto histórico y no deben usarse como instrucciones de reinstalación del piloto publicado.

# Guía de uso y preparación — MVP L1

**05/11/2026 · Portal 5089:** consultar [Histórico principal, responsables y cobertura](HISTORICO_PRINCIPAL.md) para la presentación vigente del maestro anual, filtros de material/responsable/turno, explicación de atenciones y cortes de semaforización verificados. Los apartados antiguos siguientes se conservan como antecedentes.

**04/11/2026.** El [modelo por puesto](MODELO_POR_PUESTOS.md) se validó en Pruebas, puerto 5089. La guía siguiente corresponde al esquema anterior de Desarrollo, puerto 5087.

**Actualización 03/11/2026:** la simplificación a ocho tablas y el acceso individual por puesto están validados en Pruebas (5088) y promovidos con autorización a Desarrollo (5087). Véase [Simplificación del modelo](SIMPLIFICACION_MODELO.md), sus diagramas y resultados. El resto de esta guía describe el estado inicial; para la estructura y permisos vigentes prevalece esa actualización.

02/11/2026 · Fase C. Repositorio dedicado: outputs/motherson-l1-mvp-2026-11-02. El origen anterior permanece intacto en outputs/motherson-l1-claro-2026-10-28.

## 1. Instalación disponible y acceso

| Servicio | Valor comprobado |
| --- | --- |
| Portal MVP | http://127.0.0.1:5087 |
| Base operativa | Motherson_L1_MVP_Desarrollo |
| SQL | 127.0.0.1,1433; contenedor motherson-mvp-sql-1 |
| Portal de prueba aislado | http://127.0.0.1:5088; Motherson_L1_MVP_Pruebas |
| Portal anterior conservado | http://127.0.0.1:5080 |
| Rama Git nueva | fase-c/mvp-historico-kpis |

Inicio de sesión del portal nuevo: admin.local. La contraseña generada para esta instalación está en portal/.local/acceso-inicial-Motherson_L1_MVP_Desarrollo.json, protegido y excluido de Git. Abrirlo localmente; no pegar la contraseña en chats ni confundirla con SQL_SA_PASSWORD. No se copiaron contraseñas del portal anterior.

Para iniciar, en una Terminal:

```sh
cd "/Users/nihhil/Documents/Codex/2026-10-20/referenced-chatgpt-conversation-this-is-an/outputs/motherson-l1-mvp-2026-11-02/portal"
bash scripts/start.sh
```

Mantener abierta esa Terminal. Ctrl+C detiene solo ese proceso de portal. El lanzador usa la cuenta SQL limitada de aplicación. Si el puerto ya está ocupado, conservar la instancia que está funcionando; no lanzar dos procesos en el mismo puerto.

## 2. Recorrido de demostración

1. Iniciar sesión y revisar Resumen: 10,116 piezas en la apertura de Desarrollo, 63 materiales/ubicaciones documentales. Las cifras cambiarán únicamente si se confirman movimientos nuevos.
2. Consultar Materiales y catálogos: factor de empaque, mínimo y puesto. Se muestran los 62 usos documentados y el pendiente de validación; COLOR se retiró tras verificar que estaba vacía.
3. Consultar Estaciones y ubicaciones para explicar la posición. La fila de ESTANTERIA incluye un nivel; no es otra tabla de inventario.
4. En Inventario, buscar parte/ubicación y revisar su saldo y capacidad. El total de material se calcula desde sus ubicaciones.
5. En Entradas y salidas, seleccionar un bloque permitido y material. Elegir ENTRADA para cajas enteras o SALIDA para piezas; revisar saldo previo y posterior y confirmar una vez. No se solicita motivo libre ni se elige manualmente el usuario responsable: se usa la sesión.
6. En Historial, filtrar MVP para ver el comprobante nuevo y su usuario, o HISTORICO_21 para ver eventos históricos compatibles. La consulta no genera registros ni modifica el saldo.

Esta entrega ya no incluye pantallas operativas de alarmas, solicitudes, reservas ni simulación avanzada. Los hechos anteriores de esos procesos siguen en el archivo de lectura. No se ofrece consumo MES/SAP.

Para enseñar exactamente 72→120→105, usar la evidencia de Pruebas o preparar otra prueba controlada; ese caso ya se ejecutó y persiste en 105. No modificar artificialmente saldos para volver a mostrar 72.

## 3. Perfiles mínimos

| Perfil | Alcance del backend |
| --- | --- |
| CONSULTA | Consulta de catálogo, inventario e historial |
| ENTRADAS | Consultas y entrada de cajas del puesto asignado; salida denegada |
| MOVIMIENTOS | Consultas y entradas del puesto asignado; salida denegada |
| ADMINISTRADOR | Alcance completo del MVP reducido |

La carga inicial de Desarrollo crea solo admin.local. La base de Pruebas contiene además consulta.prueba y entrada.prueba para verificar permisos, con credenciales privadas de esa base. No existe un módulo de administración avanzada de usuarios/roles en el MVP. Los doce responsables ficticios históricos no son cuentas para iniciar sesión.

## 4. Dependencias y compilación

Se reutilizan ASP.NET Core 10, Dapper, SQL Server, React, TypeScript y Vite. Versiones exactas en csproj y package-lock.json. Python 3 prepara la migración y ejecuta pruebas; no recalcula el inventario al consultar el portal. Docker contiene SQL Server, no el servidor web .NET.

Desde portal:

```sh
dotnet build Motherson.slnx
npm --prefix frontend ci
npm --prefix frontend run build
```

El frontend se publica en backend/Motherson.Api/wwwroot. No se necesita otro servidor Vite para la demostración. Después de modificar el backend, compilar y reiniciar solo el proceso del portal nuevo. No usar el antiguo dotnet watch que daba problemas de rutas en macOS.

En esta verificación se reutilizó la instancia SQL local ya activa para evitar otro servidor pesado. compose.yaml conserva una opción separada con puerto 1438 y volumen propio; esa instalación limpia no se probó en esta fase. No cambiar la contraseña de la instancia existente editando una plantilla .env ni borrar su volumen.

## 5. Preparación del histórico en una base nueva

Se necesitan la entrega fuente completa del esquema 21 y sus diez jsonl.gz. Desde portal:

```sh
python3 scripts/preparar_historico.py --origen "../../motherson-l1-claro-2026-10-28" --salida ".local/migracion"
python3 scripts/local.py --migrar ".local/migracion/plan.json"
python3 scripts/local.py --integridad
python3 scripts/exportar_acceso_lectura.py
```

Los destinos permitidos son exclusivamente Motherson_L1_MVP_Desarrollo y Motherson_L1_MVP_Pruebas. Una huella distinta detiene la carga; la misma carga sellada no repite aperturas. .local/migracion/plan.json contiene rutas de esta máquina; regenerar el plan si se mueve el directorio.

El constructor no elimina bases ni volúmenes. No incluye restauración destructiva ni vuelve a generar el año simulado. La apertura se hace por saldo conciliado, mientras el archivo conserva los movimientos anteriores para consultas. Consulte PLAN_MIGRACION.md y VALIDACION_HISTORICO.md antes de interpretar las cifras.

## 6. Acceso de Equipo 2

SQL Login: mvp_desarrollo_lectura, servidor 127.0.0.1, puerto 1433, base Motherson_L1_MVP_Desarrollo, cifrado activado y certificado local confiado para esta demostración. La contraseña/conexión lectora se entrega mediante portal/.local/acceso-equipo2-Desarrollo.json por canal privado. El script exportar_acceso_lectura.py regenera ese perfil sin imprimir la contraseña y sin incluir la cuenta de aplicación.

No entregar conexiones-*.json: esos archivos internos incluyen la cuenta de aplicación. No usar sa para Power BI. El lector no puede modificar inventario ni acceder a hashes de cuentas.

Vistas principales: reporting.vw_kpi_movimientos, reporting.vw_kpi_inventario_actual y reporting.vw_kpi_consumo_diario. El contrato enumera tipos, unidades y vistas de archivo adicionales. No se construyó una API duplicada de KPIs ni se integró todavía un dashboard Power BI.

Al abrir una conexión desde otra computadora, 127.0.0.1 apunta a esa computadora. Es necesario preparar allí la base desde las fuentes o establecer acceso privado autorizado; este portal local no es un servidor remoto publicado. El lector y el contrato quedan listos para trabajar localmente en esta instalación.

## 7. Pruebas y evidencias

```sh
python3 scripts/local.py --integridad
python3 scripts/local.py --probar-permisos
npm --prefix frontend test
npm --prefix frontend run test:ui
python3 scripts/verificar_persistencia.py
```

Playwright usa el Brave instalado en /Applications/Brave Browser.app. Si se usa otro navegador, ajustar mvp.playwright.config.ts a su ejecutable disponible. No se requiere que el usuario escriba su contraseña en una herramienta; la prueba local lee el archivo privado de esta instalación.

La prueba HTTP completa requiere una base Pruebas preparada con LAB-MAT-001=72. Tras ejecutarla, el saldo es 105 y el script rechaza repetirla sobre esos datos sin un entorno nuevo. No restablece ni borra movimientos. El comando --reiniciar de verificar_persistencia.py reinicia SQL compartido y queda fuera de las pruebas rutinarias: en esta fase se ejecutó una vez con autorización expresa.

Resultados ejecutados y límites: RESULTADOS_PRUEBAS.md. Evidencia máquina en portal/evidencias; capturas en su subcarpeta capturas. No se probaron carga masiva, restauración independiente ni despliegue remoto.

## 8. Organización y conservación

| Carpeta | Contenido |
| --- | --- |
| portal/backend | API activa reducida, migración y acceso SQL |
| portal/frontend | Seis secciones activas y pruebas |
| portal/db | Ocho tablas operativas, archivo, vistas, procedimiento y verificaciones |
| portal/scripts | Preparación, arranque y pruebas reproducibles |
| portal/etl | Normalización reutilizable anterior; no ejecutada sobre fuentes en esta fase |
| portal/evidencias | Resultados y capturas sin contraseñas |
| portal/.local | Configuración y credenciales privadas de esta instalación; excluida de Git |
| archivo-codigo | Módulos anteriores retirados de la compilación; referencia conservada |
| documentacion | Modelo, contrato, migración y pruebas |

La persistencia vive en SQL Server. No basta copiar solo el código y eliminar el volumen de Docker. Preservar las fuentes históricas, la instalación SQL y sus respaldos; ninguna limpieza destructiva forma parte de esta fase.

## Alarmas calculadas incorporadas

Resumen e Inventario incluyen alarmas informativas y cobertura temporal, sin una pestaña nueva ni cambios de esquema SQL. Las reglas y el calendario están en [ALARMAS_Y_COBERTURA.md](ALARMAS_Y_COBERTURA.md); la evidencia está en [RESULTADOS_ALARMAS.md](RESULTADOS_ALARMAS.md). El calendario publicado termina el 24/10/2026: el inventario actual conserva «Sin estimación» donde falta calendario vigente, mientras los mínimos se comparan por material.

- [Rediseño operativo de Entradas y salidas](REDISENO_ENTRADAS_Y_SALIDAS.md): recorrido, capturas y validación sin cambios en Desarrollo.

- [Conciliación documental del catálogo](CONCILIACION_CATALOGO.md): diagnóstico inicial, 62 usos PFEP recuperados, procedencia por celda, respaldo y reversión selectiva. COLOR se retiró posteriormente según [Simplificación del modelo](SIMPLIFICACION_MODELO.md); los tipos siguen pendientes. Catálogos e Inventario comparten los atributos actuales y los eventos originales permanecen intactos.
