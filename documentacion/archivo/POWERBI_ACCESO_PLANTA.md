# Power BI original en el piloto de planta

> Antecedente conservado para trazabilidad. No es la guía de instalación ni de operación vigente. Consulta el [índice actual](../README.md).

Estado revisado: 19 de noviembre de 2026.

## Informe conservado

Se conserva el informe Motherson publicado, con las páginas Crítico por Surtir, Desempeño semanal y Diagnóstico. No se sustituyen sus visuales ni medidas. El portal usa el enlace seguro generado por Power BI en File > Embed report > Website or portal y exige previamente la cuenta individual del portal y permiso de historial global. Power BI valida su propia sesión y acceso, independientemente del portal.

El espacio accesible del proyecto no está asignado a capacidad dedicada (`isOnDedicatedCapacity=false`). La cuenta Power BI del proyecto muestra Free account y Power BI trial con 59 días restantes. La API expone un elemento Premium Per User - Reserved (PP3); esto no acredita capacidad Embedded contratada ni asignada al espacio. No se inició otra evaluación, no se compraron licencias y no se habilitaron tokens de evaluación del backend en Production. Estos resultados no describen el tenant corporativo de Motherson, al que no se ha dado acceso.

## Para participantes

1. Abrir https://motherson-l1-piloto.onrender.com/ e iniciar sesión con su cuenta individual del portal.
2. Entrar a Dashboard con un rol autorizado para consultar información global.
3. Iniciar sesión en Power BI con su propia cuenta Microsoft de trabajo/escuela, previamente autorizada sobre el informe.
4. Si el inicio de sesión está bloqueado dentro del cuadro, usar Abrir en otra pestaña y después Recargar informe. No desactivar protecciones globales del navegador.
5. En un equipo compartido, cerrar la sesión del portal y también la de Microsoft. Cerrar el portal no revoca por sí mismo una sesión Microsoft previamente iniciada.

La cuenta del portal no es una cuenta Power BI. No compartir la cuenta ni contraseña del propietario del informe entre participantes. La evaluación actual es temporal y no demuestra que los 80 usuarios tengan licencias o permisos.

## Decisión de TI antes de distribuir

- Confirmar el tenant corporativo, las cuentas nominativas y los participantes que deben ver información global.
- Confirmar licencias Power BI Pro por usuario si el espacio permanece en capacidad compartida; si se cambia a PPU, confirmar PPU para los espectadores.
- Alternativamente, confirmar una capacidad ya disponible compatible. Para visualización segura sin licencia individual Pro/PPU, comprobar que informe y modelo estén en capacidad Premium admitida o Fabric F64 o superior, según requisitos vigentes.
- Si prefieren acceso solo mediante la sesión del portal (AppOwnsData), acreditar capacidad compatible, configurar identidad de aplicación y verificar permisos, alcance de datos y renovación real antes de cambiar el modo.
- Definir dónde alojar la copia corporativa del informe y quién tendrá permisos de lectura. No se otorgan permisos automáticamente ni se envían invitaciones desde esta entrega.
- Permitir los dominios oficiales de inicio de sesión y Power BI necesarios en los dispositivos administrados; probar el navegador real de planta.

## Datos y actualización

El modelo actual es Import. Su fuente es la base original de demostración `motherson-l1-equipo.database.windows.net`, no la base separada del piloto. La consulta REST confirma actualización programada desactivada y última actualización completada el 19/11/2026 a las 08:09 UTC (02:09 Ciudad de México). Ese resultado valida la actualización histórica de esa fuente, no la sincronización de movimientos del piloto.

Recargar informe no importa SQL. Renovar una sesión/token tampoco importa SQL. Para incorporar las capturas del piloto hay que preparar una copia del informe/modelo, configurar la fuente piloto con una identidad SQL de solo lectura, revisar tablas y medidas disponibles, conciliar inventario e historial y validar después de un movimiento de prueba. No se cambia ni reinstala la base original para resolverlo.

La actualización programada del modelo necesita credenciales y conectividad desde Power BI a SQL, además de licencia admitida. No se amplía el firewall a todas las direcciones. No se programan importaciones contra la fuente original como sustituto de conectar el piloto.

## Recuperación

Para retirar la visualización mientras se resuelve acceso, cambiar PowerBI__Mode a AppOwnsData conservando PowerBI__CapacityReady=false y PowerBI__AllowTrialTokens=false, y desplegar la configuración. Se vuelve al mensaje de pendiente; no afecta datos ni cuentas. La URL segura no contiene un secreto y no concede permisos por sí misma.

## Referencias oficiales

- https://learn.microsoft.com/en-us/power-bi/collaborate-share/service-embed-secure
- https://learn.microsoft.com/power-bi/developer/embedded/move-to-production
- https://learn.microsoft.com/en-us/power-bi/connect-data/refresh-data
