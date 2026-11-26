# Colaborar en el piloto Motherson

Repositorio privado: https://github.com/albertoreyesc/Motherson-L1-piloto. Cada colaborador usa su cuenta GitHub autorizada; el enlace no concede acceso.

## Ramas y revisión

La rama predeterminada y conectada a Render es `piloto/preparacion-gratuita-2026-11-19`. Un cambio integrado puede publicar automáticamente. Crea una rama desde su estado actualizado:

```sh
git clone https://github.com/albertoreyesc/Motherson-L1-piloto.git
cd Motherson-L1-piloto
git switch -c trabajo/descripcion-del-cambio
```

Propón un pull request con el problema, resultado y validación. Revisa diferencias y comprobaciones antes de integrar. No uses force push sobre ramas compartidas ni mezcles cambios ajenos. Esta guía describe la colaboración del proyecto; no es una política corporativa de Motherson.

## Preparación y comprobaciones

Empieza por el [mapa del repositorio](README.md#mapa-del-repositorio) y el [manual de instalación](documentacion/MANUAL_DE_INSTALACION.md). Requiere Node.js 22 y .NET SDK 10 para compilar; las pruebas de integración requieren SQL local aislado y Docker/Python según el manual.

```sh
npm --prefix portal/frontend ci
npm --prefix portal/frontend test
npm --prefix portal/frontend run build
dotnet build portal/Motherson.slnx
dotnet run --project portal/pruebas-azure/Azure.Pruebas.csproj
```

Vite genera `portal/backend/Motherson.Api/wwwroot/`. No versionar esa salida ni `tsconfig.tsbuildinfo`. Sus originales y avisos de licencia se conservan en el frontend. Para publicar o ejecutar un clon nuevo, compila primero el frontend.

El workflow `piloto.yml` valida PR y tiene un trabajo Azure manual condicionado al environment `piloto`. Render tiene su propio despliegue desde la rama conectada; no se debe confundir abrir un PR con desplegar ni ejecutar el workflow con el despliegue Render.

Para cambios de SQL, permisos o movimientos, ejecuta además las pruebas de integración pertinentes siguiendo [PRUEBAS.md](documentacion/administracion-v2/PRUEBAS.md). Para cambios solo documentales, verifica enlaces y rutas. No conectar pruebas al piloto compartido o al servidor original.

## Datos y configuración

- Configura secretos fuera de Git. No adjuntar `.local/`, `.env`, certificados, sesiones, respaldos privados o contraseñas.
- Los CSV simulados están sellados por SHA-256; no modificar saltos de línea ni cargar sobre el piloto.
- Conserva las migraciones y sus rutas. Instalación inicial, actualización y recuperación son procedimientos diferentes.
- Si mueves archivos, actualiza consumidores, enlaces, scripts y empaquetado. Consulta el [inventario](documentacion/ORGANIZACION_REPOSITORIO.md).
- No publiques un PBIX original sobre el informe activo sin el procedimiento de integración y validación correspondiente.
- Los cambios de documentación no requieren tocar usuarios, permisos, inventario, Power BI o infraestructura.

## Criterios de entrega

Incluye el alcance, las pruebas ejecutadas y las limitaciones reales. Mantén los manuales actuales separados de los antecedentes y evita introducir rutas personales como instrucciones de instalación. No inventes responsables, CODEOWNERS o licencias de distribución sin decisión del propietario.
