# 4 · Actualización automática

Hoy el dashboard está en modo importación: muestra los datos de la última actualización, programada o con ⟳ Actualizar ahora. Esta carpeta trae lo necesario para que el dashboard consulte la base de forma automática, de modo que una salida o una atención registrada en el portal se vea en segundos, sin que nadie le dé Actualizar.

No está activado en esta entrega, porque necesita licencias y servicios de pago. Lo que sí se dejó listo es la parte de la base de datos, para que el cambio sea corto.


## Qué hay en esta carpeta

1. `vistas_actualizacion_automatica.sql`
   - Crea 6 vistas que calculan en la base las columnas que hoy calcula el modelo de Power BI (descripción, estación, puesto, capacidad, cobertura, estado…), con los mismos nombres.
   - Así, al cambiar el modo de las tablas, las gráficas no se tienen que volver a armar.

2. Esta guía.

Además, ya está en uso desde la instalación la vista `vw_registro_bi` con sus índices (`1. BDD/05_indices_y_vista.sql`), que permite leer REGISTRO filtrado por fecha.


## Cómo quedaría

- La hoja Crítico por surtir consulta las existencias directo en Azure (DirectQuery) y se actualiza sola cuando detecta un cambio.
- Desempeño semanal y Diagnóstico siguen en importación para el historial, y consultan directo solo las atenciones más recientes.
- Los catálogos (estaciones, colores, turnos, estanterías y puestos) quedan en modo Dual, porque casi no cambian.

Esto se llama modelo mixto. Es más rápido y más barato que poner todo en DirectQuery.


## Qué necesitan

- Azure SQL en un nivel de pago, no la oferta gratuita. Con la consulta automática la base pasa encendida mientras haya alguien viendo el dashboard.
- Capacidad Fabric o Premium para el área de trabajo de Power BI. Con licencia Pro, una hoja abierta se actualiza como mucho cada 30 minutos.
- Power BI Desktop (Windows), para cambiar el modo de almacenamiento de las tablas.
- Alguien con permisos de administrador de Azure y de la capacidad de Power BI.


## Pasos

1. Pasar la base a un nivel de pago
   - En Azure: base → Compute + storage.
   - Elijan General Purpose. Serverless con pausa automática de al menos una hora, o aprovisionado si el dashboard va a estar abierto todo el turno.

2. Asignar el área de trabajo a la capacidad
   - En Power BI: área de trabajo → Configuración → Licencia → Fabric o Premium.

3. Permitir la actualización automática en la capacidad
   - Portal de administración → Configuración de capacidad → Actualización automática de página.
   - Actívenla, junto con Detección de cambios.
   - Pongan un intervalo mínimo, por ejemplo 30 segundos.

4. Crear las vistas (`vistas_actualizacion_automatica.sql`)
   - En VS Code, con su usuario administrador, denle ▶ Run.
   - Al final aparecen los materiales con menor cobertura. Deben coincidir con "Piezas próximas a agotarse" del dashboard.
   - Avísenle antes a Equipo 1: las vistas no cambian las 8 tablas, pero agregan objetos a la base que comparten con el portal.

5. Abrir el informe en Power BI Desktop
   - Descarguen el `.pbix` del área de trabajo (Archivo → Descargar este archivo) y ábranlo en Desktop.

6. Borrar las columnas calculadas que ahora vienen de las vistas
   - Tienen que borrarse antes de cambiar el origen, porque las vistas traen columnas con los mismos nombres.
   - UBICACION: Descripcion, Pieza, nombre_estacion, Estacion, Estanteria, Nivel Estante, Color, Puesto, Capacidad (pzas), Minimo (pzas), Ritmo (pzas/min) y Cobertura (min).
   - MATERIAL: Descripcion Mostrar, Tipo de uso, Existencia (pzas) y Estado.
   - ESTANTERIA: Rack. ESTACION: Linea y Estacion. PUESTO_LOGISTICO: Personal. TURNO: Minutos Turno.
   - Las medidas no se borran; siguen funcionando porque los nombres de las columnas no cambian.

7. Apuntar cada tabla a su vista y elegir su modo
   - Transformar datos → elijan la consulta de la tabla → Editor avanzado → cambien el nombre de la tabla por el de la vista.
   - Después, en la vista Modelo: tabla → Propiedades → Modo de almacenamiento.
   - UBICACION → `vw_ubicacion_bi`, DirectQuery.
   - MATERIAL → `vw_material_bi`, DirectQuery.
   - ESTANTERIA → `vw_estanteria_bi`, Dual.
   - ESTACION → `vw_estacion_bi`, Dual.
   - PUESTO_LOGISTICO → `vw_puesto_bi`, Dual.
   - TURNO → `vw_turno_bi`, Dual.
   - COLOR → se queda en su tabla, Dual.
   - REGISTRO → se queda en `vw_registro_bi`; se ajusta en el paso 8.

8. Convertir REGISTRO en tabla híbrida
   - Clic derecho en REGISTRO → Actualización incremental.
   - Guardar el historial de 2 años y actualizar los últimos 7 días.
   - Activen la opción que trae los datos más recientes con DirectQuery. Así las atenciones de hoy se consultan directo y el resto queda en importación.
   - Si Desktop marca que alguna columna calculada de REGISTRO no se permite con esa opción, pásenla a `vw_registro_bi` igual que en los pasos 6 y 7.

9. Crear la medida que detecta cambios
   - En UBICACION:
     ```
     Huella Existencias = SUMX ( UBICACION, UBICACION[cantidad] * UBICACION[id_ubicacion] )
     ```
   - Cambia con cualquier movimiento de material. Se multiplica por el id para que una entrada y una salida del mismo tamaño en ubicaciones distintas no se cancelen.

10. Revisar la velocidad
    - En Desktop: Ver → Analizador de rendimiento → Iniciar grabación → Actualizar objetos visuales.
    - Cada visual de Crítico por surtir debería tardar menos de 2 o 3 segundos. Si alguno tarda más, simplifiquen su medida o muévanla a la vista.

11. Activar la actualización automática en la hoja
    - Abran Crítico por surtir → Formato de la página → Actualización automática de página.
    - Tipo: Detección de cambios. Medida: Huella Existencias. Revisar cada 30 segundos.
    - En Desempeño semanal pueden usar un intervalo fijo, por ejemplo cada 5 minutos.

12. Publicar y poner credenciales
    - Publiquen desde Desktop al área de trabajo que está en la capacidad.
    - En el modelo semántico: Configuración → Credenciales de origen de datos → Básica, con `powerbi_lector`.
    - Programen la actualización incremental de REGISTRO, por ejemplo cada hora.

13. Probar
    - Registren una salida en el portal.
    - Sin tocar nada en Power BI, en menos de un minuto debe cambiar Piezas en línea en Crítico por surtir.


## Cuidados

- Costo: cada vez que la hoja se actualiza, consulta Azure. Muchas personas con el dashboard abierto y un intervalo corto suben el consumo de la base y de la capacidad. Empiecen con 30 a 60 segundos y ajusten.
- Velocidad: en DirectQuery cada clic consulta Azure. Por eso solo las existencias y las atenciones recientes se consultan directo.
- Fórmulas en dos lugares: con este cambio, las columnas de los pasos 6 y 7 se calculan en las vistas y ya no en el modelo. Si cambian una regla (por ejemplo el 20 % de advertencia), cámbienla en la vista.
- Reglas de convivencia con el portal: lo que se agregue en la base (vistas, índices) se avisa antes a Equipo 1, y las existencias se siguen cambiando solo desde el portal.
