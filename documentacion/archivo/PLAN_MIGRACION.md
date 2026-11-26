# Plan de migración ejecutado — Fase C

> Antecedente conservado para trazabilidad. No es la guía de instalación ni de operación vigente. Consulta el [índice actual](../README.md).

02/11/2026. Implementación en una base de desarrollo independiente. No se reemplazó la base ni el portal anterior.

## Decisiones y alcance

Se ejecutó el modelo aprobado de nueve tablas operativas. El archivo histórico y las vistas de lectura son auxiliares: no agregan inventarios editables, solicitudes, alarmas ni motores de simulación al MVP. La condición nueva de fase C sustituye la limitación A/B de conservar el histórico solo fuera de la aplicación.

El repositorio de trabajo está en outputs/motherson-l1-mvp-2026-11-02, rama fase-c/mvp-historico-kpis. Los archivos fuente originales de outputs/motherson-l1-claro-2026-10-28 se conservaron; sus 113 huellas comparadas con la auditoría A/B no cambiaron.

## Fuente comprobada y corte

Fuente usada: las diez exportaciones SQL comprimidas y el manifiesto de la entrega del esquema 21, maestro 4d577245a22d, revisión 210. No se tomó el archivo fixtures como historial anual; solo aporta descripciones contrastadas de material, que pueden quedar ausentes. El respaldo SQL original se conserva en su entrega; esta fase no afirma haber restaurado y auditado de nuevo todas sus 35 tablas.

El año base es 25/10/2025–24/10/2026. La exportación incluye continuación posterior: el evento máximo conserva fecha_evento_utc 2026-10-27T15:30:51.389889. Ese es el corte lógico de fuente.

El límite inclusivo almacenado en archivo.CORTE es 2026-10-27T15:30:51.3900000. La primera carga redondeó ese metadato al usar el parámetro SQL de fecha con precisión inferior; los eventos cargados por BulkCopy conservaron su precisión original. Se corrigió el parámetro a DateTime2 para futuras cargas y se mantuvo el corte sellado existente. La comprobación exige MAX(fecha_movimiento) <= corte_fuente, sin cambiar ni redondear los eventos originales.

## Flujo aplicado

1. Leer y comprobar archivos originales, IDs, versiones, referencias y saldos. Generar plan, reporte y proyección de eventos fuera de SQL, sin modificar las fuentes.
2. Crear exclusivamente Motherson_L1_MVP_Desarrollo y Motherson_L1_MVP_Pruebas si no existen. El código y los SQL rechazan otros nombres destino.
3. Crear nueve tablas dbo, tres tablas archivo y las vistas reporting. Conservar todos los gzip originales en archivo.FUENTE, junto a SHA-256 y conteo de filas.
4. Cargar los 709,042 eventos en archivo.EVENTO. Clasificar únicamente deltas operativos explícitos; no convertir fotografías de saldo en consumo.
5. Cargar las 63 posiciones documentales, operables, con saldo conocido y empaque/capacidad definidos. Conservar IDs de origen en el plan, sin declarar equivalencia global entre IDs antiguos y nuevos.
6. En una misma transacción, crear catálogo, cuenta inicial de carga, UBICACION y una APERTURA_SIMULADA por ubicación; comprobar suma de 10,116 piezas y sellar archivo.CORTE. La fecha de apertura es la de importación, no una fecha medida en planta.
7. Crear cuenta SQL de aplicación y cuenta lectora con permisos separados. La aplicación arranca con su cuenta limitada, no con sa.
8. Adaptar portal y realizar las pruebas de integridad, negocio, concurrencia, permisos, navegador y reinicio documentadas.

Si falla la carga de datos, su transacción se revierte. Puede quedar el esquema nuevo vacío, pero no una apertura parcial publicada. Una carga sellada con la misma huella se reconoce y no se repite; una huella distinta o una carga incompleta detiene el proceso. No se sobreescribe silenciosamente.

## Apertura sin doble contabilización

El saldo operativo arranca desde el último saldo conocido conciliado, no desde cero reproduciendo todos los movimientos anteriores. El archivo sirve para análisis y el MVP continúa desde la apertura. No se reaplican los 230,901 movimientos históricos al inventario nuevo.

La apertura tiene un índice UNIQUE filtrado por ubicación; la operación nueva tiene UUID único. El procedimiento valida perfil, versión y espacio/saldo dentro de transacción. Si dos personas operan a la vez, solo se confirma el resultado compatible con la versión y capacidad vigentes. Un reintento idéntico devuelve su comprobante; la misma clave con otros datos se rechaza.

## Identidad, procedencia y naturaleza

Se conserva el contexto de material/ubicación/turno que suministra la exportación original. Un campo histórico desconocido queda NULL. Los responsables ficticios no se convierten en cuentas autenticadas. Los IDs históricos de usuario no se unen a USUARIOS del MVP por coincidencia numérica.

Los movimientos se identifican globalmente como H21:maestro:id o MVP:id. es_simulado=1 identifica todos los datos de esta instalación. Se permite distinguir operaciones reales en el modelo para una futura instalación validada, pero el portal de demostración no ofrece un selector para convertir datos simulados en reales.

## Ejecución reproducible en esta máquina

Desde la carpeta portal nueva, con .NET 10, Python 3 y Docker disponible:

```sh
dotnet build Motherson.slnx
npm --prefix frontend ci
npm --prefix frontend run build
python3 scripts/preparar_historico.py --origen "../../motherson-l1-claro-2026-10-28" --salida ".local/migracion"
python3 scripts/local.py --migrar ".local/migracion/plan.json"
python3 scripts/local.py --integridad
python3 scripts/exportar_acceso_lectura.py
bash scripts/start.sh
```

La instancia comprobada es motherson-mvp-sql-1, puerto 1433; el lanzador detecta su puerto publicado. La preparación exige conservar la entrega fuente y sus diez exportaciones. El plan contiene rutas absolutas: si se mueve a otra máquina debe generarse allí de nuevo. No se escribe la contraseña SQL en los comandos.

Compose también conserva una configuración de instancia nueva en 1438, con volumen propio mvp_sql. Esa instalación limpia no se inició en esta verificación para evitar otro servidor y más consumo de disco/memoria. No se confunde con la instancia 1433 ya probada. Para usarla se requiere SQL_SA_PASSWORD local, iniciar el proyecto Compose nuevo y pasar su nombre real de contenedor a local.py --contenedor. No ejecutar down -v ni limpiar volúmenes para preparar el MVP.

## Recuperación y conservación

El portal anterior permanece en 5080. Volver a él no necesita borrar el MVP; basta usar su dirección y su instalación original. Los módulos retirados del código nuevo están en archivo-codigo y en el commit inicial; no se compilan ni se cargan como rutas activas.

Las credenciales nuevas y claves de sesión permanecen en portal/.local, fuera de Git. Preservar esa carpeta de forma privada al conservar esta instalación; no incorporarla al paquete público del Equipo 2. La cuenta lectora se puede entregar por un canal privado independiente.

No se borraron bases, contenedores, volúmenes ni archivos históricos. El único reinicio del SQL compartido fue aprobado expresamente por el usuario y su persistencia quedó comprobada. No se ejecutó una restauración independiente del nuevo MVP; los originales continúan disponibles y el procedimiento de carga es reproducible.
