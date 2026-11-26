# Límites conocidos y pendientes (para TI de Motherson)

Estado al 26/11/2026, rama `trabajo/modificaciones-v3`. Este documento resume qué se corrigió en la última revisión, qué límites conoce el equipo del portal y qué le toca a TI al migrar el sistema a su infraestructura. Complementa [GUIA_INSTALACION_LOCAL.md](GUIA_INSTALACION_LOCAL.md) y [ARQUITECTURA_Y_HERRAMIENTAS.md](ARQUITECTURA_Y_HERRAMIENTAS.md).

## 1. Scripts SQL de administración (en este orden)

| Script | Qué hace | ¿Se puede repetir? |
| --- | --- | --- |
| `portal/db/administracion/001_administracion.sql` | Esquema `mes2` por línea, procedimientos y bloqueo del modelo anterior | Sí |
| `portal/db/administracion/003_usuarios.sql` | Usuarios desde el portal (`mes2.UsuarioCrear`, `mes2.UsuarioActualizar`) | Sí |
| `portal/db/administracion/004_rendimiento.sql` | `mes2.Leer` deja de cargar las atenciones terminadas | Sí |

`--administracion-instalar <raíz>` ejecuta los tres en ese orden. Ninguno borra datos.

## 2. Corregido en esta revisión

| Tema | Antes | Ahora |
| --- | --- | --- |
| Ventana de 60 minutos de la alarma | Al cruzar un descanso, cambio de turno o medianoche podía quedar a 1 tick de 60 min y la alarma salía "Sin estimación" | Se calcula en ticks enteros (`CalendarioV2.Ventana`), con prueba de regresión |
| Dos materialistas a la vez en una línea | El segundo recibía "Los datos cambiaron" y tenía que capturar de nuevo | Entradas, salidas y atenciones se reintentan solas hasta 3 veces; el servidor vuelve a validar saldo y capacidad |
| Crecimiento de atenciones | Cada operación cargaba todas las atenciones históricas de la línea | Solo se cargan las abiertas; KPIs y período disponible consultan las terminadas por fecha |
| Atenciones olvidadas | Una atención sin finalizar seguía contando tiempo | Se finaliza al registrar la entrada o salida, se pregunta al salir y se listan las abiertas con su tiempo |
| Horario vencido | Las alarmas pasaban a "Sin estimación" sin aviso | Aviso amarillo 15 días antes y rojo al vencer o si falta |
| Sesión | Se cerraba a los 30 min aunque se estuviera usando | Expira por inactividad (unos 30 a 50 min sin tocar el portal) |

## 3. Límites conocidos

1. **Concurrencia por línea.** Cada operación incrementa la versión de toda la línea y se guarda con un bloqueo por línea. Con el reintento automático los choques no llegan al usuario en condiciones normales. Con muchos materialistas registrando en el mismo segundo podría agotarse el reintento; se mide con el simulador (sección 5).
2. **Atenciones terminadas sin índice dedicado.** Se filtran en SQL con `JSON_VALUE` sobre `mes2.ENTIDAD`. Es suficiente para cientos de miles de registros. Si la planta acumula millones, conviene una columna calculada indexada o una tabla de atenciones cerradas.
3. **Horario de trabajo con vigencia.** Cada línea necesita un horario vigente (máximo 2 años por captura) para calcular cobertura. Sin él, entradas, salidas e inventario funcionan, pero la alarma muestra "Sin estimación".
4. **Requisitos de la alarma de cobertura por ubicación.** Horario vigente, 60 minutos laborables desde la apertura de la ubicación y al menos dos salidas marcadas como "Consumo de producción" en esos 60 minutos. Con poca actividad lo normal es "Sin estimación".
5. **Sesión por inactividad.** El tiempo se ajusta en `Program.cs` (`ExpireTimeSpan`, 30 min, renovación deslizante) y en `portal/frontend/src/main.tsx` (`INACTIVIDAD_MS`, 20 min). Las tabletas compartidas no deben quedar con la sesión de otra persona.
6. **Fotos en la base.** Las fotos de materiales se guardan en SQL (hasta 5 MB cada una). Considerarlo en el tamaño de la base y de los respaldos.
7. **Usuarios.** Se crean en la sección *Usuarios* (solo ADMIN). Contraseña mínima de 12 caracteres; siempre queda al menos una cuenta ADMIN activa. Cambiar rol, contraseña o estado cierra la sesión de esa cuenta.
8. **Número de líneas fijo (4).** Agregar líneas no se hace desde el portal. El límite está en tres lugares: la regla `CHECK(id BETWEEN 1 AND 4)` de `mes2.LINEA` (`001_administracion.sql`, que también inserta las 4 líneas), la validación `linea is >=1 and <=4` en `AdminV2.Leer` (`AdminV2.cs`) y `Entero(a,"linea",1,4)` en `AdminV2.Usuarios.cs`. Si la planta necesita más líneas, TI puede ampliar esa regla, insertar la línea en `mes2.LINEA` con un nombre y dar acceso en `mes2.ACCESO` (las cuentas se asignan después en *Usuarios*). Una línea nueva empieza vacía y sin horario. El selector de líneas del portal ya se llena desde la base, así que no requiere cambios en pantalla.

## 4. Le corresponde a TI de Motherson

- Migrar el portal y la base a su infraestructura (escenario C de la guía): servidor y base permitidos en `AzurePreflight.cs` y `AdminV2Migration.cs`, certificado TLS y HTTPS detrás de su proxy.
- Power BI: cambiar el origen del informe a su SQL Server (on-premises data gateway si es interno).
- **Respaldos** completos y de log de SQL Server según su política, y del certificado de protección de claves.
- Monitoreo básico: disponibilidad del portal (`/health/live`) y espacio de la base.
- **Quitar la marca "datos simulados" al pasar a datos reales.** No toca la base ni el código: en `portal/backend/Motherson.Api/appsettings.json` cambiar `"DatosSimulados": true` a `false` (o definir la variable de entorno `DatosSimulados=false`) y reiniciar el portal. Con eso desaparecen la etiqueta amarilla "SIMULADO", el aviso del inicio de sesión y la marca de la API, y el pie del menú dice "Datos de planta". El aviso de *horario de prueba* es independiente: se quita desmarcando esa casilla en *Configurar línea → Horario de trabajo*.

## 5. Cómo verificar

```powershell
# Pruebas del portal (frontend)
cd portal/frontend; npm test

# Pruebas de integración con una base local nueva y portal en http://127.0.0.1:5099
python portal/scripts/probar_administracion_local.py --servir

# Prueba de carga: 5 materialistas a la vez durante 15 minutos (usar una línea o base de pruebas)
python portal/scripts/simular_operacion.py --url http://127.0.0.1:5099 --usuario persona0 --linea 1 --sesiones 5 --ubicaciones 20 --intervalo 20 --minutos 15 --crear-horario-prueba --si --password-archivo portal/.local/v2/user.password
```

El simulador imprime al final cuántas operaciones se registraron, cuántas tuvieron choque y se resolvieron con reintento, los fallos y el tiempo de respuesta (mediana y p95).

## 6. No probado todavía

- Uso real en planta: red, tabletas y número real de materialistas por línea.
- Volumen de meses de operación (las pruebas usan datos controlados y el histórico simulado).
