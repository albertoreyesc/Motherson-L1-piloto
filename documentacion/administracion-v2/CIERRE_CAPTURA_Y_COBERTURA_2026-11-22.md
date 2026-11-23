# Captura por bloques, Hoja 2 y cobertura · 22/11/2026

## Implementado

- Entradas y salidas usa tarjetas visuales inspiradas en QuickEntryPanel, con fotografía disponible, número de parte, descripción, ubicación, saldo, cajas equivalentes y espacio libre. Los filtros respetan las ubicaciones que devuelve el servidor para cada cuenta.
- Revisión de piezas y saldo antes de confirmar, control de capacidad/remanentes, permisos de entrada/salida, bloqueo de doble envío y recuperación de la misma operación pendiente. Respuestas 408/429 no descartan el comprobante.
- Inicio y cierre de atención en el material seleccionado. Un movimiento puede conservar la referencia a la atención abierta de esa ubicación y cuenta. Las atenciones no cambian piezas y el movimiento no crea una duración ficticia.
- Hoja 2 conserva el cálculo de atenciones. La causa de la captura vacía era el alcance inicial de siete días, sin actividad histórica de febrero–marzo. Ahora muestra período, corte y actividad disponible, permite seleccionar explícitamente el rango histórico y avisa cuando los filtros no corresponden a la consulta calculada. Un fallo de consulta se presenta como error.
- Calendario con descansos, vista semanal, turnos nocturnos, excepciones, vigencia e identificación de referencia simulada. Corregida la terminación del último turno nocturno después del día final de inicio válido.
- Inventario muestra el detalle del cálculo por ubicación: ventana, tasa, consumos, piezas, último consumo, fuente y corte. Resumen e Inventario utilizan la misma respuesta de alarmas.

## Configuración aplicada al piloto

Calendario simulado de Línea 1: 22/11/2026–31/12/2026, lunes a viernes, turnos del histórico 06:00–15:00 y 15:00–23:30, descanso 25/12. El usuario pidió guiarse por días laborales de Puebla; esta referencia no acredita los horarios reales de Motherson y deja pendientes los descansos internos. Ver [manual](MANUAL.md).

Su activación conservó 85 materiales y 10,464 piezas. El primer corte mostró VENTANA_INCOMPLETA: el calendario empieza el domingo y aún no existe una ventana laboral completa dentro de la vigencia. Después se requieren dos consumos positivos por ubicación; no se generaron consumos ficticios en el piloto.

Se instalaron las vistas aditivas `002_fuentes_powerbi.sql` en la base piloto, con conciliación de 10,464 piezas y 58,422 atenciones. La regla temporal de mantenimiento se retiró; quedan las dos reglas existentes de Render. La base original del equipo no se modificó.

## Pruebas

- Frontend: 60 pruebas, con previsión de cajas incompletas, capacidad, cantidades inválidas, inhabilitación y exportación Excel.
- SQL local aislado: 53 comprobaciones, incluidas apertura e idempotencia, concurrencia, permisos, relaciones de las fuentes BI, calendario con descansos, legado, nuevas atenciones y cobertura API por ubicación.
- HTTP local: 9 comprobaciones de autenticación, permisos, persistencia y CSRF.
- API local → exportador real → archivo XLSX → lectura: dos hojas; inventario y completados coinciden. El archivo de evidencia contiene exclusivamente datos controlados de prueba.
- TMDL: validación con Microsoft.AnalysisServices 19.114.12: 8 tablas, 8 relaciones y 110 medidas. La validación de estructura no ejecuta DAX ni acredita un refresco del servicio.
- Navegador local: administración inicia atención, confirma entrada y finaliza desde el mismo bloque; Hoja 2 refleja el completado. Surtidor consulta solo sus 12 ubicaciones, salida deshabilitada según su permiso, registra entrada y cierra su atención. Vista de tableta medida en 767 px y PC en 1797 px, sin desbordamiento del documento; las tablas tienen desplazamiento propio.
- Cobertura de extremo a extremo local: 60 piezas y dos consumos de 30 piezas en 60 minutos laborables dan 60 minutos/Normal. Otra ubicación del mismo material sin consumos permanece Sin estimación.

Evidencias: [carpeta de capturas y Excel de prueba](../evidencia-integracion/captura-bloques-2026-11-22/). No se escribieron operaciones de prueba en el servicio publicado.

## Power BI y colaboración

El modelo publicado se recuperó mediante Fabric para preparar una versión compatible con las fuentes v2. El candidato conserva nombres, 8 relaciones y 110 medidas, incorpora claves por línea y convierte fechas a Ciudad de México. La cobertura del candidato queda en blanco hasta incorporar un cálculo equivalente al portal; no se usa el ritmo de atenciones como consumo de piezas.

El informe publicado aún consulta la base original y tiene la actualización programada desactivada. La cuenta de aplicación recibió 401 al consultar la conexión SQL administrada; se solicitó sesión del propietario. No se reemplazó el modelo en uso con una conexión no validada. Pasos y archivos: [modelo candidato](../../powerbi/administracion-v2/README.md).

Invitación enviada a `lita1-coder` (Alejandro Lita), permiso write. Alejandro debe aceptarla con su cuenta GitHub. No se compartieron credenciales ni se hizo público el repositorio.
