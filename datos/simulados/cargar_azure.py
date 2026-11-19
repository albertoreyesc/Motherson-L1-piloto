"""Carga en Azure SQL los datos simulados: un CSV por tabla del ERD (carpeta ./datos).

Los CSV ya tienen la forma exacta del ERD: mismas tablas, mismas columnas y en el mismo orden que
01_crear_tablas.sql (carpeta 1. BDD). El script solo los revisa y los inserta; no transforma nada.

Para solo revisar los datos, sin conectarse a nada:
    python cargar_azure.py --solo-validar

Para cargarlos (les pedirá la contraseña sin mostrarla):
    python cargar_azure.py --servidor SERVIDOR.database.windows.net --base NOMBRE_BASE --usuario USUARIO
"""
import argparse
import getpass
import os
import re
import sys

import pandas as pd

BASE = os.path.dirname(os.path.abspath(__file__))
DATOS = os.path.join(BASE, 'datos')
ORDEN = ['ESTACION', 'COLOR', 'PUESTO_LOGISTICO', 'TURNO', 'ESTANTERIA', 'MATERIAL', 'UBICACION', 'REGISTRO']
SIN_IDENTITY = {'MATERIAL'}


def buscar_arriba(*partes):
    """Busca la ruta subiendo carpeta por carpeta (y una carpeta abajo en cada nivel)."""
    carpeta = BASE
    while True:
        for base in [carpeta] + [os.path.join(carpeta, s) for s in sorted(os.listdir(carpeta))
                                 if os.path.isdir(os.path.join(carpeta, s))]:
            if os.path.exists(os.path.join(base, *partes)):
                return os.path.join(base, *partes)
        padre = os.path.dirname(carpeta)
        if padre == carpeta:
            sys.exit(f'No encontré {os.path.join(*partes)} en ninguna carpeta arriba de {BASE}')
        carpeta = padre


TABLAS_SQL = buscar_arriba('01_crear_tablas.sql')   # está en la carpeta 1. BDD


# ---------------------------------------------------------------------------
# 1. Estructura del ERD (leída de 01_crear_tablas.sql)
# ---------------------------------------------------------------------------
def estructura_erd():
    """{tabla: [(columna, tipo, acepta_nulos)]} en el orden del CREATE TABLE."""
    sql = open(TABLAS_SQL, encoding='utf-8').read()
    tablas = {}
    for nombre, cuerpo in re.findall(r'CREATE TABLE (\w+) \((.*?)\n\);', sql, flags=re.S):
        cols = []
        for linea in cuerpo.splitlines():
            m = re.match(r'\s+\[?(\w+)\]?\s+(INT|VARCHAR|NVARCHAR|TIME|DATETIME2)\b.*?\b(NOT NULL|NULL)\b', linea)
            if m:
                cols.append((m.group(1), m.group(2), m.group(3) == 'NULL'))
        tablas[nombre] = cols
    return tablas


ERD = estructura_erd()


# ---------------------------------------------------------------------------
# 2. Lectura de los CSV
# ---------------------------------------------------------------------------
def leer():
    tablas, errores = {}, []
    for nombre in ORDEN:
        ruta = os.path.join(DATOS, f'{nombre}.csv')
        if not os.path.exists(ruta):
            sys.exit(f'Falta {ruta}')
        df = pd.read_csv(ruta, dtype=str, keep_default_na=False, encoding='utf-8-sig')
        esperadas = [c for c, _, _ in ERD[nombre]]
        if list(df.columns) != esperadas:
            errores.append(f'{nombre}.csv: columnas {list(df.columns)} · el ERD espera {esperadas}')
            continue
        for col, tipo, nulos in ERD[nombre]:
            vacio = df[col] == ''
            if vacio.any() and not nulos:
                errores.append(f'{nombre}.{col}: {int(vacio.sum())} vacíos en una columna NOT NULL')
            if tipo == 'INT':
                df[col] = pd.to_numeric(df[col].where(~vacio), errors='raise').astype('Int64')
            elif tipo == 'DATETIME2':
                df[col] = pd.to_datetime(df[col].where(~vacio), format='%Y-%m-%d %H:%M:%S')
            else:
                df[col] = df[col].where(~vacio, None)
        tablas[nombre] = df
    if errores:
        print('\n'.join('  ERROR  ' + e for e in errores))
        sys.exit('La estructura de los CSV no coincide con el ERD; no se cargó nada.')
    return tablas


# ---------------------------------------------------------------------------
# 3. Validación
# ---------------------------------------------------------------------------
def validar(t):
    errores = []

    def exige(condicion, texto):
        print(('  OK     ' if condicion else '  ERROR  ') + texto)
        if not condicion:
            errores.append(texto)

    print('\nEstructura: los 8 CSV tienen las columnas del ERD en el mismo orden')
    print('\nLlaves y relaciones')
    for tabla in ORDEN:
        pk = ERD[tabla][0][0]
        exige(t[tabla][pk].is_unique and t[tabla][pk].notna().all(), f'{tabla}: llave {pk} única')
    for hija, col, padre in (('ESTANTERIA', 'id_estacion', 'ESTACION'), ('MATERIAL', 'id_puesto', 'PUESTO_LOGISTICO'),
                             ('MATERIAL', 'id_color', 'COLOR'), ('UBICACION', 'numero_parte', 'MATERIAL'),
                             ('UBICACION', 'id_estanteria', 'ESTANTERIA'), ('REGISTRO', 'id_puesto', 'PUESTO_LOGISTICO'),
                             ('REGISTRO', 'id_ubicacion', 'UBICACION'), ('REGISTRO', 'id_turno', 'TURNO')):
        pk = ERD[padre][0][0]
        exige(t[hija][col].dropna().isin(t[padre][pk]).all(), f'{hija}.{col} existe en {padre}')

    print('\nReglas')
    u, m, r = t['UBICACION'], t['MATERIAL'], t['REGISTRO']
    exige(not u.duplicated(['numero_parte', 'id_estanteria']).any(), 'UBICACION: parte + estantería sin repetir')
    exige(t['COLOR'].nombre.is_unique, 'COLOR: nombre sin repetir')
    exige((u.cantidad >= 0).all() and (m.stock >= 0).all() and (m.minimo >= 0).all() and (m.cantidad_por_caja > 0).all()
          and (t['ESTANTERIA'].cajas_estanteria >= 0).all(), 'Cantidades dentro de lo permitido')
    exige((r.fecha_hora_fin.isna() | (r.fecha_hora_fin >= r.fecha_hora_inicio)).all(), 'REGISTRO: fin no anterior a inicio')
    exige((m.set_index('numero_parte').stock == u.groupby('numero_parte').cantidad.sum().reindex(m.numero_parte).fillna(0).values).all(),
          'MATERIAL.stock = suma de UBICACION.cantidad')

    print('\nValores de referencia (datos simulados al 23/10/2026)')
    iso = r.fecha_hora_fin.dt.isocalendar()
    s43 = int(((iso.year == 2026) & (iso.week == 43)).sum())
    exige(len(u) == 85, f'Ubicaciones: {len(u)} (esperado 85)')
    exige(int(u.cantidad.sum()) == 10474, f'Piezas: {int(u.cantidad.sum()):,} (esperado 10,474)')
    exige(len(r) == 58422, f'Registros: {len(r):,} (esperado 58,422)')
    exige(s43 == 1146, f'Completados en la semana del 19 al 25/10/2026: {s43:,} (esperado 1,146)')

    print('\nFilas por tabla')
    for n in ORDEN:
        print(f'  {n:<18}{len(t[n]):>8,}')
    return errores


# ---------------------------------------------------------------------------
# 4. Carga
# ---------------------------------------------------------------------------
def a_python(v):
    if pd.isna(v):
        return None
    if isinstance(v, pd.Timestamp):
        return v.to_pydatetime()
    return v.item() if hasattr(v, 'item') else v


def insertar(cur, tabla, df):
    cols = list(df.columns)
    nombres = ','.join(f'[{c}]' for c in cols)
    filas = [tuple(a_python(v) for v in f) for f in df.itertuples(index=False, name=None)]
    lote = min(1000, 2000 // len(cols))
    marca = '(' + ','.join('?' * len(cols)) + ')'
    if tabla not in SIN_IDENTITY:
        cur.execute(f'SET IDENTITY_INSERT dbo.{tabla} ON')
    for i in range(0, len(filas), lote):
        grupo = filas[i:i + lote]
        cur.execute(f"INSERT INTO dbo.{tabla} ({nombres}) VALUES {','.join([marca] * len(grupo))}", [v for f in grupo for v in f])
    if tabla not in SIN_IDENTITY:
        cur.execute(f'SET IDENTITY_INSERT dbo.{tabla} OFF')
    print(f'  {tabla:<18}{len(filas):>8,} filas')


def cargar(t, args):
    import pyodbc
    clave = getpass.getpass('Contraseña de la base: ')
    con = pyodbc.connect(f'DRIVER={{ODBC Driver 18 for SQL Server}};SERVER=tcp:{args.servidor},1433;DATABASE={args.base};'
                         f'UID={args.usuario};PWD={clave};Encrypt=yes;TrustServerCertificate=no;Connection Timeout=120', autocommit=False)
    cur = con.cursor()
    if cur.execute("SELECT OBJECT_ID('dbo.ESTACION')").fetchval() is None:
        print('Creando las 8 tablas...')
        for bloque in re.split(r'^\s*GO\s*$', open(TABLAS_SQL, encoding='utf-8').read(), flags=re.M):
            if bloque.strip():
                cur.execute(bloque)
    con.commit()

    accesos = []
    existentes = {n: cur.execute(f'SELECT COUNT(*) FROM dbo.{n}').fetchval() for n in ORDEN}
    if any(existentes.values()):
        if not args.reiniciar:
            sys.exit(f'La base ya tiene datos: {existentes}. No se cargó nada.')
        if input(f'Se borrarán estas filas: {existentes}. Escriban BORRAR para continuar: ') != 'BORRAR':
            sys.exit('No se borró ni se cargó nada.')
        # Accesos del portal (usuario y hash por puesto): se guardan para no perderlos al recargar
        accesos = cur.execute('SELECT id_puesto, usuario, [password] FROM dbo.PUESTO_LOGISTICO WHERE usuario IS NOT NULL').fetchall()
        for n in reversed(ORDEN):
            cur.execute(f'DELETE FROM dbo.{n}')

    print('Cargando...')
    for n in ORDEN:
        insertar(cur, n, t[n])
    for id_puesto, usuario, hash_ in accesos:
        cur.execute('UPDATE dbo.PUESTO_LOGISTICO SET usuario = ?, [password] = ? WHERE id_puesto = ?', usuario, hash_, id_puesto)
    if accesos:
        print(f'  Accesos del portal conservados: {len(accesos)} puestos')
    malas = {n: (c, len(t[n])) for n in ORDEN if (c := cur.execute(f'SELECT COUNT(*) FROM dbo.{n}').fetchval()) != len(t[n])}
    if malas:
        con.rollback()
        sys.exit(f'Los conteos no coinciden; no se guardó nada: {malas}')
    con.commit()
    con.close()
    print('Carga terminada: los conteos de la base coinciden con los CSV.')


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument('--solo-validar', action='store_true')
    ap.add_argument('--servidor')
    ap.add_argument('--base')
    ap.add_argument('--usuario')
    ap.add_argument('--reiniciar', action='store_true', help='borra las filas actuales antes de cargar; pide confirmación')
    args = ap.parse_args()
    if not args.solo_validar and not (args.servidor and args.base and args.usuario):
        ap.error('indiquen --solo-validar, o bien --servidor, --base y --usuario')
    tablas = leer()
    errores = validar(tablas)
    if errores:
        sys.exit(f'\nHay {len(errores)} errores; no se cargó nada.')
    print('\nValidación correcta.')
    if not args.solo_validar:
        cargar(tablas, args)


if __name__ == '__main__':
    main()
