-- Diario transaccional: no sustituye ni elimina las ocho tablas operativas.
IF SCHEMA_ID('mes') IS NULL EXEC('CREATE SCHEMA mes AUTHORIZATION dbo');
GO
ALTER TABLE dbo.UBICACION ADD version_concurrencia int NOT NULL CONSTRAINT DF_UBICACION_version DEFAULT 1;
CREATE UNIQUE INDEX UX_PUESTO_usuario ON dbo.PUESTO_LOGISTICO(usuario) WHERE usuario IS NOT NULL;
CREATE TABLE mes.CONFIGURACION (
 id int NOT NULL PRIMARY KEY CHECK(id=1), version_esquema int NOT NULL,
 es_simulado bit NOT NULL, creado_utc datetime2(7) NOT NULL DEFAULT SYSUTCDATETIME()
);
INSERT mes.CONFIGURACION(id,version_esquema,es_simulado) VALUES(1,1,1);
CREATE TABLE mes.BASE_INVENTARIO (
 id int NOT NULL PRIMARY KEY CHECK(id=1), fecha_utc datetime2(7) NOT NULL,
 filas_json nvarchar(max) NOT NULL CHECK(ISJSON(filas_json)=1)
);
CREATE TABLE mes.OPERACION (
 id_operacion uniqueidentifier NOT NULL PRIMARY KEY,
 secuencia bigint IDENTITY NOT NULL UNIQUE,
 tipo varchar(20) NOT NULL CHECK(tipo IN ('MOVIMIENTO','AJUSTE')),
 cuenta nvarchar(80) NOT NULL, tipo_cuenta varchar(30) NOT NULL, puesto_cuenta int NULL,
 solicitud nvarchar(max) NOT NULL CHECK(ISJSON(solicitud)=1),
 respuesta nvarchar(max) NOT NULL CHECK(ISJSON(respuesta)=1),
 fecha_utc datetime2(7) NOT NULL DEFAULT SYSUTCDATETIME()
);
CREATE TABLE mes.MOVIMIENTO (
 id_operacion uniqueidentifier NOT NULL PRIMARY KEY REFERENCES mes.OPERACION(id_operacion),
 id_ubicacion int NOT NULL REFERENCES dbo.UBICACION(id_ubicacion),
 numero_parte varchar(50) NOT NULL, id_puesto int NOT NULL,
 tipo varchar(10) NOT NULL CHECK(tipo IN ('ENTRADA','SALIDA')),
 cantidad_capturada int NOT NULL CHECK(cantidad_capturada>0),
 unidad_capturada varchar(10) NOT NULL CHECK(unidad_capturada='CAJAS'),
 empaque_aplicado int NOT NULL CHECK(empaque_aplicado>0),
 piezas int NOT NULL CHECK(piezas>0), saldo_anterior int NOT NULL CHECK(saldo_anterior>=0),
 saldo_posterior int NOT NULL CHECK(saldo_posterior>=0), es_consumo bit NOT NULL,
 es_simulado bit NOT NULL,
 CONSTRAINT CK_MOVIMIENTO_saldo CHECK(saldo_posterior=saldo_anterior+CASE WHEN tipo='ENTRADA' THEN piezas ELSE -piezas END)
);
CREATE INDEX IX_MOVIMIENTO_ubicacion ON mes.MOVIMIENTO(id_ubicacion);
CREATE TABLE mes.AJUSTE (
 id_operacion uniqueidentifier NOT NULL REFERENCES mes.OPERACION(id_operacion),
 entidad varchar(20) NOT NULL, clave nvarchar(100) NOT NULL, campo varchar(30) NOT NULL,
 anterior nvarchar(250) NULL, nuevo nvarchar(250) NULL,
 PRIMARY KEY(id_operacion,entidad,clave,campo)
);
CREATE TABLE mes.ARCHIVO_LEGADO (
 sha256 char(64) NOT NULL PRIMARY KEY, archivo nvarchar(120) NOT NULL,
 filas_esperadas int NOT NULL CHECK(filas_esperadas>=0), completo bit NOT NULL DEFAULT 0,
 confirmado_utc datetime2(7) NULL
);
CREATE TABLE mes.HISTORICO_LEGADO (
 sha256 char(64) NOT NULL, linea int NOT NULL CHECK(linea>0), archivo nvarchar(120) NOT NULL,
 datos nvarchar(max) NOT NULL CHECK(ISJSON(datos)=1), importado_utc datetime2(7) NOT NULL DEFAULT SYSUTCDATETIME(),
 PRIMARY KEY(sha256,linea)
);
GO
