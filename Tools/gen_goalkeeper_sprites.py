"""Porter d'hoquei patins v2 — mateix estil que els sprites del jugador.
Genera: gk_shadow, gk_shadow_save, gk_base (guardes dretes), gk_base_save (papallona),
gk_jersey (peto + espatlleres), gk_head (casc amb reixa), gk_gloves (manyopla a l'estic),
gk_glove (guant gran) i gk_glove_save (guant estirat).
Tot 1024x1024, centrat igual que el jugador (pivot 0.5, 0.5)."""
import sys
import numpy as np
from PIL import Image, ImageDraw, ImageFilter

SRC, OUT = sys.argv[1], sys.argv[2]
S = 2                      # supermostreig
N = 1024 * S

# Paleta treta dels sprites del jugador
DARK = (57, 51, 49)        # pantaló / corretges
TAN = (164, 137, 100)      # patins
TAN_D = (125, 105, 81)
UNDER = (88, 88, 88)       # cara de sota de la samarreta
PAD = (241, 237, 229)      # guardes (crema)
PAD_SIDE = (150, 143, 132)
PAD_GROOVE = (196, 189, 176)
RIM = (52, 52, 55)         # vora fosca dels guants


def load(name):
    return Image.open(f"{SRC}/{name}.png").convert("RGBA")


def new(w=N, h=N):
    return Image.new("RGBA", (w, h), (0, 0, 0, 0))


def mask_of(size, fn):
    m = Image.new("L", size, 0)
    fn(ImageDraw.Draw(m))
    return m


def brush(size, strength, seed, horiz=True):
    """Textura de pinzellades suaus, com la de la samarreta del jugador."""
    w, h = size
    r = np.random.default_rng(seed)
    im = Image.new("L", size, 128)
    d = ImageDraw.Draw(im)
    for _ in range(int(w * h / (60 * S) ** 2) + 30):
        x, y = r.uniform(0, w), r.uniform(0, h)
        L = r.uniform(30, 110) * S
        a = r.uniform(-0.35, 0.35) + (0 if horiz else np.pi / 2)
        dx, dy = np.cos(a) * L, np.sin(a) * L * 0.6
        d.line([(x - dx, y - dy), (x, y + r.uniform(-8, 8) * S), (x + dx, y + dy)],
               fill=int(r.choice([100, 156])), width=int(r.uniform(6, 16) * S), joint="curve")
    im = im.filter(ImageFilter.GaussianBlur(7 * S))
    return np.clip((np.asarray(im, np.float32) - 128) / 28.0, -1, 1) * strength


def paint(img, m, rgb, noise=0, seed=0, grad=0.0, y0=0, y1=None, horiz=True, alpha=1.0):
    """Omple la màscara m amb color + degradat vertical (llum de dalt) + pinzellada."""
    w, h = img.size
    arr = np.zeros((h, w, 4), np.float32)
    arr[..., :3] = rgb
    if grad:
        y1 = y1 if y1 is not None else h
        t = np.clip((np.arange(h, dtype=np.float32) - y0) / max(1, y1 - y0), 0, 1)
        arr[..., :3] -= t[:, None, None] * grad
    if noise:
        arr[..., :3] += brush((w, h), noise, seed, horiz)[..., None]
    arr = np.clip(arr, 0, 255)
    arr[..., 3] = np.asarray(m, np.float32) * alpha
    img.alpha_composite(Image.fromarray(arr.astype(np.uint8), "RGBA"))


def solid(img, m, rgb, alpha=1.0):
    paint(img, m, rgb, alpha=alpha)


def down(img):
    return img.resize((img.width // S, img.height // S), Image.LANCZOS)


def save(img, name):
    img.save(f"{OUT}/{name}.png")


# =====================================================================
# GUARDA (una sola, vertical: genoll a dalt, patí a baix). Es reutilitza
# per a la postura dreta i, girada, per a la papallona.
# =====================================================================
def pad_image(seed, mirror=False):
    """Guarda d'hoquei patins: cara PLANA de microfibra (no rotllos de gel), ala ampla al genoll,
    plecs horitzontals de flexió, ribet fosc, dibuix imprès i peça de peu que tapa el patí."""
    W, H = 140 * S, 256 * S
    img = new(W + 40 * S, H + 50 * S)
    ox, oy = 20 * S, 6 * S

    # patí de 4 rodes (en sobresurten les rodes pels costats i la punta)
    for wy in (oy + H - 58 * S, oy + H - 14 * S):
        for wx in (ox + 10 * S, ox + W - 30 * S):
            solid(img, mask_of(img.size, lambda d: d.rounded_rectangle((wx, wy, wx + 20 * S, wy + 30 * S), radius=8 * S, fill=255)), TAN_D)
    solid(img, mask_of(img.size, lambda d: d.rounded_rectangle((ox + 30 * S, oy + H - 20 * S, ox + W - 30 * S, oy + H + 26 * S), radius=20 * S, fill=255)), TAN)

    # silueta: ala del genoll més ampla a dalt, cos recte, peu arrodonit
    def outline(d, dy=0, inset=0):
        i = inset
        tw = 8 * S
        pts = [(ox - tw + i, oy + 40 * S + dy), (ox + i, oy + 74 * S + dy),
               (ox + 10 * S + i, oy + H - 40 * S + dy), (ox + W - 10 * S - i, oy + H - 40 * S + dy),
               (ox + W - i, oy + 74 * S + dy), (ox + W + tw - i, oy + 40 * S + dy)]
        d.polygon(pts, fill=255)
        d.ellipse((ox - tw + i, oy + i + dy, ox + W + tw - i, oy + 84 * S + dy), fill=255)
        d.rounded_rectangle((ox + 10 * S + i, oy + H - 96 * S + dy, ox + W - 10 * S - i, oy + H - 6 * S + dy - i), radius=34 * S, fill=255)

    solid(img, mask_of(img.size, lambda d: outline(d, dy=16 * S)), PAD_SIDE)   # gruix
    solid(img, mask_of(img.size, lambda d: outline(d)), DARK)                  # ribet fosc
    face = mask_of(img.size, lambda d: outline(d, inset=7 * S))                # microfibra plana
    paint(img, face, PAD, noise=7, seed=seed, grad=22, y0=oy, y1=oy + H, horiz=True)
    fa = np.asarray(face, np.float32) / 255

    def on_face(fn, rgb, a=255, blur=0):
        m = mask_of(img.size, fn)
        if blur:
            m = m.filter(ImageFilter.GaussianBlur(blur * S))
        solid(img, Image.fromarray((np.asarray(m, np.float32) * fa * a / 255).astype(np.uint8)), rgb)

    # dibuix imprès (franges en diagonal, com les guardes amb grafisme)
    sg = -1 if mirror else 1
    for k, wdt in ((0, 26), (1, 12)):
        y = oy + 128 * S + k * 40 * S
        on_face(lambda d: d.line((ox - 10 * S, y + sg * 40 * S, ox + W + 10 * S, y - sg * 40 * S), fill=255, width=wdt * S), (70, 70, 76), 235)
    # plecs de flexió horitzontals (genoll i turmell), amb llum a sota
    for y in (oy + 86 * S, oy + H - 98 * S):
        on_face(lambda d: d.line((ox, y, ox + W, y), fill=255, width=6 * S), PAD_GROOVE)
        on_face(lambda d: d.line((ox, y + 7 * S, ox + W, y + 7 * S), fill=255, width=5 * S), (255, 255, 255), 150, blur=2)
    # bloc del genoll una mica bombat
    on_face(lambda d: d.ellipse((ox + 20 * S, oy + 14 * S, ox + W - 20 * S, oy + 72 * S), fill=255), (255, 255, 255), 140, blur=8)
    # pespunt central
    cxp = ox + W / 2
    for y in range(int(oy + 100 * S), int(oy + H - 104 * S), 16 * S):
        on_face(lambda d: d.line((cxp, y, cxp, y + 8 * S), fill=255, width=3 * S), (185, 178, 165))
    return img


def paste_center(dst, src, cx, cy, angle=0):
    if angle:
        src = src.rotate(angle, resample=Image.BICUBIC, expand=True)
    dst.alpha_composite(src, (int(cx * S - src.width / 2), int(cy * S - src.height / 2)))


# ---------------- BASE: guardes dretes (paret) ----------------
img = new()
padL, padR = pad_image(11, mirror=False), pad_image(12, mirror=True)
paste_center(img, padL, 442, 704)
paste_center(img, padR, 582, 704)
base = down(img)
save(base, "gk_base")

# ---------------- BASE de PARADA: papallona (guardes obertes a terra) ----------------
img = new()
# genolls al centre, puntes cap enfora; una mica inclinades cap avall
# genolls junts al centre, puntes cap als costats, tot per DAVANT del cos (fent paret a la bola)
paste_center(img, padL, 384, 318, angle=-98)
paste_center(img, padR, 640, 318, angle=98)
save(down(img), "gk_base_save")

# ---------------- OMBRES ----------------
sh = load("player_shadow")
bb = sh.getbbox()
cx, cy = (bb[0] + bb[2]) / 2, (bb[1] + bb[3]) / 2
crop = sh.crop(bb)


def shadow(sx, sy, dy, name):
    w, h = int(crop.width * sx), int(crop.height * sy)
    c = crop.resize((w, h), Image.LANCZOS)
    o = Image.new("RGBA", (1024, 1024), (0, 0, 0, 0))
    o.paste(c, (int(cx - w / 2), int(cy - h / 2 + dy)))
    save(o, name)


shadow(1.30, 1.18, 30, "gk_shadow")
shadow(1.85, 1.05, -120, "gk_shadow_save")

# ---------------- SAMARRETA: peto ample + espatlleres de porter ----------------
j = load("player_jersey")
bb = j.getbbox()
crop = j.crop(bb)
w, h = int(crop.width * 1.12), int(crop.height * 1.05)
crop = crop.resize((w, h), Image.LANCZOS)
torso = Image.new("RGBA", (1024, 1024), (0, 0, 0, 0))
torso.paste(crop, (int((bb[0] + bb[2]) / 2 - w / 2), int(bb[1] - 6)))

img = new()
for sgn in (-1, 1):
    # dues peces d'espatllera superposades (com els "floaters" del peto de porter)
    for k, (dx, dy, rw, rh) in enumerate(((150, 470, 62, 82), (168, 538, 52, 64))):
        x = 512 + sgn * dx
        b = (x - rw, dy - rh, x + rw, dy + rh)
        und = mask_of((N, N), lambda d: d.ellipse([v * S for v in (b[0], b[1] + 16, b[2], b[3] + 16)], fill=255))
        solid(img, und, UNDER)
        top = mask_of((N, N), lambda d: d.ellipse([v * S for v in b], fill=255))
        paint(img, top, (248, 248, 248), noise=12, seed=40 + k + (sgn > 0) * 5, grad=40, y0=b[1] * S, y1=b[3] * S)
        dd = ImageDraw.Draw(img)
        dd.arc([v * S for v in (b[0] + 14, b[1] + 14, b[2] - 14, b[3] - 10)], 200, 340,
               fill=(206, 206, 206, 255), width=5 * S)
shoulders = down(img)
jersey = Image.new("RGBA", (1024, 1024), (0, 0, 0, 0))
jersey.alpha_composite(shoulders)
jersey.alpha_composite(torso)
# coll del peto: una vora corbada sota el casc (es veu quan el cap gira)
img = new()
dd = ImageDraw.Draw(img)
dd.arc([v * S for v in (400, 380, 624, 600)], 205, 335, fill=(200, 200, 200, 255), width=10 * S)
jersey.alpha_composite(down(img))
save(jersey, "gk_jersey")

# ---------------- CASC de porter: closca + reixa + placa posterior ----------------
img = new()
HX, HY = 508, 500
SHELL = (244, 244, 246)
CAGE = (38, 38, 42)
CAGE_HI = (120, 122, 128)
d = ImageDraw.Draw(img)

# reixa (davant = amunt): sobresurt per davant de la closca
cage_out = [v * S for v in (HX - 96, HY - 182, HX + 96, HY + 10)]
cage_m = mask_of((N, N), lambda d: d.ellipse(cage_out, fill=255))
# només la part de davant de l'el·lipse
cut = mask_of((N, N), lambda d: d.rectangle((0, 0, N, (HY - 70) * S), fill=255))
cm = Image.fromarray((np.asarray(cage_m, np.float32) * np.asarray(cut, np.float32) / 255).astype(np.uint8))
# forat entre barres (la pista es veu a través): pintem barres sobre transparent
bars = new()
bd = ImageDraw.Draw(bars)
for off, col in ((5, (20, 20, 22, 255)), (0, CAGE + (255,))):
    o = off * S
    bd.arc([cage_out[0], cage_out[1] + o, cage_out[2], cage_out[3] + o], 192, 348, fill=col, width=15 * S)
    bd.arc([cage_out[0] + 30 * S, cage_out[1] + 34 * S + o, cage_out[2] - 30 * S, cage_out[3] - 20 * S + o],
           200, 340, fill=col, width=9 * S)
    # barres verticals (estil "cat-eye": la central doble)
    for xo in (-60, -30, -8, 8, 30, 60):
        x = (HX + xo) * S
        top_y = HY - 182 + (abs(xo) ** 2) / 66
        bd.line((x, top_y * S + o, x * 1 + 0, (HY - 62) * S + o), fill=col, width=8 * S)
# reflex metàl·lic a la vora de la reixa
bd.arc([cage_out[0] + 4 * S, cage_out[1] + 2 * S, cage_out[2] - 4 * S, cage_out[3]], 230, 300,
       fill=CAGE_HI + (255,), width=3 * S)
img.alpha_composite(bars)

# collarí (protector de coll, típic del porter d'hoquei patins): anell encoixinat al voltant del casc
cbox = (HX - 120, HY - 64, HX + 120, HY + 136)
solid(img, mask_of((N, N), lambda d: d.ellipse([v * S for v in (cbox[0], cbox[1] + 12, cbox[2], cbox[3] + 12)], fill=255)), (30, 30, 33))
cm_ = mask_of((N, N), lambda d: d.ellipse([v * S for v in cbox], fill=255))
paint(img, cm_, (66, 66, 72), noise=8, seed=62, grad=-18, y0=cbox[1] * S, y1=cbox[3] * S)
d = ImageDraw.Draw(img)
d.arc([v * S for v in (cbox[0] + 12, cbox[1] + 12, cbox[2] - 12, cbox[3] - 12)], 10, 170, fill=(110, 110, 118, 255), width=4 * S)

# placa posterior (a baix), peça separada de la closca, amb la corretja
back = (HX - 78, HY + 30, HX + 78, HY + 122)
bm = mask_of((N, N), lambda d: d.ellipse([v * S for v in back], fill=255))
solid(img, mask_of((N, N), lambda d: d.ellipse([v * S for v in (back[0], back[1] + 10, back[2], back[3] + 10)], fill=255)), UNDER)
paint(img, bm, (226, 226, 230), noise=8, seed=61, grad=50, y0=back[1] * S, y1=back[3] * S)

# closca principal (oval, una mica més llarga que ampla)
sh_box = (HX - 98, HY - 112, HX + 98, HY + 92)
solid(img, mask_of((N, N), lambda d: d.ellipse([v * S for v in (sh_box[0], sh_box[1] + 14, sh_box[2], sh_box[3] + 14)], fill=255)), UNDER)
sm = mask_of((N, N), lambda d: d.ellipse([v * S for v in sh_box], fill=255))
paint(img, sm, SHELL, noise=10, seed=60, grad=46, y0=sh_box[1] * S, y1=sh_box[3] * S)
sa = np.asarray(sm, np.float32) / 255


def on_shell(fn, rgb, a=255, blur=0):
    m = mask_of((N, N), fn)
    if blur:
        m = m.filter(ImageFilter.GaussianBlur(blur * S))
    solid(img, Image.fromarray((np.asarray(m, np.float32) * sa * a / 255).astype(np.uint8)), rgb)


# reflex de llum
on_shell(lambda d: d.ellipse([v * S for v in (HX - 70, HY - 96, HX - 4, HY - 34)], fill=255), (255, 255, 255), 200, blur=10)
# solcs de la closca (dos, de davant a darrere) i forats de ventilació
for sgn in (-1, 1):
    on_shell(lambda d: d.arc([v * S for v in (HX + sgn * 40 - 120, HY - 110, HX + sgn * 40 + 120, HY + 120)],
                             250 if sgn > 0 else 200, 340 if sgn > 0 else 290, fill=255, width=6 * S),
             (200, 200, 204))
    for k, vy in enumerate((HY + 16, HY + 44)):
        x = HX + sgn * (52 + k * 6)
        on_shell(lambda d: d.line([v * S for v in (x - sgn * 8, vy - 12, x + sgn * 8, vy + 12)], fill=255, width=8 * S),
                 (150, 150, 156))
# vora de la closca on comença la reixa (cinta fosca)
on_shell(lambda d: d.arc([v * S for v in (sh_box[0] + 4, sh_box[1] + 4, sh_box[2] - 4, sh_box[3])], 205, 335,
                         fill=255, width=10 * S), (70, 70, 76))
# laterals: cargols de la reixa
d = ImageDraw.Draw(img)
for sgn in (-1, 1):
    x, y = HX + sgn * 86, HY - 58
    d.ellipse([v * S for v in (x - 9, y - 9, x + 9, y + 9)], fill=(170, 172, 178, 255))
    d.ellipse([v * S for v in (x - 4, y - 4, x + 4, y + 4)], fill=(90, 92, 98, 255))
# corretja de la placa posterior
save(down(img), "gk_head")

# ---------------- GUANTS de porter d'hoquei patins: plans, de rebot (iguals a les dues mans) ----------------
def big_glove(scale=1.0, seed=80):
    """Guant pla de rebot: placa ampla encoixinada que tapa mà i canell, dits en tubs curts
    a dalt i puny fosc. Mirant cap amunt (dits = davant)."""
    W, H = int(150 * scale), int(168 * scale)
    g = new((W + 40) * S, (H + 40) * S)
    ox, oy = 20 * S, 14 * S
    Ws, Hs = W * S, H * S
    # puny / canell (fosc, a baix)
    solid(g, mask_of(g.size, lambda d: d.rounded_rectangle((ox + Ws * 0.18, oy + Hs * 0.70, ox + Ws * 0.82, oy + Hs + 8 * S), radius=16 * S, fill=255)), RIM)
    # placa: gruix, vora fosca i cara clara
    plate = (ox, oy, ox + Ws, oy + Hs * 0.84)
    solid(g, mask_of(g.size, lambda d: d.rounded_rectangle((plate[0], plate[1] + 12 * S, plate[2], plate[3] + 12 * S), radius=30 * S, fill=255)), (40, 40, 42))
    solid(g, mask_of(g.size, lambda d: d.rounded_rectangle(plate, radius=30 * S, fill=255)), RIM)
    inner = (plate[0] + 10 * S, plate[1] + 8 * S, plate[2] - 10 * S, plate[3] - 10 * S)
    fm = mask_of(g.size, lambda d: d.rounded_rectangle(inner, radius=22 * S, fill=255))
    paint(g, fm, (252, 252, 252), noise=10, seed=seed, grad=36, y0=inner[1], y1=inner[3])
    fa = np.asarray(fm, np.float32) / 255

    def on_face(fn, rgb, a=255, blur=0):
        m = mask_of(g.size, fn)
        if blur:
            m = m.filter(ImageFilter.GaussianBlur(blur * S))
        solid(g, Image.fromarray((np.asarray(m, np.float32) * fa * a / 255).astype(np.uint8)), rgb)

    # dits: 4 tubs curts a dalt
    fy = inner[1] + (inner[3] - inner[1]) * 0.36
    on_face(lambda d: d.line((inner[0], fy, inner[2], fy), fill=255, width=6 * S), (190, 190, 190))
    for k in (1, 2, 3):
        x = inner[0] + (inner[2] - inner[0]) * k / 4
        on_face(lambda d: d.line((x, inner[1], x, fy), fill=255, width=5 * S), (196, 196, 196))
    for k in range(4):
        x = inner[0] + (inner[2] - inner[0]) * (k + 0.4) / 4
        on_face(lambda d: d.line((x, inner[1] + 12 * S, x, fy - 12 * S), fill=255, width=7 * S), (255, 255, 255), 170, blur=3)
    # placa del palmell: reforç pla amb ribet
    on_face(lambda d: d.rounded_rectangle((inner[0] + 16 * S, fy + 16 * S, inner[2] - 16 * S, inner[3] - 14 * S),
                                          radius=14 * S, outline=255, width=5 * S), (205, 205, 205))
    d = ImageDraw.Draw(g)
    d.line((ox + Ws * 0.24, oy + Hs * 0.93, ox + Ws * 0.76, oy + Hs * 0.93), fill=(15, 15, 17, 255), width=3 * S)
    return g


# guant de la mà de l'estic (va dins de StickPivot, sobre el mànec)
img = new()
paste_center(img, big_glove(0.86, 71), 442, 316, angle=28)
save(down(img), "gk_gloves")

# guant de la mà lliure
img = new()
paste_center(img, big_glove(0.95, 80), 668, 352, angle=-14)
save(down(img), "gk_glove")

# parada: braç estirat cap al costat
img = new()
paste_center(img, big_glove(1.05, 82), 780, 196, angle=-40)
save(down(img), "gk_glove_save")
print("ok")
