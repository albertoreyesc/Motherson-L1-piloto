# Verificación de alarmas — 02/11/2026

Resultado: ampliación incorporada en Resumen e Inventario del portal 5087. Sin nueva pestaña y sin modificaciones del esquema SQL.

## Pruebas ejecutadas

| Prueba | Resultado |
| --- | --- |
| Motor determinista C# | 19 casos aprobados: cero/desconocido, seis fronteras, tasa/muestra insuficiente, precisión de corte, turnos, fin de semana, cierres, vigencia, apertura, eventos futuros, duplicados y separación de espacios, tipos excluidos, mínimo por material |
| Frontend existente | 10 casos Vitest aprobados |
| Compilación | TypeScript/Vite y .NET aprobados; .NET sin errores ni avisos |
| API de lectura | Ambas bases devuelven los mismos campos y cantidades que el inventario anterior; cuatro consultas sucesivas no agregan movimientos; CONSULTA tiene acceso y una sesión ausente recibe 401 |
| Navegador de lectura | Dos recorridos aprobados: seis secciones existentes, alarmas, filtros, mismo corte entre pantallas, recarga y móvil; cero errores JavaScript y cero POST de negocio |
| Navegador con movimientos | Una prueba activada explícitamente en 5088, aprobada: entrada de una caja y salida de 24 piezas en LAB-CONC-SAL; actualización automática del contexto de inventario/alarmas |
| Huellas antes/después de consultar | Idénticas en ambas bases: columnas, objetos SQL, relaciones, fuentes, inventario y conteos |
| Huellas después de la prueba de captura | Desarrollo completamente idéntico; Pruebas conserva esquema, fuentes y 709,042 eventos, con exactamente dos comprobantes adicionales |

La prueba con movimientos permanece omitida deliberadamente en la rutina UI de lectura. Se ejecutó por separado con MVP_PROBAR_MOVIMIENTOS=1 y quedó su propio reporte aprobado. Una omisión posterior de esa prueba no se cuenta como una prueba ejecutada de nuevo.

## Resultado observado con los datos actuales

Desarrollo conserva 63 ubicaciones, 63 aperturas y 10,116 piezas simuladas. No se modificó su inventario para producir semáforos.

La consulta actual devuelve 63 ubicaciones sin estimación de cobertura: el calendario termina el 24/10/2026. El motivo FUERA_CALENDARIO_DOCUMENTADO aparece junto al corte. El mínimo por material sí identifica un material bajo mínimo en los datos observados: 5G1 857 745, total 38 piezas, mínimo 140. No se presenta ese mínimo como riesgo temporal calculado.

En Pruebas, LAB-CONC-SAL tenía 22 piezas. Se confirmó una entrada de 24 → 46 y una salida de 24 → 22. La versión avanzó dos unidades y quedaron los comprobantes. REGISTRO pasó de 71 a 73 filas en esa base; ningún otro cambio fue necesario. LAB-MAT-001 permanece fuera de esta captura y no se reinicializó.

Las pruebas numéricas de cobertura utilizan entradas sintéticas en memoria, identificadas como pruebas. No se insertaron tasas, calendarios extendidos ni escenarios coloreados en la base de Desarrollo.

## Archivos de evidencia

- portal/evidencias/pruebas-cobertura.json — los 19 casos del motor.
- portal/evidencias/pruebas-alarmas-api.json — coherencia y permisos HTTP.
- portal/evidencias/pruebas-ui.json — recorrido final de lectura; dos aprobados, una prueba de captura omitida por defecto.
- portal/evidencias/pruebas-alarmas-ui-movimientos-reporte.json — ejecución aprobada de captura.
- portal/evidencias/pruebas-alarmas-movimientos-ui.json — saldos y versiones de las dos capturas de Pruebas.
- portal/evidencias/alarmas-antes.json y alarmas-despues-lecturas.json — huellas iguales tras consultar.
- portal/evidencias/alarmas-despues-pruebas.json y validacion-alarmas.json — comprobación de esquema e histórico conservados, con las dos capturas autorizadas en Pruebas.
- portal/evidencias/capturas-alarmas — capturas de Resumen, Inventario y móvil.

## Correcciones durante la verificación

La nueva respuesta tipificada inicialmente omitía id_color. La prueba comparó todos los campos con la respuesta previa, detectó la diferencia y se corrigió antes de aprobar la API.

La primera compilación simultánea de frontend y backend encontró referencias a archivos estáticos reemplazados durante el proceso. Se compiló en orden —frontend y después backend— con limpieza del caché de compilación; la compilación final pasó. No fue una modificación de SQL ni una pérdida de datos.

## Límites

No se atribuyen coberturas actuales al histórico de octubre. No se habilitó ciclo universal de 102 segundos ni selector histórico de saldo. Para minutos vigentes hacen falta calendario vigente y observaciones aplicadas suficientes. No se valida productividad física ni se afirma un paro de toda la línea.

No se reinició SQL en esta ampliación. Se reiniciaron únicamente los procesos .NET del MVP para cargar el código. La evidencia de persistencia de la fase C anterior se conserva como prueba de aquel estado, anterior a las dos nuevas capturas de Pruebas.
