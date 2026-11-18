"""Scratch: render a base ship with named parts hidden, framed by the full ship. Output <out>/<variant>_<view>.png"""
import sys, json, math
from pathlib import Path
sys.path.insert(0, r"D:/amind/git/agent-1/art/tools/ship")
import bpy, numpy as np
import ship_source, ship_contract as contract, ship_render as R
argv = sys.argv[sys.argv.index("--") + 1:]
src, variants_json, out, size = Path(argv[0]), json.loads(Path(argv[1]).read_text()), Path(argv[2]), int(argv[3])
out.mkdir(parents=True, exist_ok=True)
source_scene = ship_source.open_source(src)
roles = ship_source.Roles(source_scene)
visual = sorted(n for role in contract.VISUAL_ROLES for n in roles.members.get(role, ()) if source_scene.objects[n].type == "MESH")
evaluation, bounds = R.prepare(src)
views = {"top": R.ORTHO["top"], "side": R.ORTHO["side"], "three": ((math.cos(math.radians(-45))*math.cos(math.radians(40)), math.sin(math.radians(-45))*math.cos(math.radians(40)), math.sin(math.radians(40))), (0,0,1)), "rear": ((math.cos(math.radians(135))*math.cos(math.radians(40)), math.sin(math.radians(135))*math.cos(math.radians(40)), math.sin(math.radians(40))), (0,0,1))}
for name, omit in variants_json.items():
    missing = [p for p in omit if p not in visual]
    if missing:
        raise SystemExit(f"unknown parts {missing}")
    evaluation.show_only([p for p in visual if p not in omit])
    R.render(evaluation, bounds, views, size, out, f"{name}_", textured=False)
