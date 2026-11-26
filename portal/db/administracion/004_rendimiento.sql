-- Rendimiento con meses de operación: el estado de la línea ya no carga las atenciones terminadas.
-- Las atenciones cerradas siguen en mes2.ENTIDAD (y en mes2.EVENTO); KPIs, período disponible y Power BI las
-- consultan aparte por fecha. Idempotente: reemplaza mes2.Leer con el mismo contrato y no cambia datos.
CREATE OR ALTER PROCEDURE mes2.Leer @usuario int,@linea int,@corte datetime2(7)=NULL
AS
BEGIN
 SET NOCOUNT ON;
 IF NOT EXISTS(SELECT 1 FROM mes2.ACCESO a JOIN mes.USUARIO_PILOTO u ON u.id_usuario=a.usuario WHERE a.usuario=@usuario AND a.linea=@linea AND u.activo=1) THROW 51003,N'Línea no autorizada.',1;
 SELECT l.*,SYSUTCDATETIME() ahora,u.rol,a.puesto FROM mes2.LINEA l JOIN mes2.ACCESO a ON a.linea=l.id JOIN mes.USUARIO_PILOTO u ON u.id_usuario=a.usuario WHERE l.id=@linea AND a.usuario=@usuario;
 IF @corte IS NULL
  SELECT tipo,id,datos FROM mes2.ENTIDAD WHERE linea=@linea AND (tipo<>'atencion' OR JSON_VALUE(datos,'$.fin') IS NULL);
 ELSE BEGIN
  IF EXISTS(SELECT 1 FROM mes2.LINEA WHERE id=@linea AND apertura>@corte) THROW 51004,N'No hay apertura acreditada para ese corte.',1;
  WITH versiones AS(SELECT j.tipo,j.id,j.datos,ROW_NUMBER() OVER(PARTITION BY j.tipo,j.id ORDER BY e.secuencia DESC) rn FROM mes2.EVENTO e CROSS APPLY OPENJSON(e.cambios) WITH(tipo varchar(20),id nvarchar(100),datos nvarchar(max) AS JSON) j WHERE e.linea=@linea AND e.fecha<=@corte)
  SELECT tipo,id,datos FROM versiones WHERE rn=1 AND datos IS NOT NULL AND (tipo<>'atencion' OR JSON_VALUE(datos,'$.fin') IS NULL);
 END;
END;
GO
