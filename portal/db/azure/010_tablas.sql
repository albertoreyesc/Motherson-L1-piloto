-- =====================================================================
-- 01 · Las 8 tablas del ERD (Motherson L1 · Smart Material Replenishment)
--
-- Cómo correrlo: en VS Code, conectados a su base de Azure SQL con su usuario administrador.
--                Ejecuten el archivo completo una sola vez, sobre una base vacía.
-- Qué crea: ESTACION, COLOR, PUESTO_LOGISTICO, TURNO, ESTANTERIA, MATERIAL, UBICACION, REGISTRO,
--           con sus llaves, relaciones y reglas (CHECK / UNIQUE).
-- Nota: PUESTO_LOGISTICO incluye usuario y password (hash), que el portal de captura usa para
--       el inicio de sesión por puesto. Power BI no los lee.
-- =====================================================================

CREATE TABLE dbo.ESTACION (
    id_estacion      INT IDENTITY(1,1) NOT NULL,
    id_linea         INT               NOT NULL,
    nombre_estacion  VARCHAR(100)      NOT NULL,
    CONSTRAINT PK_ESTACION PRIMARY KEY (id_estacion)
);

CREATE TABLE dbo.COLOR (
    id_color  INT IDENTITY(1,1) NOT NULL,
    nombre    VARCHAR(50)       NOT NULL,
    CONSTRAINT PK_COLOR PRIMARY KEY (id_color),
    CONSTRAINT UQ_COLOR_nombre UNIQUE (nombre)
);

CREATE TABLE dbo.PUESTO_LOGISTICO (
    id_puesto  INT IDENTITY(1,1) NOT NULL,
    Nombre     VARCHAR(100)      NOT NULL,
    Tipo       VARCHAR(50)       NULL,
    usuario    NVARCHAR(80)      NULL,   -- acceso del portal de Equipo 1 (login por puesto)
    [password] NVARCHAR(500)     NULL,   -- hash de la contraseña, nunca la contraseña en claro
    CONSTRAINT PK_PUESTO_LOGISTICO PRIMARY KEY (id_puesto)
);

CREATE TABLE dbo.TURNO (
    id_turno      INT IDENTITY(1,1) NOT NULL,
    nombre_turno  VARCHAR(50)       NOT NULL,
    hora_inicio   TIME(0)           NOT NULL,
    hora_fin      TIME(0)           NOT NULL,
    CONSTRAINT PK_TURNO PRIMARY KEY (id_turno)
);

CREATE TABLE dbo.ESTANTERIA (
    id_estanteria     INT IDENTITY(1,1) NOT NULL,
    id_estacion       INT               NOT NULL,
    estanteria        VARCHAR(50)       NOT NULL,
    nivel             VARCHAR(20)       NULL,
    cajas_estanteria  INT               NOT NULL DEFAULT 0,
    CONSTRAINT PK_ESTANTERIA PRIMARY KEY (id_estanteria),
    CONSTRAINT FK_ESTANTERIA_ESTACION FOREIGN KEY (id_estacion)
        REFERENCES dbo.ESTACION (id_estacion),
    CONSTRAINT CK_ESTANTERIA_cajas CHECK (cajas_estanteria >= 0)
);

CREATE TABLE dbo.MATERIAL (
    numero_parte       VARCHAR(50)  NOT NULL,
    id_puesto          INT          NOT NULL,
    id_color           INT          NULL,
    descripcion        VARCHAR(255) NULL,
    uso                VARCHAR(100) NULL,
    cantidad_por_caja  INT          NOT NULL DEFAULT 1,
    stock              INT          NOT NULL DEFAULT 0,
    minimo             INT          NOT NULL DEFAULT 0,
    CONSTRAINT PK_MATERIAL PRIMARY KEY (numero_parte),
    CONSTRAINT FK_MATERIAL_PUESTO FOREIGN KEY (id_puesto)
        REFERENCES dbo.PUESTO_LOGISTICO (id_puesto),
    CONSTRAINT FK_MATERIAL_COLOR FOREIGN KEY (id_color)
        REFERENCES dbo.COLOR (id_color),
    CONSTRAINT CK_MATERIAL_cantidad CHECK (cantidad_por_caja > 0),
    CONSTRAINT CK_MATERIAL_stock    CHECK (stock >= 0),
    CONSTRAINT CK_MATERIAL_minimo   CHECK (minimo >= 0)
);

CREATE TABLE dbo.UBICACION (
    id_ubicacion   INT IDENTITY(1,1) NOT NULL,
    numero_parte   VARCHAR(50)       NOT NULL,
    id_estanteria  INT               NOT NULL,
    cantidad       INT               NOT NULL DEFAULT 0,
    CONSTRAINT PK_UBICACION PRIMARY KEY (id_ubicacion),
    CONSTRAINT FK_UBICACION_MATERIAL FOREIGN KEY (numero_parte)
        REFERENCES dbo.MATERIAL (numero_parte),
    CONSTRAINT FK_UBICACION_ESTANTERIA FOREIGN KEY (id_estanteria)
        REFERENCES dbo.ESTANTERIA (id_estanteria),
    CONSTRAINT UQ_UBICACION_parte_estanteria UNIQUE (numero_parte, id_estanteria),
    CONSTRAINT CK_UBICACION_cantidad CHECK (cantidad >= 0)
);

CREATE TABLE dbo.REGISTRO (
    id_registro        INT IDENTITY(1,1) NOT NULL,
    id_puesto          INT               NOT NULL,
    id_ubicacion       INT               NOT NULL,
    id_turno           INT               NOT NULL,
    fecha_hora_inicio  DATETIME2(0)      NOT NULL DEFAULT SYSDATETIME(),
    fecha_hora_fin     DATETIME2(0)      NULL,
    CONSTRAINT PK_REGISTRO PRIMARY KEY (id_registro),
    CONSTRAINT FK_REGISTRO_PUESTO FOREIGN KEY (id_puesto)
        REFERENCES dbo.PUESTO_LOGISTICO (id_puesto),
    CONSTRAINT FK_REGISTRO_UBICACION FOREIGN KEY (id_ubicacion)
        REFERENCES dbo.UBICACION (id_ubicacion),
    CONSTRAINT FK_REGISTRO_TURNO FOREIGN KEY (id_turno)
        REFERENCES dbo.TURNO (id_turno),
    CONSTRAINT CK_REGISTRO_fechas CHECK (fecha_hora_fin IS NULL OR fecha_hora_fin >= fecha_hora_inicio)
);
GO
