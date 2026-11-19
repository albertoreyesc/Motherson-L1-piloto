-- PREPARADO, NO EJECUTADO. Solo para una Motherson_L1 sin tablas de usuario.
-- SQLCMD mode; abrir desde portal/db/azure. No usar como migración de una base existente.
-- No carga CSV, crea credenciales ni modifica firewall o recursos de Azure.
:On Error exit
SET NOCOUNT ON;
SET XACT_ABORT ON;
IF DB_NAME() <> N'Motherson_L1' THROW 51100, 'Destino incorrecto: se requiere Motherson_L1.', 1;
IF EXISTS(SELECT 1 FROM sys.tables WHERE is_ms_shipped=0)
    THROW 51101, 'La base tiene tablas. Requiere conciliación; no se instaló nada.', 1;
BEGIN TRANSACTION;
GO
:r 010_tablas.sql
:r 011_movimientos.sql
:r 012_objetos_portal.sql
:r 013_indices_bi.sql
:r 015_diario_sql.sql
:r 016_operaciones_transaccionales.sql
:r 017_vistas_y_apertura.sql
COMMIT;
GO
SELECT N'Objetos instalados; faltan usuarios, permisos y datos iniciales autorizados.' AS resultado;
