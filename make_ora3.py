"""Layered file for the owner to edit the ship silhouette of the row-2 design."""
import io, zipfile, json
from pathlib import Path
from xml.sax.saxutils import quoteattr
from PIL import Image, ImageDraw
from build import disc, BODY, AMBER, HOLE, square_frame, raster
from build5 import CORE
from build8 import ship, glass, ring, designs

HERE = Path(__file__).parent
META = json.load(open(HERE / "owner" / "layers.json"))
W, H = META["w"], META["h"]
front, back = ring(60, 26, 16, lean=14, rx=520)
mono, col = designs(front, back, 10)


def lay(g, color):
    img = Image.new("RGBA", (W, H), (0, 0, 0, 0))
    m = Image.new("L", (W, H), 0)
    d = ImageDraw.Draw(m)
    for p in getattr(g, "geoms", [g]):
        if p.geom_type != "Polygon" or p.is_empty:
            continue
        d.polygon(list(p.exterior.coords), fill=255)
        for h in p.interiors:
            d.polygon(list(h.coords), fill=0)
    img.paste(Image.new("RGBA", (W, H), color), (0, 0), m)
    return img


def render(layers):
    img = Image.new("RGBA", (W, H), (0, 0, 0, 0))
    for g, c in layers:
        img.alpha_composite(lay(g, c))
    return img


LAYERS = [  # top first
    ("ring - front", lay(front, AMBER), True, 1.0),
    ("canopy glass", lay(glass, AMBER), True, 1.0),
    ("SHIP - edit me (navy = ship)", lay(ship, HOLE), True, 1.0),
    ("planet rim light", lay(disc.difference(CORE), AMBER), True, 1.0),
    ("planet", lay(disc, BODY), True, 1.0),
    ("ring - back", lay(back.difference(disc.buffer(10)), AMBER), True, 1.0),
    ("REFERENCE - finished colour (edges, gaps)", render(col), False, 1.0),
    ("REFERENCE - finished mono", render([(mono, "#F4EEDF")]), False, 1.0),
    ("check - dark taskbar", Image.new("RGBA", (W, H), "#202020"), False, 1.0),
    ("check - light taskbar", Image.new("RGBA", (W, H), "#F3F3F3"), False, 1.0),
]


def png(im):
    b = io.BytesIO(); im.save(b, "PNG"); return b.getvalue()


out = HERE / "icon_ship_edit.ora"
with zipfile.ZipFile(out, "w") as z:
    z.writestr(zipfile.ZipInfo("mimetype"), "image/openraster", compress_type=zipfile.ZIP_STORED)
    stack, comp = [], Image.new("RGBA", (W, H), (0, 0, 0, 0))
    for i, (name, im, vis, op) in enumerate(LAYERS):
        bb = im.getbbox() or (0, 0, 1, 1)
        z.writestr(f"data/{i:02d}.png", png(im.crop(bb)))
        stack.append(f'  <layer name={quoteattr(name)} src="data/{i:02d}.png" x="{bb[0]}" y="{bb[1]}" '
                     f'visibility="{"visible" if vis else "hidden"}" opacity="{op}"/>')
    for name, im, vis, op in reversed(LAYERS):
        if vis:
            comp.alpha_composite(im)
    z.writestr("mergedimage.png", png(comp))
    th = comp.copy(); th.thumbnail((256, 256)); z.writestr("Thumbnails/thumbnail.png", png(th))
    z.writestr("stack.xml", f'<?xml version="1.0" encoding="UTF-8"?>\n<image w="{W}" h="{H}" version="0.0.5">\n<stack>\n'
               + "\n".join(stack) + "\n</stack>\n</image>\n")
print(out)
