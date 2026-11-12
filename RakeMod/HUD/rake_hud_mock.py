"""
Rake HUD mockup generator (iteration playground).

Run:  python rake_hud_mock.py
Out:  rake_hud_mock.png  (1920x1080)

Everything is procedural so it can be tweaked quickly: colours, positions,
labels and the amount of "scene" behind the HUD are defined at the top.
"""

from PIL import Image, ImageDraw, ImageFilter, ImageFont
import math
import random

W, H = 1920, 1080
OUT = "rake_hud_mock.png"

# ------------------------------------------------------------------ palette
RED = (188, 44, 32)
RED_DIM = (118, 30, 24)
RED_DEEP = (64, 16, 14)
CREAM = (236, 228, 210)
MUTED = (150, 142, 128)
DIM = (92, 86, 78)
PANEL = (10, 10, 13, 205)
PANEL_SOFT = (10, 10, 13, 150)
LINE = (120, 30, 24, 200)

FONT_DIR = "C:/Windows/Fonts/"
F_BLACK = FONT_DIR + "ariblk.ttf"
F_BOLD = FONT_DIR + "arialbd.ttf"
F_REG = FONT_DIR + "arial.ttf"
F_IMPACT = FONT_DIR + "impact.ttf"
F_MONO = FONT_DIR + "consola.ttf"


def font(path, size):
    return ImageFont.truetype(path, size)


def text(draw, xy, s, f, fill, anchor="la", tracking=0):
    """Draw text, optionally letter-spaced (small caps horror labels)."""
    if tracking <= 0:
        draw.text(xy, s, font=f, fill=fill, anchor=anchor)
        return draw.textlength(s, font=f)

    total = sum(draw.textlength(ch, font=f) for ch in s) + tracking * max(0, len(s) - 1)
    x, y = xy
    if anchor.startswith("m"):
        x -= total / 2.0
    elif anchor.startswith("r"):
        x -= total
    va = anchor[1] if len(anchor) > 1 else "a"
    for ch in s:
        draw.text((x, y), ch, font=f, fill=fill, anchor="l" + va)
        x += draw.textlength(ch, font=f) + tracking
    return total


def panel(draw, box, fill=PANEL, border=None, bw=1):
    draw.rectangle(box, fill=fill)
    if border is not None:
        draw.rectangle(box, outline=border, width=bw)


def hline(draw, x0, x1, y, fill, w=1):
    if x1 < x0:
        x0, x1 = x1, x0
    draw.rectangle((x0, y, x1, y + w - 1), fill=fill)


def vline(draw, x, y0, y1, fill, w=1):
    if y1 < y0:
        y0, y1 = y1, y0
    draw.rectangle((x, y0, x + w - 1, y1), fill=fill)


def segmented_bar(im, draw, box, ratio, color, seg=26, gap=2, alpha=255):
    """Solid bar with a handful of divider ticks - reads as a meter, not a barcode."""
    x0, y0, x1, y1 = box
    draw.rectangle(box, fill=(26, 24, 26, 220))
    fill_w = int((x1 - x0) * max(0.0, min(1.0, ratio)))
    if fill_w <= 0:
        return

    draw.rectangle((x0, y0, x0 + fill_w, y1), fill=color + (alpha,) if len(color) == 3 else color)
    # bright leading edge
    draw.rectangle((x0 + fill_w - 2, y0, x0 + fill_w, y1), fill=(245, 120, 95, 230))

    # one hairline every 20%
    step = (x1 - x0) / 5.0
    for i in range(1, 5):
        x = int(x0 + step * i)
        if x < x0 + fill_w:
            draw.rectangle((x, y0, x, y1 - 1), fill=(20, 6, 5, 210))


def dashed_ring(draw, cx, cy, r, color, width=2, dash=7, gap=5, start=0, end=360):
    a = start
    while a < end:
        draw.arc([cx - r, cy - r, cx + r, cy + r], a, min(a + dash, end), fill=color, width=width)
        a += dash + gap


def arrow_on_ring(draw, cx, cy, r, angle_deg, size=13, fill=RED, outline=(20, 8, 7)):
    a = math.radians(angle_deg)
    ca, sa = math.cos(a), math.sin(a)
    # triangle sitting on the ring and pointing at the centre
    tip = (cx + ca * (r - 2), cy + sa * (r - 2))
    base = (cx + ca * (r + size * 1.7), cy + sa * (r + size * 1.7))
    perp = (-sa, ca)
    p1 = (base[0] + perp[0] * size * 0.65, base[1] + perp[1] * size * 0.65)
    p2 = (base[0] - perp[0] * size * 0.65, base[1] - perp[1] * size * 0.65)
    draw.polygon([tip, p1, p2], fill=fill, outline=outline)


# ------------------------------------------------------------------ background
def build_background():
    img = Image.new("RGB", (W, H), (6, 8, 7))
    g = ImageDraw.Draw(img, "RGBA")

    # cold fog gradient
    for y in range(H):
        t = y / H
        c = (
            int(9 + 8 * (1 - t)),
            int(12 + 9 * (1 - t)),
            int(10 + 7 * (1 - t)),
        )
        g.line([(0, y), (W, y)], fill=c)

    # blurred tree trunks / trailer frame silhouettes
    trunks = Image.new("RGBA", (W, H), (0, 0, 0, 0))
    tg = ImageDraw.Draw(trunks)
    random.seed(7)
    for _ in range(26):
        x = random.randint(-80, W + 80)
        w = random.randint(18, 90)
        a = random.randint(28, 80)
        top = random.randint(-100, 220)
        tg.rectangle((x, top, x + w, H + 60), fill=(2, 4, 3, a))
    trunks = trunks.filter(ImageFilter.GaussianBlur(9))
    img = Image.alpha_composite(img.convert("RGBA"), trunks)

    # low ground fog
    fog = Image.new("RGBA", (W, H), (0, 0, 0, 0))
    fg = ImageDraw.Draw(fog)
    for i in range(9):
        y = H - 40 - i * 26
        fg.rectangle((0, y - 24, W, y + 24), fill=(120, 130, 122, 8))
    fog = fog.filter(ImageFilter.GaussianBlur(26))
    img = Image.alpha_composite(img, fog)

    # faint red glow behind the HUD (hunt energy)
    glow = Image.new("RGBA", (W, H), (0, 0, 0, 0))
    gg = ImageDraw.Draw(glow)
    gg.ellipse((W // 2 - 700, H - 330, W // 2 + 700, H + 260), fill=(120, 22, 16, 26))
    gg.ellipse((W // 2 - 420, H // 2 - 420, W // 2 + 420, H // 2 + 420), fill=(90, 18, 14, 10))
    glow = glow.filter(ImageFilter.GaussianBlur(120))
    img = Image.alpha_composite(img, glow)

    # embers
    embers = Image.new("RGBA", (W, H), (0, 0, 0, 0))
    eg = ImageDraw.Draw(embers)
    random.seed(21)
    for _ in range(70):
        x = random.randint(0, W)
        y = random.randint(0, H)
        r = random.choice([1, 1, 1, 2])
        a = random.randint(18, 70)
        eg.ellipse((x, y, x + r, y + r), fill=(210, 70, 45, a))
    img = Image.alpha_composite(img, embers)

    return img


# ------------------------------------------------------------------ HUD parts
def draw_top_left(draw):
    box = (28, 26, 512, 126)
    panel(draw, box, fill=PANEL_SOFT)
    vline(draw, box[0], box[1], box[3], RED, 3)

    text(draw, (50, 42), "THE RAKE", font(F_BOLD, 13), RED, tracking=3)
    text(draw, (492, 42), "2500 / 2500", font(F_MONO, 13), MUTED, anchor="ra")

    segmented_bar(None, draw, (50, 76, 492, 88), 1.0, RED, seg=14, gap=3)

    text(draw, (50, 100), "UNDEAD  ·  NIGHT HUNTER", font(F_REG, 11), DIM, tracking=1)


DAY = False


def draw_phase(draw):
    cx = W // 2
    if DAY:
        text(draw, (cx, 38), "DAY 1 / 3", font(F_BOLD, 12), (110, 130, 170), anchor="ma", tracking=5)
        text(draw, (cx, 56), "THE RAKE SLEEPS", font(F_IMPACT, 40), (190, 196, 210), anchor="ma", tracking=2)
    else:
        text(draw, (cx, 38), "NIGHT 1 / 3", font(F_BOLD, 12), RED, anchor="ma", tracking=5)
        text(draw, (cx, 56), "THE HUNT IS ON", font(F_IMPACT, 40), CREAM, anchor="ma", tracking=2)

    hline(draw, cx - 430, cx - 225, 78, LINE)
    hline(draw, cx + 225, cx + 430, 78, LINE)
    # diamonds at the ends of the hairlines
    for x in (cx - 436, cx + 436):
        draw.polygon([(x, 72), (x + 6, 78), (x, 84), (x - 6, 78)], fill=RED)


def draw_timer(draw):
    x0, y0, x1, y1 = 1672, 26, 1892, 126
    panel(draw, (x0, y0, x1, y1), fill=PANEL_SOFT)
    vline(draw, x1 - 3, y0, y1, RED, 3)

    # crescent moon
    mc = (x0 + 32, y0 + 34)
    draw.ellipse((mc[0] - 14, mc[1] - 14, mc[0] + 14, mc[1] + 14), fill=(228, 222, 205, 235))
    draw.ellipse((mc[0] - 20, mc[1] - 18, mc[0] + 8, mc[1] + 10), fill=(14, 15, 18, 255))

    if DAY:
        text(draw, (x0 + 60, y0 + 20), "DAY ENDS IN", font(F_BOLD, 11), MUTED, tracking=2)
        text(draw, (x0 + 58, y0 + 36), "04:12", font(F_IMPACT, 34), (190, 196, 210), tracking=2)
    else:
        text(draw, (x0 + 60, y0 + 20), "NIGHT ENDS IN", font(F_BOLD, 11), MUTED, tracking=2)
        text(draw, (x0 + 58, y0 + 36), "05:42", font(F_IMPACT, 34), CREAM, tracking=2)

    # thin progress line
    hline(draw, x0 + 22, x1 - 22, y1 - 22, (40, 36, 38, 220), 4)
    hline(draw, x0 + 22, x0 + 22 + int((x1 - x0 - 44) * 0.62), y1 - 22, RED, 4)


def marker(draw, cx, cy, px, py, size=13, fill=RED):
    """Small arrow at (px,py) pointing at the screen centre."""
    dx, dy = cx - px, cy - py
    d = math.hypot(dx, dy)
    if d < 0.01:
        return
    ux, uy = dx / d, dy / d
    perp = (-uy, ux)

    tip = (px + ux * size * 1.15, py + uy * size * 1.15)
    base = (px - ux * size * 0.55, py - uy * size * 0.55)
    p1 = (base[0] + perp[0] * size * 0.62, base[1] + perp[1] * size * 0.62)
    p2 = (base[0] - perp[0] * size * 0.62, base[1] - perp[1] * size * 0.62)

    draw.polygon([tip, p1, p2], fill=fill, outline=(22, 8, 7))


def pill(draw, x, y, w, h, fill, border, label, color, notch=RED, align="left"):
    panel(draw, (x, y, x + w, y + h), fill=fill, border=border)
    if notch:
        vline(draw, x, y, y + h - 1, notch, 3)
    tx = x + 14 if align == "left" else x + w - 14
    text(draw, (tx, y + h // 2 - 7), label, font(F_BOLD, 10), color,
         anchor=("la" if align == "left" else "ra"), tracking=2)


def draw_sense(draw):
    """No ring and no crosshair: the centre of the screen stays clean."""
    cx, cy = W // 2, H // 2 + 20
    rx, ry = 470, 300

    # tiny orientation marker at the top of the invisible band
    text(draw, (cx, cy - ry - 34), "FORWARD", font(F_BOLD, 9), MUTED, anchor="ma", tracking=3)
    draw.polygon([(cx, cy - ry - 14), (cx - 5, cy - ry - 24), (cx + 5, cy - ry - 24)], fill=(150, 45, 35, 150))

    if DAY:
        # daylight: only a couple of dim contacts, no attack readout
        for ang in (67, 205):
            px = cx + math.cos(math.radians(ang)) * rx
            py = cy + math.sin(math.radians(ang)) * ry
            marker(draw, cx, cy, px, py, size=11, fill=(120, 60, 50))
        return

    # contacts: identical markers for players, animals and traps
    for ang in (22, 67, 143, 205, 292):
        px = cx + math.cos(math.radians(ang)) * rx
        py = cy + math.sin(math.radians(ang)) * ry
        marker(draw, cx, cy, px, py, size=13, fill=RED)


def draw_side_notifications(draw):
    """Small callouts halfway between the centre and the screen edges."""
    cx, cy = W // 2, H // 2 + 20
    gap = 470
    h = 28

    if DAY:
        # left callout: daylight state, above the horizontal marker lane
        w = 260
        pill(draw, cx - gap - w + 30, cy - 110, w, h, (12, 16, 26, 220), (60, 80, 110, 200),
             "ATTACKS DISABLED", (160, 170, 190), notch=(70, 90, 120), align="right")
        return

    # right callouts: prey + movement state, above the horizontal lane
    w = 196
    x = cx + gap - 30
    pill(draw, x, cy - 110, w, h, (10, 10, 13, 205), (120, 36, 28, 210),
         "PREY IN REACH", CREAM)
    pill(draw, x, cy - 72, w, h, (10, 10, 13, 205), (90, 30, 24, 200),
         "CLIMBING", CREAM, notch=(70, 26, 22))

    # left callout: post-damage lockout, below the lane
    w2 = 300
    pill(draw, cx - gap - w2 + 30, cy + 72, w2, h, (112, 20, 16, 225), (190, 45, 32, 230),
         "DAMAGED  ·  ATTACK LOCKED 7s", CREAM, align="right")

def draw_ability(draw, x, y, w, h, name, glyph, cooldown, total, ready):
    panel(draw, (x, y, x + w, y + h), fill=(9, 9, 12, 210), border=(58, 24, 20, 190))

    # icon area
    icx, icy = x + w // 2, y + 52
    glyph(draw, icx, icy)

    # cooldown sweep around the icon
    if not ready:
        frac = max(0.0, min(1.0, cooldown / float(total)))
        draw.arc([icx - 38, icy - 38, icx + 38, icy + 38], -90, -90 + int(360 * frac),
                 fill=(150, 40, 30, 180), width=3)

    # name
    text(draw, (x + 12, y + h - 28), name, font(F_BOLD, 11), CREAM if ready else MUTED, tracking=2)

    # state at the right
    if ready:
        text(draw, (x + w - 12, y + h - 26), "READY", font(F_BOLD, 9), RED, anchor="ra", tracking=2)
    else:
        text(draw, (x + w - 12, y + h - 42), str(cooldown) + "s", font(F_IMPACT, 24), CREAM, anchor="ra")

    # top status notch
    vline(draw, x, y, y + 18, RED if ready else (70, 26, 22), 2)


def glyph_claw(draw, cx, cy):
    for i, off in enumerate((-12, 0, 12)):
        draw.line((cx - 16 + off, cy + 16, cx + 6 + off, cy - 16), fill=(230, 220, 200, 230), width=3)


def glyph_paw(draw, cx, cy):
    for dx, dy in ((-14, -6), (-5, -14), (6, -14), (15, -6)):
        draw.ellipse((cx + dx - 4, cy + dy - 4, cx + dx + 4, cy + dy + 4), fill=(230, 220, 200, 220))
    draw.ellipse((cx - 12, cy + 1, cx + 12, cy + 18), fill=(230, 220, 200, 220))


def glyph_camera(draw, cx, cy):
    draw.rounded_rectangle((cx - 20, cy - 12, cx + 20, cy + 14), radius=4,
                           outline=(230, 220, 200, 220), width=2)
    draw.ellipse((cx - 8, cy - 8, cx + 8, cy + 8), outline=(230, 220, 200, 220), width=2)
    draw.ellipse((cx + 12, cy + 2, cx + 16, cy + 6), fill=RED)


def glyph_leap(draw, cx, cy):
    for i in (-10, 2, 14):
        draw.line((cx + i - 8, cy - 14, cx + i + 4, cy), fill=(230, 220, 200, 230), width=3)
        draw.line((cx + i + 4, cy, cx + i - 8, cy + 14), fill=(230, 220, 200, 230), width=3)
    for i, yy in enumerate((cy - 20, cy + 20)):
        draw.line((cx - 26, yy, cx - 10, yy), fill=(150, 45, 35, 170), width=2)


def draw_abilities(draw):
    w, h, gap = 132, 132, 12
    x0 = 28
    y0 = H - 74 - h
    cards = [
        ("GRAB", glyph_claw, 12, 20, False),
        ("ANIMAL", glyph_paw, 0, 10, True),
        ("CAMERA", glyph_camera, 34, 50, False),
        ("LEAP", glyph_leap, 0, 50, True),
    ]
    for i, (name, glyph, cd, total, ready) in enumerate(cards):
        draw_ability(draw, x0 + i * (w + gap), y0, w, h, name, glyph, cd, total, ready)

    text(draw, (x0, y0 + h + 12),
         "WASD MOVE   SHIFT RUN   CTRL SNEAK   SPACE LEAP   V VIEW",
         font(F_BOLD, 10), DIM, tracking=2)


def draw_states(draw):
    right = W - 28
    text(draw, (right, H - 46), "TAB CAMERAS   M MAP   F1 MENU", font(F_BOLD, 10),
         DIM, anchor="ra", tracking=2)


def draw_frame(draw):
    # corner brackets
    m, ln, th = 12, 46, 2
    corners = [
        ((m, m), (1, 1)), ((W - m, m), (-1, 1)),
        ((m, H - m), (1, -1)), ((W - m, H - m), (-1, -1)),
    ]
    for (x, y), (sx, sy) in corners:
        hline(draw, x, x + sx * ln, y, (150, 45, 35, 90), th)
        vline(draw, x, y, y + sy * ln, (150, 45, 35, 90), th)

    # bottom hairline with a diamond
    hline(draw, 220, W - 220, H - 6, (120, 30, 24, 80), 1)
    draw.polygon([(W // 2, H - 12), (W // 2 + 6, H - 6), (W // 2, H), (W // 2 - 6, H - 6)],
                 fill=(150, 45, 35, 120))


# ------------------------------------------------------------------ compose
def main():
    global DAY, OUT
    import sys
    if "--day" in sys.argv:
        DAY = True
        OUT = "rake_hud_mock_day.png"

    img = build_background()

    hud = Image.new("RGBA", (W, H), (0, 0, 0, 0))
    d = ImageDraw.Draw(hud, "RGBA")

    draw_sense(d)
    draw_side_notifications(d)
    draw_top_left(d)
    draw_phase(d)
    draw_timer(d)
    draw_abilities(d)
    draw_states(d)
    draw_frame(d)

    img = Image.alpha_composite(img, hud)

    # gentle vignette + film grain over everything
    vign = Image.new("RGBA", (W, H), (0, 0, 0, 0))
    vd = ImageDraw.Draw(vign)
    for i in range(60):
        a = int(3 + i * 1.2)
        vd.rectangle((i * 6, i * 4, W - i * 6, H - i * 4), outline=(0, 0, 0, a))
    vign = vign.filter(ImageFilter.GaussianBlur(40))
    img = Image.alpha_composite(img, vign)

    noise = Image.effect_noise((W, H), 12).convert("L")
    noise = noise.filter(ImageFilter.GaussianBlur(0.4))
    grain = Image.new("RGBA", (W, H), (0, 0, 0, 0))
    grain.putalpha(noise.point(lambda v: int(v * 0.06)))
    img = Image.alpha_composite(img, grain)

    img.convert("RGB").save(OUT, quality=95)
    print("saved", OUT, img.size)


if __name__ == "__main__":
    main()
