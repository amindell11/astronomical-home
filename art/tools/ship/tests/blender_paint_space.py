"""Bake brush/sweep isolation and legacy compatibility; prints PAINT_SPACE_PASS on success.

Run Blender -b -P this file -- --out DIR [--legacy-module PREVIOUS_SHIP_PAINT_PY].
All outputs and temporary surfaces stay in the requested directory and process.
"""

import argparse
import copy
import importlib.util
import json
from pathlib import Path
import sys

import bpy
import numpy as np

TOOLS = Path(__file__).resolve().parents[1]
sys.path[:0] = [str(TOOLS), str(TOOLS / "tests")]
import fixture_ship
import ship_paint
import ship_source

parser = argparse.ArgumentParser()
parser.add_argument("--out", required=True)
parser.add_argument("--legacy-module")
args = parser.parse_args(sys.argv[sys.argv.index("--") + 1:])
out = Path(args.out).resolve()
out.mkdir(parents=True, exist_ok=True)
legacy = None
if args.legacy_module:
    spec = importlib.util.spec_from_file_location("legacy_paint", args.legacy_module)
    legacy = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(legacy)


def render(label, paint, module=ship_paint):
    bpy.ops.wm.read_factory_settings(use_empty=True)
    scene = bpy.context.scene
    origin = bpy.data.objects.new("SymmetryOrigin", None)
    scene.collection.objects.link(origin)
    mesh = bpy.data.meshes.new("Panel")
    mesh.from_pydata([(-1, -1, 0), (1, -1, 0), (1, 1, 0), (-1, 1, 0)], [], [(0, 1, 2, 3)])
    mesh.materials.append(bpy.data.materials.new("Zinc"))
    uv = mesh.uv_layers.new(name="PaintUV")
    uv.uv.foreach_set("vector", [.05, .05, .95, .05, .95, .95, .05, .95])
    panel = bpy.data.objects.new("Panel", mesh)
    panel.parent = origin
    scene.collection.objects.link(panel)
    evaluation = ship_source.Evaluation(scene)
    part = module.Part(evaluation, "Panel", paint)
    fixture_ship.write_paint_settings(out)
    brush = bpy.data.images.load(str(out / "brush.png"))
    brush.colorspace_settings.name = "Non-Color"
    graph = module.build_material(part, part.caps[0], "Zinc", paint["regions"]["Zinc"],
                                  paint, brush, [.7, .7, .72], None, [])
    part.mesh.materials.clear()
    part.mesh.materials.append(graph[0])
    surface = bpy.data.objects.new("Surface", part.mesh)
    evaluation.scene.collection.objects.link(surface)
    target = bpy.data.images.new("Masks", 256, 256, alpha=False)
    target.colorspace_settings.name = "Non-Color"
    pixels = module.bake(evaluation.scene, surface, {"panel": graph}, target, "masks", 0)
    ship_paint.write_png(out / f"{label}.png", pixels)
    return pixels


paint = copy.deepcopy(fixture_ship.PAINT)
paint["regions"]["Zinc"].update(plate=False, ink_walls=False)
paint["accents"] = []
folded = render("folded", paint)
if legacy is not None:
    np.testing.assert_array_equal(folded, render("legacy", paint, legacy))

signed = copy.deepcopy(paint)
signed["paint_space"] = "signed"
signed["sweep"].pop("weight_x")
signed["sweep"]["angle_deg"] = 30
signed["side_wall_depth"] = 0
signed["brush"]["contrast"] = 0
signed["sweep"]["jitter"] = 0
sweep = render("signed-sweep", signed)[24:232, 24:232]
assert np.mean(np.abs(sweep.astype(float) - sweep[:, ::-1].astype(float))) > 10

brush = copy.deepcopy(signed)
brush["brush"]["contrast"] = paint["brush"]["contrast"]
brush["shadow_depth"] = 0
brush["sweep"]["light"] = 0
texture = render("signed-brush", brush)[24:232, 24:232]
assert np.mean(np.abs(texture.astype(float) - texture[:, ::-1].astype(float))) > 2
assert np.count_nonzero(sweep[:, :, 2]) == np.count_nonzero(texture[:, :, 2]) == 0
(out / "verdict.json").write_text(json.dumps({"verdict": "pass", "legacy_compared": legacy is not None,
                                             "signed_brush": "asymmetric", "signed_sweep": "asymmetric"}, indent=2))
print("PAINT_SPACE_PASS")
