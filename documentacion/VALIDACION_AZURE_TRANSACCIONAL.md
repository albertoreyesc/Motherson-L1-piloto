# Validación de la integración preparada

Preparación: 18/11/2026. Rama: integracion/azure-propia.
Las pruebas de esta sección fueron locales. La ejecución posterior, aprobada el
19/11/2026 y verificada contra Azure real, está en
[instalación ejecutada](INSTALACION_AZURE_EJECUTADA_2026-11-19.md).

## Ejecutado y probado localmente

Se usó SQL Server 2022 real, exclusivamente en bases desechables de pruebas,
y el runtime ASP.NET Core 10.0.15. No se sustituyó SQL por un simulador para estas pruebas.

| Grupo | Comprobaciones aprobadas |
| --- | ---: |
| SQL y HTTP: movimientos, concurrencia, roles y persistencia | 68 |
| Carga completa de los ocho CSV y conciliación de fuentes BI | 8 |
| Cortes de cobertura y alcance del puesto | 4 |
| Conservación y validación del diario anterior | 85 |
| Configuración de destino y cifrado Azure | 11 |
| Interfaz y cálculos | 39 |
| Total | 215 |

Además, el instalador autocontenido se ejecutó sobre una base nueva: 15 tablas,
7 vistas y 7 procedimientos, sin errores. Compilación de frontend, backend y
publicación Release completadas; C# sin advertencias. Vite advierte del tamaño
del paquete ExcelJS, sin impedir la compilación.

Las pruebas incluyen entradas/salidas en cajas, última caja incompleta, saldo
insuficiente, ajustes de capacidad sin sobreocupación, importación completa o
rollback, UUID repetido y conflictivo, escrituras concurrentes, rollback al fallar
el historial, permisos de puestos, login/CSRF, imágenes, atenciones y reinicio del
backend conservando saldos, historial, imágenes y sesión. Se probaron la denegación
del procedimiento antiguo y la imposibilidad del lector BI de leer hashes.

La carga de demostración concilió 58,422 atenciones, 85 materiales y 10,474 piezas;
la vista BI conservó las 58,422 filas y una semana de control dio 1,146 atenciones
completadas. Esto valida fuentes SQL; no demuestra que Power BI haya actualizado
el PBIX ni evaluado todas sus medidas DAX.

## Histórico preservado y límite de la prueba de volumen

La migración verifica archivos y cadenas, conserva evidencia original y trabaja
en lotes reanudables de 1,000 filas. Se probaron duplicados y una interrupción con
1,000/1,100 filas: el archivo parcial quedó oculto y la reanudación lo completó.

La prueba del paquete completo de 1,456,098 registros NO terminó: el equipo se
quedó sin espacio suficiente. Una primera transacción grande agotó el disco local;
se sustituyó por lotes y confirmación por archivo. En el segundo intento el control
de espacio detuvo la prueba antes de bajar de 3 GiB libres. Había 1,136,423 filas
insertadas, cinco archivos confirmados y registros.jsonl.gz incompleto.
Se guardó el informe y se eliminó solo esa base desechable. No se alteraron las
fuentes, los ZIP originales ni Azure. No se presenta esta prueba como aprobada.

El respaldo Motherson-L1-legado-preservado.zip conserva doce archivos sellados,
manifiesto y dos diarios de la copia del 12/11/2026, con procedencia y hashes.
No se afirma que contenga actividad privada posterior ausente del ZIP de GitHub.
El archivo antiguo no se vuelve a aplicar al saldo de demostración.

## Pendiente al finalizar la preparación del 18/11/2026

- Crear esquema/cuentas/permisos y cargar demostración, después de aprobación.
- Repetir pruebas con conexiones reales de portal_app y powerbi_lector en Azure.
- Verificar conectividad, TLS y persistencia del entorno de despliegue.
- Completar ensayo de migración histórica de volumen con espacio suficiente.
- Configurar PBIX, actualizar y conciliar visualizaciones/medidas contra Azure SQL.
- Definir cuenta y espacio Power BI antes de publicar; incrustación aparte.

No se publicó en GitHub ni se cambió main. Los resultados locales no acreditan
una integración real ya instalada en Azure en esa fase de preparación.
