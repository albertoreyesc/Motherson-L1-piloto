# Piloto gratuito: Render + Azure SQL

Preparación 19/11/2026, exclusivamente datos simulados. El usuario autorizó crear copia privada `albertoreyesc/Motherson-L1-piloto` porque su cuenta tiene WRITE pero no ADMIN en `lita1-coder/Motherson-L1`. No se hace público ni se cambia el original.

## Alojamiento

Un único Web Service **Free** en Render, región Ohio, frontend React y backend .NET 10 en el mismo origen HTTPS. `render.yaml` fija `plan: free`; no crear Postgres, discos, trabajadores ni servicios adicionales. Usar la rama `piloto/preparacion-gratuita-2026-11-19`, Dockerfile `despliegue/render/Dockerfile`, contexto raíz. Health check `/health/live`. Nginx escucha el puerto de Render; Kestrel solo 127.0.0.1:8081. Render termina TLS y redirige HTTP a HTTPS. Nginx elimina encabezados reenviados del cliente y establece el esquema HTTPS de ese origen. ASP.NET solo acepta X-Forwarded-Proto del proxy loopback predeterminado, con límite 1. No se habilita confianza global en proxies. El límite de login de 100/minuto es compartido por quienes pasan por el proxy; no se confía en un X-Forwarded-For enviado por el visitante.

La suscripción Azure conserva el SQL piloto `motherson-piloto-l1-20261119.database.windows.net`, base `Motherson_L1`, gratuita, `useFreeLimit=true`, `freeLimitExhaustionBehavior=AutoPause`, 32 GB, respaldo Local. Nunca conectar el servidor original ni copiar datos de planta. El hosting no depende de una computadora ni de un túnel.

## Persistencia y secretos

Render Free pierde todo archivo al reiniciar. Se añadió un perfil **optativo** `Pilot:SqlPersistence=true`: imágenes en `mes.IMAGEN_PILOTO` (PNG/JPG, máximo 5 MB/material) y claves de Data Protection en `mes.CLAVE_PROTECCION_PILOTO`, cifradas con certificado privado. Las operaciones usan procedimientos específicos, no db_owner ni permisos generales de escritura. Los perfiles anteriores mantienen su almacenamiento de archivos. La migración `--piloto-almacenamiento RAIZ` requiere perfil piloto y rechaza el servidor original; solo añade tablas/procedimientos del piloto, sin reinstalar ni vaciar nada.

`preparar-privado.py` genera configuración/certificado local en `portal/.local/render`, permisos privados. Deben conservarse certificado PFX y contraseña fuera del repositorio y Render; perderlos impide recuperar claves y cookies previas. Antes de rotar hay que planear retención del certificado anterior y renovación de claves/sesiones. Validez inicial 730 días; no rotación silenciosa. La cookie dura 30 minutos. Nunca subir configuración privada, llaves, archivos de sesiones ni registros a Git, ZIP, imágenes Docker o frontend. El contexto Docker excluye .local, .env y certificados. No se pasan secretos como argumentos de compilación.

Variables del servicio: configuración generada en `portal/.local/render/configuracion.json`, más **AllowedHosts con el hostname exacto asignado por Render**. Usar Production, cuentas personales y CSRF; Power BI CapacityReady/AllowTrialTokens permanecen false. Para el informe original se usa PowerBI__Mode=UserOwnsData y MVP_POWERBI_URL con el enlace seguro obtenido en Power BI > File > Embed report > Website or portal; cada visitante necesita su propia cuenta Microsoft autorizada y licencia vigente. El JSON contiene secretos: cargar únicamente en el panel privado del servicio por un canal autorizado, nunca copiar en chat. La cuenta SQL de la aplicación es `portal_app`; `piloto_instalador` se usa solo para instalación/migración local temporal.

## Red y publicación

1. Verificar que el repositorio destino es privado y el plan del servicio es Free. Instalar la GitHub App Render únicamente en la copia del piloto; la instalación la confirma el propietario.
2. Crear el servicio con el Dockerfile y variables privadas. La primera compilación no instala bases ni ejecuta migraciones.
3. Obtener **Connect → Outbound** del servicio. Permitir únicamente esos rangos concretos en el firewall del servidor SQL piloto. Son rangos compartidos de Render, no direcciones exclusivas; no crear 0.0.0.0, AllowAzure ni 0.0.0.0–255.255.255.255. No cambiar el firewall del servidor original. Revisar los rangos si se mueve el servicio de región.
4. Eliminar la IP temporal del operador al terminar tareas administrativas. Sin secretos/destino/red correctos el backend debe fallar al arrancar; no anular controles para lograr un health check.
5. Verificar URL HTTPS real, host exacto, cookies Secure/HttpOnly/SameSite, CSRF, permisos y cierre de sesión. Probar imágenes y sesión después de reiniciar el servicio. Los 15 checks locales de `PILOTO_RENDER_PERSISTENCIA.json` usan Production HTTPS y Azure SQL; no sustituyen comprobar el hosting real.
6. Crear cuentas personales para los participantes por la consola del piloto; no compartir la cuenta del administrador ni habilitar registro anónimo. Comenzar con 5–10 personas, subir progresivamente y medir cuotas/errores. La disponibilidad para 80 usuarios no está certificada.

## Costos y límites

Objetivo USD 0/mes: un Render Web Service Free y el SQL con su oferta verificada. Mantener cuenta Render **sin método de pago**: al exceder cuota de transferencia o compilación se suspende en lugar de comprar excedentes. Si hay una tarjeta ya asociada, no desplegar hasta verificar límites de gasto que impidan cargos. No activar planes superiores ni cambiar la suscripción Azure. Render Free: 512 MB, 0.1 CPU, 750 horas gratuitas/mes compartidas por workspace; se suspende por 15 minutos de inactividad y tarda aproximadamente un minuto en despertar. Compilaciones y transferencia tienen cuota; puede suspenderse por mucho tráfico saliente, incluido SQL externo. No es producción ni servicio con disponibilidad garantizada. SQL también puede pausarse por inactividad y cuota mensual. No usar tareas para evitar la suspensión.

El acceso AppOwnsData mediante tokens del backend no se habilita sin capacidad compatible verificada. El informe original se puede consultar con sesión Microsoft (UserOwnsData / enlace seguro reportEmbed), sin secretos en Render ni tokens de evaluación del backend. Power BI exige permisos y licencia individual o capacidad compatible para cada visitante. La cuenta actual muestra evaluación con 59 días restantes al 19/11/2026; no acredita licencias corporativas. No se activó otra evaluación ni se contrató capacidad. El modelo Import sigue en la base original; las capturas del piloto no se sincronizan con ese informe. Recargar el iframe no actualiza el modelo.

## Actualizaciones y recuperación

Render puede desplegar automáticamente los cambios de la rama conectada; el Dockerfile ejecuta las pruebas frontend antes de publicar. Las migraciones SQL siempre son un paso administrativo independiente. Conservar el commit anterior: usar Rollback a una versión anterior compatible (Free ofrece dos despliegues previos) o volver a desplegar un commit verificado. Revertir código no revierte datos; no reinstalar base ni borrar imágenes/claves al recuperar una versión. El retorno a un perfil de archivos requiere plan de exportación de imágenes/claves: no desactivar SQL persistence sin prepararlo.

## Fuentes oficiales

- https://render.com/docs/free
- https://render.com/docs/docker
- https://render.com/docs/tls
- https://render.com/docs/outbound-ip-addresses
- https://render.com/docs/compute-plans
- https://learn.microsoft.com/en-us/azure/azure-sql/database/free-tier

## Estado publicado

- URL comprobada por HTTPS: **https://motherson-l1-piloto.onrender.com/**.
- Servicio Render `srv-dbvju1c0916s73bckl90`, Web Service Docker **Free**, Ohio. Billing comprobado: workspace Hobby, sin tarjeta registrada y cargos no facturados USD 0.00 al revisar. Repositorio privado `albertoreyesc/Motherson-L1-piloto`, rama `piloto/preparacion-gratuita-2026-11-19`. La GitHub App se instaló únicamente en ese repositorio con confirmación del usuario.
- Host permitido exacto `motherson-l1-piloto.onrender.com`. SQL solo permite los rangos de salida mostrados por este servicio: `74.220.50.0/24` y `74.220.58.0/24`. Son compartidos por Render Ohio. El primer arranque falló por firewall antes de crear esas reglas; el siguiente desplegó correctamente sin anular TLS ni autorización.
- Contenedor Linux amd64 construido y ejecutado localmente con límite de 512 MB. Ocho comprobaciones aprobadas (`PILOTO_RENDER_CONTENEDOR.json`). Production HTTPS con certificado verificado y persistencia SQL: 15 checks (`PILOTO_RENDER_PERSISTENCIA.json`).
- Comprobaciones HTTP en la URL real: 24 iniciales, consulta/exportación de KPIs y rechazo de las dos cuentas temporales al desactivarlas. Ver `PILOTO_RENDER_HTTP.json`. No fue una prueba de carga de 80 usuarios.
- La cuenta personal administradora inicial es `alberto.reyes`; credencial únicamente en el archivo local privado `portal/.local/piloto-despliegue/acceso-alberto.json`. No distribuir esa cuenta entre participantes. Las cuentas `verificacion.lector` y `verificacion.puesto` se desactivaron al terminar el ensayo.
- Se añadió `--piloto-revocar`: recibe por stdin un array JSON de usuarios, usando conexión administrativa temporal del servidor piloto. Actualiza solo `activo=0`, sin borrar historia; no existe endpoint público para dar de alta ni revocar cuentas.
- La interfaz del piloto ya no ofrece el formulario antiguo de cuenta compartida por puesto. El resto de perfiles conserva su comportamiento. Los roles siguen comprobándose en cada petición.

Pendientes de verificar antes de ampliar participantes: dispositivos físicos y red de planta, carga progresiva (no se certifica capacidad para 80), lista real de participantes/roles, capacidad y renovación de Power BI si se decide habilitarlo. La selección automatizada de archivo Excel en Brave requiere acceso a archivos de la extensión y no se habilitó ese permiso; lectura/aplicación por frontend y ajustes por HTTP tienen pruebas, pero queda la importación manual en dispositivos reales. El frontend conserva advertencia de tamaño del módulo ExcelJS y dos alertas moderadas de dependencias detectadas por npm; no se aplicaron cambios de versión forzados en este despliegue.

Redeploy real verificado: commit `52cecf3`, disparado automáticamente desde GitHub; conservó sesión anterior, imagen SQL y 10,474 piezas. La imagen temporal se retiró. Ver `PILOTO_RENDER_REINICIO.json`. Se eliminó el firewall temporal del operador; solo quedan los dos rangos Render. Build Filters reales ignoran `documentacion/**` y `**/*.md` para evitar consumir compilaciones por actualizar informes; los cambios de código siguen publicándose automáticamente.

### Revisión visual remota

Acceso desde Brave en la URL real comprobado en tamaños PC 1440×960, tableta 1024×768 y teléfono 390×844. No se observó desbordamiento horizontal ni alertas visibles en las vistas comprobadas. Son ventanas emuladas, no dispositivos físicos. La cuenta del administrador siguió autenticada al recargar después del despliegue.

### Credencial de Power BI

Se reemplazó la credencial compartida en el chat: la nueva obtuvo token de aplicación HTTP 200 y la anterior se eliminó de Entra. Evidencia sin secretos en `POWERBI_ROTACION_PRIVADA.json`. La nueva está únicamente en configuración privada local y no se cargó en Render. El backend local que estuviera ejecutándose con la credencial anterior debe reiniciarse para leer la nueva. El piloto publicado mantiene Power BI desactivado: esto no verifica capacidad, visualización ni renovación del informe.
