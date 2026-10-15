"""
Genera un video Y4M con una mano sintetica (tono de piel) para que Chrome
la use como camara virtual y poder verificar el escaneo automatico.

    python _verificacion/genera_mano_y4m.py _verificacion/mano.y4m
"""

import sys

W, H = 640, 480
FPS = 30
FRAMES = 90

SKIN = (205, 150, 120)
BG = (28, 30, 40)


def rgb_to_yuv(r, g, b):
    y = 0.299 * r + 0.587 * g + 0.114 * b
    u = -0.168736 * r - 0.331264 * g + 0.5 * b + 128.0
    v = 0.5 * r - 0.418688 * g - 0.081312 * b + 128.0
    return y, u, v


def hand_at(offset):
    """Devuelve (x, y) -> bool con la mano desplazada verticalmente `offset` pixeles."""
    palm = (W * 0.30, H * 0.46 + offset, W * 0.70, H * 0.96 + offset)   # x0, y0, x1, y1
    fingers = []
    for i in range(5):
        fx = W * (0.315 + i * 0.077)
        fw = W * 0.055
        top = H * (0.18 + abs(2 - i) * 0.045) + offset
        fingers.append((fx, top, fx + fw, H * 0.50 + offset))

    def inside(x, y):
        if palm[0] <= x <= palm[2] and palm[1] <= y <= palm[3]:
            return True
        for (x0, y0, x1, y1) in fingers:
            if x0 <= x <= x1 and y0 <= y <= y1:
                return True
        return False

    return inside


def offset_for_frame(f):
    """La mano sube de golpe entre los fotogramas 20 y 30 (gesto de lanzar)."""
    if f < 20:
        return 0.0
    if f < 30:
        return -(f - 20) / 10.0 * (H * 0.62)
    return -(H * 0.62)


def main():
    out = sys.argv[1] if len(sys.argv) > 1 else "_verificacion/mano.y4m"

    y_bg, u_bg, v_bg = rgb_to_yuv(*BG)
    y_skin, u_skin, v_skin = rgb_to_yuv(*SKIN)

    cw, ch = W // 2, H // 2

    with open(out, "wb") as fh:
        fh.write(b"YUV4MPEG2 W%d H%d F%d:1 Ip A1:1 C420jpeg\n" % (W, H, FPS))

        for f in range(FRAMES):
            inside = hand_at(offset_for_frame(f))
            wobble = int(3 * ((f % 30) / 30.0))   # pequena variacion de luz

            y_plane = bytearray(W * H)
            for y in range(H):
                row = y * W
                for x in range(W):
                    y_plane[row + x] = int((y_skin if inside(x, y) else y_bg) + wobble)

            u_plane = bytearray(cw * ch)
            v_plane = bytearray(cw * ch)
            for by in range(ch):
                crow = by * cw
                for bx in range(cw):
                    hand = inside(bx * 2 + 1, by * 2 + 1)
                    u_plane[crow + bx] = int(u_skin if hand else u_bg)
                    v_plane[crow + bx] = int(v_skin if hand else v_bg)

            fh.write(b"FRAME\n")
            fh.write(bytes(y_plane))
            fh.write(bytes(u_plane))
            fh.write(bytes(v_plane))

    print("Y4M escrito en %s (%d fotogramas, %dx%d, la mano sube en el gesto)" % (out, FRAMES, W, H))


if __name__ == "__main__":
    main()
