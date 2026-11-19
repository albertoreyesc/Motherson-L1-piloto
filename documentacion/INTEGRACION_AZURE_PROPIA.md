# Integración con Azure propio — 18/11/2026

## Alcance y estado

Copia de trabajo derivada de Motherson-L1-main (2).zip, con scripts y dashboard de
Entrega.zip. Se conserva autoría y procedencia en ORIGEN_INTEGRACION_AZURE.json.
Git local en rama integracion/azure-propia; no conectado ni publicado a GitHub.

Se observó en la sesión del portal de Azure:

- Base Motherson_L1, servidor motherson-l1-equipo.database.windows.net.
- Grupo rg-motherson-l1, región Canada Central, suscripción Azure for Students.
- Estado mostrado: Paused. No se reanudó mediante conexión SQL.
- No se inspeccionaron tablas ni procedimientos: no se puede afirmar que siga vacía.
- Power BI: el usuario confirmó que todavía no tiene un espacio propio.

No se ejecutaron scripts SQL, cargas, cambios de permisos, despliegues ni publicaciones.

## Primera entrega preparada

1. Instalación separada en portal/db/azure, sin scripts del modelo anterior.
2. Roles mes_portal y mes_lectura; incluye los dos procedimientos de ajustes de catálogo.
3. Diagnóstico del backend antes del arranque HTTP, sin abrir diario ni crear claves de sesión.
4. Perfil Azure y ejemplo de conexión dirigido al servidor propio.
5. CSV simulados, cargador y Power BI incorporados con procedencia explícita.

## Comprobación previa del estado real

El archivo `portal/db/azure/000_estado_actual.sql` contiene consultas de solo lectura
para inventariar tablas, vistas, procedimientos y el contrato de RegistrarMovimiento.
Todavía no se ejecutó. Debe revisarse con una cuenta autorizada que pueda ver los objetos.
Una cuenta sin visibilidad de metadatos puede ocultar objetos: no interpretar su ausencia
como confirmación de una base vacía.

El backend tiene un diagnóstico que no inicia el servidor ni llama procedimientos:

```sh
ASPNETCORE_ENVIRONMENT=Azure dotnet run --no-launch-profile --project portal/backend/Motherson.Api -- --diagnostico-azure --solo-configuracion
```

Requiere `ConnectionStrings__Sql` mediante un canal privado. Esa variante solo valida
configuración local: no conecta con Azure. Sin `--solo-configuracion` abre conexión y
ejecuta SELECT de metadatos/permisos; no habilita capturas. No se ejecutó esa variante.
El resultado no sustituye las pruebas de operaciones ni la comprobación del paquete histórico.

## Instalación futura: solo después de revisar el estado

- Ejecutar desde portal/db/azure con SQLCMD mode y detención ante errores.
- `instalar.sql` exige DB_NAME() = Motherson_L1 y ninguna tabla de usuario. Crea objetos
  dentro de una transacción; abortar ante el primer error. No usar ejecución que ignore
  las directivas SQLCMD. Si hay tablas, diseñar una migración sobre su esquema real.
- Instala 010_tablas, 011_movimientos, 012_objetos_portal, 013_indices_bi y 014_ajustes_catalogo.
- La carpeta raíz portal/db conserva los scripts históricos; no ejecutar 001–013 en lote.
- Crear por separado usuarios contenidos portal_app y powerbi_lector, con secretos
  fuera del repositorio. Luego aplicar 020_permisos.sql y revisar 030_verificar.sql.
- No se conceden permisos de escritura directa a la cuenta del portal ni acceso a hashes
  al rol lector. Revisar que las cuentas no hereden roles amplios de instalaciones previas.
- Elegir datos iniciales. La carga de simulación es opcional y separada; no hay carga automática.

## Pendientes antes de habilitar capturas

La arquitectura de movimientos aún es la del portal recibido: saldo en SQL y diario JSONL.
Esta entrega NO implementa todavía un historial central transaccional en Azure SQL.

- Definir y preparar el historial central y la idempotencia en SQL, con migración del diario.
- Resolver el arranque sin dependencia del paquete histórico de demostración, o provisionar
  temporalmente el paquete completo compatible. El ZIP no contiene manifiesto ni sus doce fuentes.
- Definir catálogo y saldo inicial: los CSV actuales son simulación.
- Revisar fecha_stock fija, es_simulado=1, calendario y turnos (la función recibida no cubre
  horas fuera de los turnos; REGISTRO exige id_turno).
- El procedimiento de capacidad valida el JSON y los límites numéricos; la regla de no bajar
  de cajas ocupadas sigue en el backend. Debe reforzarse en SQL junto con concurrencia.
- Antes de despliegue, definir MVP_ROOT, MVP_DIARIO_PATH, almacenamiento persistente de imágenes,
  claves de sesiones y hash administrativo; configurar AllowedHosts con el dominio real.
- No hay ejecución ni pruebas de Azure SQL en esta fase. La sintaxis/semántica SQL sigue pendiente
  de validación en una base de pruebas autorizada.

## Validación local

Resultados ejecutados:

- Backend: compilación sin errores ni advertencias.
- 11 comprobaciones nuevas de servidor, base y cifrado: aprobadas.
- 85 casos existentes de movimientos, idempotencia, capacidad, importación e imágenes: aprobados.
- Diagnóstico local sin conexión SQL: responde y no crea .local/keys ni diario.
- Diagnóstico local con servidor anterior: rechazado antes de abrir conexión.
- CSV: ocho tablas, claves y relaciones verificadas, 10,474 piezas, 58,422 atenciones.
- PBIX: idéntico al original; parámetros del TMDL adaptados.

Limitación del entorno: el runtime disponible es .NET 11. Se compila para net10.0;
las pruebas se ejecutaron con DOTNET_ROLL_FORWARD=Major. Falta repetirlas con runtime
.NET 10 en el entorno de destino. Las pruebas del diario simulan SQL y no demuestran
integración con Azure. No se ejecutó SQL ni se inició el servidor HTTP.

## Power BI

Pendiente crear o seleccionar espacio propio, definir permisos/licencia, publicar el PBIX,
configurar sus parámetros y credenciales lectoras. No se utiliza el espacio del otro equipo.
Consultar powerbi/README.md. Publicación e incrustación en el portal son trabajos distintos.
