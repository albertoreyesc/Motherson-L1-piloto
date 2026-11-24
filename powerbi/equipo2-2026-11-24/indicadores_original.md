# 3 · Indicadores

Todos los indicadores se calculan en el modelo de Power BI con DAX, a partir de las 8 tablas del ERD. Las fórmulas completas están en `3. PowerBI/modelo_completo.tmdl`, y dentro del modelo cada medida está en una carpeta por hoja.


## Los 22 indicadores del catálogo y dónde quedó cada uno

Los códigos son los del Catálogo ampliado de indicadores (04/11/2026).

### Hoja Crítico por surtir

[01] Estado de disponibilidad de materiales
- Dónde: gráfica Estado del material por puesto logístico.
- Medida: `Materiales Nivel Critico`, `Materiales Nivel Bajo`, `Materiales Nivel Normal`.
- Cálculo: existencia de cada ubicación contra su mínimo. Agotado si es 0; crítico si está en o bajo el mínimo; advertencia hasta 120 % del mínimo; normal en otro caso.

[04] Inventario actual en punto de uso
- Dónde: tarjeta Piezas en línea.
- Medida: `Piezas Restantes`.
- Cálculo: suma de `UBICACION.cantidad`.

[10] Materiales cerca del nivel crítico
- Dónde: nota de la tarjeta de críticos y color amarillo.
- Medida: `Materiales Nivel Bajo`.
- Cálculo: existencia mayor al mínimo y hasta el mínimo × (1 + `Margen Advertencia`).

[11] Materiales en nivel crítico
- Dónde: tarjeta Materiales en nivel crítico.
- Medida: `Materiales Nivel Critico`.
- Cálculo: existencia en o bajo el mínimo. Incluye los agotados.

### Hoja Desempeño semanal

[02] Tiempo promedio de reabastecimiento
- Dónde: tarjeta Tiempo promedio.
- Medida: `Tiempo Promedio Reabast`.
- Cálculo: promedio en minutos de fin − inicio de los registros completados.

[03] Reabastecimientos completados
- Dónde: tarjeta Reabastecimientos y gráfica por día.
- Medida: `Reabast Completados`, `Completados Var Texto`.
- Cálculo: registros con fin igual o posterior al inicio, y su variación contra la semana anterior.

[12] Registros incompletos
- Dónde: nota de la tarjeta En curso.
- Medida: `Registros Incompletos`.
- Cálculo: registros sin puesto o sin ubicación.

[14] % fuera del tiempo objetivo
- Dónde: tarjeta Fuera del tiempo objetivo.
- Medida: `% Fuera Objetivo`.
- Cálculo: completados que tardaron más de `Tiempo Objetivo Min` (4.5 min) ÷ completados.

[15] Tiempo P90
- Dónde: nota de la tarjeta Tiempo promedio.
- Medida: `Tiempo P90 Reabast`.
- Cálculo: percentil 90 de la duración.

[17] Intervalo promedio entre reabastecimientos
- Dónde: tarjeta Intervalo promedio.
- Medida: `Intervalo Promedio`.
- Cálculo: horas entre inicios seguidos del mismo material en la misma estación. Se calcula en la base, en la vista `vw_registro_bi`, con `LAG`.

[18] Reabastecimientos repetidos en un día
- Dónde: tarjeta Repetidos y tabla Repetidos en un día.
- Medida: `Reabast Repetidos`.
- Cálculo: surtidos de más después del primero del día, del mismo material en la misma estación.

[19] Tiempo desde el último reabastecimiento
- Dónde: tabla Tiempo desde el último reabastecimiento.
- Medida: `Horas Desde Ultimo`.
- Cálculo: horas entre el último fin y el cierre de la semana elegida, o el corte si es antes.

[21] Reabastecimientos en curso
- Dónde: tarjeta En curso.
- Medida: `Reabast En Curso`.
- Cálculo: registros con inicio y sin fin.

[22] Ocupación de estanterías
- Dónde: gráfica Ocupación de estanterías y su nota.
- Medida: `% Ocupacion`, `Ocupacion Nota`.
- Cálculo: cajas ocupadas (cantidad ÷ piezas por caja, redondeado hacia arriba) ÷ cajas de la estantería.

### Hoja Diagnóstico

[07] Personal con más reabastecimientos
- Dónde: Carga y ocupación por puesto (al pasar el mouse, "Surtidos") y recomendación 1.
- Medida: `Reabast Completados` por puesto.
- Cálculo: completados de cada puesto.

[08] Personal con mayor tiempo promedio
- Dónde: Carga y ocupación por puesto (al pasar el mouse, "Tiempo promedio").
- Medida: `Tiempo Promedio Reabast` por puesto.
- Cálculo: igual que [02], por puesto.

[09] Materiales más reabastecidos
- Dónde: Tiempo por material (columna Surt.) y recomendación 2.
- Medida: `Rank Lento Surtidos`, `Rank Rapido Surtidos`.
- Cálculo: completados del material.

[16] Tiempo acumulado por trabajador
- Dónde: Carga y ocupación por puesto (al pasar el mouse, "Horas surtiendo").
- Medida: `Tiempo Acumulado Horas`.
- Cálculo: suma de duraciones ÷ 60.

[20] Críticos con alta frecuencia de reabastecimiento
- Dónde: en parte, recomendaciones 2 y 4.
- Medida: `Rec 2 Material Lento`, `Rec 4 Riesgo`.
- La tabla que lo cubre completo está en [mejoras futuras](<6. Limitaciones.md>), punto 2.

### Sin visual en esta versión

[05] Estaciones con materiales agotados
- Su medida ya está en el modelo; la visual está en [mejoras futuras](<6. Limitaciones.md>), punto 1.

[06] Inventario por material y estantería
- Se quitó porque Crítico por surtir ya muestra cada pieza.

[13] Cajas equivalentes disponibles
- Se quitó por poco uso. La medida `Cajas Equivalentes` (cantidad ÷ piezas por caja) sigue en el modelo.


## Indicadores adicionales

Además del catálogo, el dashboard muestra:

1. % restante por pieza (Crítico por surtir)
   - Medida: `% Restante`.
   - Cantidad ÷ capacidad (cajas × piezas por caja).

2. Cobertura promedio (Crítico por surtir)
   - Medida: `Cobertura Promedio Turno`.
   - Piezas totales ÷ capacidad total.

3. Próximo faltante estimado (Crítico por surtir)
   - Medida: `Proximo Faltante Minutos`, `Proximo Faltante Detalle`.
   - La menor cobertura estimada entre las ubicaciones con existencia.

4. Cobertura estimada en minutos (Crítico por surtir, Piezas próximas a agotarse)
   - Columna: `UBICACION[Cobertura (min)]`.
   - Fórmula:
     - ritmo (piezas/min) = surtidos de los últimos 28 días × max(capacidad − mínimo, piezas por caja) ÷ (días con registros × minutos de turno)
     - cobertura (min) = cantidad ÷ ritmo
   - Se probó contra el consumo real del histórico simulado: el error mediano fue de 4 %.

5. Órdenes por hora y turno (Diagnóstico)
   - Medida: `Reabastecimientos` por `REGISTRO[Hora]` y turno.
   - Atenciones iniciadas en cada hora.

6. Ocupación por puesto (Diagnóstico)
   - Medida: `% Ocupacion Puesto`.
   - Horas surtiendo ÷ horas de turno disponibles (días con actividad × minutos de los 2 turnos).

7. Órdenes por hora disponible (Diagnóstico, al pasar el mouse)
   - Medida: `Ordenes Por Hora`.
   - Completados ÷ horas disponibles.


## Recomendaciones de la hoja Diagnóstico

Son reglas con umbrales, no inteligencia artificial. Cada frase cita su dato y su periodo, y se recalcula con los filtros.

1. Balanceo (`Rec 1 Balanceo`)
   - El puesto con más surtidos: su % del total y su ocupación, y los puestos con menos de 10 % de ocupación.
   - Sugiere pasar materiales a los puestos con menos carga.

2. Material lento (`Rec 2 Material Lento`)
   - El material con mayor tiempo promedio, con al menos 10 completados (`Minimo Surtidos Ranking`).
   - Lo compara contra el promedio general y cuenta sus surtidos repetidos.

3. Semana atípica (`Rec 3 Semana Atipica`)
   - La semana con el mayor aumento del tiempo promedio contra la anterior.
   - Si sube más de 20 % (`Umbral Semana Atipica`) se marca como atípica; si no, dice "tiempo estable".

4. Riesgo actual (`Rec 4 Riesgo`)
   - Los materiales en o bajo su mínimo y el de menor cobertura estimada, al corte.

5. Horas pico (`Rec 5 Horas Pico`)
   - La hora con más atenciones iniciadas, comparada con la de menos.

6. Turnos (`Rec 6 Turnos`)
   - La diferencia de surtidos entre turnos. Por debajo de 10 % se consideran equilibrados.


## Parámetros que pueden ajustar

Son medidas de la carpeta Parámetros del modelo. Para cambiarlas: abran el modelo semántico → Abrir modelo de datos → elijan la medida → cambien el número.

1. `Margen Advertencia`: 20 %. Nivel de advertencia [10].
2. `Tiempo Objetivo Min`: 4.5 min. % fuera del objetivo [14].
3. `Umbral Horas Medio` y `Umbral Horas Alto`: 8 y 24 h. Colores de [19].
4. `Umbral Repeticion Media` y `Umbral Repeticion Alta`: 4 y 10. Colores de [18].
5. `Minimo Surtidos Ranking`: 10. Rankings y recomendación 2.
6. `Umbral Semana Atipica`: 20 %. Recomendación 3.

Ojo: la columna `MATERIAL[Estado]` usa un 20 % fijo. Si cambian `Margen Advertencia`, cambien también el `1.2` de esa columna.
