# Smart Material Replenishment · Motherson

Portal para consultar inventario por ubicación, registrar entradas y atenciones de surtido, y analizar la actividad de cuatro líneas con trazabilidad de usuario y comprobante.

**[Abrir el piloto](https://motherson-l1-piloto.onrender.com/)** · **[Manual de usuario](documentacion/MANUAL_DE_USUARIO.md)** · **[Instalación y actualización](documentacion/MANUAL_DE_INSTALACION.md)**

## Estado y alcance

El repositorio es privado. La base de esta organización es la versión v3, commit `8c25f64`, integrada y verificada como **Live en Render el 26/11/2026**. La rama predeterminada y conectada a Render es `piloto/preparacion-gratuita-2026-11-19`. Integrar cambios en esa rama puede publicar automáticamente.

El piloto utiliza datos de demostración; no acredita existencias reales de planta. La configuración `Pilot:DatosSimulados` controla la marca visual y no convierte datos de prueba en datos reales. La puesta en operación requiere validación de TI y logística.

## Por dónde empezar

| Necesidad | Documento |
| --- | --- |
| Operar el sitio según el tipo de cuenta | [Manual de usuario](documentacion/MANUAL_DE_USUARIO.md) |
| Instalar localmente, en servidor o actualizar | [Manual de instalación](documentacion/MANUAL_DE_INSTALACION.md) |
| Entender componentes y flujo de datos | [Arquitectura y herramientas](documentacion/ARQUITECTURA_Y_HERRAMIENTAS.md) |
| Colaborar con una rama y pull request | [CONTRIBUTING.md](CONTRIBUTING.md) |
| Identificar migraciones e instalación inicial | [Base de datos](portal/db/README.md) |
| Ubicar modelo activo e informe Power BI | [Power BI](powerbi/README.md) |
| Revisar límites y trabajo pendiente | [Límites para TI](documentacion/LIMITES_Y_PENDIENTES.md) |
| Consultar clasificación, movimientos y limpieza | [Inventario del repositorio](documentacion/ORGANIZACION_REPOSITORIO.md) |

## Mapa del repositorio

```text
portal/
  frontend/                 React, TypeScript, recursos originales y pruebas
  backend/Motherson.Api/    API ASP.NET Core y reglas de negocio
  db/                      Instalación SQL y migraciones incrementales
  config/                  Catálogos, calendarios y ejemplos públicos
  pruebas-*/               Comprobaciones locales e integración
  scripts/                 Herramientas locales y administrativas
  evidencias/              Resultados históricos; no son datos actuales
powerbi/
  administracion-v2/       Modelo TMDL activo para las operaciones del portal
  equipo2-2026-11-24/      Diseño PBIR adaptado y entrega original identificada
  archivo/                Modelo previo, fuera de la publicación vigente
despliegue/
  render/                  Dockerfile y arranque del hosting vigente
  piloto/                  Aprovisionamiento e instalación inicial de SQL
datos/                     Fuentes simuladas de instalación/pruebas
documentacion/
  archivo/                Antecedentes; no usar como guía vigente
  administracion-v2/      Detalle técnico, actualización y recuperación
  evidencia-integracion/ Resultados fechados de validación
.github/                   Validación de PR y plantilla de revisión
render.yaml                Configuración pública del servicio
```

Se conservaron las rutas funcionales de aplicación y despliegue. Los estáticos de `wwwroot/` se generan con Vite y ya no se versionan; sus originales están en `portal/frontend/`.

## Compilar y comprobar

Requisitos para compilación: Git, **Node.js 22** y **.NET SDK 10**. Desde la raíz:

```sh
npm --prefix portal/frontend ci
npm --prefix portal/frontend test
npm --prefix portal/frontend run build
dotnet build portal/Motherson.slnx
dotnet run --project portal/pruebas-azure/Azure.Pruebas.csproj
```

La última comprobación valida configuración y cifrado con Power BI simulado; no conecta a SQL. Para obtener la imagen del portal:

```sh
docker build -f despliegue/render/Dockerfile -t motherson-portal .
```

Para abrir una instalación nueva de pruebas, sigue el **escenario A del [manual de instalación](documentacion/MANUAL_DE_INSTALACION.md#3-escenario-a--prueba-en-una-computadora-datos-de-prueba)**: usa una base local aislada. Compilar no configura una base ni crea cuentas. No utilices credenciales del piloto compartido en pruebas.

## Datos, permisos y análisis

El navegador utiliza la API autenticada; la API persiste las operaciones en SQL. Las fuentes BI de solo lectura `mes2.bi_*` exponen el catálogo y las atenciones al modelo Power BI. Administración, puesto logístico y consulta tienen permisos distintos; el servidor los verifica en cada operación.

Los KPIs del portal y el Excel tienen tres hojas: Estado actual, Actividad logística y Diagnóstico. El Dashboard añade consulta operativa cada 30 segundos mientras está visible. El informe Power BI utiliza **Import**, su propio corte y acceso Microsoft independiente. La programación documentada es lunes a viernes a las 06:00, 15:00 y 23:30, Ciudad de México; validar en el servicio si se modifica. Véase [integración](documentacion/INTEGRACION_EQUIPO2_2026-11-24.md).

## Límites confirmados

- La cobertura temporal necesita calendario y consumos suficientes por ubicación. Power BI mantiene Sin estimación hasta integrar una fuente equivalente validada.
- El calendario de referencia simulado debe validarse y renovarse con planta.
- Los planes de hosting y las licencias Microsoft tienen límites; no se garantiza acceso privado gratuito permanente ni disponibilidad industrial.
- La instalación dentro de infraestructura Motherson requiere validación propia; este repositorio no acredita su ejecución en planta.
- No hay credenciales o certificados de operación incluidos. Cada entorno configura sus secretos fuera de Git.

Los archivos de etapas sustituidas y las decisiones de esta limpieza se localizan desde el [índice de documentación](documentacion/README.md). El historial Git conserva el código retirado; no se reescribió ni se eliminó información del piloto.
