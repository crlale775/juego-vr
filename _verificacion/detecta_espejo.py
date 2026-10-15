"""
Detecta si el titulo del menu ("VR BASKETBALL CHALLENGE") esta espejado.

Idea: el titulo tiene tres palabras de longitudes 2, 9 y 9. Su "perfil de
tinta" por columnas NO es simetrico (la palabra corta esta en un extremo).
- Si la palabra CORTA (VR) cae a la IZQUIERDA  -> el texto se lee DERECHO.
- Si la palabra CORTA cae a la DERECHA         -> el texto esta ESPEJADO.

Se localiza el titulo por su color naranja, se mide la tinta por columna y se
separan las palabras por los huecos grandes.
"""

import sys
import glob
import os
from PIL import Image


def es_naranja(r, g, b):
    return r > 150 and g > 80 and b < 170 and r > b + 60 and g > b + 20


def analiza(ruta):
    try:
        img = Image.open(ruta).convert("RGB")
    except Exception as e:
        return None

    px = img.load()
    w, h = img.size

    xs, ys = [], []
    for y in range(h):
        for x in range(w):
            r, g, b = px[x, y]
            if es_naranja(r, g, b):
                xs.append(x)
                ys.append(y)

    if len(xs) < 120:
        return None

    x0, x1 = min(xs), max(xs)
    y0, y1 = min(ys), max(ys)

    # Solo la banda del titulo (arriba de la caja naranja).
    banda_y0 = y0
    banda_y1 = y0 + max(4, (y1 - y0) // 3)

    perfil = []
    for x in range(x0, x1 + 1):
        n = 0
        for y in range(banda_y0, banda_y1 + 1):
            r, g, b = px[x, y]
            if es_naranja(r, g, b):
                n += 1
        perfil.append(n)

    # Palabras = bloques de columnas con tinta separados por huecos.
    umbral = max(1, max(perfil) // 12)
    palabras = []
    inicio = None
    hueco = 0
    for i, v in enumerate(perfil):
        if v > umbral:
            if inicio is None:
                inicio = i
            hueco = 0
        else:
            if inicio is not None:
                hueco += 1
                if hueco > 6:
                    palabras.append((inicio, i - hueco))
                    inicio = None
                    hueco = 0
    if inicio is not None:
        palabras.append((inicio, len(perfil) - 1))

    anchos = [b - a + 1 for a, b in palabras]

    return {
        "archivo": os.path.basename(ruta),
        "tam": (w, h),
        "caja": (x0, y0, x1, y1),
        "palabras": len(anchos),
        "anchos": anchos,
        "perfil": perfil,
    }


def veredicto(res):
    if res is None:
        return "sin titulo naranja"
    n = len(res["anchos"])
    if n < 2:
        return "no se separaron palabras"

    ancho = res["perfil"].__len__()
    primera = res["anchos"][0]
    ultima = res["anchos"][-1]
    media = sum(res["anchos"]) / float(n)

    if primera < media * 0.7 and ultima >= media * 0.7:
        return "DERECHO (palabra corta a la izquierda)"
    if ultima < media * 0.7 and primera >= media * 0.7:
        return "ESPEJADO (palabra corta a la derecha)"
    return "indeterminado"


def main():
    patrones = sys.argv[1:] or ["_verificacion/*.png"]
    archivos = []
    for p in patrones:
        archivos.extend(glob.glob(p))

    for ruta in sorted(set(archivos)):
        res = analiza(ruta)
        if res is None:
            continue
        print("%-40s %dx%d caja=%s palabras=%d anchos=%s -> %s" % (
            res["archivo"], res["tam"][0], res["tam"][1], res["caja"],
            res["palabras"], res["anchos"], veredicto(res)))


if __name__ == "__main__":
    main()
