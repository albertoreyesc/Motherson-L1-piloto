"""Exporta solo el perfil SQL lector; nunca imprime contraseñas."""
import argparse,json,os
from pathlib import Path
ROOT=Path(__file__).resolve().parents[1]
def exportar(database):
    if database not in ('Motherson_L1_MVP_Desarrollo','Motherson_L1_MVP_Pruebas'):
        raise ValueError('Base no autorizada')
    origen=ROOT/'.local'/('conexiones-'+database+'.json')
    config=json.loads(origen.read_text())
    destino=ROOT/'.local'/('acceso-equipo2-'+database.removeprefix('Motherson_L1_MVP_')+'.json')
    with open(destino,'w',opener=lambda path,flags:os.open(path,flags,0o600)) as f:
        json.dump({'base_datos':database,'conexion_solo_lectura':config['lectura'],'nota':'Acceso local privado. No incluye credenciales de aplicación.'},f,ensure_ascii=False,indent=2)
    destino.chmod(0o600)
    print('Perfil lector guardado en '+str(destino)+'. La contraseña no se imprime.')
    return destino
if __name__=='__main__':
    parser=argparse.ArgumentParser();parser.add_argument('--database',default='Motherson_L1_MVP_Desarrollo');args=parser.parse_args();exportar(args.database)
