# Actualización de plataforma, histórico y Power BI

## Plataforma actualizada

Se comparó Motherson-L1-main (3).zip con el ZIP (2) y se integraron sus siete
archivos de código/estilos nuevos o modificados sobre la rama integracion/azure-propia.
Los recursos compilados se regeneraron. Se conservaron SQL transaccional,
idempotencia, permisos, claves privadas y apertura del Azure propio.

La navegación muestra KPIs y Dashboard. Se adaptó la pantalla nueva a la
infraestructura propia: no toma por defecto el informe del tenant del otro equipo.

## Histórico

Carga aplicada y conciliada en Azure el 19/11/2026 a las 01:29:38, hora de
Ciudad de México: **1,456,098 registros** en el archivo SQL, con sus 17 archivos o
representaciones confirmados. La autorización quedó registrada antes de ejecutar.
No se crearon tablas, cuentas ni permisos nuevos en esta fase.

La repetición en Azure insertó **cero filas nuevas**. Se conservaron las diez
operaciones existentes (seis movimientos y cuatro operaciones de ajuste), todos
los saldos/versiones del inventario y las **10,474 piezas**.

El historial consultable reúne **230,993 movimientos**: 230,976 del maestro,
11 de los diarios y seis capturas nuevas. Los otros registros brutos se conservan
como evidencia con su archivo/hash/línea; no se suman todos como operaciones
independientes ni se vuelve a aplicar su saldo al inventario actual.

Después de reiniciar el backend, las páginas uno y dos del historial devolvieron
50 filas cada una y se verificó presencia de fuentes antiguas y nuevas. La primera
consulta se midió en 5.98 segundos. Se comprobó visualmente el total en el portal.
Las 19 verificaciones SQL finales pasaron con las cuentas reales de aplicación y
lector. Esta comprobación sí usa Azure SQL real.

El PBIX preparado conserva sus fuentes operativas dbo y las vistas previstas;
archivar evidencia no cambia automáticamente el modelo de Power BI ni mezcla
espacios de identificadores históricos con ubicaciones operativas.

El ensayo completo local aprobó cuatro comprobaciones: 1,456,098 registros,
conteos por fuente, 230,987 movimientos normalizados y existencias sin cambios.
Otro ensayo sobre el mismo conjunto confirmó cero filas nuevas al repetir la carga.
Ambos usaron SQL Server 2022 real; se guardó la evidencia y se retiró la base
desechable. Los archivos originales se conservaron.

El usuario aprobó expresamente la carga histórica descrita en
APROBACION_CARGA_HISTORICA.md. Los registros proceden de la copia del 12/11/2026;
no se atribuye al ZIP (3) una actividad privada que no contiene.

## Power BI

Publicación BLOQUEADA: el usuario confirmó que no dispone de una cuenta de trabajo
ni escuela. No hay espacio/capacidad propios acreditados. No se creó ni contrató
ninguno. No se publicó un informe ni se concedieron permisos a nuevas aplicaciones.

La URL del otro equipo abre un iframe /reportEmbed que muestra “Sign in to view
this report”. Su tenant e informe son ajenos al destino propio. Sirve de referencia;
no transfiere el modelo, la propiedad, credenciales ni acceso sin sesión.

Se preparó AppOwnsData: autorización del portal y CSRF en el endpoint, consulta del
informe específico, token temporal limitado y de solo lectura, y renovación en el
navegador. No se entrega al navegador el secreto ni el token de Microsoft Entra.
El modo sigue desactivado hasta configurar cuenta, espacio, identidad y capacidad.
El lector SQL permanece powerbi_lector; no se cambiaron sus permisos.

## Pruebas de esta actualización

- Frontend: compilación y 39 pruebas aprobadas.
- C#: compilación sin advertencias/errores y 23 comprobaciones de configuración,
  cifrado y contrato Power BI. Las respuestas REST de Power BI fueron simuladas.
- Backend real conectado a Azure: siete comprobaciones aprobadas sobre sesión,
  CSRF, disponibilidad/configuración, falta de emisión sin capacidad, modo SQL
  transaccional y stock de 10,474 piezas.
- Navegación visual: Dashboard y KPIs presentes; la pantalla informa publicación
  pendiente. Las pruebas con tokens simulados no acreditan Power BI publicado.

Vite advierte del paquete grande de ExcelJS. npm audit identifica dos avisos
moderados de ExcelJS/uuid ya presentes, sin atribuirlos al SDK de Power BI.
No se aplicó una actualización forzada de ExcelJS fuera del alcance de este cambio.

El procedimiento de publicación y sus requisitos están en POWERBI_PUBLICACION_PROPIA.md.
La rama y los entregables son locales; no se modificó main ni se publicó en GitHub.
El App Service público tampoco se desplegó con este cambio.
