"""
Comprueba DIBUJADO en las capturas de las pruebas: que la mano virtual (la de la
escena y el esqueleto de los 21 puntos) aparece de verdad en pantalla, contando
pixeles de sus colores. Un estado "visible=True" no demuestra que se pinte.

    python _verificacion/revisa_dibujo.py _verificacion

Capturas que mira:
  ia_flujo.png     -> mano de la interfaz (verde de ejemplo, sin puntos)
  ia_juego.png     -> ademas la mano virtual 3D (tono piel translucido)
  ia_esqueleto.png -> el esqueleto de los 21 puntos (huesos verde claro + articulaciones)

Devuelve codigo 0 si todo se ha dibujado.
"""

import os
import sys

from PIL import Image

# Colores clave del juego (0..255).
VERDE_MANO_UI = (86, 240, 158)      # mano de ejemplo del HUD  (0.32, 0.96, 0.62)
HUESO = (120, 255, 180)             # huesos del esqueleto     (0.45, 1, 0.7)
ARTICULACION = (255, 250, 230)      # puntos de las articulaciones
PIEL = (255, 205, 175)              # mano virtual 3D          (1, 0.78, 0.64)


def cerca(pixel, color, tolerancia):
    return (abs(pixel[0] - color[0]) <= tolerancia
            and abs(pixel[1] - color[1]) <= tolerancia
            and abs(pixel[2] - color[2]) <= tolerancia)


def contar(ruta, colores, tolerancia=26):
    imagen = Image.open(ruta).convert("RGB")
    ancho, alto = imagen.size
    pixeles = imagen.load()

    cuenta = 0

    # Se mira 1 de cada 2 pixeles: sobra para saber si algo esta pintado.
    for y in range(0, alto, 2):
        for x in range(0, ancho, 2):
            p = pixeles[x, y]
            for c in colores:
                if cerca(p, c, tolerancia):
                    cuenta += 1
                    break

    return cuenta, ancho, alto


def revisar(carpeta, nombre, colores, minimo, nota):
    ruta = os.path.join(carpeta, nombre)

    if not os.path.exists(ruta):
        print("  %-22s FALTA la captura %s" % (nombre, ruta))
        return False

    cuenta, ancho, alto = contar(ruta, colores)
    ok = cuenta >= minimo

    print("  %-22s pixeles %-6d (minimo %-5d) %s  - %s"
          % (nombre, cuenta, minimo, "OK" if ok else "FALLO", nota))

    return ok


def main():
    carpeta = sys.argv[1] if len(sys.argv) > 1 else "_verificacion"
    tag = sys.argv[2] if len(sys.argv) > 2 else "ia"

    print("=== REVISION DE LO DIBUJADO EN LAS CAPTURAS ===")

    resultados = []

    resultados.append(revisar(carpeta, tag + "_flujo.png", [VERDE_MANO_UI, HUESO, ARTICULACION, PIEL],
                              60, "mano dibujada en la vista (menu)"))

    resultados.append(revisar(carpeta, tag + "_juego.png", [VERDE_MANO_UI, PIEL],
                              60, "mano de la vista y mano virtual 3D"))

    resultados.append(revisar(carpeta, tag + "_esqueleto.png", [HUESO, ARTICULACION],
                              400, "esqueleto de 21 puntos (MediaPipe)"))

    ok = all(resultados)

    print("")
    print("RESULTADO DIBUJO: " + ("OK - la mano y el esqueleto se ven en pantalla" if ok else "FALLO"))

    return 0 if ok else 1


if __name__ == "__main__":
    sys.exit(main())
