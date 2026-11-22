# Colaborar en el piloto Motherson

Repositorio privado de trabajo: https://github.com/albertoreyesc/Motherson-L1-piloto

La rama conectada a Render es `piloto/preparacion-gratuita-2026-11-19`. Los cambios de código en esa rama pueden publicar automáticamente. Trabaja en una rama propia y presenta un pull request antes de integrar.

## Carpetas

| Carpeta | Contenido |
| --- | --- |
| `portal/frontend/` | Interfaz React/Vite, estilos y pruebas frontend |
| `portal/backend/Motherson.Api/` | API ASP.NET, autenticación, reglas y persistencia |
| `portal/db/administracion/` | Migración incremental de administración de cuatro líneas |
| `portal/pruebas-administracion/` | Pruebas de integración SQL de esa ampliación |
| `portal/scripts/` | Herramientas de prueba y trabajo local |
| `despliegue/render/` | Docker y configuración pública del hosting vigente |
| `despliegue/piloto/` | Aprovisionamiento del SQL piloto; no ejecutar inicializadores sobre una base existente |
| `documentacion/administracion-v2/` | Manual vigente, validación y procedimiento de recuperación |
| `documentacion/evidencia-integracion/administracion-v2/` | Resultados y capturas con datos controlados locales |
| `datos/`, `powerbi/` | Referencias de datos e integración del informe; revisar sus README |

Las notas de fases anteriores se conservan como antecedentes. Para los cambios nuevos, empieza por `documentacion/administracion-v2/`.

## Inicio del trabajo

```sh
git clone https://github.com/albertoreyesc/Motherson-L1-piloto.git
cd Motherson-L1-piloto
git switch -c trabajo/descripcion-del-cambio
```

Requisitos: Node y .NET según Dockerfile/proyectos, y SQL Server local aislado para pruebas. Ejecuta `npm ci`, `npm test` y `npm run build` en `portal/frontend`. Las instrucciones de integración SQL están en [PRUEBAS.md](documentacion/administracion-v2/PRUEBAS.md).

No se incluyen credenciales, certificados, configuraciones `.local`, archivos `.env` ni contraseñas de usuarios. Cada colaborador prepara su entorno privado. No conectar una instancia de desarrollo al servidor original ni ejecutar cargas demo sobre el piloto compartido. Las migraciones publicadas se aplican de forma administrativa y con conciliación; un push no debe inicializar bases.

Para compartir el trabajo, invita la cuenta GitHub del compañero al repositorio privado con el permiso que corresponda. El enlace por sí solo no concede acceso. No distribuir la cuenta administradora del portal como credencial de desarrollo.
