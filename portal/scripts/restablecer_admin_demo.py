"""Restablece el acceso local de admin.local en la demostración."""
import base64
from getpass import getpass
import hashlib
import json
import os
from pathlib import Path
import struct
import tempfile

ruta = Path(__file__).resolve().parents[1] / ".local" / "admin-puestos-Motherson_L1_MVP_Demo.json"
if not ruta.is_file() or json.loads(ruta.read_text()).get("usuario") != "admin.local":
    raise SystemExit("No se encontró la configuración local de admin.local para Demo.")

clave = getpass("Nueva contraseña de admin.local (mínimo 16 caracteres): ")
if len(clave) < 16 or clave != getpass("Repite la contraseña: "):
    raise SystemExit("La contraseña es demasiado corta o las dos entradas no coinciden.")

# ASP.NET Core Identity V3: marcador 1, HMAC-SHA512 (2), iteraciones y largo de salt.
salt = os.urandom(16)
iteraciones = 100_000
subclave = hashlib.pbkdf2_hmac("sha512", clave.encode("utf-8"), salt, iteraciones, dklen=32)
contenido = b"\x01" + struct.pack(">III", 2, iteraciones, len(salt)) + salt + subclave
with tempfile.NamedTemporaryFile(mode="w", dir=ruta.parent, delete=False) as archivo:
    temporal = Path(archivo.name)
    json.dump({"usuario": "admin.local", "hash": base64.b64encode(contenido).decode()}, archivo)
try:
    temporal.replace(ruta)
finally:
    temporal.unlink(missing_ok=True)
print("Acceso actualizado. Reinicia el portal 5089 con la base Demo.")
