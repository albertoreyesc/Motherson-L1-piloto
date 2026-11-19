# Integración transaccional con Azure propio

18/11/2026. Rama local: `integracion/azure-propia`.

Destino único: `motherson-l1-equipo.database.windows.net` / `Motherson_L1`.
El usuario eligió **datos simulados de demostración**. La instalación, cuentas,
permisos y carga en Azure siguen pendientes de aprobación agrupada.

## Estado comprobado

El diagnóstico autorizado del 18/11/2026 encontró cero tablas, vistas,
procedimientos y funciones de usuario; tampoco existen portal_app ni
powerbi_lector. Se consultó como dbo, con CONTROL y VIEW DEFINITION.
Repetir el diagnóstico antes de aplicar: el estado puede cambiar desde entonces.
No se creó ningún objeto ni se cargaron datos en Azure durante el desarrollo.

El código y SQL de esta entrega se probaron con SQL Server 2022 local y .NET 10
instalado de forma aislada en portal/.local/dotnet. Esto no sustituye la prueba
final de Azure SQL, sus permisos y red.

## Modelo definitivo

Se conservan las ocho tablas dbo del ERD. UBICACION añade version_concurrencia;
PUESTO_LOGISTICO tiene índice único de usuario cuando no es NULL.

Se añaden siete tablas en mes:

| Tabla | Propósito |
| --- | --- |
| CONFIGURACION | Versión del esquema y marca de simulación |
| BASE_INVENTARIO | Apertura acreditada y snapshot de inventario |
| OPERACION | UUID único, cuenta, solicitud exacta, fecha y respuesta durable |
| MOVIMIENTO | Ubicación, cajas, empaque aplicado, piezas y saldos |
| AJUSTE | Cambios de capacidad/material con anterior y nuevo |
| HISTORICO_LEGADO | Evidencia original, archivo, hash y número de línea |
| ARCHIVO_LEGADO | Conteo esperado y confirmación de archivo completo |

mes.RegistrarMovimiento y mes.AjustarCatalogo ejecutan validación, escritura e
historial en la misma transacción. Un bloqueo de aplicación común serializa
escrituras de inventario/catálogo. Se comprueba UUID antes de aplicar una versión
nueva; una repetición idéntica devuelve la respuesta original y una distinta se
rechaza. La autorización de cuenta/puesto se revalida también en el backend.
SQL impide saldos negativos, sobreocupación, ajustes con valores anteriores
obsoletos e importaciones parciales.

El procedimiento dbo.RegistrarMovimiento anterior permanece como dependencia
de compatibilidad de los scripts recibidos; portal_app tiene DENY EXECUTE sobre
él. El backend nuevo solamente escribe mediante los procedimientos mes.

El modo `MVP_SQL_TRANSACCIONAL=true` no abre diario JSONL ni exige histórico-demo.
La apertura SQL es obligatoria: no se inventa un saldo anterior a ella. Los
indicadores rechazan cortes previos si no existe una apertura acreditada; las
atenciones históricas no equivalen a movimientos de piezas.

## Archivos de instalación y permisos

- portal/db/azure/000_estado_actual.sql: diagnóstico de lectura.
- instalar.sql: instalación con SQLCMD mode, desde esa carpeta, abortando errores.
- instalar_portal.sql: equivalente autocontenido para Azure Query editor. Utiliza
  una transacción y ejecuta cada módulo en su propio lote; no contiene :r ni GO.
- 020_permisos.sql: roles y permisos, después de crear los usuarios privados.
- 030_verificar.sql: verificación de estructura, saldos y roles.

Solo usar **una** variante de instalación. Ambas exigen el destino Motherson_L1
sin tablas de usuario; si ya existen, detenerse y conciliar. No ejecutar todos
los SQL históricos de portal/db. No ejecutar 014_ajustes_catalogo.sql: el ajuste
nuevo ya está integrado transaccionalmente en mes.AjustarCatalogo.

La creación de usuarios se prepara de forma local, sin conectar a Azure:

```sh
dotnet run --no-launch-profile --project portal/backend/Motherson.Api -- --preparar-accesos-locales portal
```

Produce cuatro archivos privados bajo portal/.local, con permisos de propietario:
conexión del backend, hash de admin.local, accesos iniciales y SQL de creación de
usuarios contenidos. No imprime contraseñas, no sobrescribe configuración previa,
no crea usuarios en Azure y no se incluye en Git o ZIP de entrega. Guardar los
accesos en un gestor privado. No reutilizar esas claves para otros entornos.

portal_app recibe el rol mes_portal, con SELECT y EXECUTE acotados. No recibe
escritura directa, apertura inicial ni importación de legado. powerbi_lector
recibe mes_lectura: columnas públicas de puestos, tablas para BI, vw_registro_bi,
mes.vw_movimientos y mes.vw_ajustes. No puede leer hashes ni ejecutar capturas.
Revisar los permisos efectivos al aplicar, además de las concesiones del rol.

## Carga de demostración

El cargador nuevo utiliza SqlBulkCopy con identidades y restricciones, verifica
hashes de los ocho CSV y abre una única transacción para todo el conjunto.
Solo admite tablas vacías, sin apertura ni operaciones y con es_simulado=true.
No borra tablas ni recarga encima. Fija la apertura al terminar y concilia conteos.

Vista previa sin SQL:

```sh
dotnet run --no-launch-profile --project portal/backend/Motherson.Api -- --cargar-demo datos/simulados
```

Tras aprobación, con la configuración Azure y una cuenta temporal de carga,
añadir --aplicar. ConnectionStrings__Sql y MVP_BASE_COMPARTIDA se suministran
por un canal privado. No usar la cuenta restringida portal_app para esta carga.

Esperados: 27 estaciones, 15 colores, 6 puestos, 2 turnos, 85 estanterías,
85 materiales, 85 ubicaciones, 58,422 atenciones y 10,474 piezas. Las cuentas de
puestos empiezan vacías y se dan de alta desde la administración del portal.

## Preservación del histórico disponible

Se encontró un paquete sellado y dos diarios en la copia local del 12/11/2026.
No se afirma que sean los archivos privados más recientes del repositorio del
18/11/2026, cuyo ZIP no incluye .local. Se conserva su procedencia exacta.

--migrar-legado CARPETA --diario ARCHIVO (repetible) valida manifiesto, hashes,
JSON y cadena del diario sobre copias temporales; sin --aplicar no conecta a SQL.
Con --aplicar inserta la evidencia en mes.HISTORICO_LEGADO usando claves
(hash del archivo, línea), en lotes reanudables de 1,000 filas. Repetir la importación no
duplica archivos ya incorporados. Se normalizan copias de los movimientos del
diario para consulta y se retienen los sobres originales, incluida la apertura.

**No vuelve a aplicar los movimientos antiguos al saldo nuevo.** Los espacios
de identificadores y archivos originales se preservan; no se hace una relación
implícita entre ubicaciones históricas y ubicaciones operativas. Los registros
brutos de distintas fuentes pueden describir el mismo hecho: no sumar todas las
fuentes como si fueran movimientos diferentes.

El ZIP de legado se entrega aparte de código y secretos. Los archivos incompletos no aparecen en el historial: ARCHIVO_LEGADO solo se confirma después de verificar integridad y conteos. Una interrupción conserva los lotes ya confirmados y permite reanudar sin duplicar. No se sobrescribe evidencia diferente bajo el mismo hash y línea.

Las imágenes previas,
si existen en otra instalación privada, deben trasladarse aparte; no están en
el ZIP de GitHub. Las imágenes nuevas se guardan en almacenamiento persistente.

## Backend y despliegue

Perfil ASPNETCORE_ENVIRONMENT=Azure carga appsettings.Azure.json, habilita el modo
SQL y comprueba servidor/base/cifrado. Suministrar ConnectionStrings__Sql con
portal_app, MVP_ROOT y MVP_ADMIN_PUESTOS_HASH. MVP_KEYS_PATH debe apuntar a claves
persistentes fuera de la carpeta pública. Mantener configurado AllowedHosts con
el dominio real del despliegue. No activar capturas contra un esquema incompleto.

Para el arranque local compartido, scripts/local.py --compartida lee los archivos
privados nuevos de Motherson_L1. El modo local anterior de demostración se conserva.

MVP_ROOT debe contener config/ y un área privada .local/imagenes-materiales.
Ese almacenamiento y MVP_KEYS_PATH deben sobrevivir reinicios y publicaciones.
No alojar esos archivos en wwwroot. No se creó App Service ni almacenamiento en
Azure: definir esos recursos y su configuración antes del despliegue.

## Recuperación

- Instalación: toda la creación se confirma o revierte; después de un error,
  inspeccionar el estado antes de reintentar. No ejecutar DROP sobre objetos existentes.
- Carga: ante fallo se revierte todo; no utilizar --reiniciar del cargador antiguo.
- Operación: reintentar con el mismo UUID y cuerpo para recuperar el comprobante.
- Un error al guardar historial revierte también existencias y stock agregado.
- Conservar respaldos y el paquete legado original. No restablecer versiones ni
  reabrir el saldo inicial para corregir operaciones ya confirmadas.
- Una restauración posterior con datos requiere un plan de recuperación y respaldo;
  no existe un rollback destructivo automático que elimine datos operativos.

## Power BI

El PBIX original y el TMDL están en powerbi/. Sus fuentes se preparan para esta
base; el PBIX requiere configurar sus parámetros en Desktop o el servicio. El
modelo actual usa Import. Concilia contra UBICACION y vw_registro_bi; para nuevas
medidas de cantidades hay vistas mes de movimientos/ajustes sin hashes.

El usuario no tiene un espacio propio. Publicación, credenciales del servicio,
actualización, ejecución de DAX en Power BI e incrustación están pendientes. No
se utilizó ningún espacio del otro equipo ni se eligió una licencia de pago.
