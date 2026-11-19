"""Verifica conciliación e invariantes del catálogo; no modifica SQL."""
import argparse
import collections
import json
from pathlib import Path

from auditar_alarmas import auditar
from catalogo_sql import BASES, datos, sql


def verificar(antes_path, protegido_path, plan_path):
    antes = json.loads(Path(antes_path).read_text())
    protegidos = {r['base']: r for r in json.loads(Path(protegido_path).read_text())}
    plan = json.loads(Path(plan_path).read_text())
    despues = {base: datos(base) for base in BASES}
    resultados = []
    for base in BASES:
        original, actual = antes[base], despues[base]
        original.setdefault('colores', [])
        for campo in ('inventario', 'corte', 'huella_registros', 'huella_eventos',
                      'colores_huerfanos', 'partes_duplicadas', 'puestos', 'colores'):
            assert original[campo] == actual[campo], f'{base}: cambió {campo}'
        assert len(original['materiales']) == len(actual['materiales'])
        cambios = {c['numero_parte']: c['uso'] for c in plan['cambios']}
        for previo, nuevo in zip(original['materiales'], actual['materiales']):
            assert {k: v for k, v in previo.items() if k != 'uso'} == {
                k: v for k, v in nuevo.items() if k != 'uso'
            }, f'{base}: cambió otro atributo de {previo["numero_parte"]}'
            assert nuevo['uso'] == cambios.get(nuevo['numero_parte'], previo['uso'])
        protegido = auditar(base)
        assert protegido == protegidos[base], f'{base}: cambió esquema, fuentes o movimientos'
        consulta_fk = """SELECT
            (SELECT COUNT(*) FROM sys.foreign_keys) total,
            (SELECT COUNT(*) FROM sys.foreign_keys WHERE is_disabled=1 OR is_not_trusted=1) deshabilitadas_o_no_confiables,
            (SELECT COUNT(*) FROM (SELECT nombre FROM dbo.COLOR GROUP BY nombre HAVING COUNT(*)>1)x) colores_duplicados
            FOR JSON PATH,WITHOUT_ARRAY_WRAPPER;"""
        if sql(base,"SELECT COUNT(*) FROM sys.tables WHERE object_id=OBJECT_ID('dbo.COLOR')")=='0':
            consulta_fk=consulta_fk.replace('(SELECT COUNT(*) FROM (SELECT nombre FROM dbo.COLOR GROUP BY nombre HAVING COUNT(*)>1)x)','0')
        fk = json.loads(sql(base, consulta_fk))
        assert fk['deshabilitadas_o_no_confiables'] == 0 and fk['colores_duplicados'] == 0
        restricciones = sql(base, 'DBCC CHECKCONSTRAINTS WITH ALL_CONSTRAINTS, NO_INFOMSGS;')
        assert not restricciones, f'{base}: restricciones incumplidas: {restricciones}'
        resultados.append({
            'base': base,
            'materiales': len(actual['materiales']),
            'usos_documentados_aplicados': len(cambios),
            'distribucion_usos': dict(collections.Counter(m['uso'] or 'PENDIENTE' for m in actual['materiales'])),
            'sin_uso': [m['numero_parte'] for m in actual['materiales'] if not m['uso']],
            'colores': len(actual['colores']),
            'materiales_sin_color': sum(m['id_color'] is None for m in actual['materiales']),
            'puestos_sin_tipo': sum(not p['tipo'] for p in actual['puestos']),
            'claves_foraneas': fk,
            'checkconstraints': 'Sin infracciones',
            'tablas_operativas': protegido['tablas_dbo'],
            'movimientos_mvp_conservados': protegido['registros'],
            'eventos_originales_conservados': protegido['eventos'],
            'invariantes': 'Esquema, relaciones, módulos SQL, fuentes archivadas, saldos y movimientos sin cambios',
        })
    return resultados, despues


if __name__ == '__main__':
    p = argparse.ArgumentParser()
    p.add_argument('--antes', required=True)
    p.add_argument('--protegido', required=True)
    p.add_argument('--plan', required=True)
    p.add_argument('--salida', required=True)
    p.add_argument('--instantanea', required=True)
    a = p.parse_args()
    resultado, instantanea = verificar(a.antes, a.protegido, a.plan)
    Path(a.salida).write_text(json.dumps(resultado, ensure_ascii=False, indent=2)+'\n')
    Path(a.instantanea).write_text(json.dumps(instantanea, ensure_ascii=False, indent=2)+'\n')
    print(json.dumps(resultado, ensure_ascii=False))
