"""Scratch: render a ship's top view with every palette-region material set to a palettes.json palette.

blender -b --factory-startup --python-exit-code 1 -P palette_render.py -- --ship SHIP_JSON --palettes JSON
    --palette NAME --out PNG [--size N] [--idmap]
Read-only on the source: nothing is saved. --idmap paints each region a distinct false colour and
writes <out>.json mapping region -> sRGB false colour, for sampling concepts per region.
"""
import argparse
import colorsys
import json
from pathlib import Path
import sys

import bpy

sys.path.insert(0, str(Path(r"D:/amind/git/agent-8/art/tools/ship")))
import ship_contract as contract  # noqa: E402
import ship_render  # noqa: E402

argv = sys.argv[sys.argv.index("--") + 1:]
p = argparse.ArgumentParser()
p.add_argument("--ship", required=True)
p.add_argument("--palettes", required=True)
p.add_argument("--palette", required=True)
p.add_argument("--out", required=True)
p.add_argument("--size", type=int, default=1024)
p.add_argument("--idmap", action="store_true")
a = p.parse_args(argv)

ship, source = contract.load_ship(a.ship)
palette = json.load(open(a.palettes, encoding="utf-8"))["palettes"][a.palette]
evaluation, framing = ship_render.prepare(source)
names = sorted(palette)
idmap = {}
for index, name in enumerate(names):
    if a.idmap:
        r, g, b = colorsys.hsv_to_rgb(index / len(names), 0.9 if index % 2 else 0.6, 1.0 if index % 3 else 0.7)
        idmap[name] = [r, g, b]
for material in bpy.data.materials:
    if material.name in palette:
        material.diffuse_color = (*idmap[material.name], 1) if a.idmap else tuple(palette[material.name])
out = Path(a.out)
written = ship_render.render(evaluation, framing, {"top": ship_render.ORTHO["top"]}, a.size, out.parent, out.stem + "_")
written["top"].replace(out)
if a.idmap:
    json.dump(idmap, open(str(out) + ".json", "w"), indent=1)
used = sorted(m.name for m in bpy.data.materials if m.name in palette)
print("PALETTE_RENDER", out, "regions-on-materials:", used, file=sys.stderr)
