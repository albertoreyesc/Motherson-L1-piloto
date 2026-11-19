# Datos de demostración

Los ocho CSV de `simulados/datos/` proceden de Entrega.zip. Son simulados: 85 materiales,
85 ubicaciones y 58,422 atenciones; no acreditan existencias reales.

El cargador original permanece con su requirements.txt. Lee el DDL de
`esquema/01_crear_tablas.sql` al buscar en carpetas hermanas. Ese DDL es una referencia
para el parser; la instalación autorizada se realizará con `portal/db/azure/instalar.sql`.

No ejecutar cargas en esta fase. `--reiniciar` borra datos y no forma parte de la migración propuesta.
El cargador original puede crear tablas si no las encuentra: no debe usarse para reemplazar
la instalación consolidada. Necesita una cuenta temporal de carga con IDENTITY_INSERT,
no la cuenta restringida portal_app.

Estos CSV no reemplazan `portal/.local/historico-demo/`, requerido por el backend actual.
