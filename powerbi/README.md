# Power BI · fuentes vigentes

El portal registra los datos en SQL; Power BI consulta las vistas `mes2.bi_*` con un lector independiente. El informe utiliza Import, no escritura desde Power Apps ni sincronización instantánea. El acceso Microsoft y las licencias son independientes de la cuenta del portal.

| Ruta | Uso |
| --- | --- |
| [administracion-v2/](administracion-v2/README.md) | Modelo TMDL activo: ocho tablas, ocho relaciones y 110 medidas |
| [equipo2-2026-11-24/report/](equipo2-2026-11-24/report/) | Diseño PBIR adaptado al modelo activo, tres páginas |
| [equipo2-2026-11-24/](equipo2-2026-11-24/README.md) | PBIX, TMDL y README originales del equipo 2; referencia, no publicar encima del piloto |
| [archivo/](archivo/README.md) | TMDL previo, fuera de la publicación vigente |
| `configuracion-privada.ejemplo.json` | Ejemplo con marcadores; valores reales solo fuera de Git |

Se retiró el PBIX duplicado de esta carpeta raíz: era idéntico byte por byte al original conservado en `equipo2-2026-11-24/Motherson.pbix`.

[Integración, actualización, límites y recuperación](../documentacion/INTEGRACION_EQUIPO2_2026-11-24.md). La cobertura temporal sigue sin estimación en Power BI; el cálculo por ubicación se consulta en Inventario del portal. Ningún archivo de esta carpeta se publica automáticamente por mover documentación o abrir un PR.
