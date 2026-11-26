# Modelo relacional implementado — MVP L1

> Antecedente conservado para trazabilidad. No es la guía de instalación ni de operación vigente. Consulta el [índice actual](../README.md).

Fase C · 02/11/2026. Nueve tablas operativas dbo, 53 columnas; tres tablas de archivo separadas, seis vistas de lectura. Las tablas de archivo no duplican el inventario operativo ni agregan flujos de negocio.

## Tablas operativas

| Tabla | Columnas implementadas |
| --- | --- |
| ESTACION | id_estacion INT PK, id_linea INT CHECK=1, nombre_estacion NVARCHAR(100); UNIQUE(línea,nombre) |
| ESTANTERIA | id_estanteria INT PK, id_estacion INT FK, estanteria NVARCHAR(80), nivel NVARCHAR(30), cajas_estanteria INT opcional; UNIQUE(estación,estantería,nivel) |
| COLOR | id_color INT PK, nombre NVARCHAR(100) UNIQUE |
| PUESTO_LOGISTICO | id_puesto INT PK, nombre NVARCHAR(120) UNIQUE, tipo NVARCHAR(60) opcional |
| MATERIAL | numero_parte VARCHAR(100) PK, id_puesto INT FK, id_color INT FK opcional, descripcion NVARCHAR(250), uso NVARCHAR(150), cantidad_por_caja INT, minimo INT; últimos cuatro opcionales |
| UBICACION | id_ubicacion INT PK, numero_parte VARCHAR(100) FK, id_estanteria INT FK, cantidad INT opcional, version_concurrencia INT; UNIQUE(parte,estantería/nivel) |
| TURNO | id_turno INT PK, nombre_turno NVARCHAR(40) UNIQUE, hora_inicio TIME(0), hora_fin TIME(0); rangos diurnos sin solapamiento en los datos cargados |
| USUARIOS | id_usuarios INT PK, nombre_usuario NVARCHAR(80) UNIQUE, nombre_visible NVARCHAR(150) opcional, hash_contrasena NVARCHAR(500), esta_activo BIT, perfil VARCHAR(20) |
| REGISTRO | Las 18 columnas detalladas debajo |

Todos los identificadores propios son INT. Los textos humanos conservan Unicode. Los números de parte son texto con colación binaria y no se reinterpretan como números. No se crea tabla LINEA porque el alcance está limitado a L1.

Stock no es una columna física de MATERIAL: reporting.vw_kpi_inventario_actual lo calcula desde UBICACION. Un NULL no se convierte en cero; una suma parcial no se presenta como total completo.

## REGISTRO y las dos adiciones de fase C

| Columna | Tipo | Regla |
| --- | --- | --- |
| id_registro | INT IDENTITY | PK |
| id_usuarios | INT | FK USUARIOS, responsable autenticado de la captura/apertura |
| id_ubicacion | INT | FK UBICACION |
| id_turno | INT NULL | FK TURNO; NULL fuera de horario |
| id_puesto | INT | FK PUESTO_LOGISTICO, puesto aplicado al registrar |
| tipo_movimiento | VARCHAR(10) | ENTRADA / SALIDA / APERTURA |
| cantidad_movimiento | INT | Piezas positivas; apertura admite cero |
| cantidad_cajas | INT NULL | Solo entrada, entero positivo |
| cantidad_por_caja_aplicada | INT NULL | Solo entrada, factor positivo conservado |
| saldo_anterior | INT | Piezas, no negativo |
| saldo_posterior | INT | Piezas, no negativo |
| fecha_movimiento | DATETIME2(7) | **Adición fase C:** instante del hecho contable, UTC de SQL Server |
| fecha_hora_inicio | DATETIME2(7) | Inicio del procesamiento de petición en servidor |
| fecha_hora_fin | DATETIME2(7) | Instante del registro dentro de transacción; no tiempo físico de entrega |
| id_operacion | UNIQUEIDENTIFIER | UNIQUE; reintento de la misma captura |
| huella_peticion | CHAR(64) | SHA-256 canónico: ubicación, tipo, cantidad y versión |
| origen | VARCHAR(24) | CAPTURA_SIMULADA/APERTURA_SIMULADA; esquema también admite CAPTURA_REAL/APERTURA_REAL |
| es_simulado | BIT | **Adición fase C:** naturaleza explícita para separar KPIs; consistente con origen mediante CHECK |

Estas dos columnas se justificaron antes de ejecutar la carga: se necesita fecha del movimiento separada del intervalo de captura y naturaleza inequívoca para reporting. El resto corresponde al diseño A/B aprobado. La instalación permanece en modo simulado: el navegador no puede decidir convertir una captura en real.

CHECK comprueba cantidades, equivalencia cajas×factor, saldos anterior/posterior, naturaleza y secuencia de fechas. Un índice filtrado permite una sola APERTURA por ubicación. Los identificadores de operación son únicos. No se usa TIMESTAMP como fecha.

## Transacción y autorización

RegistrarMovimiento es el único procedimiento de escritura expuesto a la cuenta SQL de la aplicación. En una transacción con XACT_ABORT, toma un bloqueo común de inventario, revalida la cuenta y su perfil, resuelve reintentos, bloquea la ubicación, verifica versión y saldo/capacidad, actualiza cantidad e inserta comprobante. Confirma ambos o revierte ambos.

La ocupación de un nivel suma techo(piezas/factor) de todos los materiales. La capacidad no se incrementa automáticamente. No hay reservas. Una salida válida no necesita capacidad porque no añade ocupación.

El login SQL de la aplicación tiene denegada la escritura directa en dbo y archivo, pero puede ejecutar el procedimiento por cadena de propiedad. El login lector no puede ejecutar el procedimiento ni leer USUARIOS. Los comprobantes no se editan/borran; las identidades de estación y ubicación no se reasignan después para cambiar el contexto de movimientos pasados.

Las sesiones y la protección CSRF usan nombres y ámbito criptográfico diferenciados para Desarrollo y Pruebas; una cookie de una base no autentica en la otra. Se reutilizaron cookies, PasswordHasher, CSRF, límite de intentos, manejo de errores, cliente React y bloques de entrada. No se copiaron contraseñas de otras instalaciones. Las identidades históricas se conservan como referencias de origen; no se crean cuentas de acceso para los doce responsables ficticios.

## Archivo consultable

| Tabla de archivo | Propósito |
| --- | --- |
| archivo.CORTE | Maestro, revisión, límite temporal, fecha de carga, huella de fuentes y sello |
| archivo.FUENTE | Diez binarios gzip originales, SHA-256 y número de filas; recuperación íntegra mediante vista lectora |
| archivo.EVENTO | Los 709,042 eventos tipificados con IDs, referencias y contexto de origen, incluidos eventos no operativos |

Archivo sellado rechaza nuevas inserciones y todo UPDATE/DELETE ordinario; el corte no puede modificarse después del sello. Los usuarios de aplicación/BI no tienen permisos para alterar estas tablas. La administración de SQL sigue siendo una autoridad privilegiada: el sello no pretende sustituir el control de acceso del motor ni impedir a un sysadmin cambiar el DDL.

Las referencias históricas no tienen FK hacia los catálogos actuales del MVP: eso evitaría conservar valores desconocidos y podría atribuir datos pasados a una ubicación distinta. Los IDs numéricos se acompañan de espacio_identificadores en las vistas. H21:maestro:id y MVP:id evitan colisiones.

## Relaciones operativas

~~~~mermaid
erDiagram
 ESTACION ||--o{ ESTANTERIA : contiene
 ESTANTERIA ||--o{ UBICACION : aloja
 MATERIAL ||--o{ UBICACION : identifica
 PUESTO_LOGISTICO ||--o{ MATERIAL : corresponde
 COLOR o|--o{ MATERIAL : clasifica
 UBICACION ||--o{ REGISTRO : registra
 USUARIOS ||--o{ REGISTRO : captura
 TURNO o|--o{ REGISTRO : clasifica
 PUESTO_LOGISTICO ||--o{ REGISTRO : puesto_aplicado
~~~~

SQL autoritativo: portal/db/001_mvp.sql, 002_vistas.sql y 003_movimientos.sql. El contrato de reporting, tipos, unidades y relaciones entre los dos espacios está en CONTRATO_DATOS_EQUIPO2.md. No se mantienen en ejecución el journal versionado, SQLite, generadores, alarmas, solicitudes ni reservas del esquema anterior.
