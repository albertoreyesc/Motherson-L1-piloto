# Piloto gratuito: Render + Azure SQL

Preparación 19/11/2026, exclusivamente datos simulados. El usuario autorizó crear copia privada `albertoreyesc/Motherson-L1-piloto` porque su cuenta tiene WRITE pero no ADMIN en `lita1-coder/Motherson-L1`. No se hace público ni se cambia el original.

## Alojamiento

Un único Web Service **Free** en Render, región Ohio, frontend React y backend .NET 10 en el mismo origen HTTPS. `render.yaml` fija `plan: free`; no crear Postgres, discos, trabajadores ni servicios adicionales. Usar la rama `piloto/preparacion-gratuita-2026-11-19`, Dockerfile `despliegue/render/Dockerfile`, contexto raíz. Health check `/health/live`. Nginx escucha el puerto de Render; Kestrel solo 127.0.0.1:8081. Render termina TLS y redirige HTTP a HTTPS. Nginx elimina encabezados reenviados del cliente y establece el esquema HTTPS de ese origen. ASP.NET solo acepta X-Forwarded-Proto del proxy loopback predeterminado, con límite 1. No se habilita confianza global en proxies. El límite de login de 100/minuto es compartido por quienes pasan por el proxy; no se confía en un X-Forwarded-For enviado por el visitante.

La suscripción Azure conserva el SQL piloto `motherson-piloto-l1-20261119.database.windows.net`, base `Motherson_L1`, gratuita, `useFreeLimit=true`, `freeLimitExhaustionBehavior=AutoPause`, 32 GB, respaldo Local. Nunca conectar el servidor original ni copiar datos de planta. El hosting no depende de una computadora ni de un túnel.

## Persistencia y secretos

Render Free pierde todo archivo al reiniciar. Se añadió un perfil **optativo** `Pilot:SqlPersistence=true`: imágenes en `mes.IMAGEN_PILOTO` (PNG/JPG, máximo 5 MB/material) y claves de Data Protection en `mes.CLAVE_PROTECCION_PILOTO`, cifradas con certificado privado. Las operaciones usan procedimientos específicos, no db_owner ni permisos generales de escritura. Los perfiles anteriores mantienen su almacenamiento de archivos. La migración `--piloto-almacenamiento RAIZ` requiere perfil piloto y rechaza el servidor original; solo añade tablas/procedimientos del piloto, sin reinstalar ni vaciar nada.

`preparar-privado.py` genera configuración/certificado local en `portal/.local/render`, permisos privados. Deben conservarse certificado PFX y contraseña fuera del repositorio y Render; perderlos impide recuperar claves y cookies previas. Antes de rotar hay que planear retención del certificado anterior y renovación de claves/sesiones. Validez inicial 730 días; no rotación silenciosa. La cookie dura 30 minutos. Nunca subir configuración privada, llaves, archivos de sesiones ni registros a Git, ZIP, imágenes Docker o frontend. El contexto Docker excluye .local, .env y certificados. No se pasan secretos como argumentos de compilación.

Variables del servicio: configuración generada en `portal/.local/render/configuracion.json`, más **AllowedHosts con el hostname exacto asignado por Render**. Usar Production, cuentas personales y CSRF; Power BI CapacityReady/AllowTrialTokens permanecen false. El JSON contiene secretos: cargar únicamente en el panel privado del servicio por un canal autorizado, nunca copiar en chat. La cuenta SQL de la aplicación es `portal_app`; `piloto_instalador` se usa solo para instalación/migración local temporal.

## Red y publicación

1. Verificar que el repositorio destino es privado y el plan del servicio es Free. Instalar la GitHub App Render únicamente en la copia del piloto; la instalación la confirma el propietario.
2. Crear el servicio con el Dockerfile y variables privadas. La primera compilación no instala bases ni ejecuta migraciones.
3. Obtener **Connect → Outbound** del servicio. Permitir únicamente esos rangos concretos en el firewall del servidor SQL piloto. Son rangos compartidos de Render, no direcciones exclusivas; no crear 0.0.0.0, AllowAzure ni 0.0.0.0–255.255.255.255. No cambiar el firewall del servidor original. Revisar los rangos si se mueve el servicio de región.
4. Eliminar la IP temporal del operador al terminar tareas administrativas. Sin secretos/destino/red correctos el backend debe fallar al arrancar; no anular controles para lograr un health check.
5. Verificar URL HTTPS real, host exacto, cookies Secure/HttpOnly/SameSite, CSRF, permisos y cierre de sesión. Probar imágenes y sesión después de reiniciar el servicio. Los 15 checks locales de `PILOTO_RENDER_PERSISTENCIA.json` usan Production HTTPS y Azure SQL; no sustituyen comprobar el hosting real.
6. Crear cuentas personales para los participantes por la consola del piloto; no compartir la cuenta del administrador ni habilitar registro anónimo. Comenzar con 5–10 personas, subir progresivamente y medir cuotas/errores. La disponibilidad para 80 usuarios no está certificada.

## Costos y límites

Objetivo USD 0/mes: un Render Web Service Free y el SQL con su oferta verificada. Mantener cuenta Render **sin método de pago**: al exceder cuota de transferencia o compilación se suspende en lugar de comprar excedentes. Si hay una tarjeta ya asociada, no desplegar hasta verificar límites de gasto que impidan cargos. No activar planes superiores ni cambiar la suscripción Azure. Render Free: 512 MB, 0.1 CPU, 750 horas gratuitas/mes compartidas por workspace; se suspende por 15 minutos de inactividad y tarda aproximadamente un minuto en despertar. Compilaciones y transferencia tienen cuota; puede suspenderse por mucho tráfico saliente, incluido SQL externo. No es producción ni servicio con disponibilidad garantizada. SQL también puede pausarse por inactividad y cuota mensual. No usar tareas para evitar la suspensión.

Power BI Embedded no se habilita sin capacidad compatible verificada y credencial nueva. Mientras tanto el Dashboard muestra su limitación y siguen disponibles inventario, historial y KPIs. No se afirma que el Dashboard esté listo ni que renovar token refresque los datos del modelo Import.

## Actualizaciones y recuperación

Render puede desplegar automáticamente los cambios de la rama conectada; el Dockerfile ejecuta las pruebas frontend antes de publicar. Las migraciones SQL siempre son un paso administrativo independiente. Conservar el commit anterior: usar Rollback a una versión anterior compatible (Free ofrece dos despliegues previos) o volver a desplegar un commit verificado. Revertir código no revierte datos; no reinstalar base ni borrar imágenes/claves al recuperar una versión. El retorno a un perfil de archivos requiere plan de exportación de imágenes/claves: no desactivar SQL persistence sin prepararlo.

## Fuentes oficiales

- https://render.com/docs/free
- https://render.com/docs/docker
- https://render.com/docs/tls
- https://render.com/docs/outbound-ip-addresses
- https://render.com/docs/compute-plans
- https://learn.microsoft.com/en-us/azure/azure-sql/database/free-tier

Pendiente al redactar: integración GitHub App en la copia privada, compilación completa del contenedor, creación del servicio, red restringida, publicación HTTPS y pruebas en Render/dispositivos físicos. No hay URL de piloto confirmada aún.
