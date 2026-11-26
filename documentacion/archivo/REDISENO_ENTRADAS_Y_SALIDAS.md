# Entradas y salidas — rediseño operativo

> Antecedente conservado para trazabilidad. No es la guía de instalación ni de operación vigente. Consulta el [índice actual](../README.md).

Implementado el 2 de noviembre de 2026 en el MVP de Línea 1. Disponible en el portal local del puerto 5087, al recargar la página.

## Qué cambia y para qué

- Cabecera roja `#DA2128` con operación e instrucción contextual. El fondo general gris claro y las tarjetas blancas mantienen la lectura de ubicaciones y cantidades.
- Selector Entrada · cajas / Salida · piezas con iconos, estado accesible y permisos existentes. Pasos Ubicación → Material → Confirmar con número, marca de completado y paso activo.
- Búsqueda opcional por estación, estantería, nivel, número de parte, descripción o puesto. El filtro de disponibilidad y el conteo quedan alineados con la cuadrícula.
- Área de trabajo limitada a 1,200 px. Cuatro columnas a 1,600 px de ventana, dos a 900 px y una a 390 px, verificadas en navegador. Sin desplazamiento horizontal en los tamaños comprobados.
- Tarjetas con estación principal y estantería, nivel y puesto separados. La disponibilidad usa texto y fondo neutro; no representa una alarma de cobertura.
- Selección identificada en la confirmación con borde rojo, marca y texto. Cantidad con botones de más/menos, accesos 1/2/3 y campo numérico; acciones principales de 56 px y controles rápidos de 48 px.
- Confirmación con número de parte, ubicación, unidad, conversión cajas/piezas, saldo anterior y previsto. El comprobante muestra las piezas efectivamente registradas y el saldo resultante.

## Recorrido

Para una ubicación con un solo material visible, se mantiene el salto directo a confirmar: **elegir ubicación → confirmar** para la cantidad predeterminada. Cuando hay varios materiales, se conserva la selección intermedia. La búsqueda no es obligatoria.

«Solo ubicaciones disponibles» filtra según la operación: espacio para cajas en entradas y saldo positivo en salidas. Al desmarcarlo se pueden consultar también ubicaciones bloqueadas. Esto corrige que el filtro anterior de salidas siguiera ocultando los saldos cero incluso al desmarcarlo.

Los mensajes distinguen falta de espacio, existencias desconocidas, capacidad o empaque pendiente, cantidad inválida y saldo insuficiente. Un error de concurrencia vuelve a consultar disponibilidad. No se agregan reservas al MVP.

## Protección del registro

Se conservan los endpoints, el cálculo `importe`, la disponibilidad del servidor, la autenticación, los permisos y la concurrencia. Una respuesta incierta guarda la misma clave de idempotencia y los mismos datos en el almacenamiento de sesión; mantiene bloqueados operación, ubicación y cantidad. Si la ubicación deja de aparecer en la consulta, se permite comprobar la operación original sin abrir una selección nueva.

No hay modificaciones en backend, SQL, tablas, vistas, procedimientos, capacidades ni reglas de alarmas. Navegar, buscar o cambiar filtros no genera movimientos.

## Verificación realizada

- Compilación TypeScript/Vite: correcta.
- Diez pruebas existentes de cantidades y disponibilidad: correctas.
- Suite de navegador completa: siete pruebas correctas y una de escritura omitida deliberadamente en la ejecución ordinaria.
- La prueba de escritura se activó una sola vez por separado, exclusivamente contra `Motherson_L1_MVP_Pruebas`: entrada de una caja de 24 piezas y salida de 24 piezas, material controlado `LAB-CONC-SAL`, saldo 22 → 46 → 22 y versión 4 → 6. Se conservaron los dos movimientos; no hubo borrado ni reinicialización.
- Cinco pruebas del rediseño cubren tamaños, teclado, búsqueda sin coincidencias, materiales de una misma ubicación, estados bloqueados, cantidad inválida, salidas, permisos, conflicto y reintento con la misma clave tras recargar.
- Los casos de saldo desconocido y conflicto se simularon únicamente en respuestas del navegador. Las solicitudes de los casos de red incierta se abortaron antes de llegar al backend; no modificaron SQL. Las capturas entregadas corresponden a datos consultados en Desarrollo, sin esos casos artificiales.
- Las huellas de ambas bases permanecieron idénticas después de las pruebas de solo lectura. Después de las dos capturas reales, Desarrollo conservó sus 64 registros y su huella de inventario; Pruebas pasó de 73 a 75 registros. Se conservaron las nueve tablas operativas y los 709,042 eventos originales en ambas bases, junto con sus fuentes.
- El recorrido de las seis pantallas y las alarmas existentes pasó la revisión de regresión.

No se ha medido todavía el tiempo de captura con personal en planta; se conserva el número de acciones del recorrido y se propone verificar su rapidez en la siguiente demostración.

## Evidencias

- [Ubicaciones, escritorio](../../portal/evidencias/capturas-movimientos/01-ubicaciones-escritorio.png).
- [Confirmación de entrada](../../portal/evidencias/capturas-movimientos/02-confirmacion-entrada-escritorio.png).
- [Confirmación de salida](../../portal/evidencias/capturas-movimientos/03-confirmacion-salida-escritorio.png).
- [Confirmación, móvil](../../portal/evidencias/capturas-movimientos/04-confirmacion-movil.png).
- [Ubicaciones, tablet](../../portal/evidencias/capturas-movimientos/05-ubicaciones-tablet.png).
- [Suite de navegador](../../portal/evidencias/pruebas-ui.json).
- [Ejecución separada con movimientos en Pruebas](../../portal/evidencias/layout-movimientos-pruebas.json).
- [Saldos y versiones de esos movimientos](../../portal/evidencias/layout-movimientos-ui.json).
- [Comparación de las bases](../../portal/evidencias/validacion-layout.json), respaldada por `layout-antes.json`, `layout-despues-lecturas.json` y `layout-despues-pruebas.json` en esa misma carpeta.

## Archivos de implementación

`portal/frontend/src/QuickEntryPanel.tsx`, `MvpApp.tsx` y los estilos específicos `.movimientos-ui` / `.mov-*` en `styles.css`. Las pruebas están en `portal/frontend/pruebas-ui/entradas-layout.spec.ts`. El recorrido general ahora toma el saldo de la consulta actual en lugar de exigir el saldo inicial fijo, que ya puede cambiar por capturas válidas del usuario.
