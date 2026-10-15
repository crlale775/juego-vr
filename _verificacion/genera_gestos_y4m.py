"""
Genera los videos Y4M que usan las pruebas de gestos de tiro con la camara virtual
de Chrome. Dos videos, uno por gesto:

    mano_puno.y4m    -> el puño cerrado (un bloque compacto) y de golpe la mano abierta
                        (palma + cinco dedos separados). Prueba el gesto "puño y abrir".
    mano_empuje.y4m  -> la mano se va acercando a la camara: crece de golpe. Prueba el
                        gesto "empujar hacia la camara".

    python _verificacion/genera_gestos_y4m.py _verificacion
"""

import sys
import os

W, H = 320, 240          # pequeño a proposito: se genera en Python puro
FPS = 30
FRAMES = 90

SKIN = (205, 150, 120)
BG = (28, 30, 40)


def yuv(r, g, b):
    y = 0.299 * r + 0.587 * g + 0.114 * b
    u = -0.168736 * r - 0.331264 * g + 0.5 * b + 128.0
    v = 0.5 * r - 0.418688 * g - 0.081312 * b + 128.0
    return y, u, v


Y_SKIN, U_SKIN, V_SKIN = yuv(*SKIN)
Y_BG, U_BG, V_BG = yuv(*BG)


def mano(point, escala=1.0, puño=False):
    """Devuelve una funcion (x, y) -> esta_en_la_mano."""
    cx, cy = W * 0.5, H * 0.55
    ancho_palma = W * 0.30 * escala
    alto_palma = H * 0.34 * escala

    x0 = cx - ancho_palma * 0.5
    x1 = cx + ancho_palma * 0.5
    y0 = cy - alto_palma * 0.5
    y1 = cy + alto_palma * 0.5

    dedos = []

    if not puño:
        for i in range(5):
            fx = cx - ancho_palma * 0.5 + ancho_palma * (0.08 + i * 0.20)
            fw = ancho_palma * 0.14
            alto_dedo = alto_palma * (0.75 + abs(2 - i) * 0.16)
            dedos.append((fx, y0 - alto_dedo, fx + fw, y0 + alto_palma * 0.25))

    def dentro(x, y):
        if x0 <= x <= x1 and y0 <= y <= y1:
            return True
        for (dx0, dy0, dx1, dy1) in dedos:
            if dx0 <= x <= dx1 and dy0 <= y <= dy1:
                return True
        return False

    return dentro


def escribir(ruta, funcion_frame):
    cw, ch = W // 2, H // 2

    with open(ruta, "wb") as fh:
        fh.write(b"YUV4MPEG2 W%d H%d F%d:1 Ip A1:1 C420jpeg\n" % (W, H, FPS))

        for f in range(FRAMES):
            dentro = funcion_frame(f)
            wobble = int(3 * ((f % 30) / 30.0))

            y_plane = bytearray(W * H)
            for y in range(H):
                fila = y * W
                for x in range(W):
                    y_plane[fila + x] = int((Y_SKIN if dentro(x, y) else Y_BG) + wobble)

            u_plane = bytearray(cw * ch)
            v_plane = bytearray(cw * ch)
            for by in range(ch):
                cfila = by * cw
                for bx in range(cw):
                    mano_aqui = dentro(bx * 2 + 1, by * 2 + 1)
                    u_plane[cfila + bx] = int(U_SKIN if mano_aqui else U_BG)
                    v_plane[cfila + bx] = int(V_SKIN if mano_aqui else V_BG)

            fh.write(b"FRAME\n")
            fh.write(bytes(y_plane))
            fh.write(bytes(u_plane))
            fh.write(bytes(v_plane))

    print("escrito %s (%d fotogramas, %dx%d)" % (ruta, FRAMES, W, H))


def frame_puno(f):
    """Puño cerrado hasta el fotograma 30 y, de golpe, la mano abierta."""
    return mano(None, 1.0, puño=(f < 30))


def frame_empuje(f):
    """La mano se acerca a la camara: escala de 0.62 a 1.5 en 1.2 segundos."""
    if f < 12:
        escala = 0.62
    elif f < 48:
        escala = 0.62 + (f - 12) / 36.0 * 0.88
    else:
        escala = 1.5

    return mano(None, escala, puño=False)


def main():
    carpeta = sys.argv[1] if len(sys.argv) > 1 else "_verificacion"
    os.makedirs(carpeta, exist_ok=True)

    escribir(os.path.join(carpeta, "mano_puno.y4m"), frame_puno)
    escribir(os.path.join(carpeta, "mano_empuje.y4m"), frame_empuje)


if __name__ == "__main__":
    main()
