-- Solo para importar el diagrama. No ejecutar en la base existente.

CREATE TABLE dbo.ESTACION(id_estacion int IDENTITY PRIMARY KEY,id_linea int NOT NULL CHECK(id_linea=1),nombre_estacion nvarchar(100) NOT NULL,UNIQUE(id_linea,nombre_estacion));

CREATE TABLE dbo.ESTANTERIA(id_estanteria int IDENTITY PRIMARY KEY,id_estacion int NOT NULL REFERENCES dbo.ESTACION,estanteria nvarchar(80) NOT NULL,nivel nvarchar(30) NOT NULL,cajas_estanteria int NULL CHECK(cajas_estanteria>0),UNIQUE(id_estacion,estanteria,nivel));

CREATE TABLE dbo.COLOR(id_color int IDENTITY PRIMARY KEY,nombre nvarchar(100) NOT NULL UNIQUE);

CREATE TABLE dbo.PUESTO_LOGISTICO(id_puesto int IDENTITY PRIMARY KEY,nombre nvarchar(120) NOT NULL UNIQUE,tipo nvarchar(60) NULL);

CREATE TABLE dbo.MATERIAL(numero_parte varchar(100) COLLATE Latin1_General_100_BIN2 PRIMARY KEY,id_puesto int NOT NULL REFERENCES dbo.PUESTO_LOGISTICO,id_color int NULL REFERENCES dbo.COLOR,descripcion nvarchar(250) NULL,uso nvarchar(150) NULL,cantidad_por_caja int NULL CHECK(cantidad_por_caja>0),minimo int NULL CHECK(minimo>=0));

CREATE TABLE dbo.UBICACION(id_ubicacion int IDENTITY PRIMARY KEY,numero_parte varchar(100) COLLATE Latin1_General_100_BIN2 NOT NULL REFERENCES dbo.MATERIAL,id_estanteria int NOT NULL REFERENCES dbo.ESTANTERIA,cantidad int NULL CHECK(cantidad>=0),version_concurrencia int NOT NULL DEFAULT 1 CHECK(version_concurrencia>0),UNIQUE(numero_parte,id_estanteria));

CREATE TABLE dbo.TURNO(id_turno int IDENTITY PRIMARY KEY,nombre_turno nvarchar(40) NOT NULL UNIQUE,hora_inicio time(0) NOT NULL,hora_fin time(0) NOT NULL,CHECK(hora_inicio<hora_fin));

CREATE TABLE dbo.USUARIOS(id_usuarios int IDENTITY PRIMARY KEY,nombre_usuario nvarchar(80) NOT NULL UNIQUE,nombre_visible nvarchar(150) NULL,hash_contrasena nvarchar(500) NOT NULL,esta_activo bit NOT NULL,perfil varchar(20) NOT NULL CHECK(perfil IN('CONSULTA','ENTRADAS','MOVIMIENTOS','ADMINISTRADOR')));

CREATE TABLE dbo.REGISTRO(id_registro int IDENTITY PRIMARY KEY,id_usuarios int NOT NULL REFERENCES dbo.USUARIOS,id_ubicacion int NOT NULL REFERENCES dbo.UBICACION,id_turno int NULL REFERENCES dbo.TURNO,id_puesto int NOT NULL REFERENCES dbo.PUESTO_LOGISTICO,tipo_movimiento varchar(10) NOT NULL CHECK(tipo_movimiento IN('ENTRADA','SALIDA','APERTURA')),cantidad_movimiento int NOT NULL,cantidad_cajas int NULL,cantidad_por_caja_aplicada int NULL,saldo_anterior int NOT NULL CHECK(saldo_anterior>=0),saldo_posterior int NOT NULL CHECK(saldo_posterior>=0),fecha_movimiento datetime2(7) NOT NULL,fecha_hora_inicio datetime2(7) NOT NULL,fecha_hora_fin datetime2(7) NOT NULL,id_operacion uniqueidentifier NOT NULL UNIQUE,huella_peticion char(64) NOT NULL,origen varchar(24) NOT NULL CHECK(origen IN('CAPTURA_SIMULADA','APERTURA_SIMULADA','CAPTURA_REAL','APERTURA_REAL')),es_simulado bit NOT NULL,
 CONSTRAINT CK_REGISTRO_TIPO CHECK((tipo_movimiento='ENTRADA' AND cantidad_movimiento>0 AND cantidad_cajas IS NOT NULL AND cantidad_por_caja_aplicada IS NOT NULL AND cantidad_cajas>0 AND cantidad_por_caja_aplicada>0 AND CONVERT(bigint,cantidad_movimiento)=CONVERT(bigint,cantidad_cajas)*cantidad_por_caja_aplicada AND CONVERT(bigint,saldo_posterior)=CONVERT(bigint,saldo_anterior)+cantidad_movimiento) OR (tipo_movimiento='SALIDA' AND cantidad_movimiento>0 AND cantidad_cajas IS NULL AND cantidad_por_caja_aplicada IS NULL AND CONVERT(bigint,saldo_posterior)=CONVERT(bigint,saldo_anterior)-cantidad_movimiento) OR (tipo_movimiento='APERTURA' AND cantidad_movimiento>=0 AND cantidad_cajas IS NULL AND cantidad_por_caja_aplicada IS NULL AND saldo_anterior=0 AND saldo_posterior=cantidad_movimiento)),
 CHECK(fecha_hora_fin>=fecha_hora_inicio),CHECK((es_simulado=1 AND origen IN('CAPTURA_SIMULADA','APERTURA_SIMULADA')) OR(es_simulado=0 AND origen IN('CAPTURA_REAL','APERTURA_REAL'))));