"""
Genera un video Y4M con una mano sintetica (tono de piel) colocada de forma que
el CURSOR DE MANO del juego caiga EXACTAMENTE sobre el boton "INICIAR PARTIDA".

Se usa junto a verify_mano_menu.js para comprobar, en un navegador real y sin
tocar nada, que el menu y la partida se manejan SOLO con la mano delante de la
camara trasera.

    python _verificacion/genera_mano_menu_y4m.py _verificacion/mano_menu.y4m

Como sale la posicion:

  - El juego calcula el cursor con  yaw   = (px - 0.5) * 2 * 34 grados,
                                   pitch = (0.5 - py) * 2 * 24 grados.
  - El panel del menu esta a 1,75 m del ojo (1,48 m de alto) y su escala es 0.0011.
  - El boton "INICIAR PARTIDA" esta anclado arriba, en (-180, -262) => su centro
    queda en el mundo en (-0.198, 1.543, 1.750).

  De ahi sale la posicion que el juego TIENE que ver: (px, py) = (0.405, 0.539).

  - px: la camara virtual de Chrome se anuncia como FRONTAL, asi que el juego la
    refleja (px -> 1 - px). Por eso la mano se dibuja en 1 - 0.405 = 0.595.
    Con la camara TRASERA de un telefono real no hay reflejo.
  - py: 0.539 medido desde ABAJO (como lo lee el juego).

En vez de colocar la mano a ojo, se calcula su centroide real y se traslada
justo a esa posicion.
"""

import sys

W, H = 640, 480
FPS = 30
FRAMES = 90

SKIN = (205, 150, 120)
BG = (28, 30, 40)

# Lo que el juego tiene que ver (0..1; y desde abajo).
TARGET_PX = 0.405
TARGET_PY = 0.539

# Chrome anuncia la camara virtual como frontal: el juego refleja x.
MIRRORED_IN_BROWSER = True

TARGET_IMG_X = (1.0 - TARGET_PX if MIRRORED_IN_BROWSER else TARGET_PX) * W
TARGET_IMG_Y = (1.0 - TARGET_PY) * H   # el generador cuenta desde arriba

PALM_W, PALM_H = 190.0, 130.0
FINGER_W = 20.0
FINGER_STEP = 31.0


def rgb_to_yuv(r, g, b):
    y = 0.299 * r + 0.587 * g + 0.114 * b
    u = -0.168736 * r - 0.331264 * g + 0.5 * b + 128.0
    v = 0.5 * r - 0.418688 * g - 0.081312 * b + 128.0
    return y, u, v


def inside(x, y, px0, py0):
    """Mano = palma + 5 dedos. (px0, py0) es la esquina superior izquierda de la palma."""
    if px0 <= x <= px0 + PALM_W and py0 <= y <= py0 + PALM_H:
        return True

    for i in range(5):
        fx = px0 + 18.0 + i * FINGER_STEP
        top = py0 - 52.0 + abs(2 - i) * 8.0
        if fx <= x <= fx + FINGER_W and top <= y <= py0 + 6.0:
            return True

    return False


def centroid(px0, py0):
    """Centroide real del dibujo (en pixeles), contando desde arriba."""
    sx = 0.0
    sy = 0.0
    n = 0

    for y in range(int(py0 - 60), int(py0 + PALM_H) + 2):
        if y < 0 or y >= H:
            continue
        for x in range(int(px0), int(px0 + PALM_W) + 2):
            if x < 0 or x >= W:
                continue
            if inside(x, y, px0, py0):
                sx += x
                sy += y
                n += 1

    return sx / n, sy / n, n


def main():
    out = sys.argv[1] if len(sys.argv) > 1 else "_verificacion/mano_menu.y4m"

    # 1) Se dibuja la mano en un sitio provisional y se mide su centroide.
    cx, cy, n = centroid(140.0, 140.0)

    # 2) Se traslada para que el centroide caiga en el punto objetivo.
    base_px0 = 140.0 + (TARGET_IMG_X - cx)
    base_py0 = 140.0 + (TARGET_IMG_Y - cy)

    check_x, check_y, _ = centroid(base_px0, base_py0)
    print("centroide dibujado en (%.1f, %.1f) px; objetivo (%.1f, %.1f) px"
          % (check_x, check_y, TARGET_IMG_X, TARGET_IMG_Y))

    y_bg, u_bg, v_bg = rgb_to_yuv(*BG)
    y_skin, u_skin, v_skin = rgb_to_yuv(*SKIN)

    cw, ch = W // 2, H // 2

    with open(out, "wb") as fh:
        fh.write(b"YUV4MPEG2 W%d H%d F%d:1 Ip A1:1 C420jpeg\n" % (W, H, FPS))

        for f in range(FRAMES):
            # Balanceo minimo para que la mano parezca viva, sin salir del boton.
            dx = 2.0 * ((f % 20) / 20.0 - 0.5)
            dy = 2.0 * ((f % 14) / 14.0 - 0.5)
            px0 = base_px0 + dx
            py0 = base_py0 + dy
            wobble = int(3 * ((f % 30) / 30.0))

            y_plane = bytearray(W * H)
            for y in range(H):
                row = y * W
                for x in range(W):
                    y_plane[row + x] = int((y_skin if inside(x, y, px0, py0) else y_bg) + wobble)

            u_plane = bytearray(cw * ch)
            v_plane = bytearray(cw * ch)
            for by in range(ch):
                crow = by * cw
                for bx in range(cw):
                    hand = inside(bx * 2 + 1, by * 2 + 1, px0, py0)
                    u_plane[crow + bx] = int(u_skin if hand else u_bg)
                    v_plane[crow + bx] = int(v_skin if hand else v_bg)

            fh.write(b"FRAME\n")
            fh.write(bytes(y_plane))
            fh.write(bytes(u_plane))
            fh.write(bytes(v_plane))

    print("Y4M escrito en %s (%d fotogramas, %dx%d); mano sobre INICIAR PARTIDA"
          % (out, FRAMES, W, H))


if __name__ == "__main__":
    main()
