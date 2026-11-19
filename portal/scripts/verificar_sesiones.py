"""Comprueba aislamiento de autenticación entre las dos bases sin modificar inventario."""
import json,urllib.request,urllib.error
from pathlib import Path
from test_integracion import Sesion
ROOT=Path(__file__).resolve().parents[1]
def sesion(sufijo,puerto):
 acceso=json.loads((ROOT/'.local'/('acceso-inicial-Motherson_L1_MVP_'+sufijo+'.json')).read_text())
 s=Sesion('http://127.0.0.1:'+str(puerto)).login(acceso['usuario'],acceso['password'])
 status,actor=s.call('/auth/me');assert status==200 and actor['permissions']
 cookie=next(c for c in s.cookies if c.name.endswith('.Session'))
 return s,cookie

def main():
 dev,a=sesion('Desarrollo',5087);pruebas,b=sesion('Pruebas',5088)
 assert a.name!=b.name
 for url,name,value in [(dev.url,a.name,b.value),(pruebas.url,b.name,a.value)]:
  request=urllib.request.Request(url+'/api/auth/me',headers={'Cookie':name+'='+value})
  try:
   urllib.request.urlopen(request,timeout=20)
   raise AssertionError('La sesión de otra base fue aceptada')
  except urllib.error.HTTPError as e:assert e.code==401,e.code
 report={'sesion_desarrollo':'APROBADO','sesion_pruebas':'APROBADO','cookie_pruebas_en_desarrollo':401,'cookie_desarrollo_en_pruebas':401,'sin_cambios_de_inventario':True}
 (ROOT/'evidencias/aislamiento-sesiones.json').write_text(json.dumps(report,indent=2));print(json.dumps(report))
if __name__=='__main__':main()
