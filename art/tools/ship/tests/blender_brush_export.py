"""Exercise brush-only export on disconnected UV islands without a paint baseline.

Run Blender -b --factory-startup --python-exit-code 1 -P this file -- --out DIR.
Writes fixture, bake and verdict.json under DIR; prints BRUSH_EXPORT_PASS on success.
"""

import argparse
import json
from pathlib import Path
import sys

import bpy
import numpy as np

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
import ship_contract as contract
import ship_paint
import ship_source

parser = argparse.ArgumentParser()
parser.add_argument("--out", required=True)
out = Path(parser.parse_args(sys.argv[sys.argv.index("--") + 1:]).out).resolve()
out.mkdir(parents=True, exist_ok=True)
bpy.ops.wm.read_factory_settings(use_empty=True)
scene = bpy.context.scene
origin = bpy.data.objects.new("SymmetryOrigin", None)
scene.collection.objects.link(origin)
role = bpy.data.collections.new("role.hull")
scene.collection.children.link(role)
mesh = bpy.data.meshes.new("Panel")
mesh.from_pydata([(-.5, -.5, 0), (0, -.5, 0), (0, .5, 0), (-.5, .5, 0),
                  (0, -.5, 0), (.5, -.5, 0), (.5, .5, 0), (0, .5, 0)],
                 [], [(0, 1, 2, 3), (4, 5, 6, 7)])
uv = mesh.uv_layers.new(name="PaintUV")
uv.uv.foreach_set("vector", [.05, .05, .45, .05, .45, .45, .05, .45,
                             .55, .55, .95, .55, .95, .95, .55, .95])
obj = bpy.data.objects.new("Panel", mesh)
obj.parent = origin
role.objects.link(obj)
source = out / "Fixture.blend"
bpy.ops.wm.save_as_mainfile(filepath=str(source))
ship = {"schema_version": 1, "name": "Fixture", "source": source.name, "texture_size": 1024,
        "uv_density_max_ratio": 2.0, "contour_roles": ["hull"], "exemptions": []}
(out / "ship.json").write_text(json.dumps(ship))
lock = {"recipe": contract.FINGERPRINT_RECIPE,
        "geometry": ship_source.fingerprint(scene)["components"]["geometry"]}
(out / "lock.json").write_text(json.dumps(lock))
settings = {"schema_version": 1, "image": "tile.png", "scale": .8, "blend": .25, "offset": [.23, .37, 0]}
(out / "brush.json").write_text(json.dumps(settings))
yy, xx = np.mgrid[:128, :128]
ship_paint.write_png(out / "tile.png", (100 + (xx + yy) * .4).astype(np.uint8))
layers = out / "layers"
layers.mkdir(exist_ok=True)
for name in contract.MASKS:
    ship_paint.write_png(layers / f"{name}.png", np.full((1024, 1024), 57, np.uint8))
protected = [source, *(layers.glob("*.png"))]
before = {path: contract.sha256_file(path) for path in protected}
args = ["--ship", str(out / "ship.json"), "--out", str(out / "bake"), "--brush-only"]
code, trailers = ship_paint.main(args)
assert code == 0, trailers
path = Path(dict(trailers)["SHIP_BRUSH"])
pixels = ship_paint.read_gray(path)
assert pixels.shape == (1024, 1024)


def sample(u, v):
    x, y = u * 1024 - .5, v * 1024 - .5
    x0, y0 = int(x), int(y)
    fx, fy = x - x0, y - y0
    return ((1 - fx) * (1 - fy) * pixels[y0, x0] + fx * (1 - fy) * pixels[y0, x0 + 1]
            + (1 - fx) * fy * pixels[y0 + 1, x0] + fx * fy * pixels[y0 + 1, x0 + 1])


errors = [abs(sample(.45, .05 + .4 * t) - sample(.55, .55 + .4 * t))
          for t in np.linspace(.05, .95, 100)]
assert max(errors) <= 2, max(errors)
assert np.ptp(pixels[65:440, 65:440]) > 10, "The brush must retain visible variation"
assert {path: contract.sha256_file(path) for path in protected} == before
assert not (layers / "baseline").exists()
assert not (out / "paint.json").exists()
first = contract.sha256_file(path)
ship_paint.main(args)
assert contract.sha256_file(path) == first, "Brush export must be reproducible"
try:
    ship_paint.main([*args, "--palette", "unused"])
except contract.UsageError:
    pass
else:
    raise AssertionError("Brush-only must refuse palette generation arguments")
(out / "lock.json").write_text(json.dumps({**lock, "geometry": "changed"}))
try:
    ship_paint.main(args)
except contract.Refusal as refusal:
    assert refusal.outcome[0] == 3
else:
    raise AssertionError("Brush-only must enforce the geometry lock")
assert contract.sha256_file(path) == first
(out / "verdict.json").write_text(json.dumps({"verdict": "pass", "seam_maximum_255": max(errors),
                                             "source_and_masks_unchanged": True,
                                             "reproducible": True, "geometry_lock_enforced": True}, indent=2))
print("BRUSH_EXPORT_PASS")
