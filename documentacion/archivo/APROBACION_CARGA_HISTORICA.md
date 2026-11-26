# Carga histórica preparada para Motherson_L1

> Antecedente conservado para trazabilidad. No es la guía de instalación ni de operación vigente. Consulta el [índice actual](../README.md).

La prueba completa del 19/11/2026 aprobó las cuatro comprobaciones en SQL Server
2022 local: 1,456,098 registros archivados, conteos completos, 230,987 movimientos
consultables y existencias intactas.

Destino exclusivo: motherson-l1-equipo.database.windows.net / Motherson_L1.
Fuentes: doce archivos sellados, manifiesto y dos diarios de la copia local
motherson-demo-2026-11-12. El paquete principal cubre 25/10/2025–27/10/2026;
la apertura original es del 04/11/2026. Los diarios conservan sus fechas originales.
No se afirma que haya actividad privada posterior que no venía en los ZIP.
Respaldo: outputs/Motherson-L1-legado-preservado.zip, con procedencia y hashes.

Cambios propuestos: insertar evidencia en mes.HISTORICO_LEGADO y confirmar cada
archivo en mes.ARCHIVO_LEGADO, ya existentes; lotes reanudables de 1,000 filas,
sin duplicar al reintentar. Los archivos parciales quedan ocultos hasta verificarse.
Verificar conteos/hashes, historial desde el backend, inventario de 10,474 piezas
y conservación de las seis capturas de demostración ya confirmadas.
No se crean tablas, usuarios ni permisos, ni se vuelven a aplicar movimientos al
saldo actual. No se publicará contenido ni se contratará capacidad de Power BI.
Se usa la cuenta propia de Microsoft Entra para esta carga, no portal_app.

La aprobación anterior excluía esta carga; se presenta este conjunto concreto
para autorizarlo antes de ejecutarlo en Azure, conforme a lo solicitado.

Aprobación expresa recibida del usuario el 19/11/2026: «Sí, autorizar la carga histórica descrita».
