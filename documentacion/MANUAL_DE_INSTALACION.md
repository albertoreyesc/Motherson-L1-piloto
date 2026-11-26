# Manual de instalación — Smart Material Replenishment (Motherson)

Versión del 26/11/2026 (rama `trabajo/modificaciones-v3`). Este manual explica cómo instalar el portal, cómo actualizar una instalación existente, cómo dejarlo listo para usarse y cómo pasar de datos de prueba a datos reales. Para el uso diario del portal, consulta el [Manual de usuario](MANUAL_DE_USUARIO.md).

Observación de la reunión con Motherson (16/11/2026): *"ellos deberán modificar para que corra local"*. Por eso se indica qué funciona tal como está y qué partes del código tiene que adaptar TI de Motherson para usar sus propios servidores.

Documentos relacionados: [Arquitectura y herramientas](ARQUITECTURA_Y_HERRAMIENTAS.md) · [Límites y pendientes para TI](LIMITES_Y_PENDIENTES.md) · [Actualización y recuperación](administracion-v2/ACTUALIZACION_Y_RECUPERACION.md).

**Contenido:** 1. Elige el escenario · 2. Requisitos · 3. Escenario A (una computadora) · 4. Escenario B (servidor de Motherson + Azure) · 5. Escenario C (todo en Motherson) · 6. Actualizar una instalación existente · 7. Primera puesta en marcha · 8. Pasar a datos reales · 9. Respaldo y operación · 10. Contacto

> **Estado de este manual (26/11/2026):** la compilación con Docker (paso 2) se verificó en Windows. Los demás pasos se documentaron a partir de los scripts y del código del repositorio. Ningún paso se ha ejecutado todavía en la infraestructura de Motherson; TI debe validarlos en un ambiente de pruebas antes de usarlos con datos reales.

## 1. Elige el escenario

| Escenario | Para qué sirve | ¿Requiere cambiar código? |
| --- | --- | --- |
| **A. Prueba en una computadora** | Revisar o demostrar el sistema con datos de prueba | No |
| **B. Servidor de Motherson + base Azure del piloto** | Sacar el portal de Render y alojarlo en un servidor de planta, conservando la base actual | No; solo configuración y red |
| **C. Todo en infraestructura de Motherson** (servidor y SQL Server propios) | Operación interna sin servicios externos | **Sí**: ver la sección 5 |

## 2. Requisitos comunes

- Git y acceso al repositorio privado `albertoreyesc/Motherson-L1-piloto`.
- Docker Desktop (Windows/Mac) o Docker Engine (Linux).
- Para los comandos administrativos: .NET SDK 10 y Python 3.11 o posterior.
- Para compilar sin Docker: Node.js 22.

Clonar y comprobar que todo compila, igual que en producción:

```powershell
git clone https://github.com/albertoreyesc/Motherson-L1-piloto.git
cd Motherson-L1-piloto
docker build -f despliegue/render/Dockerfile -t motherson-portal .
```

La compilación corre las pruebas del frontend, las pruebas de Azure y publica el backend .NET en Release. Si termina con `naming to docker.io/library/motherson-portal`, el código está listo.

## 3. Escenario A · Prueba en una computadora (datos de prueba)

El equipo ya tiene un procedimiento que crea una base **nueva y aislada** con datos controlados, corre las pruebas de integración y levanta el portal en `http://127.0.0.1:5099`. Funciona en Windows (PowerShell), Mac y Linux. Requiere Docker Desktop, .NET SDK 10 y Python 3.11+.

1. **SQL Server local en Docker**, accesible solo desde la propia computadora (en PowerShell, una sola línea):

   ```powershell
   docker run -d --name motherson-admin-v2-local -p 127.0.0.1:1439:1433 -e ACCEPT_EULA=Y -e MSSQL_PID=Developer -e "MSSQL_SA_PASSWORD=<contraseña-fuerte>" mcr.microsoft.com/mssql/server:2022-latest
   ```

   Espera unos 30 segundos y habilita usuarios contenidos, como en Azure SQL:

   ```powershell
   docker exec motherson-admin-v2-local /opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -P "<contraseña-fuerte>" -C -Q "EXEC sp_configure 'contained database authentication',1; RECONFIGURE;"
   ```

2. **.NET:** el script usa `portal/.local/dotnet/dotnet` si existe (Mac/Linux del equipo) o, si no, el `dotnet` instalado en el sistema.

3. **Pruebas y servidor local**, desde la raíz del repositorio:

   ```powershell
   python portal/scripts/probar_administracion_local.py --servir
   ```

   El script crea una base `MES_Integracion_Pruebas_<aleatorio>`, ejecuta las comprobaciones de `portal/pruebas-administracion` y, si pasan, abre el portal en `http://127.0.0.1:5099` con perfil Development. Cada ejecución crea una base nueva; no borra las anteriores.

4. **Entrar:** `persona0` es administrador, `persona1` operador y `persona79` lector. La contraseña temporal queda en `portal/.local/v2/user.password`.

No uses este perfil ni sus credenciales para publicar: el modo Development relaja controles pensados para producción.

## 4. Escenario B · Servidor de Motherson con la base Azure del piloto

Es el camino más corto para sacar el portal de Render sin tocar código. Se usa la **misma imagen y la misma configuración** que en Render, solo cambia el lugar donde corre el contenedor.

1. **Red:**
   - El servidor necesita salida a `motherson-piloto-<sufijo>.database.windows.net:1433`.
   - En el firewall del SQL piloto de Azure, agregar **solo** la IP pública de salida del servidor de Motherson. No usar `0.0.0.0` ni "Allow Azure services".
2. **Configuración privada:** el responsable del piloto entrega por un canal privado el archivo `portal/.local/render/configuracion.json`, que tiene la cadena de conexión de `portal_app` y el certificado de protección de claves. Agregar `AllowedHosts` con el nombre DNS interno que usará el portal (por ejemplo, `smr.motherson.local`).
3. **Ejecutar el contenedor:** convertir el JSON a un archivo `.env` con formato `CLAVE=valor`, guardado fuera del repositorio:

   ```sh
   docker run -d --name motherson-portal --restart unless-stopped \
     --env-file /ruta/privada/motherson.env -e PORT=10000 \
     -p 127.0.0.1:10000:10000 motherson-portal
   ```

4. **HTTPS obligatorio.** El contenedor espera que alguien delante termine TLS: nginx le dice a la API que la petición original fue HTTPS. Publicarlo detrás del proxy inverso corporativo (IIS con ARR, nginx, F5, etc.) con el certificado de Motherson, apuntando a `http://127.0.0.1:10000`. **No exponer el puerto 10000 directamente a la red de planta.**
5. **Verificar:**
   - `https://<nombre-interno>/health/live` responde.
   - Iniciar sesión y revisar que las cookies sean `Secure`.
   - Confirmar que se ve el inventario de la Línea 1.

Limitaciones: la base sigue siendo la gratuita de Azure (puede pausarse) y Power BI no cambia.

## 5. Escenario C · Todo en infraestructura de Motherson (requiere cambios de código)

Por seguridad, el piloto **solo acepta** como base el servidor Azure `motherson-piloto-*.database.windows.net` con la base `Motherson_L1`, y rechaza cualquier otro destino en producción. Para usar un SQL Server propio, TI debe adaptar estos puntos y conservar las protecciones:

| Archivo | Qué hace hoy | Cambio sugerido |
| --- | --- | --- |
| `portal/backend/Motherson.Api/AzurePreflight.cs` (`ExpectedServer`, `Validate`) | Exige un host con el patrón `motherson-piloto-*.database.windows.net` y la base `Motherson_L1` | Leer el servidor y la base permitidos de configuración (por ejemplo, `MVP_SQL_SERVIDOR` y `MVP_SQL_BASE`) y compararlos de forma exacta. **Mantener** `Encrypt=True`, la validación del certificado (`TrustServerCertificate=False`) y el rechazo del servidor original del equipo |
| `portal/backend/Motherson.Api/PilotHosting.cs` | Llama a `AzurePreflight.Validate` al arrancar en Production | Sin cambio si se adapta `AzurePreflight` |
| `portal/backend/Motherson.Api/PilotSetup.cs` | Instalación, permisos y usuarios pasan por `AzurePreflight` | Sin cambio si se adapta `AzurePreflight` |
| `portal/backend/Motherson.Api/AdminV2Migration.cs` (línea ~16) | Solo permite `127.0.0.1` (pruebas) o servidores `motherson-piloto-` | Aceptar el servidor configurado en el punto 1 |
| `despliegue/render/preparar-privado.py` | Arma la configuración a partir de los parámetros del despliegue Azure | Crear una variante que reciba el servidor propio y genere el certificado PFX igual que hoy |

Después de esos cambios, la instalación sería:

1. **SQL Server 2022** con certificado TLS válido para su nombre DNS. Habilitar `contained database authentication` y crear la base con `CONTAINMENT = PARTIAL`; la aplicación crea sus usuarios dentro de la base, como en Azure.
2. **Instalar el esquema y los permisos** con una cuenta administrativa temporal, ejecutando el binario publicado y montando el repositorio (rutas de ejemplo):

   ```sh
   docker run --rm --env-file /ruta/privada/instalador.env -v "$PWD":/repo \
     --entrypoint dotnet motherson-portal /app/Motherson.Api.dll --piloto-inicializar /repo
   ```

   Después, `--piloto-permisos /repo` (recibe las contraseñas de `portal_app` y del lector por la entrada estándar; ver `despliegue/piloto/inicializar.py`) y `--administracion-instalar /repo` seguido de `--administracion-activar /repo`. `--piloto-inicializar` carga los **datos simulados** de `datos/simulados`. Para operar con datos reales, cargar el catálogo de planta con la importación de Excel del portal y registrar existencias con entradas; no editar tablas a mano.
3. **Primera cuenta de administración:** `python despliegue/piloto/crear-usuario.py` (tipo ADMIN) con la conexión administrativa temporal. Al terminar, retirar esa conexión. Las demás cuentas (ADMIN, PUESTO o LECTOR) se crean después desde el portal, en la sección **Usuarios**.
4. **Portal:** igual que en el escenario B, puntos 3 a 5, con la cadena de conexión de `portal_app` hacia el SQL propio.
5. **Power BI:** cambiar el origen del informe al SQL propio. Para que Power BI Service llegue a un SQL interno hace falta un **on-premises data gateway**. Cada visitante sigue necesitando licencia.

## 6. Actualizar una instalación existente

Aplica cuando el portal ya está funcionando (por ejemplo, el piloto en Azure) y se publica una versión nueva, como la v3 de esta rama.

1. **Respaldo:** comprobar que la base tenga un punto de restauración o respaldo reciente.
2. **Bajar y compilar** la versión nueva:

   ```powershell
   git fetch
   git switch trabajo/modificaciones-v3
   git pull
   dotnet build portal\backend\Motherson.Api
   ```

3. **Actualizar el esquema SQL**, con la conexión administrativa:

   ```powershell
   dotnet run --project portal\backend\Motherson.Api -- --administracion-instalar .
   ```

   Ejecuta `001_administracion.sql`, `003_usuarios.sql` y `004_rendimiento.sql`, en ese orden. Se puede repetir y no borra datos. **No** volver a ejecutar `--administracion-activar`: la activación se hace una sola vez, en la primera instalación.
4. **Publicar** el portal como siempre, con `MVP_ADMIN_V2=true`.
5. **Verificar:** entrar con una cuenta de cada tipo (Administración, Puesto logístico y Consulta) y revisar que cada una vea solo sus secciones; crear un usuario de prueba en **Usuarios**; registrar una entrada; descargar los Excel de KPIs e Historial.
6. **Si algo falla**, no regresar a la versión anterior: frenar capturas con `UPDATE mes2.CONFIG SET solo_lectura=1 WHERE id=1` y revisar con el equipo. Detalle en [Actualización y recuperación](administracion-v2/ACTUALIZACION_Y_RECUPERACION.md).

## 7. Primera puesta en marcha

Una vez instalado, el portal no tiene catálogo ni horario en las líneas nuevas. Con una cuenta de Administración, sigue el orden de la sección 6 del [Manual de usuario](MANUAL_DE_USUARIO.md#6-administración-poner-en-marcha-una-línea):

1. Configurar línea: puestos, estaciones y estanterías.
2. Configurar línea: horario de trabajo vigente (sin él, el semáforo de cobertura muestra "Sin estimación").
3. Materiales y catálogos: importar el Excel de materiales y asignar cada material a su estantería.
4. Usuarios: crear las cuentas de cada persona, con sus líneas y su puesto.
5. Entradas y salidas: registrar las existencias iniciales con entradas. No se editan tablas a mano.

Las 4 líneas del sistema son fijas. Si la planta necesita más, ver el punto 8 de [Límites y pendientes](LIMITES_Y_PENDIENTES.md).

## 8. Pasar de datos de prueba a datos reales

- **Marca de datos simulados:** en `portal/backend/Motherson.Api/appsettings.json` cambiar `"DatosSimulados": true` a `false` (o definir la variable de entorno `DatosSimulados=false`) y reiniciar el portal. Desaparecen la etiqueta amarilla "SIMULADO", el aviso del inicio de sesión y la marca de la API, y el pie del menú dice "Datos de planta". No toca la base ni el código.
- **Horario de prueba:** en *Configurar línea → Horario de trabajo*, capturar el horario aprobado por planta y desmarcar **Es un horario de prueba**.
- **Datos de prueba:** una instalación hecha con `--piloto-inicializar` trae datos simulados. Para operar con datos reales conviene una base nueva, cargando el catálogo con la importación de Excel y las existencias con entradas.

## 9. Respaldo y operación

- Respaldar la base con el mecanismo estándar de SQL Server (completo diario y de logs, según la política de TI). Toda la información vive en SQL.
- Guardar el certificado PFX y su contraseña fuera del servidor. Si se pierden, se invalidan las sesiones y claves anteriores.
- Para detener capturas sin apagar el portal: `UPDATE mes2.CONFIG SET solo_lectura=1 WHERE id=1`.
- Procedimiento de actualización y recuperación: `documentacion/administracion-v2/ACTUALIZACION_Y_RECUPERACION.md`.

## 10. Contacto del proyecto

Equipo del portal (UPAEP). Dudas sobre el portal: Alejandro Lita y Alberto Reyes. Dudas sobre el informe Power BI: Equipo 2.
