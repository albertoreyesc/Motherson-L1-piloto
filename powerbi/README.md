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
