# Piloto de planta: preparación gratuita, todavía sin publicar

Fecha: 19/11/2026. Aproximadamente 80 participantes; tabletas y PC; duración y presupuesto sin definir. El usuario pidió exclusivamente recursos gratuitos. No hay restricciones de red comunicadas; todavía debe validarlo TI. No se envió ningún mensaje a TI ni se otorgó acceso a terceros.

## Propuesta concreta para aprobación

- Repositorio privado existente, rama separada del piloto; nunca publicar secretos.
- Grupo NUEVO `rg-motherson-piloto-<sufijo>` y servidor NUEVO `motherson-piloto-<sufijo>.database.windows.net`.
- Base `Motherson_L1` en ese servidor nuevo. El nombre se conserva para compatibilidad de scripts; el servidor y las cuentas son independientes. El perfil rechaza el servidor actual `motherson-l1-equipo.database.windows.net`.
- App Service Windows F1, una instancia compartida, aplicación y frontend bajo el mismo origen HTTPS. Se eligió Windows/IIS para utilizar la integración restringida de encabezados reenviados de ASP.NET Core Module; no habilitar confianza en proxies arbitrarios.
- SQL General Purpose serverless con oferta gratuita, `useFreeLimit=true`, `freeLimitExhaustionBehavior=AutoPause`, máximo 32 GB y respaldo local. Si la oferta no está disponible en la suscripción/región, DETENER; no sustituir por SQL de pago ni usar la base actual.
- SQL se carga únicamente desde los ocho CSV simulados sellados incluidos. No copiar la base actual ni importar el archivo privado de 1.45 millones de registros. El piloto tendrá sus propias capturas e historial; las atenciones simuladas sí forman parte del juego CSV.
- Dashboard desactivado hasta tener capacidad compatible y credencial nueva. Inventario, historial y KPIs continúan disponibles. La prueba local anterior con tokens de evaluación no acredita licencia para publicar este piloto.
- Acceso inicial solo desde la IP del operador. Tras crear cuentas personales y aprobar apertura, habilitar IP/CIDR acordados con TI o acceso por Internet con autenticación del portal. No retirar la restricción antes de tener participantes provisionados.

## Costos y límites consultados

Estimación de recursos propuestos: App Service F1 USD 0/mes y SQL USD 0/mes **si se aplica la oferta y se mantiene AutoPause**. Sin dominio comprado, Key Vault, Application Insights, Log Analytics, almacenamiento adicional ni capacidad de Power BI. Se usan App Settings privados y el almacenamiento incluido. No se configuran servicios facturables ni escalado automático.

F1 ofrece CPU compartida, 60 minutos de CPU al día, 1 GB de RAM y 1 GB de almacenamiento; no es un servicio con SLA para producción. SQL gratuito incluye 100,000 segundos de vCore por mes, 32 GB de datos y 32 GB de respaldo por base; puede pausarse por inactividad o por agotamiento mensual. Las cuotas pueden impedir pruebas continuas. Estos límites no equivalen a un número garantizado de usuarios. Comenzar con 5–10 participantes, después 20 y finalmente una prueba acordada de hasta 80; medir tiempos, errores, cuotas y uso de CPU. Nunca cambiar a pago para superar un límite sin aprobación.

Fuentes oficiales consultadas el 19/11/2026:
- https://azure.microsoft.com/en-us/pricing/details/app-service/windows/
- https://learn.microsoft.com/en-us/azure/azure-sql/database/free-tier
- https://learn.microsoft.com/en-us/azure/azure-sql/database/free-offer-faq
- https://learn.microsoft.com/en-us/aspnet/core/host-and-deploy/proxy-load-balancer?view=aspnetcore-10.0
- https://learn.microsoft.com/en-us/power-bi/developer/embedded/move-to-production

## Inspección y cambios

`Pilot:Enabled=true` activa cuentas personales en `mes.USUARIO_PILOTO`: ADMIN, PUESTO y LECTOR. Varias personas pueden pertenecer al mismo puesto. Cada movimiento conserva su usuario personal; se valida el puesto en SQL. Las cuentas se crean por consola administrativa privada, nunca con un endpoint de registro público. El perfil bloquea `admin.local` compartido. Cambiar hash o desactivar la persona invalida la sesión al consultarla de nuevo. Las cuentas de la instalación actual conservan su comportamiento cuando Pilot está desactivado.

La administración antigua de acceso por puesto se rechaza en el piloto. Aprovisionar cada persona con `crear-usuario.py`; no utilizar el formulario antiguo de acceso por puesto. El lector puede consultar inventario pero no capturar ni acceder al historial/Dashboard globales. El administrador puede ajustar catálogos, capacidades e imágenes. Las atenciones de REGISTRO siguen identificando el puesto; la identidad personal queda registrada en los movimientos mes, no se añade retroactivamente a registros históricos.

Cookies HttpOnly, Secure y SameSite Strict en Production; CSRF en todos los POST; permisos revalidados por petición; sesión de 30 minutos. La cuota de login del piloto es 100 solicitudes por minuto/IP para permitir un turno de 80 personas detrás de una misma salida NAT (la instalación normal mantiene 10). Las contraseñas se guardan como hash ASP.NET Identity. El estado SQL requiere sesión; `/health/live` solo confirma proceso vivo, no revela la base ni consulta SQL. Se permite un reintento acotado del error SQL 40613 al reanudar serverless; no se repiten mutaciones ni se ocultan otros errores.

No hay migración al arrancar el servidor. `--piloto-inicializar` se ejecuta aparte y rechaza tablas preexistentes. Instala esquema y apertura con CSV simulados. Si la carga falla tras crear esquema, no vuelva a instalar ni borre nada: diagnosticar y usar el cargador `--cargar-demo ... --aplicar` solo si la base sigue vacía y sin apertura; luego continuar permisos.

## Archivos persistentes

| Archivo o estado | Piloto publicado |
|---|---|
| Claves de sesión/Data Protection | `D:\home\motherson-piloto\keys` mediante `MVP_KEYS_PATH` |
| Imágenes PNG/JPG de materiales | `D:\home\motherson-piloto\imagenes` mediante `MVP_IMAGES_PATH`, fuera de wwwroot |
| Metadatos documentales | `config` del paquete publicado, como respaldo si no hay copia en MVP_ROOT |
| Movimientos, ajustes, UUID y respuestas | SQL `mes`, persistencia transaccional |
| Cuentas personales/hash/rol/puesto | SQL `mes.USUARIO_PILOTO` |
| Claves SQL y Power BI | App Settings privados; no archivos públicos |
| JSONL y paquete histórico local | No se usan en modo SQL transaccional |
| Archivos temporales de imagen | Misma carpeta privada; monitorizar espacio y limpiar solo temporales abandonados |

El disco de publicación puede ser de solo lectura con Run From Package. Las rutas de datos son distintas. F1 tiene 1 GB compartido: imágenes, claves, paquetes y registros compiten por espacio. Limitar logs y revisar cuota antes de un piloto largo. La persistencia de disco no sustituye un respaldo: exportar fotos y claves antes de cambios, por un canal privado aprobado. Las claves no se cifran con un certificado propio en esta propuesta gratuita; se protegen por permisos y almacenamiento de App Service. No distribuirlas.

## Preparación local (no crea recursos)

Requisitos: .NET SDK 10, Node 22.12 o superior compatible y Python 3. En Windows use `python` donde se indica `python3`.

```sh
python3 despliegue/piloto/publicar-local.py
```

Produce `portal/.local/piloto-build/piloto.zip` sin secretos. Incluye frontend compilado, backend, web.config y metadatos. Validar Bicep con `az bicep build --file despliegue/piloto/main.bicep`. La compilación de Bicep ya se verificó localmente; **no** sustituye la validación ni el what-if de Azure.

## Creación, solo después de aprobación

1. Confirmar suscripción/tenant, elegibilidad SQL gratuita, cuota F1 y región de la oferta (la instalación anterior usa Canada Central). Usar Azure CLI autenticado localmente. No se encontró Azure CLI ni GitHub CLI en esta máquina al preparar la entrega.
2. Ejecutar `python3 despliegue/piloto/parametros.py`. Crea contraseñas nuevas aleatorias en archivo privado con permisos 600, nunca en Git. Revisión de facturación debe mostrar cero. No reutilizar claves del portal actual.
3. Crear únicamente el grupo nuevo y revisar el plan:

```sh
az group create --name rg-motherson-piloto-SUFIJO --location canadacentral
az deployment group what-if --resource-group rg-motherson-piloto-SUFIJO --template-file despliegue/piloto/main.bicep --parameters @portal/.local/piloto-despliegue/parametros.json
```

El what-if debe incluir solo recursos del grupo nuevo, plan F1 y SQL con `useFreeLimit=true`/`AutoPause`. No ejecutar si aparece el servidor original, un SKU de pago, almacenamiento externo o cobro por excedentes. El aviso de ARM/Bicep sobre sufijo de nube se debe a que el perfil valida Azure público `.database.windows.net` explícitamente; no está preparado para nubes soberanas.
4. Con la aprobación correspondiente:

```sh
az deployment group create --resource-group rg-motherson-piloto-SUFIJO --template-file despliegue/piloto/main.bicep --parameters @portal/.local/piloto-despliegue/parametros.json
```

5. Verificar `sku.name=F1`, `useFreeLimit=true`, `freeLimitExhaustionBehavior=AutoPause` y respaldo Local leyendo los recursos creados. Si la verificación falla, no publicar ni continuar. Ningún script cambia automáticamente a un SKU de pago.
6. Desde la IP exacta del operador, ejecutar `python3 despliegue/piloto/inicializar.py`. Usa configuración administrativa en memoria para el servidor NUEVO; crea la base de aplicación, los CSV simulados y las cuentas SQL restringidas. `portal_app` no recibe db_owner ni actualización directa de saldos. La credencial administrativa nunca se coloca en la Web App.
7. Aprovisionar un administrador personal y participantes con `crear-usuario.py`. Requiere variables privadas `Pilot__Enabled=true`, `MVP_AZURE_SERVER=<nuevo-host>`, `MVP_BASE_COMPARTIDA=Motherson_L1`, y `ConnectionStrings__Sql` administrativa temporal del nuevo servidor. Cargar la conexión desde el archivo privado a la sesión del proceso; no escribir claves en comandos o capturas. Revocación: sesión administrativa del piloto, `UPDATE mes.USUARIO_PILOTO SET activo=0 WHERE usuario=@usuario` con parámetro. Restablecer contraseña exige generar hash Identity nuevo; no guardar texto plano ni reutilizar hash entre personas.
8. Aplicar las IP exactas de salida de la aplicación con `python3 despliegue/piloto/firewall-sql.py rg-motherson-piloto-SUFIJO SUFIJO`. No crear `AllowAllWindowsAzureIps`, regla 0.0.0.0 ni rangos globales. Quitar `OperadorTemporal` de SQL al terminar la instalación. Si cambian IP de salida tras mover la app o cambiar plan, revisar antes de reconectar.
9. Publicar el ZIP y leer el hostname real; no inventar una URL:

```sh
az webapp deploy --resource-group rg-motherson-piloto-SUFIJO --name motherson-piloto-SUFIJO --src-path portal/.local/piloto-build/piloto.zip --type zip
az webapp show --resource-group rg-motherson-piloto-SUFIJO --name motherson-piloto-SUFIJO --query defaultHostName -o tsv
```

Usar HTTPS. Probar arranque frío, encabezados reenviados bajo IIS, cookies Secure, CSRF, lectura SQL, cierre de sesión, persistencia de fotos y sesión tras reinicio. Esas verificaciones del alojamiento real están pendientes. No habilitar redirecciones o confianza global en proxies para sortear errores.
10. Abrir acceso a los participantes solo después de validar credenciales individuales y acordar acceso con TI. La plantilla deja denegado el tráfico ajeno a la IP del operador. No se coordinó aún con TI porque no hay contacto ni instrucción para enviar mensajes.

## GitHub privado y despliegue automático

El repositorio indicado en la conversación es `https://github.com/lita1-coder/Motherson-L1`. Su privacidad y acceso deben comprobarse antes de empujar; el checkout actual no tiene remoto configurado. No se creó ni se publicó un repositorio durante esta preparación.

Flujo `.github/workflows/piloto.yml`: compila y prueba en PR; despliega solo mediante `workflow_dispatch` con `deploy=true`, environment `piloto` y OIDC. Configurar en GitHub aprobación requerida del environment si está disponible en su plan; si no, restringir quién puede lanzar el workflow. Repositorio privado gratuito tiene cuota de Actions: al agotarse, detener las ejecuciones o compilar localmente; no habilitar gasto adicional.

Crear una identidad de despliegue distinta de la identidad de Power BI, con federación:
- Issuer `https://token.actions.githubusercontent.com`.
- Subject `repo:lita1-coder/Motherson-L1:environment:piloto`.
- Audience `api://AzureADTokenExchange`.
- Rol **Website Contributor** limitado al recurso Web App del piloto. No dar Owner/Contributor a toda la suscripción.
- Variables del environment: AZURE_CLIENT_ID, AZURE_TENANT_ID, AZURE_SUBSCRIPTION_ID y AZURE_WEBAPP_NAME. No agregar un secreto de aplicación a GitHub.

El workflow solo publica código. No instala SQL, crea cuentas, modifica capacidad, secretos ni firewall. En caso de fallo de prueba no despliega. No subir `.local`, `.env`, backups, claves, fotos privadas ni los parámetros seguros.

## Recuperación y puesta en marcha

F1 no usa slots de despliegue. Antes de una actualización conservar el ZIP anterior, hash SHA256 y etiqueta/commit. Para revertir código, detener capturas, volver a publicar ese ZIP y validar login/lectura. No volver a ejecutar instalación SQL ni restablecer tablas. Revertir una versión que no soporte el esquema actual requiere análisis previo; no prometer downgrade de datos automático.

Respaldar fotos y claves privadas por separado. SQL gratuito ofrece recuperación con restricciones: no confundir un ZIP de código con respaldo SQL ni prometer restauración directa a otra base gratuita. Al cerrar el piloto, exportar lo que se deba conservar antes de eliminar recursos; la eliminación requiere instrucción expresa.

Instrucciones para participantes: abrir la URL HTTPS autorizada, iniciar sesión con su usuario personal, elegir su sección y capturar solo datos de prueba. No compartir contraseñas. Ante operación pendiente, reintentar con el mismo comprobante/UUID del cliente; no duplicar manualmente. Si el sitio se pausa por cuotas, avisar al responsable y esperar; no cambiar el plan. El Dashboard explicará su pendiente mientras no exista capacidad.

Checklist con TI: acceso HTTPS 443, DNS del hostname real, navegador actualizado en tabletas y PC, salida a Power BI solo si se habilita después, revisión de proxy corporativo/cookies, participantes y roles, horario/duración, contacto de incidencias y decisión sobre datos a conservar. Aunque no se hayan comunicado restricciones, estas comprobaciones están pendientes.

## Evidencia y pendientes

Ver `documentacion/evidencia-integracion/PILOTO_LOCAL.json` y `PILOTO_HTTP_LOCAL.json`. La prueba de 80 sesiones es de inventario en un servidor y SQL locales, no reproduce las cuotas de F1 ni la latencia de planta. La prueba no certifica 80 capturas simultáneas ni 80 usuarios durante una jornada.

Pendiente: aprobación de publicación; disponibilidad gratuita real y región; repositorio privado y OIDC; rotar secreto expuesto si se habilita Power BI; habilitar cuentas de personas reales; pruebas de dispositivos físicos; TLS/cookies y persistencia en Azure; prueba de carga progresiva en F1; Dashboard y renovación con capacidad autorizada. No existe todavía una URL de piloto publicada.


### Resultado local de esta preparación

- 39 pruebas de frontend aprobadas y compilación de producción correcta.
- 25 comprobaciones de destino/cifrado/Power BI aprobadas.
- 19 verificaciones del perfil piloto y SQL local nuevo aprobadas.
- 27 comprobaciones HTTP aprobadas, incluyendo consulta/exportación de KPIs con corte válido, ajustes, imágenes, permisos y concurrencia.
- 3 pruebas de navegador aprobadas en tamaños PC 1440×960, tableta 1024×768 y teléfono 390×844; sin desbordamiento horizontal ni errores JavaScript en los recorridos comprobados. Son tamaños emulados, no dispositivos físicos de planta.
- Sesión, imagen privada y saldo de 10,474 piezas conservados tras reiniciar el backend local.
- Prueba de 80 sesiones distintas: inventario concurrente, p95 0.178 s y máximo 0.188 s en la máquina local; no extrapolar al alojamiento gratuito.
- Bicep compilado y artefacto .NET Release generado sin errores; validación/what-if remotos pendientes.

La importación Excel mantiene sus pruebas de frontend y se ejerció la aplicación de cambios de catálogo por HTTP. Falta una importación manual de un archivo en los dispositivos reales. No se ha comprobado TLS, persistencia ni rendimiento del App Service remoto porque no se ha desplegado.
