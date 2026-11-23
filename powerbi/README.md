# Estado vigente · 22/11/2026

El informe original está publicado y conserva sus diseños. La preparación para operaciones v2 y el bloqueo concreto de conexión están en [administracion-v2/README.md](administracion-v2/README.md). Las notas siguientes corresponden a la preparación histórica; no describen el estado actual de publicación.

---

# Dashboard para el entorno propio

- `Motherson.pbix`: copia original de Entrega.zip. No publicado ni reconfigurado internamente.
- `modelo_completo.tmdl`: copia textual adaptada a los parámetros ServidorSQL =
  motherson-l1-equipo.database.windows.net y BaseSQL = Motherson_L1.

El modelo usa Import y lee dbo.vw_registro_bi como REGISTRO; las demás tablas se leen
del esquema dbo. Usa powerbi_lector. No debe recibir la cuenta del portal ni su contraseña.

El usuario confirmó que aún no dispone de un espacio propio de Power BI. Antes de publicar:
definir el espacio y la cuenta, revisar licencia/capacidad vigente, configurar los parámetros
del PBIX en Power BI Desktop o el servicio, credenciales y actualización. Editar el TMDL
no cambia los parámetros del PBIX ni un modelo ya publicado.

No se habilitó DirectQuery ni se incrustó un informe en el portal. Primero validar totales
y atenciones en Import; después definir actualización y, si se requiere, incrustación.

## Actualización y conciliación pendiente

En Desktop, ajustar ServidorSQL y BaseSQL, borrar permisos de fuentes anteriores
y autenticarse exclusivamente como powerbi_lector contra la base indicada.
Actualizar y comparar inventario por ubicación, stock agregado y atenciones con
SQL; repetir después de una captura de demostración. Registrar hora de corte y
resultado. En el servicio, configurar las credenciales de la fuente y la frecuencia
de actualización después de elegir cuenta/espacio. No copiar credenciales al PBIX
ni al TMDL; definir la red permitida según el despliegue, sin abrir firewall aquí.

Referencias oficiales: [usuarios contenidos Azure SQL](https://learn.microsoft.com/en-us/azure/azure-sql/database/logins-create-manage?view=azuresql),
[actualización programada Power BI](https://learn.microsoft.com/en-us/power-bi/connect-data/refresh-scheduled-refresh).

El flujo privado de Dashboard y sus requisitos están documentados en [publicación propia](../documentacion/POWERBI_PUBLICACION_PROPIA.md). La carga histórica ya está archivada y conciliada en Azure; la publicación y una actualización real del PBIX siguen pendientes.
