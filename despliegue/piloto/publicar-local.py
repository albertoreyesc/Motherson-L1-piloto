"""Compila y crea un ZIP desplegable sin secretos. No publica ni crea recursos."""
from pathlib import Path
import subprocess,zipfile,shutil
root=Path(__file__).resolve().parents[2]
out=root/'portal/.local/piloto-build';out.mkdir(parents=True,exist_ok=True)
for cmd,cwd in [(['npm','ci'],root/'portal/frontend'),(['npm','test'],root/'portal/frontend'),(['npm','run','build'],root/'portal/frontend'),(['dotnet','publish','portal/backend/Motherson.Api/Motherson.Api.csproj','-c','Release','-o',str(out/'app')],root)]:
 subprocess.run(cmd,cwd=cwd,check=True)
shutil.copytree(root/'portal/config',out/'app/config',dirs_exist_ok=True)
# Dotnet publish usa una lista positiva de archivos; impedir cualquier estado local accidental.
with zipfile.ZipFile(out/'piloto.zip','w',zipfile.ZIP_DEFLATED) as z:
 for p in (out/'app').rglob('*'):
  if p.is_file():
   rel=p.relative_to(out/'app')
   if '.local' in rel.parts or p.name.startswith('.env'):raise SystemExit('Archivo privado en el artefacto; publicación detenida.')
   z.write(p,rel.as_posix())
print('Artefacto preparado: portal/.local/piloto-build/piloto.zip')
