# Validación local

Fecha: 22/11/2026. Base SQL de pruebas nueva y aislada, con datos controlados. No se ejecutaron migraciones ni pruebas de escritura en Azure o Render.

| Grupo | Resultado |
| --- | --- |
| Frontend, importación, CSV y Excel de KPIs | 51 pruebas aprobadas, incluidas las regresiones existentes |
| Integración SQL Server 2022 | 35 comprobaciones aprobadas |
| Motor de cobertura existente | 19 casos aprobados |
| API HTTP con sesión y protección CSRF | 9 comprobaciones aprobadas |
| Compilación frontend | Correcta; advertencia de tamaño del paquete ExcelJS, cargado bajo demanda |
| Compilación backend Release | Correcta, cero advertencias y cero errores |
| Navegador | Importación confirmada de principio a fin; panel actual único; KPIs con atención persistida; línea vacía y selección de línea |

Total: **114 comprobaciones automatizadas**, además de las verificaciones del navegador. Las pruebas no representan actividad real de planta.

## Casos relevantes

- Apertura conciliada con el saldo presente en SQL y activación repetida sin reapertura.
- Tres líneas inicialmente vacías; escritura en otra línea no cambia Línea 1.
- Lectura cruzada y escritura con cuenta lectora rechazadas por el servidor; cuenta revocada deja de leer.
- Usuario SQL de ejecución sin modificación directa de entidades.
- Binario anterior no puede escribir el saldo antiguo después de activar.
- Dos capturas simultáneas con la misma versión: una confirma, otra recibe conflicto; saldo y capacidad correctos.
- Importación con ceros y sufijos, alta y variante autorizadas; repetición del UUID sin duplicado.
- Lote inválido sin cambios parciales; vista previa del servidor sin escrituras y error con hoja/fila/campo.
- Inhabilitar, retirar remanente y reactivar conservando identidad; eliminación con dependencias rechazada; eliminación sin uso permitida.
- Foto persistida en SQL y actualización de versión para impedir una carrera con eliminación.
- Inicio/cierre real de una atención: Hoja 2 cuenta un completado; un único inicio deja el promedio N/D.
- Línea sin actividad muestra cero; corte histórico conserva el saldo previo; corte futuro rechazado.
- Calendario nocturno, umbrales configurados y fronteras 14.999/15/29.999/30/59.999/60; tipo de umbral inválido rechazado.
- Ausencia de calendario, consumo insuficiente, observación desde apertura y periodos no laborables comprobados por el motor.
- Reinicio real del proceso: material, fotografía y saldo recuperados; no dependen de memoria del servidor.
- Libro Excel serializado y leído de nuevo: ambas hojas y sus valores concilian con la respuesta utilizada.

## Navegador y capturas

Los anchos CSS medidos fueron 1439 px (PC), 768 px (tableta) y 398 px (teléfono). En los tres, el ancho desplazable del documento coincide con el ancho visible. Las tablas tienen su propio desplazamiento interno. Las mediciones están en `interfaz.json`; se ajustó el tamaño de prueba del navegador integrado, que escala su presentación en la aplicación.

Se verificó en pantalla la importación de un CSV con una fila de título, encabezados alternativos y un alta `000900-CE`, con variante, revisión y modelo distintos. El puesto se creó desde la interfaz en Línea 4 de pruebas; no se reutilizó un puesto de otra línea. La operación devolvió un comprobante y el resultado descargable.

Las capturas conservadas corresponden a Resumen, línea vacía e importación. La captura del tramo inferior de KPIs no se conserva porque el navegador integrado devolvió una imagen vacía; los valores y anchos de ese módulo se verificaron mediante su interfaz accesible y sus dimensiones, además de las pruebas SQL y de exportación. Las capturas y los resultados estructurados están en `documentacion/evidencia-integracion/administracion-v2/`. Queda pendiente la validación física con los dispositivos, navegadores y concurrencia reales de planta, y la configuración de su calendario autorizado. No se midió una garantía de capacidad de Render Free bajo carga real.

## Reproducir

1. Tener Node con las dependencias fijadas en `portal/frontend/package-lock.json`, SDK y runtime .NET 10 y una instancia local SQL Server 2022 en Docker llamada `motherson-admin-v2-local`, limitada a `127.0.0.1`. Habilitar usuarios contenidos exclusivamente en esa instancia de pruebas. El runtime aislado utilizado está en `portal/.local/dotnet/dotnet`.
2. Ejecutar `python3 portal/scripts/probar_administracion_local.py`. El script comprueba el contenedor/puerto loopback y crea una base **nueva con nombre aleatorio** por ejecución; no borra bases previas. Obtiene la contraseña del contenedor local sin imprimirla. Las credenciales generadas solo se guardan en `.local`, excluido de Git.
3. Para abrir la aplicación local al terminar la suite, añadir `--servir`. Usa `http://127.0.0.1:5099` y el perfil Development exclusivamente local. Los usuarios controlados son persona0 (administrador), persona1 (operador) y persona79 (lector); la contraseña temporal se conserva en el archivo privado local. No son cuentas del piloto publicado.
4. Con ese servicio de prueba activo, ejecutar `python3 portal/scripts/probar_http_administracion.py`.
5. En frontend, ejecutar `npm ci`, `npm test` y `npm run build`. Para el motor, construir `portal/pruebas-cobertura` y ejecutar su binario con `portal/config/calendario-historico.json` como argumento.

No utilizar el perfil local ni las credenciales de prueba para publicar. Las pruebas HTTP se refieren al estado final controlado de la suite SQL y deben ejecutarse antes de realizar capturas manuales que cambien sus cantidades.
