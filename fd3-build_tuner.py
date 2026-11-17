"""Bake the faction dress tuner: the template, one 3x3 pass atlas per station, dress and angle, the captures, the settings.

Run: python build_tuner.py <passes dir> <backdrops dir> <dress-colour.json> <out dir>
The page is <out dir>/faction-dress-tuner.html; it loads passes/*.png and backdrops/*.jpg beside it.
"""
import json
import sys
from pathlib import Path

from PIL import Image

passes, backdrops, colour, out = (Path(a) for a in sys.argv[1:5])
NAMES = ("covA", "covB", "litA", "litB", "midA", "midB", "emis", "brush", "hatch")
STATIONS = ("gimbal", "kite", "drum")
DRESSES = ("neutral", "shoal", "veil", "bastion")
VIEWS = ("tumble1", "top")

(out / "passes").mkdir(parents=True, exist_ok=True)
(out / "backdrops").mkdir(parents=True, exist_ok=True)
size = Image.open(passes / f"gimbal-tumble1-covA.png").width
for station in STATIONS:
    for dress in DRESSES:
        tag = station if dress == "neutral" else f"{station}-{dress}-kit"
        for view in VIEWS:
            atlas = Image.new("RGBA", (size * 3, size * 3), (0, 0, 0, 0))
            for i, name in enumerate(NAMES):
                atlas.paste(Image.open(passes / f"{tag}-{view}-{name}.png").convert("RGBA"), ((i % 3) * size, (i // 3) * size))
            atlas.save(out / "passes" / f"{tag}-{view}.png", optimize=True)
for name in ("copper", "chalk", "verdant", "ice"):
    Image.open(backdrops / f"{name}-flight.png").convert("RGB").save(out / "backdrops" / f"{name}.jpg", quality=84,
                                                                      optimize=True)
data = {"size": size, "names": NAMES, "defaults": json.loads(colour.read_text(encoding="utf-8"))}
template = (Path(__file__).parent / "template.html").read_text(encoding="utf-8")
assert "/*DATA*/null" in template
page = out / "faction-dress-tuner.html"
page.write_text(template.replace("/*DATA*/null", json.dumps(data)), encoding="utf-8", newline="\n")
total = sum(p.stat().st_size for p in out.rglob("*") if p.is_file())
print(f"wrote {len(list(out.rglob('*.*')))} files, {total / 1e6:.1f} MB")
