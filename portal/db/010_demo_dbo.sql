-- Instala el modelo final en una base candidata vacía.
IF DB_NAME() <> N'Motherson_L1_MVP_Demo' OR @@TRANCOUNT = 0
    THROW 51000, 'Se requiere una transacción en la base de demostración.', 1;
IF EXISTS (SELECT 1 FROM sys.tables WHERE is_ms_shipped=0)
    THROW 51000, 'La base de demostración debe estar vacía para instalar el esquema.', 1;

CREATE TABLE dbo.ESTACION (
    id_estacion int IDENTITY PRIMARY KEY,
    id_linea int NOT NULL CHECK(id_linea=1),
    nombre_estacion nvarchar(100) NOT NULL,
    UNIQUE(id_linea,nombre_estacion)
);
CREATE TABLE dbo.ESTANTERIA (
    id_estanteria int IDENTITY PRIMARY KEY,
    id_estacion int NOT NULL REFERENCES dbo.ESTACION(id_estacion),
    estanteria nvarchar(80) NOT NULL,
    nivel nvarchar(30) NOT NULL,
    cajas_estanteria int NULL CHECK(cajas_estanteria>0),
    UNIQUE(id_estacion,estanteria,nivel)
);
CREATE TABLE dbo.PUESTO_LOGISTICO (
    id_puesto int IDENTITY PRIMARY KEY,
    nombre nvarchar(120) NOT NULL UNIQUE,
    usuario nvarchar(80) NULL,
    [password] nvarchar(500) NULL,
    CONSTRAINT CK_PUESTO_ACCESO CHECK (
        (usuario IS NULL AND [password] IS NULL) OR
        (usuario IS NOT NULL AND [password] IS NOT NULL AND usuario<>N'admin.local'))
);
CREATE UNIQUE INDEX UX_PUESTO_USUARIO ON dbo.PUESTO_LOGISTICO(usuario) WHERE usuario IS NOT NULL;
CREATE TABLE dbo.COLOR (
    id_color int IDENTITY PRIMARY KEY,
    nombre varchar(10) COLLATE Latin1_General_100_BIN2 NOT NULL UNIQUE
);
CREATE TABLE dbo.MATERIAL (
    numero_parte varchar(100) COLLATE Latin1_General_100_BIN2 PRIMARY KEY,
    id_puesto int NOT NULL REFERENCES dbo.PUESTO_LOGISTICO(id_puesto),
    descripcion nvarchar(250) NULL,
    uso nvarchar(150) NULL,
    cantidad_por_caja int NULL CHECK(cantidad_por_caja>0),
    minimo int NULL CHECK(minimo>=0),
    id_color int NULL REFERENCES dbo.COLOR(id_color),
    stock int NULL CONSTRAINT CK_MATERIAL_STOCK CHECK(stock>=0)
);
CREATE TABLE dbo.UBICACION (
    id_ubicacion int IDENTITY PRIMARY KEY,
    numero_parte varchar(100) COLLATE Latin1_General_100_BIN2 NOT NULL REFERENCES dbo.MATERIAL(numero_parte),
    id_estanteria int NOT NULL REFERENCES dbo.ESTANTERIA(id_estanteria),
    UNIQUE(numero_parte,id_estanteria)
);
CREATE INDEX IX_UBICACION_ESTANTERIA ON dbo.UBICACION(id_estanteria);
CREATE TABLE dbo.TURNO (
    id_turno int IDENTITY PRIMARY KEY,
    nombre_turno nvarchar(40) NOT NULL UNIQUE,
    hora_inicio time(0) NOT NULL,
    hora_fin time(0) NOT NULL,
    CHECK(hora_inicio<hora_fin)
);
CREATE TABLE dbo.REGISTRO (
    id_registro int IDENTITY PRIMARY KEY,
    id_puesto int NOT NULL REFERENCES dbo.PUESTO_LOGISTICO(id_puesto),
    id_ubicacion int NOT NULL REFERENCES dbo.UBICACION(id_ubicacion),
    fecha_hora_inicio datetime2(7) NOT NULL,
    fecha_hora_fin datetime2(7) NULL,
    id_turno int NULL CONSTRAINT FK_REGISTRO_TURNO REFERENCES dbo.TURNO(id_turno),
    CONSTRAINT CK_REGISTRO_INTERVALO CHECK(fecha_hora_fin>=fecha_hora_inicio)
);
CREATE UNIQUE INDEX UX_REGISTRO_ATENCION_ABIERTA
ON dbo.REGISTRO(id_puesto,id_ubicacion) WHERE fecha_hora_fin IS NULL;
CREATE INDEX IX_REGISTRO_PUESTO_FECHA ON dbo.REGISTRO(id_puesto,fecha_hora_inicio DESC,id_registro DESC);
GO
