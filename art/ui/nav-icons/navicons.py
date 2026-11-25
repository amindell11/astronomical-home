# /// script
# requires-python = ">=3.11"
# dependencies = ["resvg-py==0.2.6", "pillow>=10"]
# ///
"""Bakes the nav icon family from settings.json: SVGs, a game-scale preview, the tuner page, Unity sprites."""
import argparse
import hashlib
import io
import json
import subprocess
from pathlib import Path

import resvg_py
from PIL import Image, ImageDraw

HERE = Path(__file__).parent
SETTINGS = HERE / "settings.json"
ICONS_JS = HERE / "icons.js"
TEMPLATE = HERE / "tuner.template.html"
KINDS = ["mod", "repair", "armory", "exit", "lull", "enemy"]
# Sprites the game uses: path under Assets/Visuals/Ui, kind, pixel size.
SPRITES = [
    ("Minimap/Finds/ModFind.png", "mod", 128),
    ("Minimap/Finds/RepairFind.png", "repair", 128),
    ("Minimap/Finds/ArmoryFind.png", "armory", 128),
    ("Minimap/Beacons/ExitBeacon.png", "exit", 256),
    ("Minimap/Beacons/LullBeacon.png", "lull", 256),
]
GAME_PX = 33  # 13.75 canvas units at 1080p
BACKGROUNDS = [(8, 16, 30), (3, 5, 14), (38, 20, 96), (20, 95, 170)]


def svgs():
    out = subprocess.run(["node", str(ICONS_JS), str(SETTINGS)], capture_output=True, text=True, check=True)
    return json.loads(out.stdout)


def raster(svg, px):
    png = resvg_py.svg_to_bytes(svg_string=svg, width=px, height=px)
    return Image.open(io.BytesIO(bytes(png))).convert("RGBA")


def write_svgs(icons):
    folder = HERE / "svg"
    folder.mkdir(exist_ok=True)
    for kind in KINDS:
        (folder / f"{kind}.svg").write_text(icons[kind] + "\n", encoding="utf-8", newline="\n")


def preview(icons, path):
    cell = 64
    sheet = Image.new("RGBA", (cell * len(KINDS) * 2 + 16, cell * len(BACKGROUNDS)), (24, 24, 28, 255))
    draw = ImageDraw.Draw(sheet)
    for row, colour in enumerate(BACKGROUNDS):
        draw.rectangle([0, row * cell, sheet.width, (row + 1) * cell - 1], fill=colour)
        for i, kind in enumerate(KINDS):
            small = raster(icons[kind], GAME_PX)
            sheet.alpha_composite(small, (i * cell + (cell - GAME_PX) // 2, row * cell + (cell - GAME_PX) // 2))
            big = raster(icons[kind], 56)
            sheet.alpha_composite(big, (len(KINDS) * cell + 16 + i * cell + 4, row * cell + 4))
    path.parent.mkdir(parents=True, exist_ok=True)
    sheet.save(path)


def tuner(path):
    settings = json.loads(SETTINGS.read_text(encoding="utf-8"))
    defaults = {"palette_preset": "custom", "colour_sim": "none", **settings}
    page = TEMPLATE.read_text(encoding="utf-8")
    page = page.replace("/*NAV_ICONS_JS*/", ICONS_JS.read_text(encoding="utf-8"))
    page = page.replace("/*DEFAULTS*/", json.dumps(defaults))
    page = page.replace("/*SETTINGS_HASH*/", hashlib.sha1(json.dumps(defaults).encode()).hexdigest()[:8])
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(page, encoding="utf-8", newline="\n")


def export(icons, ui_root):
    for rel, kind, px in SPRITES:
        target = ui_root / rel
        target.parent.mkdir(parents=True, exist_ok=True)
        raster(icons[kind + "_white"], px).save(target)
        print(f"wrote {target}")


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--tuner", type=Path, help="write the tuner page here")
    parser.add_argument("--export", type=Path, help="Assets/Visuals/Ui folder to write the white sprites into")
    args = parser.parse_args()
    icons = svgs()
    write_svgs(icons)
    preview(icons, HERE / "out" / "lineup.png")
    if args.tuner:
        tuner(args.tuner)
    if args.export:
        export(icons, args.export)


if __name__ == "__main__":
    main()
