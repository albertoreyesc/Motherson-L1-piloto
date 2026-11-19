# Indicadores del Equipo 2 en el portal

06/11/2026. El portal muestra los 10 KPIs que el Equipo 2 integró en su Power BI para la entrega del 05/11/2026 (KPIhoja1.pdf y KPIhoja2.pdf) y los descarga en Excel. **No cambia la estructura ni los datos de SQL Server**: las ocho tablas de `Motherson_L1_MVP_Demo` quedan igual.

## Dónde está

Sección **Indicadores** del menú. La ve la cuenta de administración (permiso `historico`) cuando el portal corre sobre Demo, igual que el Historial de movimientos. Las cuentas por puesto no la ven.

| Control | Uso |
| --- | --- |
| Corte (hora de México) | Momento al que se reconstruye el inventario y se evalúan los reabastecimientos en curso. Por defecto, ahora |
| Actividad desde / hasta | Periodo de los KPIs de actividad. Por defecto, los últimos 7 días |
| Estación, Material | Filtros opcionales |
| Semana del histórico (19–25 oct) | Atajo al periodo del histórico maestro que usa el Power BI de ejemplo |
| Descargar Excel | Genera un `.xlsx` con lo que está en pantalla |

## Los 10 KPIs

| Hoja | ID | KPI | Cálculo |
| --- | --- | --- | --- |
| 1 · Estado actual | [04] | Inventario actual en punto de uso | Suma de existencias de las ubicaciones al corte (piezas) |
| 1 · Estado actual | [13] | Cajas equivalentes disponibles | Σ existencia ÷ piezas por caja, por material; puede ser fraccionario |
| 1 · Estado actual | [10] | Materiales en advertencia | mínimo < existencia ≤ 1.20 × mínimo; % sobre materiales evaluados |
| 1 · Estado actual | [11] | Materiales en nivel crítico | 0 < existencia ≤ mínimo |
| 1 · Estado actual | [01] | Materiales agotados | existencia = 0 |
| 2 · Actividad logística | [03] | Reabastecimientos completados | Fin registrado dentro del periodo y fin ≥ inicio |
| 2 · Actividad logística | [21] | Reabastecimientos en curso | Iniciados y sin fin al corte |
| 2 · Actividad logística | [18] | Reabastecimientos repetidos en un día | Σ MAX(registros − 1, 0) por día, material y estación |
| 2 · Actividad logística | [12] | Registros incompletos | Sin responsable o sin ubicación identificada |
| 2 · Actividad logística | [17] | Intervalo promedio entre reabastecimientos | Promedio de horas entre inicios consecutivos del mismo material y estación |

Los materiales sin mínimo o sin saldo quedan fuera del denominador (estado «Sin datos»), como piden las condiciones de cálculo de los PDF. La tabla «Materiales que requieren atención» ordena agotados, críticos y advertencia, y dentro de cada estado la menor cobertura (existencia ÷ mínimo) primero.

## De dónde salen los datos

El cálculo es el que ya existía en el backend (`GET /api/kpis`, `Store.Kpis.cs` y `KpisMotor.cs`). Se hizo un solo cambio:

- **Actividad logística.** Antes solo contaba los reabastecimientos del histórico maestro, que termina el 27/10/2026. Ahora también cuenta las atenciones capturadas en el portal (`dbo.REGISTRO`: puesto, ubicación, inicio y fin; el fin queda vacío mientras la atención está abierta), que es la definición de REGISTRO de los PDF. Material y estación se obtienen por REGISTRO → UBICACION → ESTANTERIA → ESTACION. El responsable de una atención del portal es su puesto. Nuevo archivo: `portal/backend/Motherson.Api/Store.KpisPortal.cs` (solo lectura).
- **Estado actual.** Sin cambios: el inventario al corte se reconstruye desde el saldo inicial y la continuación de movimientos del portal.

Todos los datos de esta instalación siguen siendo **simulados**.

## Excel

Se genera en el navegador con la librería `exceljs` (MIT), que se carga solo al presionar el botón. Hojas:

| Hoja | Contenido |
| --- | --- |
| Resumen KPIs | Los 10 KPIs con valor, unidad, porcentaje y fórmula, más el corte, el periodo y los filtros |
| Atención | Materiales agotados, críticos y en advertencia, en orden de prioridad |
| Materiales | Estado de todos los materiales del alcance |
| Inventario | Existencias por ubicación al corte, con cajas equivalentes y capacidad |
| Registros | Reabastecimientos evaluados (histórico maestro y portal) con inicio, fin y estado |
| Por día, Repetidos, Más reabastecidos, Último reabasto | Detalle de la actividad |

El Power BI del Equipo 2 (`updatepowerbi.pbix`) no lee SQL Server: carga archivos CSV y JSONL desde las carpetas de sus parámetros `RutaCSV` y `RutaEquipo2`. El Excel sirve para revisar o compartir los valores del portal; conectarlo a su modelo requiere que ellos cambien esas consultas.

## Compilar

La librería nueva requiere instalar dependencias una vez. Desde `portal`:

```sh
npm --prefix frontend ci
npm --prefix frontend run build
dotnet build Motherson.slnx
```

El frontend compilado ya viene en `backend/Motherson.Api/wwwroot`. Si solo cambias el backend, basta `dotnet build`.

## Pruebas

- `npm --prefix frontend test`: 14 pruebas (10 existentes y 4 nuevas: orden de prioridad, formato del corte, porcentajes y contenido del Excel).
- Recorrido en navegador con la política de seguridad (CSP) del backend y API simulada: la sección carga sin errores y el botón descarga el `.xlsx`.
- **Pendiente:** `dotnet build` y una consulta contra la base Demo real. El entorno donde se hizo el cambio no tenía .NET ni SQL Server.
