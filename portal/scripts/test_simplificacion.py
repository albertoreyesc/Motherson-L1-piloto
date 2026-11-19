"""Permisos y conservación. Los movimientos SQL de prueba se revierten."""
import json
import concurrent.futures
import time
from pathlib import Path
from catalogo_sql import sql
from simplificar_modelo import estado, verificar
from test_integracion import Sesion

ROOT = Path(__file__).resolve().parents[1]
BASE = 'Motherson_L1_MVP_Pruebas'
resultados = []

def caso(nombre, fn):
    fn()
    resultados.append({'caso': nombre, 'resultado': 'APROBADO'})

def ejecutar_sql(texto):
    salida = sql(BASE, texto)
    assert 'CASO_OK' in salida, salida

def rechazo(texto, numero):
    ejecutar_sql(f"""BEGIN TRY BEGIN TRANSACTION;
      {texto}
      ROLLBACK; THROW 51999,'Se aceptó una operación inválida',1;
      END TRY BEGIN CATCH
      IF @@TRANCOUNT>0 ROLLBACK;
      IF ERROR_NUMBER()<>{numero} THROW;
      END CATCH; PRINT 'CASO_OK';""")

def main():
    verificar()
    antes = estado()
    acceso = json.loads((ROOT/'.local'/('acceso-inicial-'+BASE+'.json')).read_text())
    url = 'http://127.0.0.1:5088'
    admin = Sesion(url).login(acceso['usuario'], acceso['password'])
    operador = Sesion(url).login('entrada.prueba', acceso['password'])
    consulta = Sesion(url).login('consulta.prueba', acceso['password'])
    assert admin.call('/health')[1]['baseDatos'] == BASE
    rows = admin.call('/inventario')[1]
    propio = next(r for r in rows if r['numero_parte']=='LAB-MAT-001')
    ajeno = next(r for r in rows if r['id_puesto'] != propio['id_puesto'])

    def identidad():
        a = admin.call('/auth/me')[1]; o = operador.call('/auth/me')[1]
        assert a['name']=='admin.local' and a['puestoId'] is None
        assert o['name']=='entrada.prueba' and o['puestoId']==propio['id_puesto']
        assert o['permissions']==['view','entry'] and a['permissions']==['view','entry','exit','catalog']
    caso('Cuentas individuales; administrador sin asignación ficticia', identidad)

    def denegaciones():
        body = dict(ubicacion=ajeno['id_ubicacion'], cajas=1, version=ajeno['version_concurrencia'])
        assert operador.call('/central/entries', body)[0]==403
        assert consulta.call('/central/entries', body)[0]==403
        for s in (operador, consulta):
            assert s.call('/movimientos/salidas',dict(ubicacion=propio['id_ubicacion'],piezas=1,version=propio['version_concurrencia']))[0]==403
        assert Sesion(url).call('/inventario')[0]==401
    caso('API rechaza material ajeno, salidas operativas y captura de consulta', denegaciones)

    def publico():
        c = admin.call('/catalogo')[1]
        assert 'colores' not in c
        assert len([m for m in c['materiales'] if m['uso']])==62
        for m in c['materiales']: assert 'id_color' not in m
        for r in rows: assert 'color' not in r and 'id_color' not in r
        for path in ('/catalogo','/inventario','/inventario?alarmas=true','/historial?origen=MVP','/auth/me'):
            s,d = admin.call(path); assert s==200
            def revisar(v):
                if isinstance(v,dict):
                    for k,x in v.items():
                        assert k.lower() not in ('hash_contrasena','password','contraseña','contrasena','connectionstring')
                        revisar(x)
                elif isinstance(v,list):
                    for x in v: revisar(x)
            revisar(d)
        h = admin.call('/historial?origen=MVP')[1]
        assert all(r['usuario_captura']=='admin.local' and r['es_simulado'] for r in h['filas'])
    caso('API sin credenciales ni COLOR; 62 usos y autoría original', publico)

    encabezado = """DECLARE @u int=(SELECT id_ubicacion FROM dbo.UBICACION WHERE numero_parte='LAB-MAT-001'),
      @a int=(SELECT id_usuarios FROM dbo.USUARIOS WHERE nombre_usuario='admin.local'),
      @o int=(SELECT id_usuarios FROM dbo.USUARIOS WHERE nombre_usuario='entrada.prueba'),
      @v int,@f int=(SELECT cantidad_por_caja FROM dbo.MATERIAL WHERE numero_parte='LAB-MAT-001'),
      @op uniqueidentifier=NEWID(),@op2 uniqueidentifier=NEWID(),@h char(64)=REPLICATE('a',64),@n int;
      SELECT @v=version_concurrencia FROM dbo.UBICACION WHERE id_ubicacion=@u;"""
    def ciclo():
        ejecutar_sql("BEGIN TRANSACTION;"+encabezado+"""
          EXEC dbo.RegistrarMovimiento @a,@u,'SALIDA',@f,@v,@op,@h;
          SELECT @v=version_concurrencia FROM dbo.UBICACION WHERE id_ubicacion=@u;
          EXEC dbo.RegistrarMovimiento @o,@u,'ENTRADA',1,@v,@op2,@h;
          SELECT @n=COUNT(*) FROM dbo.REGISTRO;
          EXEC dbo.RegistrarMovimiento @o,@u,'ENTRADA',1,@v,@op2,@h;
          IF (SELECT COUNT(*) FROM dbo.REGISTRO)<>@n THROW 51999,'Reintento duplicado',1;
          IF NOT EXISTS(SELECT 1 FROM dbo.REGISTRO WHERE id_operacion=@op2 AND id_usuarios=@o) THROW 51999,'Capturista incorrecto',1;
          ROLLBACK; PRINT 'CASO_OK';""")
    caso('SQL: salida administrativa, entrada del puesto y reintento idempotente, con ROLLBACK', ciclo)
    caso('SQL: versión desactualizada rechazada sin pérdida de saldo', lambda: rechazo(encabezado+"EXEC dbo.RegistrarMovimiento @a,@u,'SALIDA',1,@v,@op,@h; EXEC dbo.RegistrarMovimiento @a,@u,'SALIDA',1,@v,@op2,@h;",51009))
    caso('SQL: misma clave con otra huella rechazada', lambda: rechazo(encabezado+"EXEC dbo.RegistrarMovimiento @a,@u,'SALIDA',1,@v,@op,@h; SET @h=REPLICATE('b',64); EXEC dbo.RegistrarMovimiento @a,@u,'SALIDA',1,@v,@op,@h;",51009))
    caso('SQL: desactivación impide capturar sin borrar el puesto', lambda: rechazo(encabezado+"UPDATE dbo.USUARIOS SET esta_activo=0 WHERE id_usuarios=@o; EXEC dbo.RegistrarMovimiento @o,@u,'ENTRADA',1,@v,@op,@h;",51001))
    caso('SQL: operador sin asignación no puede capturar', lambda: rechazo(encabezado+"UPDATE dbo.USUARIOS SET id_puesto=NULL WHERE id_usuarios=@o; EXEC dbo.RegistrarMovimiento @o,@u,'ENTRADA',1,@v,@op,@h;",51003))
    def simultanea():
        # Otra sesión SQL conserva el mismo bloqueo de inventario. La API debe
        # rechazar el movimiento al agotar su espera, sin actualizar saldo.
        with concurrent.futures.ThreadPoolExecutor(max_workers=1) as pool:
            bloqueo=pool.submit(sql,BASE,"BEGIN TRAN; DECLARE @r int; EXEC @r=sys.sp_getapplock @Resource='MVP_INVENTARIO',@LockMode='Exclusive',@LockOwner='Transaction',@LockTimeout=1000; IF @r<0 THROW 51999,'No se pudo preparar el bloqueo de ensayo',1; WAITFOR DELAY '00:00:18'; ROLLBACK;")
            for _ in range(10):
                ocupado=sql(BASE,"SELECT COUNT(*) FROM sys.dm_tran_locks WHERE resource_database_id=DB_ID() AND resource_type='APPLICATION' AND request_mode='X' AND request_status='GRANT' AND resource_description LIKE '%MVP_INVENTARIO%'")
                if ocupado!='0': break
                time.sleep(.1)
            assert ocupado!='0','La sesión competidora no adquirió el bloqueo'
            s,d=admin.call('/movimientos/salidas',dict(ubicacion=propio['id_ubicacion'],piezas=1,version=propio['version_concurrencia']))
            assert s==409 and 'ocupado' in d['error'].lower(), (s,d)
            bloqueo.result(timeout=30)
    caso('Dos sesiones SQL: API rechaza inventario ocupado sin crear movimiento',simultanea)
    caso('Saldo, comprobantes, usuarios originales y archivo conservados', lambda: None if estado()==antes else (_ for _ in ()).throw(AssertionError('Cambio protegido')))
    out={'base':BASE,'casos':resultados,'registros_conservados':len(antes['registros']),'eventos_conservados':antes['eventos'],'movimientos_de_prueba':'Todos en transacciones revertidas; solo se consumen valores IDENTITY, sin reutilizarlos.'}
    (ROOT/'evidencias/simplificacion/pruebas-permisos.json').write_text(json.dumps(out,ensure_ascii=False,indent=2))
    print(json.dumps(out,ensure_ascii=False))

if __name__=='__main__': main()
