# Resultados de pruebas ejecutadas — Fase C

02/11/2026. Desarrollo: Motherson_L1_MVP_Desarrollo / portal 5087. Pruebas: Motherson_L1_MVP_Pruebas / portal 5088. Ambas son bases nuevas; el portal anterior 5080 se conservó.

## Resultado por requisito

| Comprobación | Resultado y evidencia |
| --- | --- |
| Integridad original | 10 gzip en SQL con SHA exacto; 709,042 eventos. evidencias/integridad-sql.json y validacion-historico.json |
| Correspondencia migrada | 230,901 movimientos compatibles; otros 478,141 eventos preservados. Sin IDs/versiones repetidos ni referencias no nulas inválidas |
| Apertura conciliada | Desarrollo: 63 aperturas, 10,116 piezas; repetir carga no agrega aperturas. recarga-idempotente.txt |
| Entrada y salida | LAB-MAT-001: 72 + 2 cajas×24 = 120; salida 15 = 105. pruebas-http.json |
| Idempotencia | Mismo UUID y cuerpo devuelve comprobante original; mismo UUID con cuerpo diferente, HTTP 409 |
| Validación | Cero, negativo, fracción y desbordamiento: 400. Salida mayor al saldo, saldo desconocido y conflicto de versión/capacidad: rechazo sin alterar saldo |
| Atomicidad | Trigger de prueba falla después de INSERT en REGISTRO; actualización previa de UBICACION se revierte, saldo 72 |
| Concurrencia de salida | Dos salidas de 50 sobre 72 con misma versión: una 200, otra 409; saldo 22 |
| Concurrencia de entrada | Dos entradas de 48 sobre 72, capacidad 144: una 200, otra 409; saldo 120 |
| Autorización | CONSULTA lee y no escribe; ENTRADAS no registra salida; sesión ausente recibe 401. Validado por HTTP real |
| Aislamiento de sesiones | Cookies y protección diferenciadas por base. Una cookie de Pruebas renombrada como Desarrollo y la inversa reciben 401. aislamiento-sesiones.json |
| Historia unificada | Consultas muestran archivo H21 y comprobantes MVP con IDs distintos; filtros de fecha/ubicación/origen comprobados |
| Aperturas y simulaciones | vw_kpi_consumo_diario vacía por diseño en esta instalación simulada. Prueba SQL temporal con SALIDA real=5 y simulada=7 dio solo 5; APERTURA excluida; todo revertido |
| Duplicados por consulta | Lecturas y recargas no crean movimientos. UUID único y apertura única comprobados |
| Cuenta SQL lectora | Lectura de vistas correcta; cuatro operaciones indebidas denegadas. permisos-lectura.json |
| Backend | Build .NET 10 aprobado, cero avisos/errores; login/CSRF/API probados |
| Frontend | Build TypeScript/Vite aprobado; 10 pruebas Vitest aprobadas; recorrido real de seis secciones y móvil aprobado |
| Reinicio de contenedor | Reinicio autorizado de motherson-mvp-sql-1, mismas huellas y comprobantes antes/después. persistencia.json |
| Conservación del código original | 113 archivos coinciden con huellas A/B. origen-conservado.json |
| Módulos retirados | Rutas activas antiguas de alarmas/solicitudes/simulación no disponibles: 404 |

## Prueba de persistencia autorizada

Se reinició el contenedor SQL compartido, con autorización expresa del usuario. La comparación antes/después verificó:

- LAB-MAT-001: 105 piezas.
- Mismos tres comprobantes: MVP:64, MVP:68, MVP:69.
- 68 ubicaciones de prueba, incluyendo la pendiente con saldo NULL.
- 230,901 movimientos históricos consultables.
- Huella del inventario: f9670bcceb6c37eaee74b204dc1368088eeeb97f3c3db1342f60f3c7b7adddad.
- Sesión del portal con permisos válidos después de recuperar la conexión.

Los puertos 5080, 5087 y 5088 respondieron después con HTTP 200 y SQL correcto. No se borró ni reinicializó volumen alguno.

## Recorrido de navegador

Se utilizó el Brave instalado, mediante Playwright con un perfil temporal. Se verificaron login, Resumen, Materiales y catálogos, Estaciones y ubicaciones, Inventario, Entradas y salidas e Historial de movimientos. Se abrió una confirmación visual de entrada sin guardarla en Desarrollo, se consultó el archivo H21 y se comprobó móvil de 390×844 sin desbordamiento horizontal. No hubo errores JavaScript. Capturas en portal/evidencias/capturas.

La última ejecución aprobó después de los ajustes de singular/plural y textos de contexto. Las pruebas de escritura se hicieron exclusivamente en la base de Pruebas; el inventario de Desarrollo conservó su apertura.

## Problemas detectados y corregidos

- La primera carga falló por una sentencia IDENTITY_INSERT sin lista explícita de columnas. Se revirtió la transacción de datos, se corrigieron las listas y se cargó correctamente; no se publicó una apertura parcial.
- /auth/me podía resolver una sobrecarga que devolvía 200 sin cuerpo. Se tipó el resultado y se validó la forma de la sesión en React. Se reiniciaron los procesos propios; login y navegación completos pasaron.
- Se separaron nombres de cookies y el ámbito de protección de sesión por base, evitando aceptar una sesión de Pruebas en Desarrollo aunque coincidan los IDs de usuario. Se verificaron ambos rechazos HTTP 401.
- Se identificó la diferencia de precisión del metadato de corte, manteniendo intactos los eventos. La validación usa límite inclusivo y las futuras cargas usan DateTime2.
- La primera ejecución de navegador necesitó el Brave instalado y ajustar un selector de etiqueta; el informe entregado corresponde al recorrido final aprobado.

No se presentan estos intentos fallidos como pruebas aprobadas. La evidencia JSON registra la ejecución que pasó después de corregirlos.

## Repetición controlada

Desde portal:

```sh
python3 scripts/local.py --integridad
python3 scripts/local.py --probar-permisos
npm --prefix frontend test
npm --prefix frontend run test:ui
python3 scripts/verificar_sesiones.py
python3 scripts/verificar_persistencia.py
```

La última orden solo compara el estado actual y escribe estado-actual.json sin reemplazar persistencia.json del reinicio; --reiniciar es una acción adicional sobre el contenedor compartido y no se incluye en la rutina ordinaria. El usuario ya autorizó y se ejecutó un reinicio en esta fase.

Para repetir toda la prueba HTTP se necesita una base de Pruebas recién preparada con LAB-MAT-001=72. scripts/test_integracion.py se detiene si ya tiene 105; no descuenta ni borra movimientos para forzar el resultado. La prueba de fallo deliberado 006 se instala únicamente en Pruebas, nunca en Desarrollo.

## Límites de lo verificado

No se ejecutaron instalación limpia del Compose alternativo 1438, restauración independiente del MVP, prueba de carga masiva ni integración de dashboard Power BI. No se conectó MES/SAP. No se validaron en piso los saldos, tiempos ni capacidades simulados. El alcance comprobado es un MVP local con archivo histórico SQL consultable y pruebas funcionales/concurrentes descritas.

## Ampliación de alarmas

La verificación de la ampliación de lectura en Resumen e Inventario está en RESULTADOS_ALARMAS.md. Conserva las nueve tablas y no escribe al consultar. Sus dos capturas válidas de Pruebas son posteriores a la evidencia de reinicio conservada de la fase C inicial. No se reinició SQL en esta ampliación.
