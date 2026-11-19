"""Ejecuta pruebas contra la última base aislada creada por probar_instalacion_sql_local.py."""
import json,os,subprocess,sys,shutil,time
from pathlib import Path
root=Path(__file__).resolve().parents[1]
config=json.loads((root/'.local/ultima-prueba-sql.json').read_text())
assert config['database'].startswith('MES_Integracion_Pruebas_')
instance=json.loads(subprocess.check_output(['docker','inspect',config['container']]))[0]
ports=instance['NetworkSettings']['Ports']['1433/tcp'];assert ports[0]['HostIp']=='127.0.0.1'
values=dict(v.split('=',1) for v in instance['Config']['Env'] if '=' in v)
password=values['MSSQL_SA_PASSWORD'].replace('"','""')
env=os.environ.copy();env['MES_TEST_CONNECTION']=f'Server=127.0.0.1,{ports[0]["HostPort"]};Database={config["database"]};User ID=sa;Password="{password}";Encrypt=True;TrustServerCertificate=True'
runtime=root/'.local/dotnet/dotnet';assert runtime.is_file(),'Prepara el runtime .NET 10 aislado.'
command=[str(runtime),str(root/'pruebas-sql/bin/Debug/net10.0/Sql.Pruebas.dll'),str(root),*sys.argv[1:]]
if '--legado' in sys.argv:
    process=subprocess.Popen(command,env=env,stdout=subprocess.PIPE,stderr=subprocess.PIPE,text=True)
    while process.poll() is None:
        if shutil.disk_usage(root).free < 3*1024**3:
            process.terminate();process.communicate(timeout=30)
            print('Prueba de legado detenida por espacio local. Los lotes se pueden reanudar; no se modificó Azure.')
            sys.exit(3)
        time.sleep(2)
    stdout,stderr=process.communicate();result=subprocess.CompletedProcess(command,process.returncode,stdout,stderr)
else:
    result=subprocess.run(command,env=env,capture_output=True,text=True)
if result.stdout:print(result.stdout)
if result.stderr:print(result.stderr)
if result.returncode==0:
 (root/'.local'/('resultado-sql-demo.json' if '--demo' in sys.argv else 'resultado-sql-legado.json' if '--legado' in sys.argv else 'resultado-sql-consultas.json' if '--consultas' in sys.argv else 'resultado-sql.json')).write_text(result.stdout)
sys.exit(result.returncode)
