"""Bake the tuner page: the shared fragment body, the settings block and the images go into one HTML file."""
import base64
import io
import json
from pathlib import Path

from PIL import Image

HERE = Path(__file__).parent
TEXTURES = Path("D:/amind/git/astronomical-home/src/Asteroids3D/Assets/Visuals/Locales/Sky/Planets")
BACKDROPS = {"VerdantGiant": HERE.parent / "refs" / "Locale_6-engine-v6.png",
             "BlueCrescent": HERE.parent / "refs" / "Locale_7-engine-v6.png"}


def data_uri(path, quality):
    buffer = io.BytesIO()
    Image.open(path).convert("RGB").save(buffer, "JPEG", quality=quality, subsampling=0)
    return "data:image/jpeg;base64," + base64.b64encode(buffer.getvalue()).decode("ascii")


settings = json.loads((HERE / "settings.json").read_text(encoding="utf-8"))
images = {}
for planet_id, planet in settings["planets"].items():
    images[planet["texture"]] = data_uri(TEXTURES / f"{planet['texture']}.png", 92)
    images[planet_id + "_backdrop"] = data_uri(BACKDROPS[planet_id], 90)
baked = {"settings": settings, "shader": (HERE / "planet_body.glsl").read_text(encoding="utf-8"), "images": images}
page = (HERE / "tuner.template.html").read_text(encoding="utf-8").replace(
    "/*__BAKED__*/", json.dumps(baked, ensure_ascii=False).replace("</", "<\\/"))
out = HERE / "planet-tuner.html"
out.write_text(page, encoding="utf-8")
print(out, f"{out.stat().st_size / 1e6:.2f} MB")
