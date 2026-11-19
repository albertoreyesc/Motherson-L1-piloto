"""Lanzador local. Las credenciales Docker se mantienen solo en memoria del proceso."""
import argparse,json,os,re,subprocess,sys
from pathlib import Path
ROOT=Path(__file__).resolve().parents[1]
def env_admin(db,container):
    info=json.loads(subprocess.check_output(['docker','inspect',container],text=True))[0]
    values=dict(x.split('=',1) for x in info['Config']['Env'] if '=' in x)
    ports=info['NetworkSettings']['Ports'].get('1433/tcp') or []
    if not ports:raise SystemExit('La instancia SQL local no está iniciada')
    port=ports[0]['HostPort'];pwd=values.get('MSSQL_SA_PASSWORD')
    if not pwd:raise SystemExit('No se encontró la configuración local SQL')
    env=os.environ.copy();env.update(MVP_ROOT=str(ROOT),MVP_DATABASE=db,MVP_ADMIN_SQL='Server=127.0.0.1,'+port+';Database=master;User Id=sa;Password="'+pwd.replace('"','""')+'";Encrypt=True;TrustServerCertificate=True',ASPNETCORE_ENVIRONMENT='Development')
    return env
def run_compartida(args):
    # Base compartida del Equipo 2 (Azure). La cadena vive solo en .local, que no se sube a GitHub.
    path=ROOT/'.local'/'conexiones-compartida.json'
    if not path.exists():raise SystemExit('Falta .local/conexiones-compartida.json. Copia config/conexiones-compartida.ejemplo.json y escribe la contraseña.')
    cadena=json.loads(path.read_text(encoding='utf-8'))['app']
    base=re.search(r'(?:Database|Initial Catalog)\s*=\s*([^;]+)',cadena,re.I)
    if not base or 'ESCRIBE_AQUI' in cadena:raise SystemExit('Revisa la cadena de conexión: falta la base o la contraseña.')
    admin=ROOT/'.local'/'admin-puestos-Motherson_L1.json'
    if not admin.exists():raise SystemExit('Falta la configuración privada de admin.local (admin-puestos-Motherson_L1.json).')
    env=os.environ.copy();env.update(MVP_ROOT=str(ROOT),MVP_DATABASE=base.group(1).strip(),MVP_BASE_COMPARTIDA=base.group(1).strip(),MVP_AZURE_SERVER="motherson-l1-equipo.database.windows.net",MVP_SQL_TRANSACCIONAL="true",ConnectionStrings__Sql=cadena,ASPNETCORE_URLS='http://127.0.0.1:'+str(args.puerto),ASPNETCORE_ENVIRONMENT='Development',MVP_ADMIN_PUESTOS_HASH=json.loads(admin.read_text(encoding='utf-8'))['hash'])
    runtime=ROOT/'.local/dotnet/dotnet'
    powerbi=ROOT/'.local/powerbi-privado.json'
    if powerbi.exists():
        for key,value in json.loads(powerbi.read_text(encoding='utf-8'))['PowerBI'].items():
            env['PowerBI__'+key]=str(value).lower() if isinstance(value,bool) else str(value)
    dll=ROOT/'backend/Motherson.Api/bin/Debug/net10.0/Motherson.Api.dll'
    if runtime.exists() and dll.exists():
        return subprocess.call([str(runtime),str(dll)],cwd=ROOT/'backend/Motherson.Api',env=env)
    return subprocess.call(['dotnet','run','--no-build','--project',str(ROOT/'backend/Motherson.Api'),'--no-launch-profile'],cwd=ROOT,env=env)
def run(args):
    if args.compartida:return run_compartida(args)
    if args.database not in ['Motherson_L1_MVP_Desarrollo','Motherson_L1_MVP_Pruebas','Motherson_L1_MVP_Demo']:raise SystemExit('Base no autorizada')
    if args.database == 'Motherson_L1_MVP_Demo' and not args.iniciar:
        raise SystemExit('Usa scripts/preparar_demo.py para preparar o verificar la demostración.')
    if args.iniciar:
        path=ROOT/'.local'/('conexiones-'+args.database+'.json')
        if not path.exists():raise SystemExit('Prepara la base primero con scripts/preparar_demo.py preparar' if args.database.endswith('_Demo') else 'Prepara la base primero con --migrar')
        env=os.environ.copy();env.update(MVP_ROOT=str(ROOT),MVP_DATABASE=args.database,ConnectionStrings__Sql=json.loads(path.read_text())['app'],ASPNETCORE_URLS='http://127.0.0.1:'+str(args.puerto),ASPNETCORE_ENVIRONMENT='Development')
        acceso_puestos=ROOT/'.local'/('admin-puestos-'+args.database+'.json')
        if acceso_puestos.exists():
            env['MVP_ADMIN_PUESTOS_HASH']=json.loads(acceso_puestos.read_text())['hash']
        cmd=['dotnet','run','--no-build','--project',str(ROOT/'backend/Motherson.Api'),'--no-launch-profile']
    else:
        env=env_admin(args.database,args.contenedor);cmd=['dotnet','run','--no-build','--project',str(ROOT/'backend/Motherson.Api'),'--no-launch-profile','--']
        if args.migrar:cmd+=['--migrar',str(Path(args.migrar).resolve())]
        elif args.integridad:cmd+=['--integridad']
        elif args.sembrar_pruebas:cmd+=['--sembrar-pruebas']
        elif args.probar_permisos:cmd+=['--probar-permisos']
        else:cmd+=['--verificar-sql']
    return subprocess.call(cmd, cwd=ROOT, env=env)
if __name__=='__main__':
    p=argparse.ArgumentParser();p.add_argument('--database',default='Motherson_L1_MVP_Desarrollo');p.add_argument('--contenedor',default='motherson-mvp-sql-1');p.add_argument('--migrar');p.add_argument('--iniciar',action='store_true');p.add_argument('--sembrar-pruebas',action='store_true');p.add_argument('--probar-permisos',action='store_true');p.add_argument('--integridad',action='store_true');p.add_argument('--puerto',type=int,default=5087);p.add_argument('--compartida',action='store_true',help='Usa la base compartida de .local/conexiones-compartida.json')
    try:
        sys.exit(run(p.parse_args()))
    except KeyboardInterrupt:
        sys.exit(130)
