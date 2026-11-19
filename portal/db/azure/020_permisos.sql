-- Cuenta administradora, después de instalar objetos y crear usuarios contenidos
-- portal_app y powerbi_lector por un canal de aprovisionamiento privado.
-- No contiene contraseñas. No añade db_owner ni db_datawriter.
SET XACT_ABORT ON;
IF DB_NAME() <> N'Motherson_L1' THROW 51100, 'Destino incorrecto.', 1;
IF USER_ID('portal_app') IS NULL OR USER_ID('powerbi_lector') IS NULL
    THROW 51102, 'Primero deben existir portal_app y powerbi_lector.', 1;
BEGIN TRANSACTION;
IF DATABASE_PRINCIPAL_ID('mes_portal') IS NULL CREATE ROLE mes_portal AUTHORIZATION dbo;
IF DATABASE_PRINCIPAL_ID('mes_lectura') IS NULL CREATE ROLE mes_lectura AUTHORIZATION dbo;
GRANT SELECT ON dbo.ESTACION TO mes_portal;
GRANT SELECT ON dbo.COLOR TO mes_portal;
GRANT SELECT ON dbo.PUESTO_LOGISTICO TO mes_portal;
GRANT SELECT ON dbo.TURNO TO mes_portal;
GRANT SELECT ON dbo.ESTANTERIA TO mes_portal;
GRANT SELECT ON dbo.MATERIAL TO mes_portal;
GRANT SELECT ON dbo.UBICACION TO mes_portal;
GRANT SELECT ON dbo.REGISTRO TO mes_portal;
GRANT SELECT ON dbo.vw_modelo_puestos TO mes_portal;
GRANT SELECT ON dbo.vw_kpi_inventario_actual TO mes_portal;
GRANT SELECT ON dbo.vw_kpi_atenciones TO mes_portal;
GRANT EXECUTE ON dbo.IniciarAtencion TO mes_portal;
GRANT EXECUTE ON dbo.FinalizarAtencion TO mes_portal;
GRANT EXECUTE ON dbo.GuardarAccesoPuesto TO mes_portal;
GRANT EXECUTE ON dbo.RegistrarMovimiento TO mes_portal;
GRANT EXECUTE ON dbo.AjustarCapacidadEstanteria TO mes_portal;
GRANT EXECUTE ON dbo.AjustarMateriales TO mes_portal;
GRANT SELECT ON dbo.ESTACION TO mes_lectura;
GRANT SELECT ON dbo.COLOR TO mes_lectura;
GRANT SELECT ON dbo.PUESTO_LOGISTICO(id_puesto,Nombre,Tipo) TO mes_lectura;
GRANT SELECT ON dbo.TURNO TO mes_lectura;
GRANT SELECT ON dbo.ESTANTERIA TO mes_lectura;
GRANT SELECT ON dbo.MATERIAL TO mes_lectura;
GRANT SELECT ON dbo.UBICACION TO mes_lectura;
GRANT SELECT ON dbo.REGISTRO TO mes_lectura;
GRANT SELECT ON dbo.vw_registro_bi TO mes_lectura;
IF NOT EXISTS(SELECT 1 FROM sys.database_role_members WHERE role_principal_id=DATABASE_PRINCIPAL_ID('mes_portal') AND member_principal_id=USER_ID('portal_app'))
    ALTER ROLE mes_portal ADD MEMBER portal_app;
IF NOT EXISTS(SELECT 1 FROM sys.database_role_members WHERE role_principal_id=DATABASE_PRINCIPAL_ID('mes_lectura') AND member_principal_id=USER_ID('powerbi_lector'))
    ALTER ROLE mes_lectura ADD MEMBER powerbi_lector;
COMMIT;
