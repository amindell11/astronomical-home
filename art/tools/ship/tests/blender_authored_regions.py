"""Exercise palette masks across faces, palette swaps and preserved owner edits on the fixture."""

import argparse
import copy
import json
import os
from pathlib import Path
import subprocess
import sys

import bpy
import numpy as np

TOOLS = Path(__file__).resolve().parents[1]
sys.path[:0] = [str(TOOLS), str(TOOLS / "tests")]
import fixture_ship
import ship_contract as contract
import ship_paint
import ship_source

parser = argparse.ArgumentParser()
parser.add_argument("--out", required=True)
folder = Path(parser.parse_args(sys.argv[sys.argv.index("--") + 1:]).out).resolve()
folder.mkdir(parents=True, exist_ok=True)
spec = {"schema_version": 1, "name": "Fixture", "source": "Fixture.blend", "texture_size": 256,
        "uv_density_max_ratio": 2., "contour_roles": ["hull"], "exemptions": []}
ship_json = folder / "ship.json"
ship_json.write_text(json.dumps(spec))
source = fixture_ship.build(ship_json, folder / "new")
bpy.context.preferences.filepaths.save_version = 0
bpy.ops.wm.save_as_mainfile(filepath=str(source))
fixture_ship.write_paint_settings(folder)
paint = copy.deepcopy(fixture_ship.PAINT)
paint["authored_regions"] = "regions.json"
paint["brush"]["contrast"] = 0
paint["side_wall_depth"] = 0
paint["accents"] = []
for flags in paint["regions"].values():
    flags.update(plate=False, sweep=False, ink_walls=False)
paint["regions"]["Signal"] = {"plate": False, "sweep": False, "ink_walls": False}
(folder / "paint.json").write_text(json.dumps(paint))
palettes = copy.deepcopy(fixture_ship.PALETTES)
for name, colours in palettes["palettes"].items():
    colours["Signal"] = [.03, .7, .9, 1] if name == "zinc" else [.7, .03, .2, 1]
(folder / "palettes.json").write_text(json.dumps(palettes))
marks = [{"parts": ["Fuselage"], "region": "Signal", "mirror_x": True,
          "where": {"y": [-.25, .25], "nz": [.8, None]}, "shape": {"kind": "box"}, "opacity": 1}]
(folder / "regions.json").write_text(json.dumps({"schema_version": 1, "marks": marks}))
environment = {key: value for key, value in os.environ.items() if key != "OCIO"}


def tool(name, *args, expected=0):
    result = subprocess.run([bpy.app.binary_path, "-b", "--factory-startup", "--python-exit-code", "1",
                             "-P", str(TOOLS / f"ship_{name}.py"), "--", "--ship", str(ship_json),
                             "--out", str(folder / ("out_" + name)), *args],
                            capture_output=True, text=True, env=environment)
    assert result.returncode == expected, (result.stdout, result.stderr[-4000:])
    return json.loads((folder / ("out_" + name) / f"ship_{name}.json").read_text())


tool("lock", "--mode", "lock")
source_hash = contract.sha256_file(source)
first = tool("paint")
signal_path = Path(first["masks"]["Region_Signal"])
signal = ship_paint.read_gray(signal_path)
assert (signal == 255).sum() > 10, "Stripe crossing the original faces is missing"
assert (signal == 0).sum() > 10
weights_path = Path(first["authored_regions"]["paths"]["RegionWeights0"])
assert weights_path.read_bytes()[25] == 6, "Runtime region weights must retain RGBA, including alpha"
first_maps = {name: contract.sha256_file(path) for name, path in first["masks"].items()}
first_preview = contract.sha256_file(first["preview"])
second = tool("paint", "--palette", "jade")
assert first_maps == {name: contract.sha256_file(path) for name, path in second["masks"].items()}
assert first_preview != contract.sha256_file(second["preview"]), "Palette did not recolor the coverage masks"
material = (Path(second["unity"]["root"]) / "Materials/jade/Zinc.mat").read_text()
assert "_PaintRegionCount: 5" in material and "_PaintRegionMask1:" in material
assert "_PaintRegionColor4:" in material
point = tuple(np.argwhere(signal == 255)[len(np.argwhere(signal == 255)) // 2])
signal[point] = 0
zinc = ship_paint.read_gray(second["masks"]["Region_Zinc"])
zinc[point] = 255
ship_paint.write_png(signal_path, signal)
ship_paint.write_png(second["masks"]["Region_Zinc"], zinc)
third = tool("paint")
assert ship_paint.read_gray(third["masks"]["Region_Signal"])[point] == 0
assert ship_paint.read_gray(third["masks"]["Region_Zinc"])[point] == 255
assert third["edited_texels"]["Region_Signal"] == 1
assert third["edited_texels"]["Region_Zinc"] == 1
marks[0]["parts"] = ["Absent hull"]
(folder / "regions.json").write_text(json.dumps({"schema_version": 1, "marks": marks}))
unchanged = contract.sha256_file(signal_path)
refused = tool("paint", expected=3)
assert "unknown region or visual part" in refused["refusal"]
assert contract.sha256_file(signal_path) == unchanged, "Invalid input overwrote owner paint"
assert contract.sha256_file(source) == source_hash
tool("lock", "--mode", "verify")
print("AUTHORED_REGIONS_PASS")
