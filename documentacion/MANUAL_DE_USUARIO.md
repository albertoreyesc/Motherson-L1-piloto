# Manual de usuario — Smart Material Replenishment (Motherson)

Versión v3 del 26/11/2026, integrada mediante el PR #2 (base `8c25f64`). Para instalar o actualizar el sistema, consulta el [Manual de instalación](MANUAL_DE_INSTALACION.md).

El portal registra las **entradas y salidas de material** en las estanterías de cada línea, mide cuánto tarda cada surtido (**atenciones**) y avisa cuando una ubicación se está quedando sin material (**semáforo de cobertura**). Todo queda en un historial con su comprobante.

## Contenido

1. [Tipos de cuenta](#1-tipos-de-cuenta)
2. [Entrar, cambiar de línea y salir](#2-entrar-cambiar-de-línea-y-salir)
3. [Cómo se arma una línea](#3-cómo-se-arma-una-línea)
4. [Puesto logístico: registrar entradas y atenciones](#4-puesto-logístico-registrar-entradas-y-atenciones)
5. [Consulta: Resumen, Inventario, Historial, KPIs y Dashboard](#5-consulta-resumen-inventario-historial-kpis-y-dashboard)
6. [Administración: poner en marcha una línea](#6-administración-poner-en-marcha-una-línea)
7. [Administración: tareas del día a día](#7-administración-tareas-del-día-a-día)
8. [Semáforo de cobertura](#8-semáforo-de-cobertura)
9. [Problemas frecuentes](#9-problemas-frecuentes)

---

## 1. Tipos de cuenta

Cada persona entra con **su propia cuenta**. El sistema guarda quién hizo cada movimiento, así que una cuenta no se comparte.

| Cuenta | Para quién | Qué ve |
| --- | --- | --- |
| **Administración** | Responsable de logística o del sistema | Todo: Resumen, Materiales y catálogos, Estaciones y ubicaciones, Inventario, Entradas y salidas, Historial, KPIs, Dashboard, Configurar línea y Usuarios |
| **Puesto logístico** | Surtidor o materialista (por ejemplo, el montacargas) | Entradas y salidas e Inventario, **solo de los materiales de su puesto** |
| **Consulta** | Supervisión, calidad, gerencia | Resumen, Inventario, Historial, KPIs y Dashboard, sin poder editar |

Las cuentas las crea Administración en la sección **Usuarios** (ver [7.5](#75-usuarios)).

## 2. Entrar, cambiar de línea y salir

### Iniciar sesión
1. Abre el portal en el navegador (la dirección te la da Administración).
2. Escribe tu **Usuario** y tu **Contraseña**. El ícono del ojo muestra la contraseña para revisar que la escribiste bien.
3. Pulsa **Iniciar sesión**.

Si sale "Credenciales inválidas", revisa mayúsculas y minúsculas. Si sigue fallando, pide a Administración que revise tu cuenta.

### La pantalla
- **Menú de la izquierda:** las secciones que permite tu cuenta. En el celular el menú se cambia por una lista **Sección**.
- **Línea de trabajo** (arriba): elige la línea en la que vas a trabajar. Solo aparecen las líneas a las que tienes acceso, y cada línea tiene sus propios datos.
- **Actualizar datos:** vuelve a leer la información más reciente. Úsalo si otra persona acaba de registrar algo.
- **Etiqueta amarilla SIMULADO:** indica que el portal trabaja con datos de prueba. Desaparece cuando el sistema pasa a datos reales.

### Cerrar sesión
Pulsa **Cerrar sesión** (arriba a la derecha). Si dejas el portal sin tocar unos 30 minutos, la sesión se cierra sola. Mientras lo uses, se mantiene abierta.

> En tabletas compartidas, cierra siempre tu sesión al terminar para que nadie registre movimientos a tu nombre.

### Aviso de horario
Si aparece un aviso amarillo o rojo sobre el **horario de la línea**, el horario de trabajo está por vencer, ya venció o falta. Las entradas y salidas siguen funcionando, pero el semáforo de cobertura deja de calcular. Avisa a Administración (ver [6.4](#64-horario-de-trabajo)).

## 3. Cómo se arma una línea

Es útil entender esta cadena, porque explica por qué alguien "ve" o "no ve" un material:

```
Puesto ──tiene──▶ Materiales ──se guardan en──▶ Estanterías ──están en──▶ Estaciones
```

- Un **puesto** (por ejemplo, el montacargas) es responsable de ciertos **materiales**.
- Cada material se guarda en una o varias **estanterías**. Cada combinación de material y estantería es una **ubicación**, con su propio saldo de piezas.
- Una estantería tiene un **nivel** (repisa) y un número de **espacios** (cajas que caben).
- Las estanterías están en una **estación** de la línea.

Un puesto logístico ve **una tarjeta por cada ubicación** de sus materiales. Si sus materiales están en una sola estantería, solo ve esa.

## 4. Puesto logístico: registrar entradas y atenciones

Sección **Entradas y salidas**.

### 4.1 Encontrar el material
- Una cuenta de puesto logístico trabaja siempre en **Entrada**. Las cuentas de Administración ven además los botones **Entrada** y **Salida** para cambiar de movimiento.
- **Buscar material o ubicación:** número de parte, descripción o estantería.
- **Estación:** filtra por estación.
- **Solo con espacio disponible:** oculta las ubicaciones llenas.
- Debajo se muestra cuántas ubicaciones hay y cuántas **atenciones en curso** tienes.

Cada tarjeta muestra la foto, el número de parte, el **saldo** y el **espacio libre**. Una tarjeta aparece en gris cuando no se puede usar, y dice por qué:
- **Sin espacio en la estantería:** no caben más cajas (en Entrada).
- **Sin existencias:** no hay piezas que retirar (en Salida).
- **Material inhabilitado · solo retiro** o **Ubicación desactivada:** Administración la sacó de operación.

### 4.2 Registrar una entrada
1. Toca la tarjeta del material. Se abre el detalle con saldo actual, piezas por caja y cajas libres. Para elegir otro, usa **← Cambiar material**.
2. **Atención del surtido:** pulsa **Iniciar atención** cuando empieces a surtir. Así se mide cuánto tardas. Es opcional, pero sin ella no hay tiempos en los KPIs.
3. En **Cajas a ingresar** pon el número de cajas con **−** y **+**.
4. Pulsa **Revisar entrada**. Se muestra cuántas piezas son y cómo queda el saldo.
5. Si está bien, pulsa **Confirmar**. Si no, **Corregir cantidad**.
6. Aparece el aviso **Entrada registrada** con su comprobante.

Al registrar, la atención **se finaliza sola**. Si vas a hacer otro movimiento dentro de la misma atención, abre **Opciones** antes de confirmar y desmarca **Finalizar la atención al registrar**.

### 4.3 Salidas
Las salidas (consumo de producción, devoluciones o retiro de remanente) las registra **Administración**, por eso una cuenta de puesto logístico no tiene el botón **Salida**.

### 4.4 Atenciones abiertas
- En la parte de arriba se listan tus atenciones abiertas con los minutos que llevan. Cada una tiene un botón **Finalizar**.
- Si intentas cambiar de material con una atención abierta, el portal pregunta si la finalizas. **Una atención abierta sigue contando tiempo** y afecta los KPIs.

### 4.5 Inventario
En **Inventario** ves cada ubicación de tus materiales con su saldo, cajas libres, mínimo del material y la **cobertura** (cuántos minutos de material le quedan, ver [8](#8-semáforo-de-cobertura)). **Ver cálculo** explica de dónde sale ese número.

## 5. Consulta: Resumen, Inventario, Historial, KPIs y Dashboard

### 5.1 Resumen
Existencias totales, materiales activos y ubicaciones activas de la línea, y el bloque **Alarmas y cobertura temporal**, que muestra cuántas ubicaciones hay en cada color del semáforo. **¿Por qué aparece cada estado?** explica las reglas.

**Consultar cobertura histórica** muestra cómo estaban las alarmas en una fecha y hora anteriores. Es solo de consulta y no cambia nada.

### 5.2 Inventario
Igual que en [4.5](#45-inventario), pero con todos los materiales de la línea. Se puede buscar por parte o estantería y filtrar por estación.

### 5.3 Historial
Todo lo que pasó en la línea: altas, cambios, entradas, salidas y atenciones, con **Fecha**, **Cuenta**, **Operación**, **Detalle** y **Comprobante**. **Ver datos** muestra el detalle completo de un registro. **Descargar Excel** exporta el historial.

**Ver movimientos del sistema anterior** muestra, solo en Línea 1, los registros previos al 22/11/2026.

### 5.4 KPIs
1. **Periodo:** **Hoy**, **7 días**, **30 días** o **Personalizado** (desde y hasta).
2. **Filtros:** **Estación** y **Turno** (1: 06:00 a 14:59; 2: 15:00 a 05:59). En **Más filtros** están **Material** y el **corte del inventario**.
3. Los indicadores se calculan solos al cambiar un filtro.

Arriba hay cuatro tarjetas:
- **Por atender:** materiales agotados, críticos o en advertencia contra su mínimo.
- **Reabastecimientos:** atenciones completadas en el periodo.
- **Tiempo promedio:** duración promedio de una atención.
- **Fuera de objetivo:** porcentaje de atenciones que tardaron más del objetivo.

Abajo hay tres pestañas:
- **Existencias:** materiales a atender primero, con saldo, mínimo, estado y ubicaciones.
- **Actividad:** completados por día, repeticiones el mismo día, en curso al corte, registros incompletos e intervalo promedio.
- **Tiempos:** actividad por puesto, tiempos por material, inicios por hora y comparación por turno.

**Excel ▾**:
- **Esta consulta:** descarga exactamente lo que ves, con los mismos filtros.
- **Histórico completo:** descarga todas las atenciones disponibles, sin cargarlas en pantalla.

Cada indicador tiene **¿Cómo se calcula?**. Un **0** significa que no hubo actividad; **N/D** significa que no hay datos suficientes para calcularlo.

### 5.5 Dashboard
Muestra el informe de Power BI cuando está configurado para la línea. Power BI pide además una cuenta Microsoft autorizada. **Recargar informe** y **Abrir en otra pestaña** ayudan si no carga.

## 6. Administración: poner en marcha una línea

Las líneas nuevas empiezan vacías. Sigue este orden la primera vez:

| Paso | Dónde | Qué haces |
| --- | --- | --- |
| 1 | Configurar línea → **Puestos** | Crear los puestos logísticos |
| 2 | Configurar línea → **Estaciones** | Crear las estaciones |
| 3 | Configurar línea → **Estanterías** | Crear estanterías con estación, nivel y espacios |
| 4 | Configurar línea → **Horario de trabajo** | Capturar días, turnos y descansos |
| 5 | Materiales y catálogos → **Importar Excel** | Cargar los materiales con su puesto |
| 6 | Materiales y catálogos → **Asignar a estantería** | Decir dónde se guarda cada material |
| 7 | **Usuarios** | Crear las cuentas de cada persona |
| 8 | Entradas y salidas | Registrar la primera entrada de cada ubicación |

### 6.1 Puestos, estaciones y estanterías
En **Configurar línea**, elige la pestaña **Puestos**, **Estaciones** o **Estanterías**.
- Para crear: toca la tarjeta **Nuevo puesto**, **Nueva estación** o **Nueva estantería**, llena el formulario de la derecha y pulsa **Crear…**.
- Para editar: toca la tarjeta existente, cambia los datos y pulsa **Guardar cambios**.
- Una **estantería** pide nombre, **¿En qué estación está?**, **Nivel o repisa** y **Espacios (cajas)**. Los espacios no pueden quedar por debajo de las cajas que ya están ocupadas.

Cambiar el nombre de un puesto conserva sus materiales, atenciones e historial.

### 6.2 Importar materiales desde Excel
1. En **Materiales y catálogos** pulsa **Importar Excel**.
2. **Descargar plantilla**, llénala y súbela con **Seleccionar archivo Excel o CSV** (máximo 10 MB y 2,000 filas).
3. Elige la **Hoja** y la **Fila de encabezados**, y revisa a qué campo corresponde cada columna.
4. Revisa la vista previa: altas, cambios, filas sin cambios y errores. Si hay materiales nuevos, marca **Autorizar materiales nuevos**. Si el archivo trae un puesto que no existe, puedes marcar **Crear los puestos faltantes**.
5. Pulsa **Aplicar lote completo**. O se aplica todo o no se aplica nada. Puedes **Descargar resultado** como comprobante.

Reglas importantes:
- Un material nuevo necesita **número de parte**, **puesto** y **piezas por caja**.
- **Celda vacía** = conserva el dato actual. **[BORRAR]** = vacía un campo de texto (descripción, variante, revisión, modelo o uso).
- Guarda el número de parte como **texto** en Excel para no perder ceros o sufijos.
- La importación **no cambia existencias** ni asigna estanterías.
- No se puede cambiar el puesto de un material que **ya tiene ubicaciones**; así no se mezcla el historial de dos puestos.

### 6.3 Asignar materiales a estanterías
1. En **Materiales y catálogos** filtra por **Puesto** y, si quieres, en **Mostrar** elige **Sin ubicación**.
2. En la columna **Ubicación y espacios** pulsa **Asignar a estantería** (o **Asignar a otra estantería** si ya tiene una).
3. Elige la estantería en **¿En qué estantería se guarda?**. Junto a cada una se ven sus espacios libres.
4. Pulsa **Asignar**. La ubicación empieza en **0 piezas**; las existencias se registran con una entrada.

Si la estantería no aparece, todavía no existe o no tiene espacio. El atajo **¿No está la estantería? Créala en Configurar línea** te lleva directo.

Un material puede estar en varias estanterías. Cada una es una ubicación distinta y una tarjeta más para el puesto.

### 6.4 Horario de trabajo
**Configurar línea → Horario de trabajo.** Con esto el sistema sabe cuándo se trabaja y puede calcular la cobertura.
1. **¿Cuándo aplica este horario?:** desde y hasta (máximo 2 años).
2. **¿Quién lo autorizó?:** nombre o área que lo aprobó.
3. **Días que se trabaja** y **Turnos** (hora de Ciudad de México). Marca **Termina al día siguiente** para turnos nocturnos.
4. **Descansos de este turno** (comida, pausas): no cuentan como tiempo de trabajo.
5. **Días festivos o especiales:** fechas que no se trabajan o que sí se trabajan aunque no sea día normal.
6. **Semáforo de cobertura:** minutos con los que cambia de color (verde mayor que amarillo, amarillo mayor que rojo).
7. **Es un horario de prueba:** márcalo mientras el horario no esté validado con planta.
8. Revisa **Así queda tu semana** y pulsa **Guardar horario**.

El portal avisa 15 días antes de que el horario venza. Renuévalo antes para no perder las alarmas.

## 7. Administración: tareas del día a día

### 7.1 Registrar salidas
En **Entradas y salidas** elige **Salida**, toca la tarjeta, pon las **Cajas a retirar**, **Revisar salida** y **Confirmar**. En **Opciones**:
- **Es consumo de producción** viene marcado. Desmárcalo solo si es devolución, ajuste o retiro de remanente; en ese caso no cuenta para la alarma.
- **Finalizar la atención al registrar**, igual que en la entrada.

### 7.2 Espacios de una estantería
En **Materiales y catálogos**, junto a cada ubicación hay botones **−** y **+** para cambiar los espacios de la estantería. Pulsa **Guardar** o **Cancelar**. También se cambian en **Configurar línea → Estanterías**, junto con el nombre, nivel y estación.

### 7.3 Fotos y opciones de un material
- **Subir foto** o **Cambiar** (PNG o JPG). **Quitar** borra solo la foto.
- **Opciones** de cada material:
  - **Nueva revisión:** crea otro material copiando los datos (por ejemplo, `EK4747-410` → `EK4747-411`). Puede asignarse a las mismas estanterías e inhabilitar el anterior.
  - **Inhabilitar / Reactivar:** un material inhabilitado no admite entradas ni atenciones; su remanente se retira con salidas.
  - **Renombrar parte:** corrige un número de parte mal capturado, conservando su historial.
  - **Eliminar sin dependencias:** solo si nunca se usó (sin ubicaciones, fotos ni atenciones).

Todas piden un **motivo**, que queda en el historial.

### 7.4 Desactivar, mover o reactivar ubicaciones
**Estaciones y ubicaciones → Administrar ubicaciones.**
- **Desactivar:** si la ubicación no tiene piezas, solo se desactiva.
- **Mover y desactivar:** si tiene piezas, eliges **¿A qué estantería se mueven las piezas?** y se trasladan. El traslado no cuenta como consumo.
- **Reactivar:** vuelve a operar la misma ubicación con su historial.

No se puede desactivar una ubicación con una atención abierta. Las ubicaciones nunca se borran.

### 7.5 Usuarios
**Usuarios → Nuevo usuario.**
1. **Usuario (para iniciar sesión):** de 3 a 80 caracteres, sin espacios ni acentos.
2. **Nombre de la persona.**
3. Tipo de cuenta: **Administración**, **Puesto logístico** o **Consulta**.
4. **Líneas a las que entra.** Para puesto logístico, elige también **el puesto** en cada línea.
5. **Contraseña inicial** (mínimo 12 caracteres) y repítela.
6. **Crear usuario.**

Para cambiar algo, pulsa **Editar**. Desmarcar **Cuenta activa** impide que la persona entre, pero conserva su historial. Cambiar el tipo, la contraseña o el estado cierra la sesión de esa cuenta. Siempre debe quedar al menos una cuenta de Administración activa.

## 8. Semáforo de cobertura

La cobertura estima **cuántos minutos de material quedan** en una ubicación al ritmo de consumo reciente.

| Color | Significado (valores iniciales) |
| --- | --- |
| Normal (verde) | 60 minutos o más |
| Advertencia (amarillo) | De 30 a 59 minutos |
| Crítico (rojo) | De 15 a 29 minutos |
| Riesgo estimado de paro | Menos de 15 minutos |
| Agotado | Saldo cero |
| Sin estimación | No hay datos suficientes para calcular |

Para que una ubicación tenga minutos calculados se necesitan:
- un **horario de trabajo vigente**;
- **60 minutos laborables** desde que se abrió la ubicación;
- al menos **dos salidas marcadas como consumo de producción** en los últimos 60 minutos laborables.

Con poca actividad lo normal es **Sin estimación**. La causa se ve en **Ver cálculo**.

El semáforo es una **estimación**: no confirma un paro real. Además es distinto del **mínimo del material** que se usa en KPIs, que compara las piezas totales del material contra su mínimo.

## 9. Problemas frecuentes

| Situación | Qué hacer |
| --- | --- |
| Un puesto no ve un material | El material debe ser de ese puesto **y** tener una estantería asignada ([6.3](#63-asignar-materiales-a-estanterías)). Después pulsa **Actualizar datos** |
| La tarjeta está en gris | Lee el motivo en la tarjeta: sin espacio, sin existencias, material inhabilitado o ubicación desactivada |
| No aparece el botón **Salida** | Las salidas solo las registra Administración |
| Aparece **Operación pendiente de comprobar** | Se perdió la conexión al enviar. Pulsa **Comprobar operación pendiente**; **no** la vuelvas a capturar, para evitar duplicados |
| "Los datos cambiaron" | Otra persona registró algo al mismo tiempo. El portal reintenta solo; si persiste, pulsa **Actualizar datos** y repite |
| Todo dice **Sin estimación** | Revisa que el horario esté vigente y que haya salidas marcadas como consumo ([8](#8-semáforo-de-cobertura)) |
| Me sacó del portal | La sesión venció por inactividad o Administración cambió tu cuenta. Vuelve a iniciar sesión |
| No aparece una sección | Tu tipo de cuenta no la incluye ([1](#1-tipos-de-cuenta)) |
| No aparece la línea que necesito | Pide a Administración que te dé acceso en **Usuarios** |
