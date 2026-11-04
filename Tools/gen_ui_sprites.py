"""Peces d'interfície comunes amb l'estil de l'art (ButtonFrame / UpgradeCard), per fer 9-slice:
  ui_steel   / ui_steel_s   marc metàl·lic amb cargols i interior fosc (text blanc a sobre)
  ui_paper   / ui_paper_s   paper crema amb contorn marró dibuixat a mà (text fosc a sobre)
  ui_track / ui_fill        barres (fons fosc i farciment blanc per tenyir)
Les versions _s són petites, per a l'OnGUI (que dibuixa les vores a mida real de píxel)."""
import sys
import numpy as np
from PIL import Image, ImageDraw, ImageFilter

sys.path.insert(0, sys.argv[2] if len(sys.argv) > 2 else ".")
OUT = sys.argv[1]
S = 3

STEEL = (82, 103, 129)
STEEL_LIGHT = (150, 180, 210)
STEEL_DARK = (34, 49, 69)
PLATE = (64, 92, 128)
PAPER = (243, 234, 218)
PAPER_SHADE = (226, 214, 194)
INK = (114, 89, 73)
NAVY_LINE = (28, 38, 54)
INSIDE = (30, 41, 58)


def rrect_pts(x0, y0, x1, y1, r, wobble, seed):
    g = np.random.default_rng(seed)
    pts = []
    for cx, cy, a0 in ((x1 - r, y0 + r, -90), (x1 - r, y1 - r, 0), (x0 + r, y1 - r, 90), (x0 + r, y0 + r, 180)):
        for t in np.linspace(0, 90, 9):
            a = np.deg2rad(a0 + t)
            pts.append((cx + np.cos(a) * r, cy + np.sin(a) * r))
    out = []
    for i in range(len(pts)):
        p, q = pts[i], pts[(i + 1) % len(pts)]
        out.append(p)
        k = int(np.hypot(q[0] - p[0], q[1] - p[1]) // 10)
        for j in range(1, k):
            out.append((p[0] + (q[0] - p[0]) * j / k, p[1] + (q[1] - p[1]) * j / k))
    m = len(out)
    nz = np.convolve(g.normal(0, wobble, m + 8), np.ones(5) / 5, mode="same")[:m]
    cx, cy = (x0 + x1) / 2, (y0 + y1) / 2
    res = []
    for (px, py), n in zip(out, nz):
        dx, dy = px - cx, py - cy
        l = np.hypot(dx, dy) or 1
        res.append(((px + dx / l * n) * S, (py + dy / l * n) * S))
    return res


def pmask(size, pts):
    m = Image.new("L", size, 0)
    ImageDraw.Draw(m).polygon(pts, fill=255)
    return m


def fill(img, m, rgb):
    img.paste(Image.new("RGBA", img.size, tuple(rgb) + (255,)), (0, 0), m)


def line(img, pts, rgb, w):
    ImageDraw.Draw(img).line(pts + [pts[0]], fill=tuple(rgb) + (255,), width=max(1, int(w * S)), joint="curve")


def rivet(img, x, y, r):
    d = ImageDraw.Draw(img)
    d.ellipse([(x - r) * S, (y - r) * S, (x + r) * S, (y + r) * S], fill=PLATE + (255,), outline=NAVY_LINE + (255,), width=max(1, int(1.6 * S)))
    k = r * 0.5
    d.line([(x - k) * S, (y - k) * S, (x + k) * S, (y + k) * S], fill=NAVY_LINE + (255,), width=max(1, int(1.4 * S)))
    d.line([(x - k) * S, (y + k) * S, (x + k) * S, (y - k) * S], fill=NAVY_LINE + (255,), width=max(1, int(1.4 * S)))


def save(img, name, w):
    img.resize((w, w if img.width == img.height else int(img.height * w / img.width)), Image.LANCZOS).save(f"{OUT}/{name}.png")


def steel(N, band, r, rv, seed):
    img = Image.new("RGBA", (N * S, N * S), (0, 0, 0, 0))
    outer = rrect_pts(3, 3, N - 3, N - 3, r, 0.5, seed)
    fill(img, pmask(img.size, outer), STEEL)
    a = pmask(img.size, rrect_pts(3, 3, N - 3, N - 3, r, 0.5, seed))
    b = pmask(img.size, rrect_pts(3 + N * 0.02, 3 + N * 0.025, N - 3, N - 3, r, 0.5, seed))
    bev = Image.fromarray(np.clip(np.asarray(a, np.int16) - np.asarray(b, np.int16), 0, 255).astype(np.uint8))
    fill(img, bev, STEEL_LIGHT)
    line(img, outer, NAVY_LINE, N / 110)
    # finestra interior fosca
    fill(img, pmask(img.size, rrect_pts(band - 3, band - 3, N - band + 3, N - band + 3, r * 0.55, 0.4, seed + 1)), STEEL_DARK)
    inner = rrect_pts(band, band, N - band, N - band, r * 0.5, 0.4, seed + 2)
    fill(img, pmask(img.size, inner), INSIDE)
    # una mica de llum a la part de dalt de la finestra (com el gris del botó, però fosc)
    top = pmask(img.size, rrect_pts(band, band, N - band, band + N * 0.08, r * 0.3, 0.3, seed + 3)).filter(ImageFilter.GaussianBlur(N * 0.03 * S))
    im = np.asarray(top, np.float32) * np.asarray(pmask(img.size, inner), np.float32) / 255 * 0.25
    img.alpha_composite(Image.merge("RGBA", (*Image.new("RGB", img.size, STEEL).split(), Image.fromarray(im.astype(np.uint8)))))
    k = band * 0.5 + r * 0.2   # cargols dins de la cantonada arrodonida
    for x, y in ((k, k), (N - k, k), (k, N - k), (N - k, N - k)):
        rivet(img, x, y, rv)
    return img


def paper(N, r, ink_w, seed):
    img = Image.new("RGBA", (N * S, N * S), (0, 0, 0, 0))
    pts = rrect_pts(4, 4, N - 4, N - 4, r, N / 160, seed)
    m = pmask(img.size, pts)
    g = np.random.default_rng(seed)
    w, h = img.size
    n = g.normal(0, 1, (6, 6)).astype(np.float32)
    n = np.asarray(Image.fromarray(((n * 30) + 128).clip(0, 255).astype(np.uint8)).resize((w, h), Image.BICUBIC), np.float32)
    arr = np.zeros((h, w, 4), np.float32)
    arr[..., :3] = np.array(PAPER, np.float32) + ((n - 128) / 128 * 6)[..., None]
    arr[..., 3] = np.asarray(m, np.float32)
    img.alpha_composite(Image.fromarray(arr.clip(0, 255).astype(np.uint8), "RGBA"))
    line(img, pts, INK, ink_w)
    return img


# Panells grans (pantalles amb Canvas): 256 px, vora de 9-slice = 64
save(steel(256, 30, 34, 8, 1), "ui_steel", 256)
save(paper(256, 30, 4, 2), "ui_paper", 256)
# Panells petits (OnGUI): 96 px, vora = 28
save(steel(96, 14, 14, 4, 3), "ui_steel_s", 96)
save(paper(96, 14, 2.4, 4), "ui_paper_s", 96)

# Barres: 48 x 20, vora = 9
def bar(color_fill, outline_rgb, seed):
    W, H = 48, 20
    img = Image.new("RGBA", (W * S, H * S), (0, 0, 0, 0))
    pts = rrect_pts(1.5, 1.5, W - 1.5, H - 1.5, 8, 0.15, seed)
    fill(img, pmask(img.size, pts), color_fill)
    if outline_rgb:
        line(img, pts, outline_rgb, 1.6)
    img.resize((W, H), Image.LANCZOS).save(f"{OUT}/{'ui_track' if outline_rgb else 'ui_fill'}.png")


bar((16, 22, 33), STEEL, 5)
bar((255, 255, 255), None, 6)
print("ok")
