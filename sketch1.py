"""Round-1 sketch: exit and lull container x glyph grids, judged beside the three finds."""
import io, math, sys
import resvg_py
from PIL import Image, ImageDraw, ImageFont

INK = "#040B11"
SW = 7  # glyph stroke width, viewBox units (64)
COL = {"mod": "#FFD23F", "repair": "#3CC060", "armory": "#C8203C", "exit": "#4FD8FF", "lull": "#B48CFF"}

def poly(n, r, rot=0, cx=32, cy=32):
    return " ".join(f"{cx + r * math.cos(rot + i * 2 * math.pi / n):.2f},{cy + r * math.sin(rot + i * 2 * math.pi / n):.2f}" for i in range(n))

CONTAINERS = {
    "diamond": lambda c: f'<path d="M32 2 L62 32 L32 62 L2 32Z" fill="{c}"/>',
    "circle": lambda c: f'<circle cx="32" cy="32" r="27" fill="{c}"/>',
    "hexagon": lambda c: f'<polygon points="{poly(6, 30, 0)}" fill="{c}"/>',
    "rounded sq": lambda c: f'<rect x="9" y="9" width="46" height="46" rx="9" fill="{c}"/>',
    "gate": lambda c: f'<path d="M9 58 V30 A23 23 0 0 1 55 30 V58Z" fill="{c}"/>',
    "octagon": lambda c: f'<polygon points="{poly(8, 29, math.pi / 8)}" fill="{c}"/>',
}
S = f'fill="none" stroke="{INK}" stroke-width="{SW}" stroke-linecap="round" stroke-linejoin="round"'
GLYPHS = {
    "star": f'<path d="M32 16 Q34 30 48 32 Q34 34 32 48 Q30 34 16 32 Q30 30 32 16Z" fill="{INK}"/>',
    "plus": f'<path d="M32 19 V45 M19 32 H45" {S}/>',
    "target": f'<circle cx="32" cy="32" r="10" {S.replace(str(SW), "5")}/><circle cx="32" cy="32" r="3" fill="{INK}"/>',
    "arch": f'<path d="M23 44 V32 A9 9 0 0 1 41 32 V44" {S}/>',
    "chevron up": f'<path d="M21 38 L32 26 L43 38" {S}/>',
    "dbl chevron": f'<path d="M22 32 L32 22 L42 32 M22 43 L32 33 L42 43" {S.replace(str(SW), "6")}/>',
    "exit arrow": f'<path d="M32 44 V22 M23 30 L32 21 L41 30" {S}/>',
    "pause": f'<path d="M26 22 V42 M38 22 V42" {S}/>',
    "crescent": f'<path d="M37 19 A14 14 0 1 0 45 38 A11 11 0 1 1 37 19Z" fill="{INK}"/>',
    "beacon": f'<circle cx="32" cy="32" r="4.5" fill="{INK}"/><path d="M22 22 A14 14 0 0 0 22 42 M42 22 A14 14 0 0 1 42 42" {S.replace(str(SW), "5")}/>',
    "hourglass": f'<path d="M23 20 H41 L23 44 H41Z" {S.replace(str(SW), "5")}/>',
}

def svg(container, glyph, colour):
    return f'<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 64 64">{CONTAINERS[container](colour)}{GLYPHS[glyph]}</svg>'

def raster(s, px):
    big = Image.open(io.BytesIO(bytes(resvg_py.svg_to_bytes(svg_string=s, width=px * 4, height=px * 4)))).convert("RGBA")
    return big.resize((px, px), Image.LANCZOS)

GAME = 33
FIELD, SKY = (8, 16, 30), (20, 95, 170)
font = ImageFont.truetype("arial.ttf", 12); bold = ImageFont.truetype("arialbd.ttf", 14)

def sheet(role, containers, glyphs, out):
    CW, CH, LW = 150, 112, 110
    finds = [("diamond", "star", COL["mod"]), ("diamond", "plus", COL["repair"]), ("diamond", "target", COL["armory"])]
    W = LW + CW * len(containers); H = 40 + CH * len(glyphs)
    im = Image.new("RGBA", (W, H), (24, 24, 28, 255)); d = ImageDraw.Draw(im)
    d.text((8, 12), role.upper(), fill="white", font=bold)
    for i, c in enumerate(containers):
        d.text((LW + i * CW + CW / 2, 14), c, fill=(220, 220, 220), font=font, anchor="mm")
    for j, g in enumerate(glyphs):
        y = 40 + j * CH
        d.text((8, y + CH / 2), g, fill=(220, 220, 220), font=font, anchor="lm")
        for i, c in enumerate(containers):
            x = LW + i * CW
            d.rectangle([x + 2, y + 2, x + CW - 2, y + CH / 2], fill=FIELD)
            d.rectangle([x + 2, y + CH / 2, x + CW - 2, y + CH - 2], fill=SKY)
            s = svg(c, g, COL[role])
            big = raster(s, 64)
            im.alpha_composite(big, (x + 6, y + (CH - 64) // 2))
            # game scale: candidate beside the find row, on field and sky
            for k, yy in enumerate((y + 10, y + CH / 2 + 4)):
                xx = x + 76
                im.alpha_composite(raster(s, GAME), (xx, int(yy)))
                for n, f in enumerate(finds[:1]):
                    im.alpha_composite(raster(svg(*f), GAME), (xx + 36, int(yy)))
    im.save(out)

def family(out):
    """Find row at game scale for reference: the current-palette finds re-drawn as SVG."""

sheet("exit", ["circle", "hexagon", "rounded sq", "gate", "octagon"], ["arch", "chevron up", "dbl chevron", "exit arrow"], sys.argv[1] + "_exit.png")
sheet("lull", ["circle", "hexagon", "rounded sq", "gate", "octagon"], ["pause", "crescent", "beacon", "hourglass"], sys.argv[1] + "_lull.png")
