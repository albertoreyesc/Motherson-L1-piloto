# Administración de cuatro líneas — actualización propuesta

Fecha: 22 de noviembre de 2026. Rama: `mejoras/administracion-cuatro-lineas-2026-11-22`.
Estado: implementación y comprobación local; **no aplicada a Render ni Azure**. Se requiere aprobar la publicación y la activación de la base del piloto. La instalación original queda fuera de alcance.

## Diagnóstico y decisiones

- Resumen montaba la cobertura histórica junto a la actual con tarjetas y títulos similares. La nueva pantalla tiene un panel actual; la consulta histórica está cerrada por defecto, identificada por línea y corte. Conserva también el acceso al cálculo anterior de Línea 1.
- En las capturas, 85 ubicaciones estaban sin estimación porque no había calendario documental disponible para el corte. No era evidencia de que todos los materiales estuvieran normales. Se mantienen el mínimo por material y la cobertura temporal por ubicación como indicadores diferentes.
- El importador anterior actualizaba un catálogo existente con valores controlados. El flujo nuevo permite mapear columnas y autorizar altas y nomenclaturas; la validación y la transacción se hacen también en el servidor.
- Los movimientos de inventario no equivalían a atenciones logísticas. La Hoja 2 necesita inicio y cierre. Se agregó el registro explícito de atención, independiente del movimiento y con horas del servidor; no se inventa actividad para llenar los indicadores.
- La aplicación anterior suponía una línea. La ampliación usa un contexto obligatorio de línea en cada lectura y operación.

Las cuentas y la autenticación siguen compartidas. Los materiales, puestos, estaciones, estanterías, asignaciones, mínimos, capacidades, imágenes, calendario y actividad pertenecen a cada línea. Un número de parte igual en dos líneas no implica un saldo ni una configuración compartidos. La identidad de un material es su identificador estable dentro de su línea; cambiar su número de parte no cambia ese identificador.

## Persistencia y compatibilidad

`portal/db/administracion/001_administracion.sql` agrega el esquema `mes2`, cuatro líneas, permisos por cuenta/línea, entidades, eventos y fotografías. No elimina las tablas anteriores. Los cambios de entidades y el comprobante se confirman en una misma transacción SQL con bloqueo por línea, versión esperada y UUID de operación. Los parches conservan antes/después; la apertura inicial conserva la instantánea acreditada. Las imágenes se guardan en SQL y usan el mismo bloqueo de línea para evitar carreras con una eliminación.

La instalación prepara el esquema sin activar el nuevo flujo. La activación toma **las cantidades que existan en ese momento** y verifica la suma total, por material, las referencias y capacidades. No utiliza 10,474, 10,464 ni otra cantidad de prueba como valor a restaurar. Si no hay conciliación, aborta. Se conserva el diario anterior de movimientos y sus comprobantes; no se vuelven a sumar sobre el saldo capturado.

Los registros anteriores se mantienen consultables. Las atenciones existentes se incorporan conservando sus horas, convertidas por la vista SQL vigente a UTC. Los cortes de cobertura anteriores a la ampliación usan el motor anterior; los posteriores reconstruyen los cambios del nuevo diario. La activación repetida no vuelve a abrir ni copia otra vez el inventario.

La bandera `MVP_ADMIN_V2` debe coincidir con la activación SQL. Tras activar, los disparadores impiden que una versión anterior escriba saldos, materiales o atenciones en las tablas antiguas. El servidor nuevo rechaza los endpoints operativos antiguos y solicita recargar la aplicación. Esto evita dos inventarios que diverjan silenciosamente.

## Procedimiento de publicación — pendiente de aprobación

1. Revisar esta rama, las pruebas y el diff. Confirmar el destino aislado del piloto y el mecanismo de recuperación de Azure SQL disponible para esa base. Guardar la versión actualmente desplegada y sus parámetros, por un canal privado. No registrar secretos ni incluir archivos `.local` en la entrega.
2. Construir frontend y backend con las versiones fijadas en el proyecto. Preparar el despliegue sin activar todavía el flujo v2. Pausar la recepción de nuevas capturas durante el cambio; conservar los comprobantes de las últimas operaciones.
3. Con la conexión administrativa privada del **piloto**, ejecutar el binario nuevo con `--administracion-instalar RAIZ_DEL_PROYECTO`. El esquema se instala antes de activar y es repetible. No ejecutar inicializadores de demo, `FijarSaldoInicial`, ni scripts de reinstalación.
4. Revisar los usuarios activos y la matriz `mes2.ACCESO`. Los administradores existentes reciben cuatro líneas; operadores y lectores existentes conservan Línea 1. Para asignar un operador a otra línea, el responsable debe verificar el identificador de su cuenta y el puesto de la línea destino antes de aprovisionar el acceso. Este alta de permisos no se hace automáticamente desde el importador.
5. Ejecutar `--administracion-activar RAIZ_DEL_PROYECTO` una sola vez. Comprobar el comprobante de apertura, las cantidades por material y que las líneas 2, 3 y 4 estén vacías.
6. Arrancar el servicio nuevo con `MVP_ADMIN_V2=true` y el perfil Production del piloto, manteniendo HTTPS, protección CSRF, cookies, certificado de protección de claves, permisos SQL mínimos y controles de Power BI existentes. No utilizar la configuración Development de las pruebas locales.
7. Validar con las cuentas asignadas el acceso, saldo conciliado, historial anterior, consulta histórica, inicio/cierre de atención y descarga Excel. Configurar los calendarios **reales aprobados**; los usados en pruebas no son calendarios de planta.
8. Reabrir capturas y comprobar las primeras operaciones por su UUID y saldo anterior/posterior. Si hay conflicto de versión, actualizar y revisar; no repetir con un UUID nuevo una operación de resultado desconocido.

El despliegue automático de Render solo se dispara después de la aprobación solicitada. No se amplía el firewall, no se compra capacidad y no se cambia el servidor original.

## Recuperación sin pérdida de movimientos

- **Antes de activar:** se puede mantener o volver al binario anterior con `MVP_ADMIN_V2=false`. El esquema agregado permanece inactivo; no hace falta borrarlo.
- **Después de activar:** no se permite simplemente apagar la bandera y arrancar el binario antiguo, porque sus tablas ya no contienen las nuevas operaciones. Conservar el servicio compatible con v2 y poner `mes2.CONFIG.solo_lectura=1` desde administración SQL para frenar nuevas escrituras mientras se investiga. Las consultas siguen disponibles.
- Recuperar el último artefacto compatible con v2 y conservar su conexión y claves; volver a habilitar escrituras solo tras conciliar. Para recuperar una base mediante el mecanismo de Azure, restaurar en un destino nuevo y comparar los eventos confirmados posteriores al punto recuperado antes de redirigir usuarios. No sobrescribir la base activa con una copia antigua.
- Si fuera imprescindible volver al modelo anterior después de operar con v2, hace falta una migración inversa específica, revisada y aprobada, que concilie todas las líneas y comprobantes. **No se incluye un borrado ni una proyección inversa automática**: el esquema anterior no representa las cuatro líneas.

## Power BI y límites explícitos

El informe y sus controles de acceso/licencia permanecen como están. Solo Línea 1 utiliza el informe actualmente configurado; las otras líneas muestran que no tienen un informe asociado.

Se prepararon `mes2.vw_inventario_bi` y `mes2.vw_atenciones_bi` como fuentes de lectura. El informe publicado **no queda conectado automáticamente** a esas vistas. Su modelo debe revisarse y actualizarse por separado para incorporar datos v2; mientras siga consultando las tablas antiguas mostrará el estado anterior a la activación. Recargar el iframe no importa datos desde SQL. Antes de publicar para uso operativo, acordar esta limitación y la revisión del modelo con el responsable del informe. No se habilita publicación pública ni se eluden licencias.

## Evidencia y validación pendiente

Consultar `documentacion/evidencia-integracion/administracion-v2/`: resultados SQL, HTTP, motor de cobertura y capturas. Los materiales y las atenciones de esa evidencia son **datos controlados locales**, no actividad real de la planta. La base publicada no se inspeccionó ni modificó en esta implementación.

Las pruebas locales verifican consistencia y funcionamiento; no sustituyen una prueba de carga con la concurrencia real de la planta, ni validan un calendario que todavía no se ha proporcionado. La exclusión mutua y la versión son por línea: dos capturas simultáneas pueden producir un conflicto que exige refrescar y revisar, no una pérdida silenciosa de saldo.
