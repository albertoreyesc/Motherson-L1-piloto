-- Fuentes de compatibilidad para conservar las ocho tablas y los visuales del informe.
-- Solo vistas y permisos sobre el piloto activo. No modifica saldos ni tablas originales.
IF DB_NAME() NOT LIKE 'MES_Integracion_Pruebas_%' AND DB_NAME() <> 'Motherson_L1'
 THROW 51120,'Base no autorizada para fuentes BI v2.',1;
IF NOT EXISTS(SELECT 1 FROM mes2.CONFIG WHERE id=1 AND activo=1)
 THROW 51121,'Se requiere la administración v2 activa.',1;
GO
CREATE OR ALTER VIEW mes2.bi_ESTACION AS
SELECT CONCAT(linea,':',id) COLLATE Latin1_General_100_BIN2 id_estacion,linea id_linea,JSON_VALUE(datos,'$.nombre') nombre_estacion FROM mes2.ENTIDAD WHERE tipo='estacion';
GO
CREATE OR ALTER VIEW mes2.bi_PUESTO_LOGISTICO AS
SELECT CONCAT(linea,':',id) COLLATE Latin1_General_100_BIN2 id_puesto,JSON_VALUE(datos,'$.nombre') Nombre,CAST(NULL AS nvarchar(80)) Tipo FROM mes2.ENTIDAD WHERE tipo='puesto';
GO
CREATE OR ALTER VIEW mes2.bi_ESTANTERIA AS
SELECT CONCAT(linea,':',id) COLLATE Latin1_General_100_BIN2 id_estanteria,CONCAT(linea,':',JSON_VALUE(datos,'$.estacion')) COLLATE Latin1_General_100_BIN2 id_estacion,JSON_VALUE(datos,'$.nombre') estanteria,JSON_VALUE(datos,'$.nivel') nivel,TRY_CONVERT(int,JSON_VALUE(datos,'$.capacidad')) cajas_estanteria FROM mes2.ENTIDAD WHERE tipo='estanteria';
GO
CREATE OR ALTER VIEW mes2.bi_COLOR AS
SELECT CONCAT('legado:',id_color) COLLATE Latin1_General_100_BIN2 id_color,nombre FROM dbo.COLOR
UNION
SELECT CONCAT('variante:',JSON_VALUE(datos,'$.variante')),JSON_VALUE(datos,'$.variante') FROM mes2.ENTIDAD WHERE tipo='material' AND COALESCE(JSON_VALUE(datos,'$.variante'),'')<>'';
GO
CREATE OR ALTER VIEW mes2.bi_MATERIAL AS
SELECT CONCAT(m.linea,':',m.id) COLLATE Latin1_General_100_BIN2 clave_material,JSON_VALUE(m.datos,'$.numeroParte') numero_parte,CONCAT(m.linea,':',JSON_VALUE(m.datos,'$.puesto')) COLLATE Latin1_General_100_BIN2 id_puesto,
 CASE WHEN COALESCE(JSON_VALUE(m.datos,'$.variante'),'')<>'' THEN CONCAT('variante:',JSON_VALUE(m.datos,'$.variante')) WHEN old.id_color IS NOT NULL THEN CONCAT('legado:',old.id_color) END COLLATE Latin1_General_100_BIN2 id_color,
 JSON_VALUE(m.datos,'$.descripcion') descripcion,JSON_VALUE(m.datos,'$.uso') uso,TRY_CONVERT(int,JSON_VALUE(m.datos,'$.empaque')) cantidad_por_caja,
 (SELECT COALESCE(SUM(CONVERT(bigint,JSON_VALUE(u.datos,'$.cantidad'))),0) FROM mes2.ENTIDAD u WHERE u.tipo='ubicacion' AND u.linea=m.linea AND JSON_VALUE(u.datos,'$.material') COLLATE Latin1_General_100_BIN2=m.id) stock,
 TRY_CONVERT(int,JSON_VALUE(m.datos,'$.minimo')) minimo
FROM mes2.ENTIDAD m LEFT JOIN dbo.MATERIAL old ON m.linea=1 AND JSON_VALUE(m.datos,'$.numeroParteLegado') COLLATE Latin1_General_100_BIN2=old.numero_parte COLLATE Latin1_General_100_BIN2 WHERE m.tipo='material';
GO
CREATE OR ALTER VIEW mes2.bi_UBICACION AS
SELECT CONCAT(u.linea,':',u.id_ubicacion) COLLATE Latin1_General_100_BIN2 id_ubicacion,CONCAT(u.linea,':',u.id_material) COLLATE Latin1_General_100_BIN2 clave_material,u.numero_parte,CONCAT(u.linea,':',u.id_estanteria) COLLATE Latin1_General_100_BIN2 id_estanteria,u.cantidad FROM mes2.vw_inventario_bi u;
GO
CREATE OR ALTER VIEW mes2.bi_TURNO AS
SELECT CASE WHEN DATEPART(hour,hora_inicio)=6 THEN 1 ELSE 2 END id_turno,nombre_turno,hora_inicio,hora_fin FROM dbo.TURNO WHERE hora_inicio IN (CONVERT(time,'06:00'),CONVERT(time,'15:00'));
GO
CREATE OR ALTER VIEW mes2.bi_REGISTRO AS
WITH a AS(
 SELECT v.linea,v.id_atencion,v.id_material,v.id_ubicacion,v.inicio_utc,v.fin_utc,v.turno_inicio,
  COALESCE(JSON_VALUE(ent.datos,'$.puesto'),CONVERT(nvarchar(100),leg.id_puesto)) puesto,
  u.id_estacion
 FROM mes2.vw_atenciones_bi v
 JOIN mes2.vw_inventario_bi u ON u.linea=v.linea AND u.id_ubicacion=v.id_ubicacion COLLATE Latin1_General_100_BIN2
 LEFT JOIN mes2.ENTIDAD ent ON ent.linea=v.linea AND ent.tipo='atencion' AND ent.id=v.id_atencion COLLATE Latin1_General_100_BIN2
 LEFT JOIN dbo.REGISTRO leg ON v.linea=1 AND v.id_atencion=CONCAT('legado-',leg.id_registro) COLLATE Latin1_General_100_BIN2
), secuencia AS(
 SELECT *,LAG(inicio_utc) OVER(PARTITION BY linea,id_material,id_estacion ORDER BY inicio_utc,id_atencion) anterior FROM a
)
SELECT CONCAT(linea,':',id_atencion) COLLATE Latin1_General_100_BIN2 id_registro,CONCAT(linea,':',puesto) COLLATE Latin1_General_100_BIN2 id_puesto,CONCAT(linea,':',id_ubicacion) COLLATE Latin1_General_100_BIN2 id_ubicacion,turno_inicio id_turno,
 CONVERT(datetime2,inicio_utc AT TIME ZONE 'Central Standard Time (Mexico)') fecha_hora_inicio,
 CONVERT(datetime2,fin_utc AT TIME ZONE 'Central Standard Time (Mexico)') fecha_hora_fin,
 CASE WHEN anterior IS NOT NULL THEN DATEDIFF_BIG(millisecond,anterior,inicio_utc)/3600000.0 END intervalo_h FROM secuencia;
GO
IF DATABASE_PRINCIPAL_ID('mes_lectura') IS NOT NULL
BEGIN
 GRANT SELECT ON mes2.bi_ESTACION TO mes_lectura;
 GRANT SELECT ON mes2.bi_PUESTO_LOGISTICO TO mes_lectura;
 GRANT SELECT ON mes2.bi_ESTANTERIA TO mes_lectura;
 GRANT SELECT ON mes2.bi_COLOR TO mes_lectura;
 GRANT SELECT ON mes2.bi_MATERIAL TO mes_lectura;
 GRANT SELECT ON mes2.bi_UBICACION TO mes_lectura;
 GRANT SELECT ON mes2.bi_TURNO TO mes_lectura;
 GRANT SELECT ON mes2.bi_REGISTRO TO mes_lectura;
END;
