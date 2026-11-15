"""Rake HUD concept renderer (dark forest-monster direction).

The HUD is authored in 1920x1080 layout units and rendered at S=0.5 into a
960x540 canvas; the preview window upscales it with nearest-neighbour, which
gives the coarse pixel blending for free and keeps the frame cheap.

No paper, tape, cassettes or handwriting: bone, ember, blood, claw marks.
"""

import math
import os
import random

from PIL import Image, ImageDraw, ImageFilter, ImageFont

W, H = 1920, 1080
S = 0.5
CW, CH = int(W * S), int(H * S)

INK = (6, 8, 7, 235)
BONE = (216, 208, 190, 240)
BONE_DIM = (150, 145, 132, 175)
EMBER = (198, 118, 36, 235)
MOSS = (86, 108, 82, 210)

FONT_DIR = r"C:\Windows\Fonts"
_font_cache = {}


def _font(name, size):
    key = (name, int(size * S))
    if key in _font_cache:
        return _font_cache[key]
    try:
        f = ImageFont.truetype(os.path.join(FONT_DIR, name), max(6, int(size * S)))
    except Exception:
        f = ImageFont.load_default()
    _font_cache[key] = f
    return f


def sc(v):
    return v * S


def sc_pt(p):
    return (p[0] * S, p[1] * S)


class SDraw(object):
    """ImageDraw proxy that converts layout units to canvas pixels."""

    def __init__(self, d):
        self.d = d

    def polygon(self, pts, fill=None, outline=None):
        self.d.polygon([sc_pt(p) for p in pts], fill=fill, outline=outline)

    def line(self, pts, fill=None, width=1, joint=None):
        self.d.line([sc_pt(p) for p in pts], fill=fill, width=max(1, int(width * S)), joint=joint)

    def arc(self, box, start, end, fill=None, width=1):
        self.d.arc([sc(v) for v in box], start, end, fill=fill, width=max(1, int(width * S)))

    def ellipse(self, box, fill=None, outline=None, width=1):
        self.d.ellipse([sc(v) for v in box], fill=fill, outline=outline, width=max(1, int(width * S)))

    def rounded_rectangle(self, box, radius=0, fill=None, outline=None, width=1):
        self.d.rounded_rectangle([sc(v) for v in box], radius=sc(radius), fill=fill,
                                 outline=outline, width=max(1, int(width * S)))

    def text(self, xy, text, font=None, fill=None, anchor=None):
        self.d.text(sc_pt(xy), text, font=font, fill=fill, anchor=anchor)

    def textlength(self, text, font=None):
        return self.d.textlength(text, font=font) / S


def tracked(d, xy, text, fnt, fill, tracking=4, anchor="ls"):
    x, y = xy
    widths = [d.textlength(ch, font=fnt) for ch in text]
    total = sum(widths) + tracking * max(0, len(text) - 1)
    if anchor[0] == "m":
        x -= total / 2.0
    elif anchor[0] == "r":
        x -= total
    for ch, w in zip(text, widths):
        d.text((x, y), ch, font=fnt, fill=fill, anchor=anchor)
        x += w + tracking
    return total


def organic(cx, cy, w, h, jitter=5.0, points=18, seed=7):
    rnd = random.Random(seed)
    pts = []
    for i in range(points):
        a = 2 * math.pi * i / points
        j = 1.0 + rnd.uniform(-jitter, jitter) / 100.0
        pts.append((cx + math.cos(a) * w * 0.5 * j, cy + math.sin(a) * h * 0.5 * j))
    return pts


def _paste(im, local, x, y):
    w = max(1, int(local.width * S))
    h = max(1, int(local.height * S))
    im.alpha_composite(local.resize((w, h), Image.BOX), (int(x * S), int(y * S)))


def _shadow_poly(im, pts, pad=46, blur=13, alpha=150):
    xs = [p[0] for p in pts]
    ys = [p[1] for p in pts]
    x0 = (min(xs) - pad) * S
    y0 = (min(ys) - pad) * S
    x1 = (max(xs) + pad) * S
    y1 = (max(ys) + pad) * S
    w, h = max(2, int(x1 - x0)), max(2, int(y1 - y0))
    sh = Image.new("RGBA", (w, h), (0, 0, 0, 0))
    ImageDraw.Draw(sh).polygon([(p[0] * S - x0, p[1] * S - y0) for p in pts], fill=(0, 0, 0, alpha))
    im.alpha_composite(sh.filter(ImageFilter.GaussianBlur(max(2, int(blur * S)))), (int(x0), int(y0)))


def plate(im, d, box, color, jitter=5, seed=7, shadow=True):
    x0, y0, x1, y1 = box
    cx, cy = (x0 + x1) / 2.0, (y0 + y1) / 2.0
    pts = organic(cx, cy, x1 - x0, y1 - y0, jitter, 20, seed)

    if shadow:
        _shadow_poly(im, pts)

    d.polygon(pts, fill=color)
    rnd = random.Random(seed)
    for i in range(0, len(pts), 3):
        p = pts[i]
        d.line([p, (p[0] + rnd.uniform(-16, 16), p[1] - 0.2)], fill=(255, 255, 255, 10), width=1)


def vein(d, x, y, angle, length, width, color, depth=2, seed=1):
    rnd = random.Random(seed)
    px, py = x, y
    steps = max(3, int(length / 14))
    a = angle
    for i in range(steps):
        a += rnd.uniform(-0.30, 0.30)
        nx = px + math.cos(a) * 14
        ny = py + math.sin(a) * 14
        w = max(1.0, width * (1.0 - i / steps) + rnd.uniform(-0.4, 0.4))
        d.line([(px, py), (nx, ny)], fill=color, width=w)
        px, py = nx, ny
        if depth > 0 and i > 1 and rnd.random() < 0.22:
            vein(d, px, py, a + rnd.choice([-0.7, 0.7]), length * 0.45, w * 0.6, color, depth - 1, seed + i)


def ember_arc(im, d, box, start, end, width, color, glow=9):
    if end <= start:
        return
    x0, y0, x1, y1 = [int(v) - glow - 4 for v in box]
    w, h = max(2, x1 - x0), max(2, y1 - y0)
    local = Image.new("RGBA", (w, h), (0, 0, 0, 0))
    ld = ImageDraw.Draw(local)
    ld.arc([box[0] - x0, box[1] - y0, box[2] - x0, box[3] - y0], start, end,
           fill=color, width=width + 4)
    _paste(im, local.filter(ImageFilter.GaussianBlur(glow)), x0, y0)
    d.arc(box, start, end, fill=color, width=width)


# ------------------------------------------------------------------ glyphs

def glyph_claw(d, cx, cy, s, color):
    for k in (-1, 0, 1):
        d.line([(cx + k * s * 0.32 - s * 0.18, cy - s * 0.55),
                (cx + k * s * 0.32 + s * 0.14, cy + s * 0.55)],
               fill=color, width=max(2, int(s * 0.13)))


def glyph_paw(d, cx, cy, s, color):
    d.ellipse([cx - s * 0.22, cy - s * 0.05, cx + s * 0.22, cy + s * 0.42], fill=color)
    for dx, dy, r in ((-0.34, -0.34, 0.13), (0.0, -0.46, 0.14), (0.34, -0.34, 0.13)):
        d.ellipse([cx + dx * s - r * s, cy + dy * s - r * s,
                   cx + dx * s + r * s, cy + dy * s + r * s], fill=color)


def glyph_eye(d, cx, cy, s, color):
    wid = max(2, int(s * 0.1))
    d.arc([cx - s * 0.5, cy - s * 0.36, cx + s * 0.5, cy + s * 0.36], 200, 340, fill=color, width=wid)
    d.arc([cx - s * 0.5, cy - s * 0.36, cx + s * 0.5, cy + s * 0.36], 20, 160, fill=color, width=wid)
    d.ellipse([cx - s * 0.11, cy - s * 0.11, cx + s * 0.11, cy + s * 0.11], fill=color)


def glyph_leap(d, cx, cy, s, color):
    wid = max(2, int(s * 0.13))
    for k in (-1, 1):
        d.line([(cx + k * s * 0.1, cy - s * 0.45), (cx + k * s * 0.42, cy), (cx + k * s * 0.1, cy + s * 0.45)],
               fill=color, width=wid, joint="curve")


def glyph_trap(d, cx, cy, s, color):
    d.line([(cx - s * 0.4, cy - s * 0.4), (cx + s * 0.4, cy + s * 0.4)], fill=color, width=2)
    d.line([(cx + s * 0.4, cy - s * 0.4), (cx - s * 0.4, cy + s * 0.4)], fill=color, width=2)


# ------------------------------------------------------------------ masks

VIGNETTE = None
BLOOD_MASK = None
HALO = None


def _build_masks():
    global VIGNETTE, BLOOD_MASK
    if VIGNETTE is not None:
        return

    v = Image.new("L", (CW, CH), 0)
    d = ImageDraw.Draw(v)
    for i in range(40):
        t = i / 40.0
        inset = int((1 - t) * 520 * S)
        d.rounded_rectangle([inset, inset, CW - inset, CH - inset], radius=int(240 * S),
                            outline=int(120 * t ** 2.2), width=max(1, int(18 * S)))
    VIGNETTE = v.filter(ImageFilter.GaussianBlur(int(34 * S)))

    m = Image.new("L", (CW, CH), 0)
    d = ImageDraw.Draw(m)
    for i in range(32):
        t = i / 32.0
        inset = int((1 - t) * 460 * S)
        d.rounded_rectangle([inset, inset, CW - inset, CH - inset], radius=int(200 * S),
                            outline=int(255 * t ** 1.6), width=max(1, int(26 * S)))
    BLOOD_MASK = m.filter(ImageFilter.GaussianBlur(int(30 * S)))


def halo_image():
    global HALO
    if HALO is None:
        size = int(620 * S)
        HALO = Image.new("RGBA", (size, size), (0, 0, 0, 0))
        pad = int(130 * S)
        ImageDraw.Draw(HALO).ellipse([pad, pad, size - pad, size - pad], fill=(4, 6, 5, 200))
        HALO = HALO.filter(ImageFilter.GaussianBlur(int(95 * S)))
    return HALO


# ------------------------------------------------------------------ pieces

def draw_health(im, d, hp, max_hp, t):
    ratio = max(0.0, min(1.0, hp / float(max_hp)))
    x0, y0, x1, y1 = 610, 928, 1310, 1050
    plate(im, d, (x0, y0, x1, y1), (8, 10, 9, 218), 6, 31)

    wy = 1012
    d.rounded_rectangle([x0 + 34, wy - 14, x1 - 34, wy + 14], radius=12, fill=(18, 8, 7, 235))
    fill_w = int((x1 - x0 - 68) * ratio)
    if fill_w > 4:
        pulse = 0.72 + 0.28 * math.sin(t * 4.2)
        col = (int(150 * pulse), int(26 * pulse), int(18 * pulse), 240)
        d.rounded_rectangle([x0 + 34, wy - 9, x0 + 34 + fill_w, wy + 9], radius=8, fill=col)

        glow = Image.new("RGBA", (int(fill_w) + 60, 70), (0, 0, 0, 0))
        ImageDraw.Draw(glow).rounded_rectangle([30, 23, 30 + fill_w, 47], radius=10, fill=(150, 26, 18, 60))
        _paste(im, glow.filter(ImageFilter.GaussianBlur(12)), x0 + 4, wy - 35)

    for i in range(11):
        rx = x0 + 44 + i * ((x1 - x0 - 88) / 10.0)
        d.polygon([(rx, wy - 14), (rx + 6, wy - 23), (rx + 11, wy - 13)], fill=(190, 182, 164, 180))
        d.polygon([(rx + 2, wy + 14), (rx + 7, wy + 23), (rx + 12, wy + 13)], fill=(150, 145, 132, 140))

    tracked(d, (x0 + 30, y0 + 30), "THE RAKE", _font("bahnschrift.ttf", 20), BONE_DIM, 8)
    tracked(d, (x1 - 30, y0 + 30), "%d / %d" % (hp, max_hp), _font("bahnschrift.ttf", 23), BONE, 3, anchor="rs")

    if ratio < 0.6:
        strength = int((0.6 - ratio) * 255)
        for i in range(7):
            rnd = random.Random(i * 17 + 3)
            side = -1 if i % 2 == 0 else 1
            sx = 960 + side * rnd.uniform(220, 300)
            vein(d, sx, wy, -math.pi / 2 + side * rnd.uniform(0.2, 0.9),
                 rnd.uniform(90, 210), 3.2, (110, 22, 16, strength), 2, i + 11)


def draw_phase(im, d, night, night_no, time_left, time_total, t):
    cx, cy, r = 1752, 110, 52
    accent = EMBER if night else (150, 176, 140)

    im.alpha_composite(halo_image(), (int((cx - 310) * S), int((cy - 300) * S)))

    d.ellipse([cx - r - 12, cy - r - 12, cx + r + 12, cy + r + 12], outline=(220, 214, 198, 26), width=2)
    d.ellipse([cx - r, cy - r, cx + r, cy + r], outline=(220, 214, 198, 42), width=2)

    frac = 0.0 if time_total <= 0 else max(0.0, min(1.0, time_left / float(time_total)))
    ember_arc(im, d, [cx - r - 6, cy - r - 6, cx + r + 6, cy + r + 6], -90, -90 + 360 * frac, 5, accent, 12)

    moon = Image.new("RGBA", (160, 160), (0, 0, 0, 0))
    md = ImageDraw.Draw(moon)
    md.ellipse([30, 30, 130, 130], fill=(216, 208, 190, 240))
    md.ellipse([62, 24, 150, 118], fill=(0, 0, 0, 0))
    _paste(im, moon, cx - 80, cy - 80)

    for k in range(3):
        d.line([(cx - 26 + k * 9, cy - 22), (cx - 8 + k * 9, cy + 16)], fill=(40, 34, 28, 60), width=1)

    tracked(d, (cx, 32), ("NIGHT %d / 3" if night else "DAY %d / 3") % night_no,
            _font("bahnschrift.ttf", 21), accent, 6, anchor="ms")

    mins = int(max(0, time_left)) // 60
    secs = int(max(0, time_left)) % 60
    tracked(d, (cx, 206), "%02d:%02d" % (mins, secs), _font("bahnschrift.ttf", 46), BONE, 3, anchor="ms")

    tracked(d, (cx, 258), "THE HUNT IS ON" if night else "THE RAKE SLEEPS",
            _font("bahnschrift.ttf", 16),
            (176, 168, 150, 170) if night else (170, 186, 160, 180), 5, anchor="ms")


def draw_abilities(im, d, abilities, t):
    plate(im, d, (92, 884, 600, 1044), (7, 9, 8, 195), 9, 77)
    glyphs = {"GRAB": glyph_claw, "ANIMAL": glyph_paw, "CAMERA": glyph_eye, "LEAP": glyph_leap}
    f_name = _font("bahnschrift.ttf", 16)

    for i, (name, cd, total) in enumerate(abilities):
        cx = 158 + i * 124
        cy = 936
        s = 78
        ready = cd <= 0.01

        pts = organic(cx, cy, s, s, 9.0, 8, seed=40 + i)
        d.polygon(pts, fill=(9, 11, 10, 225))
        d.line(pts + [pts[0]], fill=(212, 204, 188, 60), width=2)

        if ready:
            ember_arc(im, d, [cx - s // 2 - 5, cy - s // 2 - 5, cx + s // 2 + 5, cy + s // 2 + 5],
                      -90, 270, 3, (150, 88, 26, 160), 8)
        else:
            frac = 0.0 if total <= 0 else max(0.0, min(1.0, cd / float(total)))
            ember_arc(im, d, [cx - s // 2 - 5, cy - s // 2 - 5, cx + s // 2 + 5, cy + s // 2 + 5],
                      -90, -90 + 360 * (1 - frac), 4, EMBER, 9)

        glyphs[name](d, cx, cy, 34, BONE if ready else BONE_DIM)

        tracked(d, (cx, cy + 62), name, f_name, (214, 206, 190, 235) if ready else (168, 162, 148, 200), 2, anchor="ms")
        tracked(d, (cx, cy + 88), "READY" if ready else "%ds" % int(math.ceil(cd)), f_name,
                EMBER if ready else (150, 120, 84, 200), 2, anchor="ms")


def draw_markers(d, markers, t):
    f_dist = _font("bahnschrift.ttf", 16)

    for mx, my, kind, dist in markers:
        breath = 0.5 + 0.5 * math.sin(t * 2.6 + mx * 0.01)
        if kind == "prey":
            col = (int(140 + 40 * breath), 26, 18, 230)
            for k in range(3):
                a0 = -90 + k * 120 + 12
                d.arc([mx - 54, my - 54, mx + 54, my + 54], a0, a0 + 96, fill=col, width=2)
            d.arc([mx - 26, my - 14, mx + 26, my + 14], 200, 340, fill=(214, 206, 190, 200), width=2)
            d.arc([mx - 26, my - 14, mx + 26, my + 14], 20, 160, fill=(214, 206, 190, 200), width=2)
            d.ellipse([mx - 3, my - 3, mx + 3, my + 3], fill=(190, 40, 28, 230))
            tracked(d, (mx, my + 76), "%d m" % dist, f_dist, BONE_DIM, 2, anchor="ms")
        elif kind == "animal":
            for k in (-1, 1):
                d.line([(mx + k * 16, my - 14), (mx + k * 4, my), (mx + k * 16, my + 14)],
                       fill=(196, 188, 172, 165), width=2)
            tracked(d, (mx, my + 34), "%d m" % dist, f_dist, (150, 145, 132, 150), 2, anchor="ms")
        else:
            glyph_trap(d, mx, my, 20, (150, 145, 132, 170))
            tracked(d, (mx, my + 32), "%d m" % dist, f_dist, (150, 145, 132, 140), 2, anchor="ms")


def draw_damage(im, d, damage):
    if damage <= 0.01:
        return

    a = int(170 * min(1.0, damage))
    red = Image.new("RGBA", (CW, CH), (110, 12, 8, 0))
    red.putalpha(BLOOD_MASK.point(lambda p: p * a // 255))
    im.alpha_composite(red)

    if damage > 0.35:
        rnd = random.Random(7)
        for _ in range(9):
            sx = rnd.choice([rnd.uniform(60, 460), rnd.uniform(W - 460, W - 60)])
            sy = rnd.uniform(80, H - 80)
            r = rnd.uniform(10, 46)
            d.ellipse([sx - r, sy - r * 0.8, sx + r, sy + r * 0.8], fill=(120, 16, 10, 120))
            for _ in range(3):
                rr = r * rnd.uniform(0.15, 0.4)
                ox, oy = rnd.uniform(-r * 2, r * 2), rnd.uniform(-r * 2, r * 2)
                d.ellipse([sx + ox - rr, sy + oy - rr, sx + ox + rr, sy + oy + rr], fill=(110, 14, 9, 110))


def render_overlay(state, t):
    _build_masks()

    im = Image.new("RGBA", (CW, CH), (0, 0, 0, 0))
    d = SDraw(ImageDraw.Draw(im))

    if state.get("damage", 0) > 0.01:
        draw_damage(im, d, state["damage"])

    draw_abilities(im, d, state["abilities"], t)
    draw_health(im, d, state["hp"], state["max_hp"], t)
    draw_phase(im, d, state["night"], state["night_no"], state["time_left"], state["time_total"], t)
    draw_markers(d, state.get("markers", []), t)

    v = Image.new("RGBA", (CW, CH), (0, 0, 0, 0))
    v.putalpha(VIGNETTE)
    im.alpha_composite(v)
    return im


def prepare_background(path_or_image):
    if isinstance(path_or_image, str):
        img = Image.open(path_or_image)
    else:
        img = path_or_image
    return img.convert("RGBA").resize((CW, CH), Image.LANCZOS)


def compose(state, background, t):
    """background: RGBA already at CWxCH (see prepare_background)."""
    base = background.copy()
    base.alpha_composite(render_overlay(state, t))
    return base
