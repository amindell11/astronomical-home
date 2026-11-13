"""Lineup of every current nav icon at 1080p in-game size, on minimap field and sky tones."""
import math, sys
from PIL import Image, ImageDraw, ImageFont

ASSETS = r"D:\amind\git\astronomical-home\src\Asteroids3D\Assets"
OUT = sys.argv[1]
PX = 1920 / 800  # canvas scale at 1080p (ScaleWithScreenSize, ref 800x600, match width)
SS = 4           # supersample

FIND = {"Mod": ("ModFind.png", (0xFF, 0xDA, 0x4A)),
        "Repair": ("RepairFind.png", (0x3C, 0xC0, 0x60)),
        "Armory": ("ArmoryFind.png", (0xB8, 0x18, 0x38))}
IVORY = (232, 252, 245)
INK = (4, 11, 17)
EXIT = (115, 240, 194)
LULL = (255, 209, 89)
GENERIC = (255, 235, 4)

def tinted(name, tint, size):
    im = Image.open(f"{ASSETS}/Visuals/Ui/Minimap/Finds/{name}").convert("RGBA")
    r, g, b, a = im.split()
    r = r.point(lambda v: v * tint[0] // 255); g = g.point(lambda v: v * tint[1] // 255); b = b.point(lambda v: v * tint[2] // 255)
    return Image.merge("RGBA", (r, g, b, a)).resize((size, size), Image.LANCZOS)

def layer(size):
    return Image.new("RGBA", (size, size), (0, 0, 0, 0))

def berth(px, minimap, accent):
    S = round(px) * SS
    out = layer(S)
    units = 22 if minimap else 54
    k = S / units
    c = S / 2
    P = lambda x, y: (c + x * k, c - y * k)
    def disc(x, y, r, col, alpha):
        l = layer(S); d = ImageDraw.Draw(l)
        cx, cy = P(x, y); d.ellipse([cx - r * k, cy - r * k, cx + r * k, cy + r * k], fill=col + (255,))
        l.putalpha(l.getchannel("A").point(lambda v: v * alpha // 255)); out.alpha_composite(l)
    def arch(radius, leg, width):
        for col, th, alpha in ((INK, width + 3, 217), (IVORY, width, 255)):
            l = layer(S); d = ImageDraw.Draw(l)
            pts = [P(math.cos(i * math.pi / 32) * radius, math.sin(i * math.pi / 32) * radius) for i in range(33)]
            d.line(pts, fill=col + (255,), width=round(th * k), joint="curve")
            d.line([P(-radius, 0), P(-radius, -leg)], fill=col + (255,), width=round(th * k))
            d.line([P(radius, 0), P(radius, -leg)], fill=col + (255,), width=round(th * k))
            l.putalpha(l.getchannel("A").point(lambda v: v * alpha // 255)); out.alpha_composite(l)
    if minimap:
        disc(0, 0, 11, INK, 235); arch(7, 5, 2.8); disc(0, -3, 2.1, accent, 255)
    else:
        disc(0, 0, 27, (4, 14, 17), 71); arch(14, 10, 2.4)
        disc(-14, -11, 2.5, accent, 255); disc(14, -11, 2.5, accent, 255)
        disc(0, -15, 2.1, accent, 255); disc(0, -24, 1.5, accent, 128)
    return out.resize((round(px), round(px)), Image.LANCZOS)

def pointer(name, tint):
    icon = round(22 * .59 * PX); arrow = 9 * .59 * PX
    W = icon + round(arrow) + 4
    S = W * SS
    out = layer(S)
    ic = tinted(name, tint, icon * SS)
    out.alpha_composite(ic, ((S - icon * SS) // 2, S - icon * SS))
    d = ImageDraw.Draw(out)
    a = arrow * SS; top = S - icon * SS - 2 * SS - a
    d.polygon([(S / 2, top), (S / 2 + a / 2, top + a), (S / 2 - a / 2, top + a)], fill=tint + (255,))
    return out.resize((W, W), Image.LANCZOS)

def generic():
    s = 2 * .59 * PX
    S = 8 * SS; l = layer(S); d = ImageDraw.Draw(l)
    r = s * SS / 2; d.ellipse([S / 2 - r, S / 2 - r, S / 2 + r, S / 2 + r], fill=GENERIC + (255,))
    return l.resize((8, 8), Image.LANCZOS)

FIND_PX = round(14 / 112 * 110 * PX)  # 14 world units; minimap shows 112 units across 110 canvas units
icons = []
for k, (f, col) in FIND.items():
    icons.append((f"{k} find\n(minimap, {FIND_PX}px)", tinted(f, col, FIND_PX)))
for k, (f, col) in FIND.items():
    icons.append((f"{k} edge\npointer", pointer(f, col)))
icons += [("Exit beacon\n(minimap)", berth(13.75 * PX, True, EXIT)),
          ("Lull beacon\n(minimap)", berth(13.75 * PX, True, LULL)),
          ("Exit beacon\n(screen edge)", berth(37.125 * PX, False, EXIT)),
          ("Lull beacon\n(screen edge)", berth(37.125 * PX, False, LULL)),
          ("Explore/key\nobjective dot", generic())]

BGS = [("minimap field", (8, 16, 30)), ("deep sky", (3, 5, 14)), ("nebula", (38, 20, 96)), ("bright nebula", (20, 95, 170))]
CELL = 120; LABEL = 34; HEAD = 130
font = ImageFont.truetype("arial.ttf", 12); big = ImageFont.truetype("arial.ttf", 13)
W = HEAD + CELL * len(icons); H = LABEL + CELL * len(BGS) + 30 + 4 * 100
sheet = Image.new("RGBA", (W, H), (24, 24, 28, 255))
d = ImageDraw.Draw(sheet)
for i, (lab, _) in enumerate(icons):
    d.multiline_text((HEAD + i * CELL + CELL / 2, 4), lab, fill=(220, 220, 220), font=font, anchor="ma", align="center")
for j, (name, col) in enumerate(BGS):
    y = LABEL + j * CELL
    d.rectangle([HEAD, y, W, y + CELL - 1], fill=col + (255,))
    d.text((8, y + CELL / 2), name, fill=(220, 220, 220), font=big, anchor="lm")
    for i, (_, im) in enumerate(icons):
        sheet.alpha_composite(im, (HEAD + i * CELL + (CELL - im.width) // 2, y + (CELL - im.height) // 2))
y0 = LABEL + CELL * len(BGS) + 10
d.text((8, y0 + 10), "3x zoom, minimap field", fill=(220, 220, 220), font=big)
y0 += 30
d.rectangle([0, y0, W, H], fill=BGS[0][1] + (255,))
x = 10
for _, im in icons:
    z = im.resize((im.width * 3, im.height * 3), Image.NEAREST)
    if x + z.width > W: break
    sheet.alpha_composite(z, (x, y0 + (H - y0 - z.height) // 2)); x += z.width + 24
sheet.save(OUT)
print(OUT, sheet.size, "find px", FIND_PX)
