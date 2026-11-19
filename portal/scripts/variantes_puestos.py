"""Cruce de variantes con trazabilidad por fila. El libro se abre en modo lectura."""
from collections import Counter, defaultdict
import hashlib
import re

from openpyxl import load_workbook


REFERENCIA = re.compile(r"[A-Z0-9]{3} \d{3} \d{3}(?: [A-Z0-9]+)*")
REVISION = re.compile(r"[A-Z]{1,2}")


def normalizar(texto):
    return " ".join(texto.split()) if isinstance(texto, str) else ""


def cruzar(libro, materiales, puestos):
    destino = defaultdict(list)
    for material in materiales:
        destino[normalizar(material["numero_parte"])].append(material)
    puestos = {normalizar(puesto["nombre"]): puesto["id_puesto"] for puesto in puestos}
    registros = []
    asignaciones = {}
    encontradas = set()
    archivo = load_workbook(libro, read_only=True, data_only=True)
    try:
        hoja = archivo["Partes"]
        if normalizar(hoja["B2"].value) != "PUESTO LOGÍSTICO + MATERIAL" or normalizar(hoja["C2"].value) != "PARTE":
            raise ValueError("No se encontraron los encabezados esperados en Partes!B2:C2.")
        previo = None
        for fila, (original, contraste) in enumerate(
            hoja.iter_rows(min_row=3, min_col=2, max_col=3, values_only=True), 3
        ):
            parte = normalizar(original)
            parte_contraste = normalizar(contraste)
            contexto = previo[1] if previo and previo[0] == parte else None
            previo = None
            if original is None:
                continue
            registro = dict(hoja=hoja.title, fila=fila, texto_original=original,
                            parte=parte, puesto_contexto=contexto, codigo=None)
            registros.append(registro)
            if parte in puestos:
                registro.update(estado="PUESTO", puesto_contexto=parte)
                previo = (parte_contraste, parte)
                continue
            if not REFERENCIA.fullmatch(parte):
                registro["estado"] = "FORMATO_NO_VALIDADO"
                continue
            candidatos = destino[parte]
            encontradas.update(m["numero_parte"] for m in candidatos)
            tokens = parte.split()
            codigo = tokens[3] if len(tokens) == 4 and REVISION.fullmatch(tokens[3]) else None
            registro["codigo"] = codigo
            if not candidatos:
                registro["estado"] = "FUERA_DEL_MVP"
                continue
            if len(candidatos) != 1 or (parte_contraste and parte_contraste != parte):
                registro["estado"] = "CRUCE_AMBIGUO"
                continue
            material = candidatos[0]
            if contexto and puestos[contexto] != material["id_puesto"]:
                registro["estado"] = "PUESTO_DIFIERE"
                continue
            if codigo is None:
                registro["estado"] = "SIN_SUFIJO" if len(tokens) == 3 else "SUFIJO_AMBIGUO"
                continue
            registro["estado"] = "CONFIRMADO"
            asignaciones[material["numero_parte"]] = codigo
    finally:
        archivo.close()

    # Una fila contradictoria invalida la asignación de toda la referencia.
    dudas = {r["parte"] for r in registros if r["estado"] in ("CRUCE_AMBIGUO", "PUESTO_DIFIERE")}
    asignaciones = {p: c for p, c in asignaciones.items() if normalizar(p) not in dudas}
    for registro in registros:
        if registro["estado"] == "CONFIRMADO" and registro["parte"] in dudas:
            registro["estado"] = "CONFLICTO_EN_OTRA_FILA"
    return {
        "archivo": libro.name, "sha256": hashlib.sha256(libro.read_bytes()).hexdigest(),
        "totales_filas": dict(Counter(r["estado"] for r in registros)),
        "asignaciones": dict(sorted(asignaciones.items())),
        "materiales_sin_coincidencia": sorted(set(m["numero_parte"] for m in materiales) - encontradas),
        "filas": registros,
    }
