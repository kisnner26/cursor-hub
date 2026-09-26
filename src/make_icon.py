# Genera icon.ico (y un preview) para Cursor Hub: papel crema, cursor a tinta, florecita rosa.
from PIL import Image, ImageDraw, ImageFilter
import math, random

N = 1024
PAPER, INK, MID, PINK, PINK_D = (245, 240, 232), (26, 18, 8), (90, 74, 48), (234, 196, 196), (201, 154, 154)

def draw(size_hint):
    im = Image.new('RGBA', (N, N), (0, 0, 0, 0))
    d = ImageDraw.Draw(im)
    pad, rad = 40, 210
    d.rounded_rectangle((pad, pad, N - pad, N - pad), rad, fill=PAPER)
    # grano de papel (solo en tamaños grandes)
    if size_hint >= 64:
        g = Image.new('RGBA', (N, N), (0, 0, 0, 0)); gp = g.load(); rnd = random.Random(7)
        for _ in range(26000):
            x, y = rnd.randrange(N), rnd.randrange(N); gp[x, y] = (70, 50, 20, rnd.randrange(10, 40))
        mask = Image.new('L', (N, N), 0); ImageDraw.Draw(mask).rounded_rectangle((pad, pad, N - pad, N - pad), rad, fill=255)
        im.paste(g, (0, 0), Image.composite(g, Image.new('RGBA', (N, N)), mask).split()[3])
    border = 22 if size_hint >= 48 else 44
    d.rounded_rectangle((pad, pad, N - pad, N - pad), rad, outline=INK, width=border)

    # cursor flecha, relleno de tinta
    s = 1.0
    ox, oy = 250, 190
    arrow = [(0, 0), (0, 520), (130, 400), (225, 610), (320, 568), (228, 362), (400, 362)]
    arrow = [(ox + x * s, oy + y * s) for x, y in arrow]
    d.polygon(arrow, fill=INK)
    # flor de cinco pétalos abajo a la derecha
    cx, cy, pr = 700, 700, 95 if size_hint >= 32 else 120
    for i in range(5):
        a = -math.pi / 2 + i * 2 * math.pi / 5
        px, py = cx + math.cos(a) * pr * 0.95, cy + math.sin(a) * pr * 0.95
        d.ellipse((px - pr * 0.72, py - pr * 0.72, px + pr * 0.72, py + pr * 0.72), fill=PINK, outline=PINK_D if size_hint >= 48 else None, width=14)
    d.ellipse((cx - pr * 0.42, cy - pr * 0.42, cx + pr * 0.42, cy + pr * 0.42), fill=INK)
    return im

sizes = [16, 24, 32, 48, 64, 128, 256]
imgs = [draw(sz).resize((sz, sz), Image.LANCZOS) for sz in sizes]
imgs[-1].save('icon.ico', format='ICO', sizes=[(s, s) for s in sizes], append_images=imgs[:-1])
# preview
pv = Image.new('RGBA', (sum(sizes) + 20 * len(sizes) + 20, 300), (60, 60, 60, 255)); x = 20
for im in imgs:
    pv.paste(im, (x, 280 - im.size[1]), im); x += im.size[0] + 20
pv.save('icon_preview.png')
draw(256).resize((512, 512), Image.LANCZOS).save('icon_big.png')
print('ok')
