# Publicación del 22/11/2026

## Estado verificado

- Repositorio privado actualizado: https://github.com/albertoreyesc/Motherson-L1-piloto
- Rama desplegada: `piloto/preparacion-gratuita-2026-11-19`.
- Código publicado: `77516493cb3abd7d6dca45ff41d83c6c3de12de0`.
- Rama de trabajo conservada: `mejoras/administracion-cuatro-lineas-2026-11-22`.
- Render confirmó **Deploy succeeded | Live** para `dep-dc1l7qrbc2fs73egi3n0`.
- URL HTTPS: https://motherson-l1-piloto.onrender.com/
- Verificados por HTTP: servicio saludable, API protegida, inicio de sesión de la cuenta de planta y consulta del catálogo anterior.

## Migración pendiente: acceso administrativo de Azure

La publicación del código no activa por sí sola la ampliación SQL. `MVP_ADMIN_V2` sigue desactivada y el portal mantiene el flujo anterior mientras se completa la migración.

El usuario aprobó migrar y publicar. Al intentar la operación, Microsoft rechazó la sesión administrativa con **AADSTS530035 / BlockedBySecurityDefaults**, tanto en Azure CLI como en Azure Portal. El acceso SQL directo del operador también está restringido por el firewall (40615). No se alteró la configuración de seguridad para evitar esos controles.

Por lo tanto, **todavía no se han instalado ni activado las migraciones en la base publicada**. No se cambiaron saldos, cuentas ni reglas de firewall en esta actualización. La recuperación del acceso administrativo es el requisito pendiente para continuar la guía de actualización.

Una vez recuperado el acceso: comprobar el destino piloto, autorizar únicamente la IP temporal exacta del operador, registrar el estado vigente, instalar el esquema, conciliar y activar, habilitar `MVP_ADMIN_V2=true` en Render y verificar las cuatro líneas. Retirar después el acceso temporal del operador. No utilizar inicializadores de demostración.

## Colaboración

El mapa de carpetas e instrucciones para el compañero está en [CONTRIBUTING.md](../../CONTRIBUTING.md). El repositorio continúa privado: cada colaborador debe entrar con su propia cuenta GitHub y tener una invitación aceptada. No se publicaron secretos ni credenciales.
