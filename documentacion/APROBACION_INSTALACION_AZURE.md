# Conjunto concreto pendiente de aprobación

Destino exclusivo: motherson-l1-equipo.database.windows.net / Motherson_L1.
Los datos seleccionados por el usuario son SIMULADOS DE DEMOSTRACIÓN.
Este documento no constituye autorización ni registra ejecución.

## Aplicación propuesta

1. Repetir diagnóstico de lectura y comprobar que sigue sin tablas de usuario.
   Si el estado cambió, detener instalación y conciliar; no borrar objetos.
2. Ejecutar una sola variante del instalador versionado. Conservar ocho tablas
   operativas dbo, añadir siete tablas mes y crear las vistas, procedimientos,
   función e índices del paquete. El instalador crea todo en una transacción.
3. Crear usuarios contenidos portal_app y powerbi_lector con claves privadas ya
   generadas localmente. Aplicar 020_permisos.sql: roles mes_portal/mes_lectura,
   ejecución y lectura acotadas, sin escritura directa del backend y sin acceso
   BI a hashes. Denegar al backend el procedimiento antiguo de movimientos.
4. Cargar los ocho CSV verificados y fijar apertura en una sola transacción:
   27 estaciones, 15 colores, 6 puestos, 2 turnos, 85 estanterías, 85 materiales,
   85 ubicaciones, 58,422 atenciones y 10,474 piezas, marcados como simulación.
5. Conectar el backend local con portal_app y ejecutar la verificación del esquema,
   permisos y conciliación. Comprobar una captura y su repetición sin duplicar,
   así como reinicio del backend; estas capturas serán pruebas de demostración
   y quedarán identificadas en su historial. Nunca operar habitualmente como dbo.

Las claves privadas permanecen fuera de Git y de los ZIP públicos. El acceso
administrativo se usa únicamente para instalación/carga, no como cuenta del portal.
Las ocho tablas y las siete tablas mes suman quince tablas de usuario.

## Trabajo que seguirá pendiente

La carga histórica completa queda pendiente de completar su ensayo de volumen
con espacio suficiente. El respaldo y el migrador ya están preparados; no se
reaplicarán movimientos antiguos sobre el nuevo inventario. La aprobación de este
conjunto no incluye esa carga histórica pendiente.

No incluye crear App Service/almacenamiento, cambiar firewall o nivel de servicio,
publicar Power BI, incrustar informes, subir a GitHub ni modificar main.
La publicación requiere definir cuenta y espacio propios. La conciliación del
PBIX actualizado sigue pendiente; solo se probaron sus fuentes SQL localmente.

## Recuperación

El instalador y la carga de demostración revierten su transacción ante error.
Usuarios/permisos son una fase separada y su resultado se inspecciona antes de
reintentar. No se entrega un borrado automático para corregir datos ya confirmados.
Si falla la verificación final, detener el backend y conservar la evidencia;
conciliar el estado antes de volver a ejecutar cualquier fase.

Pregunta de aprobación: ¿Autorizas aplicar este conjunto en Motherson_L1,
incluyendo estructura, cuentas/permisos, datos simulados y verificaciones descritas?
