CREATE OR ALTER TRIGGER dbo.TR_PRUEBA_FALLO_REGISTRO ON dbo.REGISTRO AFTER INSERT AS
 IF DB_NAME()='Motherson_L1_MVP_Pruebas' AND EXISTS(SELECT 1 FROM inserted i JOIN dbo.UBICACION u ON u.id_ubicacion=i.id_ubicacion WHERE u.numero_parte='LAB-ROLLBACK' AND i.tipo_movimiento<>'APERTURA') THROW 51009,'Fallo de prueba después de actualizar saldo: transacción revertida.',1;
