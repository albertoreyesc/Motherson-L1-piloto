CREATE OR ALTER VIEW reporting.vw_kpi_movimientos AS
 SELECT CONVERT(varchar(80),'H21:'+c.maestro+':'+CONVERT(varchar(20),e.id_registro_origen)) id_movimiento,
 CONVERT(varchar(24),'HISTORICO_21') origen,CONVERT(bit,1) es_simulado,e.numero_parte,e.id_puesto,e.id_estacion,e.id_estanteria,e.id_ubicacion,e.tipo_movimiento,e.cantidad_piezas,e.fecha_movimiento,
 CONVERT(datetime2(7),NULL) fecha_hora_inicio,CONVERT(datetime2(7),NULL) fecha_hora_fin,e.id_turno,e.id_usuario,
 CONVERT(varchar(20),'HISTORICO_21') espacio_identificadores,e.id_posicion_origen,e.estacion,e.estanteria,e.nivel,e.responsable_simulado,e.proceso_generador,e.saldo_anterior,e.saldo_posterior,
 CONVERT(nvarchar(150),NULL) usuario_captura,e.origen_declarado
 FROM archivo.EVENTO e CROSS JOIN archivo.CORTE c WHERE c.sellado=1 AND e.tipo_movimiento IS NOT NULL
 UNION ALL
 SELECT CONVERT(varchar(80),'MVP:'+CONVERT(varchar(20),r.id_registro)),r.origen,CONVERT(bit,CASE WHEN r.origen IN('CAPTURA_SIMULADA','APERTURA_SIMULADA') THEN 1 WHEN r.origen IN('CAPTURA_REAL','APERTURA_REAL') THEN 0 END),u.numero_parte,r.id_puesto,s.id_estacion,t.id_estanteria,u.id_ubicacion,r.tipo_movimiento,r.cantidad_movimiento,r.fecha_movimiento,r.fecha_hora_inicio,r.fecha_hora_fin,r.id_turno,r.id_usuarios,
 CONVERT(varchar(20),'MVP'),CONVERT(int,NULL),s.nombre_estacion,t.estanteria,t.nivel,CONVERT(nvarchar(150),NULL),CONVERT(nvarchar(100),'PORTAL_MVP'),r.saldo_anterior,r.saldo_posterior,usr.nombre_usuario,CONVERT(nvarchar(100),r.origen)
 FROM dbo.REGISTRO r JOIN dbo.UBICACION u ON u.id_ubicacion=r.id_ubicacion JOIN dbo.ESTANTERIA t ON t.id_estanteria=u.id_estanteria JOIN dbo.ESTACION s ON s.id_estacion=t.id_estacion JOIN dbo.USUARIOS usr ON usr.id_usuarios=r.id_usuarios;
GO
CREATE OR ALTER VIEW reporting.vw_kpi_inventario_actual AS
 SELECT u.id_ubicacion,u.numero_parte,m.descripcion,m.uso,m.id_puesto,p.nombre puesto,m.cantidad_por_caja,m.minimo,u.cantidad,u.version_concurrencia,t.id_estanteria,t.estanteria,t.nivel,t.cajas_estanteria,s.id_estacion,s.nombre_estacion estacion,
 CASE WHEN COUNT(u.cantidad) OVER(PARTITION BY u.numero_parte)=COUNT(*) OVER(PARTITION BY u.numero_parte) THEN SUM(CONVERT(bigint,u.cantidad)) OVER(PARTITION BY u.numero_parte) END stock_total_material,
 CONVERT(bit,1) es_simulado
 FROM dbo.UBICACION u JOIN dbo.MATERIAL m ON m.numero_parte=u.numero_parte JOIN dbo.ESTANTERIA t ON t.id_estanteria=u.id_estanteria JOIN dbo.ESTACION s ON s.id_estacion=t.id_estacion JOIN dbo.PUESTO_LOGISTICO p ON p.id_puesto=m.id_puesto;
GO
-- Solo salidas de operaciones REALES. La entrega histórica es simulada: esta vista puede estar vacía.
CREATE OR ALTER VIEW reporting.vw_kpi_consumo_diario AS
 SELECT CONVERT(date,fecha_movimiento AT TIME ZONE 'UTC' AT TIME ZONE 'Central Standard Time (Mexico)') fecha_local,numero_parte,espacio_identificadores,id_estacion,estacion,SUM(CONVERT(bigint,cantidad_piezas)) cantidad_piezas,COUNT_BIG(*) movimientos
 FROM reporting.vw_kpi_movimientos WHERE tipo_movimiento='SALIDA' AND es_simulado=0
 GROUP BY CONVERT(date,fecha_movimiento AT TIME ZONE 'UTC' AT TIME ZONE 'Central Standard Time (Mexico)'),numero_parte,espacio_identificadores,id_estacion,estacion;
GO
CREATE OR ALTER VIEW reporting.vw_historico_eventos AS
 SELECT e.* FROM archivo.EVENTO e CROSS JOIN archivo.CORTE c WHERE c.sellado=1;
GO
CREATE OR ALTER VIEW reporting.vw_estado_fuentes AS
 SELECT nombre,sha256,filas FROM archivo.FUENTE;
GO
-- Acceso recuperable a todas las columnas originales, sin exponer permisos de escritura.
CREATE OR ALTER VIEW reporting.vw_archivos_originales AS SELECT nombre,sha256,filas,contenido_gzip FROM archivo.FUENTE;
