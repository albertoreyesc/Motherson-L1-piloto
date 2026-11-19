IF DB_NAME()<>'Motherson_L1_MVP_Pruebas' THROW 51000,'Solo Pruebas',1;
DECLARE @post int=(SELECT MIN(id_puesto) FROM dbo.PUESTO_LOGISTICO),@estacion int,@estante int,@u int,@usuario int=(SELECT id_usuarios FROM dbo.USUARIOS WHERE nombre_usuario='admin.local');
IF NOT EXISTS(SELECT 1 FROM dbo.ESTACION WHERE nombre_estacion='LAB-PRUEBAS') INSERT dbo.ESTACION(id_linea,nombre_estacion)VALUES(1,'LAB-PRUEBAS');
SELECT @estacion=id_estacion FROM dbo.ESTACION WHERE nombre_estacion='LAB-PRUEBAS';
DECLARE casos CURSOR LOCAL FOR SELECT parte FROM(VALUES('LAB-MAT-001'),('LAB-CONC-SAL'),('LAB-CONC-ENT'),('LAB-ROLLBACK'),('LAB-PENDIENTE'))v(parte);
DECLARE @parte varchar(100);OPEN casos;FETCH NEXT FROM casos INTO @parte;
WHILE @@FETCH_STATUS=0 BEGIN
 IF NOT EXISTS(SELECT 1 FROM dbo.MATERIAL WHERE numero_parte=@parte) BEGIN
  INSERT dbo.MATERIAL(numero_parte,id_puesto,descripcion,cantidad_por_caja,minimo)VALUES(@parte,@post,'Datos exclusivos de prueba',24,15);
  INSERT dbo.ESTANTERIA(id_estacion,estanteria,nivel,cajas_estanteria)VALUES(@estacion,@parte,'A',6);SET @estante=CONVERT(int,SCOPE_IDENTITY());
  INSERT dbo.UBICACION(numero_parte,id_estanteria,cantidad)VALUES(@parte,@estante,NULL);SET @u=CONVERT(int,SCOPE_IDENTITY());
  IF @parte<>'LAB-PENDIENTE' BEGIN
   INSERT dbo.REGISTRO(id_usuarios,id_ubicacion,id_puesto,tipo_movimiento,cantidad_movimiento,saldo_anterior,saldo_posterior,fecha_movimiento,fecha_hora_inicio,fecha_hora_fin,id_operacion,huella_peticion,origen)VALUES(@usuario,@u,@post,'APERTURA',72,0,72,SYSUTCDATETIME(),SYSUTCDATETIME(),SYSUTCDATETIME(),NEWID(),REPLICATE('0',64),'APERTURA_SIMULADA');
   UPDATE dbo.UBICACION SET cantidad=72 WHERE id_ubicacion=@u;
  END;
 END;
 FETCH NEXT FROM casos INTO @parte;
END;CLOSE casos;DEALLOCATE casos;

-- Asignación de la cuenta de prueba al puesto de sus materiales LAB.
UPDATE dbo.USUARIOS SET id_puesto=@post WHERE nombre_usuario=N'entrada.prueba' AND perfil='ENTRADAS' AND id_puesto IS NULL;
