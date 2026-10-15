"""
Comprueba que el texto del panel del menu ya NO esta espejado.

El propio juego informa en el diagnostico donde cae el panel en pantalla:
    [DIAG] panelpix 'Menu' x=500..754 y=166..343 pantalla=1254x564

Con ese rectangulo se recortan dos capturas del mismo menu (una normal y otra
con ?menuflip=1, que gira el panel 180 grados como el fallo original) y se
compara el "perfil de tinta" (pixeles claros por columna). Si una es espejo de
la otra:
    correlacion(perfil_A, perfil_B)            -> baja
    correlacion(perfil_A, perfil_B invertido)  -> alta

    python _verificacion/compara_espejo.py normal.png girado.png 500 754 166 343 564
"""

import sys
from PIL import Image


def recorte(img, x0, x1, y0, y1, alto):
    """Recorta usando coordenadas de Unity (origen abajo-izquierda)."""
    izq = max(0, int(x0))
    der = min(img.size[0], int(x1))
    arriba = max(0, int(alto - y1))
    abajo = min(img.size[1], int(alto - y0))
    return img.convert("RGB").crop((izq, arriba, der, abajo))


def perfil(img):
    """
    Pixeles claros por columna, con umbral adaptado a CADA captura: la iluminacion
    del juego cambia de golpe (apagones), asi que no se puede usar un valor fijo.
    """
    gris = img.convert("L")
    px = gris.load()
    w, h = gris.size

    valores = [px[x, y] for y in range(h) for x in range(w)]
    media = sum(valores) / float(len(valores))
    varianza = sum((v - media) ** 2 for v in valores) / float(len(valores))
    desvio = varianza ** 0.5
    umbral = media + 0.55 * desvio

    return [sum(1 for y in range(h) if px[x, y] > umbral) for x in range(w)], media, umbral


def mascara(img, banda=0.0, alto_banda=1.0):
    """Mascara 2D de tinta (1 = pixel claro) adaptada al brillo de la captura."""
    gris = img.convert("L")
    w, h = gris.size
    y0 = int(h * banda)
    y1 = max(y0 + 1, int(h * (banda + alto_banda)))

    px = gris.load()
    valores = [px[x, y] for y in range(y0, y1) for x in range(w)]
    media = sum(valores) / float(len(valores))
    varianza = sum((v - media) ** 2 for v in valores) / float(len(valores))
    umbral = media + 0.55 * (varianza ** 0.5)

    return [1 if px[x, y] > umbral else 0 for y in range(y0, y1) for x in range(w)]


def correlacion2d(a, b):
    """Correlacion pixel a pixel de dos mascaras 2D invertibles en espejo."""
    n = min(len(a), len(b))
    if n == 0:
        return 0.0
    a, b = a[:n], b[:n]
    ma, mb = sum(a) / float(n), sum(b) / float(n)
    da = [v - ma for v in a]
    db = [v - mb for v in b]
    num = sum(x * y for x, y in zip(da, db))
    den = (sum(x * x for x in da) ** 0.5) * (sum(y * y for y in db) ** 0.5)
    return num / den if den > 1e-9 else 0.0


def espejo2d(mascara, ancho):
    """Invierte la mascara en espejo, fila por fila."""
    alto = len(mascara) // ancho
    salida = []
    for y in range(alto):
        fila = mascara[y * ancho:(y + 1) * ancho]
        salida.extend(reversed(fila))
    return salida


def correlacion(a, b):
    n = min(len(a), len(b))
    if n == 0:
        return 0.0
    a, b = a[:n], b[:n]
    ma, mb = sum(a) / float(n), sum(b) / float(n)
    da = [v - ma for v in a]
    db = [v - mb for v in b]
    num = sum(x * y for x, y in zip(da, db))
    den = (sum(x * x for x in da) ** 0.5) * (sum(y * y for y in db) ** 0.5)
    return num / den if den > 1e-9 else 0.0


def ancho_de_tinta(img):
    """Cuanto se reparte la tinta: sirve para avisar si el recorte salio vacio."""
    p, _, _ = perfil(img)
    return sum(p), (max(p) if p else 0)


def main():
    if len(sys.argv) < 7:
        print("uso: compara_espejo.py normal.png girado.png x0 x1 y0 y1 alto")
        return 1

    a = Image.open(sys.argv[1])
    b = Image.open(sys.argv[2])
    x0, x1, y0, y1, alto = [int(v) for v in sys.argv[3:8]]

    ca = recorte(a, x0, x1, y0, y1, alto)
    cb = recorte(b, x0, x1, y0, y1, alto)

    pa, media_a, umbral_a = perfil(ca)
    pb, media_b, umbral_b = perfil(cb)

    print("recorte del panel: %dx%d px" % (ca.size[0], ca.size[1]))
    print("  captura A: brillo medio %5.1f · umbral %5.1f · tinta %d" % (media_a, umbral_a, sum(pa)))
    print("  captura B: brillo medio %5.1f · umbral %5.1f · tinta %d" % (media_b, umbral_b, sum(pb)))

    # El titulo: la banda superior del panel. Es el texto mas largo y asimetrico,
    # asi que es el que mejor revela si la imagen esta invertida.
    ancho = ca.size[0]
    ma = mascara(ca, 0.0, 0.30)
    mb_ = mascara(cb, 0.0, 0.30)
    mb_espejo = espejo2d(mb_, ancho)

    igual = correlacion2d(ma, mb_)
    espejo = correlacion2d(ma, mb_espejo)

    print("")
    print("TITULO (banda superior del panel, comparacion 2D pixel a pixel)")
    print("  A vs B tal cual          : %+.3f" % igual)
    print("  A vs B girado en espejo  : %+.3f" % espejo)

    if sum(pa) < 200 or sum(pb) < 200:
        print("El recorte esta casi vacio: no se puede comparar asi")
        return 1

    directa = correlacion(pa, pb)
    espejada = correlacion(pa, list(reversed(pb)))

    print("perfil de tinta  A vs B tal cual            : %+.3f" % directa)
    print("perfil de tinta  A vs B girado en espejo    : %+.3f" % espejada)
    print("")

    if espejo > igual + 0.10:
        print("RESULTADO: el texto de A coincide con B girado en espejo.")
        print("           B es la captura con el panel girado 180 grados (la que se veia al")
        print("           reves), asi que A, la que trae el juego, se lee DERECHO.")
        return 0

    print("RESULTADO: no se pudo separar el espejado -> REVISAR")
    return 1


if __name__ == "__main__":
    sys.exit(main())
