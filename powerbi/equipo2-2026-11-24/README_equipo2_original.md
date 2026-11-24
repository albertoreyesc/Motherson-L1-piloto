# Smart Material Replenishment · Motherson L1

Este paquete les permite montar desde cero el sistema de monitoreo de surtimiento a línea con su propia cuenta de Azure y su propia organización de Power BI.

![Arquitectura](<5. Documentacion/arquitectura.png>)


## Qué hay en cada carpeta

1. `1. BDD`
   - Los scripts SQL, numerados en el orden en que se corren (01 a 07).
   - El diagrama del ERD.

2. `2. Datos`
   - Un año de operación simulada de la Línea 1: un CSV por cada tabla del ERD.
   - El script que los carga en la base. Es opcional.

3. `3. PowerBI`
   - `Motherson.pbix`: el dashboard.
   - `modelo_completo.tmdl`: el modelo de datos en texto, por si necesitan recrearlo.

4. `4. Actualizacion_Automatica`, opcional
   - Lo necesario para que el dashboard consulte la base de forma automática: las vistas y una guía paso a paso.
   - No se usa en la instalación normal.

5. `5. Documentacion`
   - Guía de instalación, arquitectura, indicadores, manual de uso, operación y mejoras futuras.


## Qué necesitan

- Una suscripción de Azure. La oferta gratuita de Azure SQL alcanza.
- Una cuenta de Power BI de su organización, con licencia Pro o la prueba gratuita.
- VS Code con la extensión SQL Server (mssql), para correr los scripts.
- Python 3.12 y ODBC Driver 18 for SQL Server, solo si van a cargar los datos simulados.
- El portal de captura de Equipo 1, para registrar atenciones y movimientos.


## Pasos

La guía completa, con lo que deben ver en cada paso, está en [1. Instalacion.md](<5. Documentacion/1. Instalacion.md>). En resumen:

1. Crear la base en Azure SQL
   - En portal.azure.com, con la oferta gratuita.
   - Agregar su IP en el firewall.

2. Crear las tablas (`01_crear_tablas.sql`)
   - En VS Code, conectados con su usuario administrador.
   - Darle ▶ Run, arriba a la derecha.

3. Cargar los datos simulados (`cargar_azure.py`), opcional
   - En una terminal, dentro de la carpeta `2. Datos`.

4. Crear los objetos del portal, los usuarios, los índices y la vista de Power BI (`02` a `05`)
   - Uno por uno, en ese orden, con ▶ Run.
   - Antes de correr el `04`, cambiar sus 3 contraseñas.

5. Verificar (`07_verificar.sql`)
   - Todo debe salir en OK.

6. Publicar el dashboard (`Motherson.pbix`)
   - Subirlo a un área de trabajo de Power BI.
   - Escribir su servidor y su base en los parámetros.
   - Poner las credenciales de `powerbi_lector` y actualizar.

7. Conectar el portal
   - Con el usuario `portal_app`.

8. Dejarlo operando
   - Programar la actualización y dar acceso a quien lo vaya a usar.


## Cómo saber que todo quedó bien

Con los datos simulados recién cargados deben ver estos valores:

- `07_verificar.sql`
  - Todas las filas en OK.
  - 85 materiales, 10,474 piezas, 58,422 registros y 1,146 completados en la semana del 19 al 25/10/2026.

- Hoja Crítico por surtir
  - 10,474 piezas en 85 materiales.
  - 3 críticos y 6 en advertencia.
  - Cobertura de 63 %.
  - Próximo faltante en unos 12 min (5QV 419 512, estación AL09).

- Hoja Desempeño semanal, semana S43
  - 1,146 reabastecimientos (▼ −0.8 %).
  - Tiempo promedio de 3.2 min, con P90 de 4.5 min.
  - 8.1 % fuera del objetivo.
  - 721 repetidos e intervalo de 12.5 h.
  - 79 % de ocupación.

- Hoja Diagnóstico, del 01/02/2026 al 31/03/2026
  - 9,254 surtidos.
  - SZ01 concentra el 38 % (3,471) y tiene 30 % de ocupación.
  - El material más lento es la Caja de acond importada 5WB 816 001 B, con 52.1 min.


## Seguridad

- El paquete no incluye ninguna contraseña. El script de usuarios se detiene si no cambian las de ejemplo.
- Power BI y las personas de consulta solo pueden leer, y no ven los hashes de contraseña del portal.
- El portal solo escribe por medio de procedimientos, que revisan los datos antes de guardarlos.


## Autoría

- Equipo 2: dashboard, modelo de datos de Power BI, carga de datos simulados y procedimiento de movimientos.
- Equipo 1: ERD, portal de captura con sus vistas y procedimientos, e histórico simulado.
