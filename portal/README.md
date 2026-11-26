# Aplicación del portal

Punto de entrada: [README del repositorio](../README.md). La guía anterior de los puertos 5087/5089 corresponde al historial Git y no describe el despliegue v3.

| Ruta | Responsabilidad |
| --- | --- |
| `frontend/src/` | Interfaz y pruebas unitarias |
| `frontend/public/` | Originales de marca, fuentes y licencias |
| `backend/Motherson.Api/` | API, autenticación y reglas SQL |
| `backend/Motherson.Api/wwwroot/` | Salida generada por `npm run build`; ignorada por Git |
| `db/` | [Esquema e instalación frente a actualización](db/README.md) |
| `config/` | [Recursos públicos y configuración de ejemplo](config/README.md) |
| `pruebas-*/` | Pruebas .NET, algunas con SQL local obligatorio |
| `scripts/` | [Clasificación y herramientas](scripts/README.md) |
| `evidencias/` | [Evidencias históricas](evidencias/README.md) |
| `administracion-azure/`, `verificacion-azure/` | Consolas para intervenciones del entorno Azure original; no son el servidor web ni se ejecutan al arrancar |
| `etl/` | Normalización de fuentes de etapas anteriores; se conserva por sus consumidores de instalación/pruebas |
| `compose*.yaml` | SQL local de desarrollo; sus puertos y contenedor difieren del escenario A de administración v2 |

Primero compila el frontend; después ejecuta o publica .NET. Sin el primer paso, un clon limpio no contiene los estáticos de la interfaz. Docker y GitHub Actions realizan ese paso antes de publicar.

Para desarrollo e instalación sigue el [manual vigente](../documentacion/MANUAL_DE_INSTALACION.md); para integración y validación sigue [PRUEBAS.md](../documentacion/administracion-v2/PRUEBAS.md). No ejecutes un inicializador sobre una base existente.
