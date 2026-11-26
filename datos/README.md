# Datos simulados de instalación y pruebas

Los ocho CSV de `simulados/datos/` proceden de la entrega de demostración: 85 materiales, 85 ubicaciones y 58,422 atenciones iniciales. Son fuentes congeladas, no saldos actuales del portal ni datos reales de planta. `sha256.json` conserva sus huellas; `.gitattributes` evita conversión de saltos de línea.

`DemoData.Load`, los inicializadores del piloto y las pruebas SQL locales utilizan estos archivos. `esquema/01_crear_tablas.sql` es una referencia del cargador original. Por ello se conservan sus rutas y el cargador con sus dependencias.

El cargador original puede crear tablas y su opción `--reiniciar` borra datos. No ejecutarlo para actualizar el piloto ni para organizar este repositorio. Una instalación nueva sigue el [manual vigente](../documentacion/MANUAL_DE_INSTALACION.md) y los scripts seleccionados de [SQL](../portal/db/README.md).

La operación v3 utiliza SQL/mes2 en el perfil piloto; los paquetes privados `.local/historico-demo` corresponden a modalidades anteriores y no se incluyen ni se reconstruyen como parte de esta limpieza.
