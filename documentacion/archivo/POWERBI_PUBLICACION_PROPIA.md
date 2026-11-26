# Publicación propia y Dashboard

> Antecedente conservado para trazabilidad. No es la guía de instalación ni de operación vigente. Consulta el [índice actual](../README.md).

## Estado al 19/11/2026

El usuario confirmó que no tiene cuenta de trabajo/escuela ni espacio propio.
No se publicó Power BI, no se creó una cuenta, no se aceptaron condiciones,
no se concedieron permisos a aplicaciones y no se contrató capacidad.
El Gmail que administra Azure no constituye por sí mismo una cuenta admitida
para el alta de Power BI. Véase [alta oficial](https://learn.microsoft.com/en-us/power-bi/fundamentals/service-self-service-signup-purchase-for-power-bi?tabs=free-sign-up).

El vínculo recibido abre dashboard.html del otro equipo y contiene un iframe:
app.powerbi.com/reportEmbed, reportId aced7ff3-76ea-4499-89f7-4d107af590ef,
tenant 662cd520-d20f-4912-bea3-c9bd81f78841. Al inspeccionarlo se mostró
“Sign in to view this report”. autoAuth=true no acredita acceso sin sesión.
Sirve de referencia visual; no concede propiedad, permisos de publicación ni
conecta el informe a Motherson_L1. No se copiaron credenciales del otro equipo.

## Implementación preparada

Se integraron del ZIP (3) la navegación Dashboard, la etiqueta KPIs y sus estilos,
conservando las transacciones, apertura, historia SQL y controles del backend propio.
El componente DashboardEquipo2 mantiene su nombre de archivo para reconocer la
procedencia; la interfaz y configuración se refieren al espacio propio.

Hay dos modos explícitos:

- Enlace seguro /reportEmbed: sigue requiriendo una cuenta Microsoft con acceso.
- AppOwnsData: el backend obtiene identidad de aplicación, consulta únicamente
  el informe configurado y genera un embed token limitado a informe/dataset,
  sin permiso de edición. El navegador solo recibe ese token temporal y lo renueva
  antes de expirar; no recibe ClientSecret ni el token de Microsoft Entra.

La ruta GET /api/dashboard-equipo2 informa disponibilidad. POST
/api/dashboard/embed-token exige sesión del portal, permiso historico y CSRF.
Los puestos no tienen ese permiso: el informe global es administrativo.
No existe fallback a un informe público. Se rechazan URLs /view y hosts ajenos.
La integración devuelve pendiente mientras falten identificadores, secreto y
CapacityReady=true. Ese indicador debe fijarse solo después de comprobar la
capacidad y permisos reales; no contrata ni acredita capacidad por sí mismo.

Las comprobaciones REST con respuestas simuladas verifican formato, alcance y
que no salgan secretos. No demuestran funcionamiento contra Power BI real.

## Orden para activar la publicación

1. Disponer de cuenta de trabajo/escuela y acceso autorizado al tenant propio.
   El usuario debe completar cualquier alta y aceptación de condiciones.
2. Definir un espacio propio y licencias/capacidad adecuadas. AppOwnsData en
   producción requiere capacidad. No se puede usar My Workspace con service principal.
3. En Power BI Desktop, abrir el PBIX original preservado y cambiar ServidorSQL
   a motherson-l1-equipo.database.windows.net y BaseSQL a Motherson_L1, o usar el
   TMDL adaptado. La edición de TMDL no modifica internamente el PBIX original.
4. Usar powerbi_lector y su clave privada para la fuente SQL; nunca portal_app ni
   el administrador. Actualizar y conciliar con SQL: 58,422 atenciones y 10,474
   piezas actuales, además de cortes/medidas seleccionados. La historia archivada
   no se suma otra vez a UBICACION ni a REGISTRO.
5. Publicar en el espacio elegido. Configurar credenciales del modelo semántico,
   parámetros y actualización en el servicio; comprobar el historial de refresh.
6. Para acceso sin otra sesión: registrar identidad de aplicación propia, habilitar
   el uso de APIs de Power BI para el grupo autorizado y dar acceso al espacio.
   Estos cambios de acceso se presentan al usuario antes de aplicarlos.
7. Guardar PowerBI__TenantId, ClientId, ClientSecret, WorkspaceId, ReportId y
   CapacityReady en variables privadas del backend. El archivo de ejemplo no se
   carga automáticamente; no completar ese archivo versionado con secretos.
8. Probar token y renovación, denegación a puestos/no autenticados, sesión vencida,
   informe autorizado y conciliación tras actualizar desde Azure. Incrustar en el
   portal publicado exige además desplegar el backend y frontend preparados.

No se configura el enlace del otro tenant como destino por defecto. Si se decide
consultarlo temporalmente se necesita su permiso; no equivale a publicación propia.

## Referencias oficiales

- [Service principal y capacidad](https://learn.microsoft.com/en-us/power-bi/developer/embedded/embed-service-principal).
- [Permisos y tokens](https://learn.microsoft.com/en-us/power-bi/developer/embedded/embed-tokens).
- [Renovación de token](https://learn.microsoft.com/en-us/javascript/api/overview/powerbi/refresh-token).
- [Publicar en web hace público el informe](https://learn.microsoft.com/en-us/power-bi/collaborate-share/service-publish-to-web).


## Prueba real del backend — 19 de noviembre de 2026

El usuario publicó Motherson en su espacio 851cabfa-9a0e-43ef-a5b0-88576f9201b7, confirmó actualización exitosa y agregó la aplicación como Member. Informe: 561bace1-3a3f-4671-b8e3-9eaf4b90a00c. Las capturas muestran 10,474 piezas, 85 materiales y 1,146 reabastecimientos en la semana seleccionada.

Se verificaron autenticación de aplicación, consulta del informe y generación de token real. El portal local devolvió HTTP 200 tanto en el estado privado como en POST /api/dashboard/embed-token, usando sesión y protección CSRF. El secreto solo vive en portal/.local/powerbi-privado.json (permisos 600, ignorado por Git). El lanzador local carga esa configuración en variables de entorno. No se guarda ningún token en la evidencia.

AllowTrialTokens permite desarrollo solamente cuando ASPNETCORE_ENVIRONMENT=Development. CapacityReady sigue false: no se ha adquirido ni acreditado capacidad de producción. Dos comprobaciones adicionales verifican que la excepción de prueba no funciona en Production; las 25 comprobaciones locales pasaron. Falta comprobar visualmente el informe incrustado en el navegador. La prueba puede estar sujeta al límite de tokens de evaluación de Microsoft. Antes de producción, reemplazar la credencial compartida en el chat por una nueva y configurar capacidad compatible.
