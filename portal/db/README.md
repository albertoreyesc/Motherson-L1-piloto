# SQL · instalación frente a actualización

Las rutas se conservan porque las consolas administrativas, las pruebas y los scripts cargan los archivos por nombre. **No ejecutar todos los SQL en orden numérico sobre una base existente.**

| Ruta | Propósito y consumidor |
| --- | --- |
| `azure/010_*` a `azure/020_*` | Instalación/roles del modelo SQL base. `PilotSetup.Initialize`, `PilotSetup.Permissions` y las pruebas locales los seleccionan explícitamente |
| `azure/instalar.sql`, `azure/instalar_portal.sql` | Entrada de instalación inicial con comprobación de base vacía; no actualiza un piloto con datos |
| `administracion/001_administracion.sql` | Esquema mes2 y procedimientos de administración de líneas |
| `administracion/003_usuarios.sql` | Gestión de cuentas desde el portal |
| `administracion/004_rendimiento.sql` | Índices y rendimiento de consultas mes2 |
| `administracion/002_fuentes_powerbi.sql` | Vistas BI de catálogo, ubicaciones y atenciones; administración separada de la preparación del esquema |
| `001_mvp.sql` a `013_azure_ajustes_catalogo.sql` en esta carpeta | Evoluciones anteriores aún referenciadas por herramientas locales, migración/pruebas e histórico; conservar, no ejecutar indiscriminadamente |

`AdminV2Migration` prepara 001, 003 y 004. La activación es otra operación explícita y evita volver a abrir una base activa. Las vistas 002 tienen su propia validación y conciliación; no se deduce el procedimiento de actualización solo del nombre del archivo.

Sigue el [manual de instalación](../../documentacion/MANUAL_DE_INSTALACION.md) y [actualización/recuperación](../../documentacion/administracion-v2/ACTUALIZACION_Y_RECUPERACION.md). Las credenciales administrativas son privadas; no se incluyen en estos scripts. Esta organización no ejecutó migraciones ni alteró datos.
