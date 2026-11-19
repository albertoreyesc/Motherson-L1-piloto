# Acceso al piloto Motherson L1

Abre **https://motherson-l1-piloto.onrender.com/** desde una PC, tableta o teléfono con navegador actualizado. El portal funciona sin la computadora del desarrollador.

1. Entra con tu usuario y contraseña personal, entregados por el responsable del piloto. No compartas cuentas.
2. Comprueba el indicador **SIMULADO · L1**. Este entorno se usa solo con datos de prueba; no captura inventario real de planta.
3. En Inventario consulta el material, estación y ubicación. Usa **Actualizar** para volver a consultar los datos compartidos.
4. Si tu rol permite capturas, entra en **Entradas y salidas**, elige el material/ubicación y confirma la cantidad de cajas. Revisa el resultado antes de continuar. Una atención de inicio/fin es distinta de una entrada o salida de material.
5. Si una operación queda pendiente, utiliza la opción de comprobar la misma operación. No crees otra captura para intentar repetirla. Si aparece un conflicto, actualiza el inventario y revisa lo ocurrido.
6. Los ajustes de catálogo, capacidad e imágenes corresponden a administración. El acceso a historial y KPIs depende del rol.
7. El Dashboard de Power BI muestra un aviso mientras no exista capacidad verificada; inventario, capturas e indicadores propios pueden probarse.
8. Pulsa **Cerrar sesión** al terminar, especialmente en dispositivos compartidos. La sesión dura 30 minutos.

El alojamiento es gratuito y puede tardar en despertar tras un periodo sin uso. Si se agota una cuota, puede quedar suspendido. No está acreditado para 80 usuarios simultáneos ni para operar una jornada real de producción. Comenzar con un grupo pequeño y avisar al responsable si aparecen errores o lentitud.

Para una incidencia indica sección, hora aproximada, material/ubicación y mensaje mostrado. No compartas contraseñas, cookies, tokens ni fotografías con información de planta.

Las altas, roles y revocaciones las gestiona el responsable del piloto. La lista de participantes reales está pendiente; las cuentas usadas para verificar permisos ya fueron desactivadas.


## Dashboard original de Power BI

Se conserva el informe original con las páginas Crítico por Surtir, Desempeño semanal y Diagnóstico. Power BI solicita una sesión Microsoft individual con permiso para ese informe y licencia admitida. La cuenta del portal no concede acceso a Power BI. Si el navegador bloquea la autenticación dentro del cuadro, usar «Abrir en otra pestaña». No compartir la cuenta de demostración. En un dispositivo compartido, cerrar tanto el portal como Microsoft.

El informe muestra la base original de demostración, con actualización manual del modelo. Las capturas del piloto están en una base separada y no aparecerán en el informe hasta preparar y conciliar una copia del modelo para el piloto. «Recargar informe» solo vuelve a mostrar el modelo disponible.


## Responsable de administración del piloto

Se creó la cuenta del portal `admin.planta`, con nombre visible Administración Motherson L1 y rol Administrador. Su inicio de sesión, nombre, permisos y cierre de sesión se comprobaron por HTTPS. Las operaciones realizadas con esa cuenta se atribuyen a ese usuario; asignarla a un responsable y crear cuentas individuales para otras personas.

También se creó una cuenta Microsoft estándar de Administración Motherson L1 en el directorio de pruebas existente, con permiso Viewer en el espacio del informe. Solo tiene Power BI Free, que no acredita acceso al informe compartido en el espacio actual. Primer inicio de sesión, cambio personal de contraseña inicial, configuración de seguridad y licencia admitida están pendientes. No se contrató Pro/PPU ni se activó una evaluación nueva. El directorio de pruebas no es el directorio corporativo de Motherson.

El responsable conserva ambos accesos en un archivo privado fuera del repositorio. Las claves no se incluyen en esta guía ni en los ZIP de código. Evidencia sin contraseñas: `documentacion/evidencia-integracion/PILOTO_CUENTA_PLANTA.json`.
