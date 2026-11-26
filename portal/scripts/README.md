# Herramientas del repositorio

Las rutas se conservan por sus consumidores y documentación. Son utilidades administrativas, no acciones de arranque automático. Revisar destino, credenciales privadas y manual antes de ejecutar.

| Grupo | Herramientas | Alcance |
| --- | --- | --- |
| Organización y documentación | `verificar_repositorio.py` | Enlaces locales, rutas requeridas y huella del PBIX; no conecta a servicios ni modifica datos |
| Administración v2/v3 local | `probar_administracion_local.py`, `probar_http_administracion.py` | Base NUEVA local aislada; la primera crea datos de prueba y opcionalmente sirve en 5099 |
| Instalación/transacciones | `probar_instalacion_sql_local.py`, `ejecutar_pruebas_sql.py` | Comprobaciones SQL locales; requieren infraestructura y secretos propios |
| Preparación/verificación Azure | `preparar_instalador_azure.py`, `verificar_azure_http.py`, `verificar_continuacion.py` | Leer controles de destino y perfil antes de usar; no son un despliegue Render |
| Catálogo y fuentes | `preparar_catalogo.py`, `catalogo_sql.py`, `auditar_catalogo.py`, `actualizar_catalogo.py`, `variantes_puestos.py` | Referencias de catálogos y tareas administrativas de etapas anteriores |
| Desarrollo e histórico anterior | `local.py`, `start.sh`, `preparar_demo.py`, `preparar_historico.py`, `simular_operacion.py`, `restablecer_admin_demo.py` | Entornos 5087/5089 y diario local; no sustituir el procedimiento v3 con estos arranques |
| Migración de modelos anteriores | `migrar_puestos.py`, `restaurar_puestos.py`, `simplificar_modelo.py` | Potencial escritura: conservar por trazabilidad y consumidores; no ejecutar como limpieza |
| Auditoría y evidencia histórica | `auditar_alarmas.py`, `test_*.py`, `verificar_persistencia.py`, `verificar_sesiones.py`, `diagramar_simplificacion.py`, `exportar_acceso_lectura.py` | Algunas rutas dependen de entornos originales; revisar individualmente antes de ejecutar |

[Instalación vigente](../../documentacion/MANUAL_DE_INSTALACION.md) · [Pruebas](../../documentacion/administracion-v2/PRUEBAS.md). Las herramientas cuya utilidad completa depende de datos privados no disponibles se conservan; no se presume que estén obsoletas por su fecha.
