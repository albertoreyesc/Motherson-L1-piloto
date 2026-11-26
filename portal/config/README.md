# Configuración

| Archivo | Alcance |
| --- | --- |
| `catalogo-documentado.json` | Metadatos documentados consumidos por preparación y catálogo |
| `calendario-historico.json` | Referencia del histórico; no confirma horarios actuales de planta |
| `calendario-piloto-l1-2026.json` | Referencia simulada del piloto; validar vigencia y autorización antes de operar |
| `piloto.env.example` | Variables públicas y marcadores para configurar el hosting piloto |
| `conexiones-compartida.ejemplo.json` | Ejemplo del entorno Azure original utilizado por herramientas anteriores; no es la conexión del piloto |

Los recursos JSON se copian al contenedor en la fase de compilación. Los secretos de cada entorno permanecen en `.local/`, archivos `.env` privados o la configuración protegida del proveedor. No cargar `piloto.env.example` sin sustituir los marcadores y revisar el [manual de instalación](../../documentacion/MANUAL_DE_INSTALACION.md).
