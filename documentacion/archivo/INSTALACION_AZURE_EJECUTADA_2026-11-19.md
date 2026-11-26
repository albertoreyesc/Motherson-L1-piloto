# Instalación aplicada y verificada el 19/11/2026

> Antecedente conservado para trazabilidad. No es la guía de instalación ni de operación vigente. Consulta el [índice actual](../README.md).

Destino: motherson-l1-equipo.database.windows.net / Motherson_L1.
Autorización: aprobación expresa del usuario del 19/11/2026 para estructura,
cuentas/permisos, datos simulados y verificaciones de demostración.
Rama de trabajo: integracion/azure-propia. Sin publicación en GitHub ni cambios en main.

## Ejecutado en Azure

El diagnóstico previo volvió a confirmar cero tablas, vistas y procedimientos;
portal_app y powerbi_lector no existían. La base estaba pausada; el primer intento
no estuvo disponible y un reintento conectó correctamente, sin cambiar nivel de
servicio ni firewall.

Se ejecutó una sola variante: portal/db/azure/instalar_portal.sql. Azure devolvió
INSTALACION_CONFIRMADA. Quedaron 15 tablas de usuario, 7 vistas y 7 procedimientos,
además de la función e índices incluidos en el instalador.
Se crearon portal_app y powerbi_lector con sus claves privadas y se aplicó
020_permisos.sql. Las conexiones directas de ambas cuentas funcionan con cifrado
y validación del certificado. El portal usa exclusivamente portal_app.

La carga se ejecutó mediante DemoData.Load con autenticación de Microsoft Entra
de la cuenta propia, sin guardar una contraseña administrativa en el backend.
Los ocho archivos pasaron validación de hashes y se cargaron junto con la apertura
en una sola transacción. Conteos comprobados:

| Tabla | Filas |
| --- | ---: |
| ESTACION | 27 |
| COLOR | 15 |
| PUESTO_LOGISTICO | 6 |
| TURNO | 2 |
| ESTANTERIA | 85 |
| MATERIAL | 85 |
| UBICACION | 85 |
| REGISTRO | 58,422 |

Inventario final: **10,474 piezas simuladas**, igual a los CSV aprobados.
La apertura queda fechada el 19/11/2026 a las 00:43:09, hora de Ciudad de México.
No se cargó histórico legado: mes.HISTORICO_LEGADO conserva cero filas.

## Probado realmente contra Azure

- 18 comprobaciones SQL aprobadas: identidad real de las dos cuentas, apertura y
  simulación, conteos, stock agregado, restricciones de escritura, bloqueo del
  procedimiento antiguo y permisos de BI. El lector no puede consultar hashes
  ni capturar movimientos; sí consulta las fuentes del dashboard.
- 37 aserciones HTTP aprobadas (24 mensajes distintos; varias lecturas se repiten):
  login, inventario, salida/entrada, idempotencia, saldo insuficiente, concurrencia,
  CSRF, capacidad +/− y rechazo bajo ocupación, importación de dos materiales,
  cancelación completa por fila obsoleta y recuperación de mínimos originales.
- La última caja incompleta se probó en Azure dentro de una transacción externa
  revertida. Se comprobó conjuntamente el retorno de saldo e historial al estado
  anterior; no dejó un movimiento operativo ficticio.
- Reinicios del backend mantuvieron sesión, saldos e historial. También se verificó
  la cobertura al corte: seis movimientos confirmados y 10,474 piezas.
- Comprobación visual: portal local autenticado muestra simulación, existencias e
  historial con admin.local, fecha, piezas, saldo anterior/posterior y UUID.

Quedaron seis movimientos de demostración (tres salidas y tres entradas de 20
piezas) en la ubicación 13, y seis detalles de ajuste (dos de capacidad y cuatro
de mínimos). Los valores originales quedaron recuperados mediante operaciones
compensatorias; se conservó el historial. Dos movimientos corresponden a un primer
ensayo que se detuvo porque su aserción esperaba HTTP 422 para saldo insuficiente;
el contrato devuelve HTTP 409. Se corrigió la expectativa y se completó el ensayo.
No se borró evidencia. Los UUID y solicitudes están en los JSON de evidencia.

## Acceso y alcance del despliegue

El portal queda iniciado en http://127.0.0.1:5093, conectado a Azure SQL real.
Es una ejecución local; no se desplegó ni se cambió el App Service publicado.
Usuario administrativo: admin.local. Su contraseña y las dos claves SQL están en
portal/.local/accesos-iniciales-azure.json, con permisos solo del propietario.
No se incluyen en Git, informes públicos ni paquetes. Los seis puestos de la
carga siguen sin credenciales; sus accesos se habilitan desde la administración.

Para volver a iniciar localmente:

```sh
python3 portal/scripts/local.py --compartida --puerto 5093
```

Conservar la configuración privada y las claves persistentes del backend.
Las imágenes nuevas requieren mantener su almacenamiento privado; las imágenes
previas que no estaban en el ZIP no se trasladaron ni se inventaron.

## Pendiente

- Migración completa del legado, excluida de esta autorización; respaldo preservado.
- Power BI: cuenta y espacio propios, parámetros del PBIX, credenciales lectoras,
  actualización y conciliación de medidas/visuales tras actualizar. Las fuentes SQL
  sí conciliaron en Azure; no se ejecutó una actualización real de Power BI.
- Publicación del backend en el entorno definitivo e incrustación, como pasos separados.
- Calendario operativo vigente: el documental acaba el 24/10/2026. Por eso la
  cobertura temporal actual aparece sin estimación; no se inventaron turnos ni tasas.

## Evidencia y recuperación

Los archivos AZURE_INICIAL.json, AZURE_FINAL.json, AZURE_HTTP.json y AZURE_CORTE.json
están en documentacion/evidencia-integracion. Se conserva también el primer ensayo.
Las capturas se entregan fuera del repositorio en outputs/.
La instalación ya no debe repetirse: sus guardas rechazan bases con tablas.
Para recuperarse de incidencias conservar los datos y diagnosticar; no ejecutar
DROP, reiniciar la demostración ni volver a aplicar los CSV sobre la apertura.
