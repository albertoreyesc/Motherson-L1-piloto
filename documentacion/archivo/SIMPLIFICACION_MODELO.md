# Simplificación del MVP L1 y acceso individual por puesto

> Antecedente conservado para trazabilidad. No es la guía de instalación ni de operación vigente. Consulta el [índice actual](../README.md).

03/11/2026. Implementación y reversión validadas en **Motherson_L1_MVP_Pruebas**, portal http://127.0.0.1:5088. Modelo operativo: **8 tablas, 50 atributos y 9 claves foráneas**. Las tres tablas del esquema `archivo` permanecen fuera de ese conteo y no se modificaron.

**Promoción completada con autorización explícita del usuario:** http://127.0.0.1:5087 ya utiliza el modelo de ocho tablas. Se creó el respaldo lógico de Desarrollo, se aplicó la migración selectiva y se reinició únicamente su proceso web; SQL no se reinició. Las contraseñas existentes se conservaron. La sesión anterior debe iniciarse de nuevo por el cambio controlado de cookie.

## Decisiones confirmadas

- Conservar cuentas **individuales** vinculadas al puesto; no crear cuentas compartidas entre turnos.
- Mantener `USUARIOS` y la cuenta administrativa separada de los puestos operativos.
- Operadores: solo entradas de materiales de su propio puesto. Salidas: únicamente administrador.
- No asignar `admin.local` artificialmente a un puesto: sus movimientos existentes abarcan varios puestos.

La reducción a siete tablas no se fuerza. Una persona/cuenta y un puesto son entidades distintas: varias cuentas pueden pertenecer al mismo puesto. Fusionarlas limitaría esa cardinalidad y perdería la identificación individual del capturista.

## Diagnóstico y cambio implementado

| Elemento | Función y evidencia previa | Decisión aplicada primero en Pruebas y promovida a Desarrollo | Riesgo que se evita |
| --- | --- | --- | --- |
| COLOR / MATERIAL.id_color | COLOR tenía 0 filas; los materiales tenían 0 referencias de color, en ambas bases | Retirar tabla, FK y atributo; retirar columna y contador del portal | No se eliminan valores documentados ni se deducen colores desde sufijos |
| USUARIOS / REGISTRO.id_usuarios | Dev: admin.local tenía 64 comprobantes en cinco puestos; Pruebas: 75. Dos cuentas de ensayo no tenían movimientos | Conservar cuentas e identificadores; añadir USUARIOS.id_puesto opcional como FK | No se transforma la cuenta administrativa en persona de un único puesto ni se reatribuye el pasado |
| perfil | Define autorización efectiva del servidor | Conservar; MOVIMIENTOS y ENTRADAS pasan a entrada del puesto; solo ADMINISTRADOR permite salida | Un login operativo no obtiene ajustes ni salidas por eliminar el perfil |
| esta_activo | Permite revocar una cuenta conservando su identidad | Conservar y comprobar en cada solicitud autenticada y captura SQL | No hay que borrar un puesto ni perder sus referencias para revocar acceso |
| hash_contrasena | Verificador del mecanismo de contraseñas existente | Conservar solo en USUARIOS; no moverlo al catálogo de puestos | Sin credenciales compartidas ni contraseñas en texto plano; reporting y API pública no lo exponen |
| UBICACION.version_concurrencia | El cliente envía la versión leída; SQL rechaza una confirmación obsoleta | Conservar físicamente y omitir del diagrama conceptual | Dos terminales no confirman sobre una lectura antigua inadvertidamente |
| REGISTRO.es_simulado | 0 inconsistencias con origen, en las dos bases | Retirar la columna física; derivar el mismo campo en la vista de movimientos a partir de los cuatro orígenes explícitos permitidos | Se mantiene la distinción simulada/real sin dos atributos editables que puedan discrepar |

`id_operacion`, `huella_peticion`, restricciones de capacidad/saldo y bloqueo transaccional se conservan. Un campo técnico puede ser necesario aunque no aparezca en el formulario aprobado.

## Modelo resultante y significado de las relaciones

| Tabla | Propósito |
| --- | --- |
| ESTACION | Estación de la Línea 1 |
| ESTANTERIA | Estantería y nivel dentro de la estación; capacidad en cajas |
| MATERIAL | Referencia completa, descripción, uso PFEP, empaque, mínimo y puesto responsable |
| UBICACION | Relación material–nivel y saldo actual único de esa ubicación |
| PUESTO_LOGISTICO | Catálogo de los seis puestos; no almacena credenciales |
| USUARIOS | Cuenta individual, hash, vigencia de acceso, perfil y asignación actual opcional a puesto |
| TURNO | PFEP T1 06:00–15:00 y PFEP T2 15:00–23:30, sin duplicar inventarios |
| REGISTRO | Comprobante inmutable con cuenta capturista, puesto del material, ubicación, turno, cantidades, saldos y tiempos |

Las nueve FK son: ESTANTERIA→ESTACION; MATERIAL→PUESTO_LOGISTICO; UBICACION→MATERIAL y ESTANTERIA; USUARIOS→PUESTO_LOGISTICO; REGISTRO→USUARIOS, UBICACION, TURNO y PUESTO_LOGISTICO.

`USUARIOS.id_puesto` describe la asignación actual para autorizar nuevas entradas. `REGISTRO.id_puesto` conserva el puesto del material al registrar el comprobante. `REGISTRO.id_usuarios` identifica la cuenta capturista. No se cambió el significado de ninguno de los dos atributos existentes del comprobante. Cambiar una asignación actual no reescribe movimientos anteriores.

La FK de usuario a puesto admite NULL para administración y consulta. Un operador sin asignación queda bloqueado para capturar. No se inventaron equivalencias para personas reales. Solo `entrada.prueba`, cuenta de ensayo ya existente, se vinculó al puesto del material `LAB-MAT-001`; esa asignación no es un padrón de personal de planta. En Pruebas sigue habiendo tres cuentas; en Desarrollo sigue existiendo únicamente la cuenta inicial.

Las cuentas no se separan por base/turno para duplicar existencias: el movimiento conserva `id_turno` según hora del servidor en México. Esta fase no implementa un calendario de autorización individual por turno ni la matriz ampliada de seis perfiles de fases anteriores.

## Controles de inventario y sesiones

Ejemplo de concurrencia: dos terminales leen versión 7. La primera confirma una captura y el saldo pasa a versión 8. La segunda, que sigue enviando 7, recibe un conflicto y debe actualizar. Si reintenta la **misma** operación ya confirmada, su identificador y huella permiten devolver el comprobante sin volver a sumar o descontar.

La operación completa se valida en SQL bajo transacción y bloqueo de inventario: cuenta activa, perfil, puesto del material, versión, cantidad, saldo y capacidad de cajas completas. El filtrado visual de bloques acompaña esos controles; no es la única barrera de autorización. El total material continúa calculándose desde sus ubicaciones.

El prefijo de cookie de la versión propuesta cambia a `AccesoPuestoV1`, separado por base. Al activarla se requiere iniciar sesión nuevamente. Las contraseñas existentes no se cambiaron. Se conserva el mecanismo de hash y el lector KPI no recibe acceso a sus valores.

## Naturaleza simulada y reporting

`origen` sigue limitado por CHECK a `CAPTURA_SIMULADA`, `APERTURA_SIMULADA`, `CAPTURA_REAL` y `APERTURA_REAL`. El procedimiento del portal continúa registrando `CAPTURA_SIMULADA`; esta entrega no añade consumo MES/SAP.

`reporting.vw_kpi_movimientos.es_simulado` conserva su nombre y tipo BIT, calculado desde origen para comprobantes del MVP; la rama del archivo histórico permanece simulada. La vista de consumo real sigue excluyendo simulaciones y aperturas; se comprobó con datos temporales revertidos. La vista de inventario mantiene la naturaleza de esta entrega simulada.

La vista de inventario y el contrato de catálogo de la propuesta dejan de devolver `id_color`, `color` y `colores`. Antes de promoverla, cualquier consumidor que use esas columnas debe retirarlas de su selección. En Desarrollo ya está activa esta estructura; los nombres y tipos de los campos conservados permanecen iguales.

## Conservación comprobada

| Comprobación | Desarrollo | Pruebas |
| --- | ---: | ---: |
| Tablas operativas antes → después | 9 → 8 | 9 → 8 |
| Comprobantes conservados | 64 | 75 |
| Eventos originales conservados | 709,042 | 709,042 |
| Usos documentados conservados | 62 | 62 |
| Materiales | 63 | 68, incluyendo cinco fixtures |

Las huellas de inventario y fuentes originales coinciden antes/después en ambas bases. La instantánea tomada tras preparar Pruebas confirmó que Desarrollo aún estaba intacto. Tras la promoción autorizada, la comparación lógica de Desarrollo sigue siendo idéntica al respaldo: solo cambiaron las columnas, módulos y relaciones previstos. En Pruebas también se añadió la asignación del fixture; no se alteraron los comprobantes originales. Los ensayos de entrada/salida ejecutaron ROLLBACK; consumieron valores de IDENTITY sin crear comprobantes permanentes. No se renumeraron ni reutilizaron identificadores.

Los 62 usos proceden de la conciliación documental previa. **57N 858 065 A MEC** sigue pendiente de validación con Motherson; no se equipara automáticamente con la referencia sin A. Los tipos de puesto permanecen vacíos y ocultos por defecto, por falta de una clasificación confirmada. Las fuentes, el simulador y los eventos archivados no se regeneraron.

## Migración, respaldo y reversión

Ejecutar desde `portal`. El script usa la instancia local ya configurada y nunca imprime la contraseña SQL:

El portal de Pruebas está iniciado en **5088**. Su acceso administrativo conserva la contraseña de `portal/.local/acceso-inicial-Motherson_L1_MVP_Pruebas.json`, que es distinta de la configuración privada de Desarrollo. Si posteriormente se detiene, puede iniciarse con `bash scripts/start.sh --database Motherson_L1_MVP_Pruebas --puerto 5088`; no lanzar otra instancia mientras el puerto esté ocupado.

```sh
python3 scripts/simplificar_modelo.py --salida evidencias/simplificacion/idempotencia-pruebas.json
```

Por defecto solo apunta a Pruebas. Antes de la primera aplicación crea un respaldo lógico privado en `.local/simplificacion/` con los datos protegidos, definiciones originales de módulos y restricciones. La transacción verifica que COLOR siga vacía y origen siga siendo consistente; si no, se detiene. No restaura ni reemplaza la base completa.

La reversión selectiva se ejecuta así, usando el archivo exacto registrado en la evidencia de aplicación:

```sh
python3 scripts/simplificar_modelo.py --database Motherson_L1_MVP_Pruebas --revertir "/ruta/al/respaldo.json" --salida evidencias/simplificacion/reversion-controlada.json
```

Se probó la secuencia aplicar → revertir → aplicar → repetir sin cambios adicionales. La reversión reconstruye la columna técnica derivable y la estructura de color vacía; mantiene IDs, saldos y horas. **Se bloquea si hubo cambios posteriores o nuevas asignaciones**, para no borrarlos silenciosamente. La asignación actual de `entrada.prueba` es posterior al ensayo de reversión y exige revisar su conservación antes de volver a revertir; no quitarla por conveniencia.

La opción `--database Motherson_L1_MVP_Desarrollo` utiliza la misma migración selectiva y exige evidencias aprobadas de permisos, repetición y reversión en Pruebas. **Ya se ejecutó con autorización explícita**, respaldo propio y reinicio solo del portal 5087. La segunda ejecución en Desarrollo devolvió cero cambios. Su respaldo exacto se encuentra referenciado en `portal/evidencias/simplificacion/aplicacion-desarrollo.json`; se mantiene en `.local/simplificacion/` con permisos privados. Para una reversión futura de Desarrollo debe indicarse expresamente esa base, utilizar su respaldo y coordinar también el retorno de los binarios y vistas compatibles. No ejecutar migración inicial ni restaurar la base completa para actualizar este modelo.

## Validación ejecutada y archivos de revisión

- Compilación .NET y build TypeScript/Vite aprobados.
- Diez casos de SQL/API: cuentas individuales, ámbito por puesto, salidas, privacidad, entrada válida, reintento, huella incompatible, versión obsoleta, revocación, cuenta sin asignación y competencia de bloqueo; algunos casos agrupan más de una comprobación. Informe en `portal/evidencias/simplificacion/pruebas-permisos.json`.
- Diez pruebas unitarias existentes aprobadas.
- Dos recorridos de navegador en Pruebas y un recorrido adicional en Desarrollo aprobados: catálogo documentado e Inventario; entrada por puesto y cuenta de consulta. Sin enviar movimientos desde el navegador.
- Integridad SQL y claves foráneas sin infracciones; diez fuentes verificadas. Lector KPI: lectura de vistas permitida y cuatro intentos de escritura/lectura de credenciales denegados.
- Comparación inicial de conservación en `portal/evidencias/simplificacion/comparacion-conservacion.json` y verificación final de la promoción en `validacion-desarrollo.json`; segunda ejecución sin cambios en `idempotencia-pruebas.json`; reversión en `reversion-pruebas.json`.

No se probó carga masiva. Desarrollo se promovió después de la validación y de la autorización explícita. La competencia de dos sesiones comprueba bloqueo y conflicto sin persistir datos de ensayo; no es una prueba de rendimiento de planta.

## Diagramas y diccionario

- [Modelo físico SVG](../diagramas/anteriores/mvp-simplificado-fisico.svg): las ocho tablas y sus 50 atributos reales, incluidos los controles técnicos.
- [Modelo conceptual SVG](../diagramas/anteriores/mvp-simplificado-conceptual.svg): omite versión de concurrencia, hash, huella e identificador técnico de operación para explicar el negocio.
- [ERD físico editable](../diagramas/anteriores/mvp-simplificado-fisico.erd) y [ERD conceptual editable](../diagramas/anteriores/mvp-simplificado-conceptual.erd): abrir directamente con ERD Designer. No es necesario importar SQL ni ejecutar DDL.
- [Diccionario completo](../DICCIONARIO_SIMPLIFICADO.csv): tipo SQL, nulabilidad, clave primaria y significado de los 50 atributos.
- Capturas actuales: `portal/evidencias/simplificacion/capturas/`; imágenes revisadas de los diagramas en esa carpeta de evidencias.

Los archivos `mvp-relaciones-listo.erd`, `mvp-operativo.erd` y el DDL documental anterior se conservan como versión de nueve tablas. Los nuevos diagramas se generaron desde metadatos SQL de Pruebas y representan también el modelo promovido a Desarrollo; no contienen filas ni contraseñas. `diagramar_simplificacion.py` permite regenerarlos desde la extracción auditada sin consultar datos de negocio.
