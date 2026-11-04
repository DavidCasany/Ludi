"""Marcador v2 amb l'estil de l'art de l'artista (ButtonFrame + UpgradeCard):
il·lustració plana, contorns foscos dibuixats a mà (lleugerament irregulars), metall blau acer amb
cargols, paper crema amb textura suau, colors apagats. Sense brillantors."""
import sys
import numpy as np
from PIL import Image, ImageDraw, ImageFilter

OUT = sys.argv[1]
S = 2
rng = np.random.default_rng(5)

# Paleta treta de ButtonFrame.png i UpgradeCard.png
STEEL = (82, 103, 129)
STEEL_LIGHT = (150, 180, 210)
STEEL_DARK = (34, 49, 69)
PLATE = (64, 92, 128)
PAPER = (243, 234, 218)
PAPER_SHADE = (226, 214, 194)
INK = (114, 89, 73)       # contorn marró de la targeta
INK_DARK = (45, 56, 64)   # blau nit del patí (dígits)
NAVY_LINE = (28, 38, 54)


def new(w, h):
    return Image.new("RGBA", (w * S, h * S), (0, 0, 0, 0))


def rrect_pts(x0, y0, x1, y1, r, wobble=1.2, seed=0, n=28):
    """Punts d'un rectangle arrodonit amb un tremolor suau, com un traç fet a mà."""
    g = np.random.default_rng(seed)
    pts = []
    corners = [(x1 - r, y0 + r, -90), (x1 - r, y1 - r, 0), (x0 + r, y1 - r, 90), (x0 + r, y0 + r, 180)]
    for cx, cy, a0 in corners:
        for t in np.linspace(0, 90, n // 4 + 2):
            a = np.deg2rad(a0 + t)
            pts.append((cx + np.cos(a) * r, cy + np.sin(a) * r))
    # intercala punts als costats rectes
    out = []
    for i in range(len(pts)):
        p, q = pts[i], pts[(i + 1) % len(pts)]
        out.append(p)
        dist = np.hypot(q[0] - p[0], q[1] - p[1])
        k = int(dist // 18)
        for j in range(1, k):
            out.append((p[0] + (q[0] - p[0]) * j / k, p[1] + (q[1] - p[1]) * j / k))
    # tremolor de baixa freqüència
    m = len(out)
    noise = np.convolve(g.normal(0, wobble, m + 8), np.ones(5) / 5, mode="same")[:m]
    cx, cy = (x0 + x1) / 2, (y0 + y1) / 2
    res = []
    for (px, py), nz in zip(out, noise):
        dx, dy = px - cx, py - cy
        l = np.hypot(dx, dy) or 1
        res.append(((px + dx / l * nz) * S, (py + dy / l * nz) * S))
    return res


def poly_mask(size, pts):
    m = Image.new("L", size, 0)
    ImageDraw.Draw(m).polygon(pts, fill=255)
    return m


def fill(img, m, rgb, a=1.0):
    layer = Image.new("RGBA", img.size, tuple(rgb) + (255,))
    mm = Image.fromarray((np.asarray(m, np.float32) * a).astype(np.uint8))
    img.paste(layer, (0, 0), mm) if a >= 1 else img.alpha_composite(Image.merge("RGBA", (*layer.split()[:3], mm)))


def paper_texture(img, m, base, seed):
    """Paper crema amb taques suaus (com la targeta)."""
    w, h = img.size
    g = np.random.default_rng(seed)
    n = g.normal(0, 1, (h // 24 + 2, w // 24 + 2)).astype(np.float32)
    n = np.asarray(Image.fromarray(((n * 30) + 128).clip(0, 255).astype(np.uint8)).resize((w, h), Image.BICUBIC), np.float32)
    n = (n - 128) / 128 * 7
    arr = np.zeros((h, w, 4), np.float32)
    arr[..., :3] = np.array(base, np.float32) + n[..., None]
    arr[..., 3] = np.asarray(m, np.float32)
    img.alpha_composite(Image.fromarray(arr.clip(0, 255).astype(np.uint8), "RGBA"))


def outline(img, pts, rgb, width):
    d = ImageDraw.Draw(img)
    d.line(pts + [pts[0]], fill=tuple(rgb) + (255,), width=int(width * S), joint="curve")


def rivet(img, x, y, r=9):
    d = ImageDraw.Draw(img)
    d.ellipse([(x - r) * S, (y - r) * S, (x + r) * S, (y + r) * S], fill=PLATE + (255,), outline=NAVY_LINE + (255,), width=2 * S)
    # creu del cargol, com als botons
    k = r * 0.5
    d.line([(x - k) * S, (y - k) * S, (x + k) * S, (y + k) * S], fill=NAVY_LINE + (255,), width=2 * S)
    d.line([(x - k) * S, (y + k) * S, (x + k) * S, (y - k) * S], fill=NAVY_LINE + (255,), width=2 * S)


def save(img, name):
    img.resize((img.width // S, img.height // S), Image.LANCZOS).save(f"{OUT}/{name}.png")


def steel_frame(W, H, r, seed, rivets=True):
    """Marc de metall com ButtonFrame: vora clara a dalt, cos blau acer, filet fosc interior."""
    img = new(W, H)
    outer = rrect_pts(4, 4, W - 4, H - 4, r, 1.0, seed)
    fill(img, poly_mask(img.size, outer), STEEL)
    # bisell clar (a dalt i a l'esquerra)
    hl = poly_mask(img.size, rrect_pts(4, 4, W - 4, H - 4, r, 1.0, seed))
    sh = poly_mask(img.size, rrect_pts(9, 10, W - 4, H - 4, r, 1.0, seed))
    bev = Image.fromarray(np.clip(np.asarray(hl, np.int16) - np.asarray(sh, np.int16), 0, 255).astype(np.uint8))
    fill(img, bev, STEEL_LIGHT)
    outline(img, outer, NAVY_LINE, 2.5)
    return img


# ===================== MARCADOR (una sola peça de fons) =====================
W, H = 868, 140
img = steel_frame(W, H, 34, 1)
# finestres de paper: equip local | resultat | equip visitant
TEAM_W, SCORE_W, GAP = 300, 200, 12
x = 22
wins = []
for w in (TEAM_W, SCORE_W, TEAM_W):
    wins.append((x, 20, x + w, H - 22))
    x += w + GAP
for i, (x0, y0, x1, y1) in enumerate(wins):
    pts = rrect_pts(x0, y0, x1, y1, 16, 0.9, 10 + i)
    # filet fosc del forat al metall (com l'interior del botó)
    fill(img, poly_mask(img.size, rrect_pts(x0 - 3, y0 - 3, x1 + 3, y1 + 3, 18, 0.9, 10 + i)), STEEL_DARK)
    m = poly_mask(img.size, pts)
    paper_texture(img, m, PAPER, 20 + i)
    # ombra interior a dalt (el paper queda enfonsat dins el marc)
    top = poly_mask(img.size, rrect_pts(x0, y0, x1, y0 + 14, 8, 0.6, 30 + i)).filter(ImageFilter.GaussianBlur(4 * S))
    tm = Image.fromarray((np.asarray(top, np.float32) * np.asarray(m, np.float32) / 255 * 0.35).astype(np.uint8))
    fill(img, tm, PAPER_SHADE, 1.0) if False else img.alpha_composite(Image.merge("RGBA", (*Image.new("RGB", img.size, PAPER_SHADE).split(), tm)))
    outline(img, pts, INK, 3)
# cargols a les cantonades
for rx, ry in ((14, 14), (W - 14, 14), (14, H - 14), (W - 14, H - 14)):
    rivet(img, rx, ry, 7)
save(img, "hud_board")

# ===================== FRANJA DE COLOR D'EQUIP (es tenyeix) =====================
# Va dins la finestra de l'equip, al costat de fora, darrere de l'escut
BW, BH = 120, 88
img = new(BW, BH)
pts = rrect_pts(3, 3, BW - 3, BH - 3, 14, 0.8, 41)
fill(img, poly_mask(img.size, pts), (255, 255, 255))
# ratlles de llapis suaus (textura de color pintat a mà)
d = ImageDraw.Draw(img)
for k in range(0, BW + BH, 14):
    d.line([(k * S, 0), ((k - BH) * S, BH * S)], fill=(232, 232, 232, 255), width=3 * S)
mm = poly_mask(img.size, pts)
arr = np.asarray(img).copy()
arr[..., 3] = np.minimum(arr[..., 3], np.asarray(mm))
img = Image.fromarray(arr)
save(img, "hud_band")
# contorn de la franja (no es tenyeix)
img = new(BW, BH)
outline(img, pts, INK, 3)
save(img, "hud_band_line")

# ===================== PESTANYA DEL RELLOTGE (metall amb cargols) =====================
CW, CH = 220, 50
img = steel_frame(CW, CH, 18, 51)
x0, y0, x1, y1 = 30, 11, CW - 30, CH - 12
fill(img, poly_mask(img.size, rrect_pts(x0 - 2, y0 - 2, x1 + 2, y1 + 2, 10, 0.6, 52)), STEEL_DARK)
fill(img, poly_mask(img.size, rrect_pts(x0, y0, x1, y1, 9, 0.6, 53)), INK_DARK)
rivet(img, 16, CH / 2, 6)
rivet(img, CW - 16, CH / 2, 6)
save(img, "hud_clock")

# ===================== CARTELL GRAN (GOL!, DESCANS...) =====================
# Paper crema amb contorn, i una franja de color (tenyible) a sota del text
BW2, BH2 = 820, 230
img = new(BW2, BH2)
pts = rrect_pts(8, 8, BW2 - 8, BH2 - 8, 30, 1.6, 61)
# ombra plana desplaçada (com les il·lustracions)
sh = [(px + 8 * S, py + 10 * S) for px, py in pts]
fill(img, poly_mask(img.size, sh), (20, 26, 38))
paper_texture(img, poly_mask(img.size, pts), PAPER, 62)
outline(img, pts, INK, 4)
save(img, "hud_banner")
img = new(BW2, BH2)
band = rrect_pts(40, BH2 - 70, BW2 - 40, BH2 - 34, 14, 0.8, 63)
fill(img, poly_mask(img.size, band), (255, 255, 255))
outline(img, band, INK, 3)
save(img, "hud_banner_band")
print("ok")
