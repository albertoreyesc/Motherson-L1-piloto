# Motherson L1 · Portal 5089

> Antecedente conservado para trazabilidad. No es la guía de instalación ni de operación vigente. Consulta el [índice actual](../README.md).

La instalación vigente utiliza **Motherson_L1_MVP_Demo**. Conserva las ocho tablas y ocho claves foráneas de `dbo`. Las operaciones cuantitativas nuevas se conservan en un diario privado; las atenciones mantienen su función en `REGISTRO`. Consulta [Continuación de movimientos](CONTINUACION_MOVIMIENTOS.md) para conocer los cálculos, las pruebas y el respaldo necesario.

Abre esta carpeta completa en Antigravity mediante **File → Open Folder**. La solución .NET está en `portal/Motherson.slnx`; el frontend React, en `portal/frontend`.

## Abrir en esta Mac

Abre Docker Desktop. Si `motherson-mvp-sql-1` está detenido, inícialo:

```sh
docker start motherson-mvp-sql-1
```

En una Terminal:

```sh
cd "/Users/nihhil/Documents/Codex/2026-10-20/referenced-chatgpt-conversation-this-is-an/outputs/Motherson-L1-5089-Antigravity/portal"
python3 scripts/local.py --iniciar --database Motherson_L1_MVP_Demo --puerto 5089
```

Espera `Now listening on: http://127.0.0.1:5089`, abre [el portal](http://127.0.0.1:5089/) y deja esa Terminal abierta. Usa las cuentas vigentes de esta instalación; la contraseña del portal es distinta de las credenciales SQL. No se han restablecido contraseñas.

Si el puerto está ocupado, comprueba la instancia existente. Para reiniciarla, usa Ctrl+C en su propia Terminal, espera a que termine y ejecuta el comando una sola vez. La continuación admite un único escritor; no abras otra instancia con el mismo diario.

**No ejecutes una preparación, migración ni restauración SQL para abrir esta instalación ya preparada.** Los procedimientos de Pruebas de [MODELO_POR_PUESTOS.md](MODELO_POR_PUESTOS.md) documentan una etapa anterior.

## Compilar después de editar

Requiere .NET SDK 10, Node.js/npm y Python 3.11 o posterior. Desde `portal`, con las dependencias ya instaladas:

```sh
npm --prefix frontend run build
dotnet build Motherson.slnx
```

Después reinicia únicamente el portal. En una instalación nueva, instala primero las dependencias con `npm --prefix frontend ci`.

## Archivos privados que deben conservarse

- `portal/.local/historico-demo/`: manifiesto y doce fuentes selladas.
- `portal/.local/continuacion-movimientos/`: saldo inicial verificable y movimientos nuevos en `eventos.jsonl`.
- Configuración privada de conexión y administración de Demo, separada de los datos analíticos.

El código por sí solo no contiene estos datos. Un respaldo SQL por sí solo tampoco contiene los movimientos del diario. Para trasladar el proyecto, sigue [la guía de demostración](DEMOSTRACION_DBO.md) y el procedimiento de respaldo de la continuación. No publiques `.local` ni lo adjuntes completo a una entrega de KPI.

## Cambios incorporados el 06/11/2026

- Sección **Indicadores** con los 10 KPIs del Equipo 2 (Hoja 1 · Estado actual y Hoja 2 · Actividad logística) y botón **Descargar Excel**. La actividad ahora incluye las atenciones del portal (`dbo.REGISTRO`). Sin cambios en SQL. Detalle en [INDICADORES_KPI.md](INDICADORES_KPI.md).
- Dependencia nueva del frontend (`exceljs`): ejecuta una vez `npm --prefix frontend ci` antes de volver a compilar el frontend.

## Cambios incorporados el 05/11/2026

- Entradas por cajas y salidas por piezas con bloques visuales, capacidad compartida, comprobante y reintentos sin duplicar.
- Saldo efectivo común para Resumen, Inventario e Historial, calculado desde el corte SQL y las operaciones nuevas.
- Alarmas informativas en Resumen e Inventario y consulta independiente de cortes históricos.
- Conservación de las fuentes, SQL y diagramas. Evidencias en `portal/evidencias/continuacion/`.
