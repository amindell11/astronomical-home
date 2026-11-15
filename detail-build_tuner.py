"""Bake the tuner page: template + pass renders + locale captures + colour.json defaults.

Run: python build_tuner.py <passes dir> <backdrops dir> <colour.json> <out.html>
"""
import base64
import io
import json
import sys
from pathlib import Path

from PIL import Image

passes, backdrops, colour, out = (Path(a) for a in sys.argv[1:5])
VIEW = "tumble1"
NAMES = ("covA", "covB", "litA", "litB", "midA", "midB", "emis", "brush", "hatch")


def uri(data, mime):
    return f"data:{mime};base64," + base64.b64encode(data).decode("ascii")


def png(path):
    buf = io.BytesIO()
    Image.open(path).save(buf, "PNG", optimize=True)
    return uri(buf.getvalue(), "image/png")


def jpeg(path):
    buf = io.BytesIO()
    Image.open(path).convert("RGB").save(buf, "JPEG", quality=84, optimize=True)
    return uri(buf.getvalue(), "image/jpeg")


size = Image.open(passes / f"gimbal-{VIEW}-covA.png").width
data = {
    "size": size,
    "defaults": json.loads(colour.read_text(encoding="utf-8")),
    "passes": {s: {n: png(passes / f"{s}-{VIEW}-{n}.png") for n in NAMES} for s in ("gimbal", "kite", "drum")},
    "backdrops": {n: jpeg(backdrops / f"{n}-flight.png") for n in ("copper", "chalk", "verdant", "ice")},
}
template = (Path(__file__).parent / "template.html").read_text(encoding="utf-8")
assert "/*DATA*/null" in template
out.write_text(template.replace("/*DATA*/null", json.dumps(data)), encoding="utf-8")
print(f"wrote {out.name}: {out.stat().st_size / 1e6:.1f} MB")
