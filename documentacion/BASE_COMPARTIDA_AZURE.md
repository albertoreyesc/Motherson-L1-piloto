# Portal conectado a la base compartida de Azure

10/11/2026. El portal puede guardar en la base de Azure del Equipo 2 (`mothersonbdd.database.windows.net`, base `db`) para que su Power BI muestre lo capturado. El arranque local con `Motherson_L1_MVP_Demo` no cambia.

## Qué se guarda en Azure

| Captura en el portal | Dónde queda en Azure |
| --- | --- |
| Inicio y fin de una atención | `dbo.REGISTRO`, en hora de México |
| Entrada o salida de material | `dbo.UBICACION.cantidad` y `dbo.MATERIAL.stock`, mediante `dbo.RegistrarMovimiento` |
| Usuario y contraseña de un puesto | `dbo.PUESTO_LOGISTICO.usuario` y `password` (hash) |

El detalle de cada movimiento (quién, cuándo, cajas, saldo anterior y posterior) sigue en el diario local del portal, porque el modelo de 8 tablas no tiene dónde guardarlo. Con la base compartida el diario está en `portal/.local/continuacion-movimientos-compartida/`, separado del de Demo.

## Preparación en Azure (la hace el Equipo 2)

1. `portal/db/011_azure_objetos_portal.sql`: 3 vistas, 1 función y 3 procedimientos. Ya aplicado el 10/11/2026.
2. Su procedimiento `dbo.RegistrarMovimiento`. Ya aplicado.
3. `portal/db/012_azure_inventario_por_ubicacion.sql`: la vista de inventario lee `UBICACION.cantidad`.
4. Permisos para el usuario del portal: `SELECT` en las 8 tablas y las 3 vistas, y `EXECUTE` en `IniciarAtencion`, `FinalizarAtencion`, `GuardarAccesoPuesto` y `RegistrarMovimiento`.

## Arranque

1. Copia `portal/config/conexiones-compartida.ejemplo.json` a `portal/.local/conexiones-compartida.json` y escribe la contraseña. `.local` no se sube a GitHub.
2. Desde `portal`:

```sh
npm --prefix frontend run build
dotnet build Motherson.slnx
python3 scripts/local.py --compartida --puerto 5089
```

3. Entra como `admin.local` (misma contraseña que en Demo) y da de alta el acceso de cada puesto. En Azure empiezan sin usuario.

La base gratuita se pausa sola: el primer arranque puede tardar hasta 90 segundos. Mientras el portal está abierto consulta la base con frecuencia, así que ciérralo al terminar la prueba para no gastar la cuota.

## Si el portal bloquea las capturas

El portal comprueba que el inventario de Azure sea igual al saldo con el que empezó más los movimientos que él registró. Si alguien cambia existencias o el catálogo directamente en Azure, muestra un aviso y no permite capturar, para no mezclar saldos.

Para volver a empezar desde lo que haya en Azure: detén el portal, cambia el nombre de la carpeta `portal/.local/continuacion-movimientos-compartida/` (no la borres) y arranca de nuevo. El portal toma el inventario actual como nuevo saldo inicial.

## Cambios de código

| Archivo | Cambio |
| --- | --- |
| `Program.cs`, `MvpStore.cs` | La base indicada en `MVP_BASE_COMPARTIDA` queda autorizada. Errores 51010–51012 de `RegistrarMovimiento` con su mensaje. `/api/kpis` declara su tipo de retorno para compilar |
| `Store.Puestos.cs` | La base compartida usa el modelo de Demo (vistas en `dbo`) |
| `Store.Continuacion.cs`, `DiarioMovimientos.cs` | Cada movimiento se aplica primero en SQL y después en el diario; la verificación compara SQL contra saldo inicial + diario |
| `Store.KpisPortal.cs` | Indicadores lee las atenciones por `vw_kpi_atenciones`, que entrega UTC |
| `scripts/local.py` | Opción `--compartida` |
| `pruebas-continuacion/Program.cs` | 9 casos nuevos del modo compartido (43 en total, sin SQL) |

No se probó contra Azure ni contra un SQL Server: las pruebas del diario simulan el procedimiento.
