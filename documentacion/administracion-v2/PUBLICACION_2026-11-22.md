# Publicación del 22/11/2026

## Resultado

Migración instalada y activada en la base aislada del piloto. Render sirve la administración v2 con `MVP_ADMIN_V2=true` y código `15ed03e3e8aaf84c8e2889f881b3b57d771da844`. Despliegue de activación: `dep-dc1ptb1ksfps739j65cg`.

- Portal: https://motherson-l1-piloto.onrender.com/
- Repositorio privado: https://github.com/albertoreyesc/Motherson-L1-piloto
- Rama publicada: `piloto/preparacion-gratuita-2026-11-19`.
- Rama de trabajo: `mejoras/administracion-cuatro-lineas-2026-11-22`.
- Base destino: `motherson-piloto-l1-20261119.database.windows.net` / `Motherson_L1`. La base original del equipo no se modificó.

## Conciliación y comprobaciones

La activación comprobó 85 materiales y 10,464 piezas. La inspección posterior confirmó 85 ubicaciones, 24 operaciones previas, 58,422 registros históricos y 2 imágenes conservados. Las líneas 2, 3 y 4 tienen cero materiales y cero piezas; no se duplicó el inventario de Línea 1.

Verificación HTTPS del servicio publicado: salud 200, API anónima 401, cuenta de planta autenticada, cuatro líneas accesibles, saldo de Línea 1 de 10,464 piezas y protección CSRF 400 sin token. La Hoja 2 devolvió 9,254 atenciones completadas para 03/02/2026–31/03/2026; las otras líneas devolvieron cero. No se generaron movimientos ni atenciones ficticias para comprobar la publicación.

El acceso temporal de mantenimiento SQL se retiró al finalizar. Permanecen exclusivamente las dos reglas existentes de salida de Render (`RenderOhio50` y `RenderOhio58`). Se conserva el alojamiento gratuito y la configuración gratuita de Azure; no se contrataron recursos.

Antes de activar SQL, un intento anticipado de habilitar la opción de Render fue rechazado por la comprobación de coherencia al iniciar. El servicio anterior permaneció disponible y se restableció la opción. La transición final se realizó después de conciliar SQL y se verificó por HTTPS.

Las 118 pruebas automatizadas previas y su alcance están en [PRUEBAS.md](PRUEBAS.md). La conciliación privada contiene el estado anterior; no se incluyen credenciales ni datos administrativos privados en Git.

La revisión visual del portal publicado confirmó una única sección principal de alarmas, 10,464 piezas y el cambio a Línea 2 vacía y de regreso a Línea 1. Evidencia: [Resumen publicado](../evidencia-integracion/publicacion-2026-11-22/resumen-publicado.jpg). Si una pestaña conserva la interfaz anterior, utilizar recarga completa (macOS: Cmd + Shift + R).

## Uso y límites pendientes

Consultar el [Manual de usuario](../MANUAL_DE_USUARIO.md) para importación flexible, inhabilitación y eliminación controlada, atenciones, calendario, KPIs y selección de líneas. La cobertura temporal requiere calendario autorizado y consumos suficientes; en su ausencia muestra **Sin estimación**. Las nuevas líneas requieren configurar sus propios catálogos y ubicaciones.

El informe Power BI existente de Línea 1 se conserva. La actualización del modelo para utilizar las nuevas fuentes SQL y la licencia/acceso Microsoft de cada participante son trabajos independientes pendientes. Recargar el informe no actualiza por sí solo su modelo desde SQL.

## Colaboración

El mapa de carpetas y la preparación del entorno están en [CONTRIBUTING.md](../../CONTRIBUTING.md). El compañero requiere invitación al repositorio privado con su propia cuenta GitHub; no se publicaron credenciales ni se cambió la visibilidad del repositorio.
