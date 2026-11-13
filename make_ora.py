"""Write the icon pieces as a layered OpenRaster file for hand arrangement in GIMP."""
import io, zipfile, math
from pathlib import Path
from xml.sax.saxutils import quoteattr
from PIL import Image
from shapely.geometry import Point, Polygon
from shapely.affinity import rotate, scale
from sketch2 import ship, fill, SS
from sketch3 import stroke

HERE = Path(__file__).parent
OUT = HERE / "icon_pieces.ora"
C, R = (0.5, 0.5), 0.44
HOLE, BODY, AMBER = "#0B1024", "#2B6F8F", "#F2A33A"


def layer(g, color):
    img = Image.new("RGBA", (SS, SS), (0, 0, 0, 0))
    fill(img, g, color)
    return img


def ring_parts(yc=0.58):
    rx, ry, tilt = 0.48, 0.12, -14
    cx, cy = 0.5, yc - ry
    outer = rotate(scale(Point(cx, cy).buffer(1, 256), rx, ry), tilt)
    inner = rotate(scale(Point(cx, cy).buffer(1, 256), rx - 0.045, ry - 0.03), tilt)
    ring = outer.difference(inner)
    front = ring.difference(Polygon([(0, 0), (1, 0), (1, cy), (0, cy)]).buffer(0))
    return front, ring.difference(front)


disc = Point(*C).buffer(R, 256)
rim = disc.difference(Point(C[0] + 0.027, C[1] + 0.036).buffer(R, 256))
ring_front, ring_back = ring_parts()

# (name, image, visible), top of stack first
LAYERS = [
    ("crossbar - blade (thin left, thick right)", layer(stroke([(0.24, 0.62), (0.5, 0.55), (0.96, 0.46)], 0.012, 0.07), AMBER), True),
    ("crossbar - nike hook", layer(stroke([(0.27, 0.55), (0.36, 0.65), (0.55, 0.61), (0.86, 0.45)], 0.01, 0.075, ease=0.7), AMBER), False),
    ("ring - front arc", layer(ring_front, AMBER), False),
    ("leg - left contrail", layer(stroke([(0.40, 0.50), (0.23, 0.88)], 0.035, 0.0), HOLE), False),
    ("leg - right contrail", layer(stroke([(0.60, 0.50), (0.77, 0.88)], 0.035, 0.0), HOLE), False),
    ("ship - merged tail", layer(ship(0.60, 0.5, 0.5, close=0.12), HOLE), True),
    ("ship - original tines", layer(ship(0.60, 0.5, 0.5), HOLE), False),
    ("rim light", layer(rim, AMBER), True),
    ("planet disc", layer(disc, BODY), True),
    ("ring - back arc (keep below planet)", layer(ring_back, AMBER), False),
    ("check - dark taskbar", Image.new("RGBA", (SS, SS), "#202020"), False),
    ("check - light taskbar", Image.new("RGBA", (SS, SS), "#F3F3F3"), False),
]


def png_bytes(im):
    b = io.BytesIO()
    im.save(b, "PNG")
    return b.getvalue()


stack = []
with zipfile.ZipFile(OUT, "w") as z:
    z.writestr(zipfile.ZipInfo("mimetype"), "image/openraster", compress_type=zipfile.ZIP_STORED)
    for i, (name, im, vis) in enumerate(LAYERS):
        bbox = im.getbbox() or (0, 0, 1, 1)
        crop = im.crop(bbox)
        src = f"data/{i:02d}.png"
        z.writestr(src, png_bytes(crop))
        stack.append(f'  <layer name={quoteattr(name)} src="{src}" x="{bbox[0]}" y="{bbox[1]}" '
                     f'visibility="{"visible" if vis else "hidden"}" opacity="1.0"/>')
    comp = Image.new("RGBA", (SS, SS), (0, 0, 0, 0))
    for name, im, vis in reversed(LAYERS):
        if vis:
            comp.alpha_composite(im)
    z.writestr("mergedimage.png", png_bytes(comp))
    th = comp.copy(); th.thumbnail((256, 256))
    z.writestr("Thumbnails/thumbnail.png", png_bytes(th))
    z.writestr("stack.xml", f'<?xml version="1.0" encoding="UTF-8"?>\n<image w="{SS}" h="{SS}" version="0.0.5">\n<stack>\n'
               + "\n".join(stack) + "\n</stack>\n</image>\n")
print(OUT)
