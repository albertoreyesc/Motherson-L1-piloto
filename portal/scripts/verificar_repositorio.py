"""Comprueba enlaces Markdown y fuentes del repositorio, sin conectar a servicios o SQL.
Ejecutar desde cualquier directorio: python portal/scripts/verificar_repositorio.py
"""
from pathlib import Path
import hashlib
import json
import re
import subprocess
from urllib.parse import unquote

root = Path(__file__).resolve().parents[2]
tracked = subprocess.check_output(['git', 'ls-files', '-z'], cwd=root).decode().split('\0')
errors = []
links = 0
# Include new documentation before staging as well as existing tracked documents.
paths = {root / p for p in tracked if p.endswith('.md')}
paths.update((root / 'documentacion').rglob('*.md'))
for path in sorted(paths):
    if not path.is_file():
        continue
    text = re.sub(r'```[^\n]*\n.*?```', '', path.read_text(), flags=re.S)
    for match in re.finditer(r'!?\[[^\]\n]*\]\(([^\s)]+)\)', text):
        target = match[1]
        if target.startswith(('https:', 'http:', 'mailto:', '#', 'codex:')):
            continue
        links += 1
        local = unquote(target.split('#')[0])
        if not (path.parent / local).exists():
            errors.append(f'{path.relative_to(root)} -> {target}')
required = ['portal/frontend/package-lock.json', 'portal/frontend/vite.config.ts',
            'portal/backend/Motherson.Api/Motherson.Api.csproj',
            'despliegue/render/Dockerfile', 'despliegue/render/iniciar.sh',
            'portal/config', 'portal/db/administracion/001_administracion.sql',
            'portal/db/administracion/002_fuentes_powerbi.sql',
            'portal/db/administracion/003_usuarios.sql',
            'portal/db/administracion/004_rendimiento.sql',
            'powerbi/administracion-v2/definition',
            'powerbi/equipo2-2026-11-24/report/definition.pbir']
for name in required:
    if not (root / name).exists():
        errors.append(f'Ruta requerida ausente: {name}')
provenance = json.loads((root / 'powerbi/equipo2-2026-11-24/procedencia.json').read_text())
pbix = root / 'powerbi/equipo2-2026-11-24/Motherson.pbix'
pbix_ok = hashlib.sha256(pbix.read_bytes()).hexdigest() == provenance['pbixSHA256']
if not pbix_ok:
    errors.append('La entrega PBIX conservada cambió respecto a su procedencia')
print(json.dumps({'enlacesLocales': links, 'rutasRequeridas': len(required),
                  'pbixOriginalConservado': pbix_ok,
                  'errores': errors}, ensure_ascii=False, indent=2))
raise SystemExit(1 if errors else 0)
