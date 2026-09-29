import argparse
import json
import math
import statistics
import sys
from pathlib import Path

import bpy


parser = argparse.ArgumentParser()
parser.add_argument("--blend", type=Path, default=Path(__file__).parent / "Crimson.blend")
parser.add_argument("--report", type=Path)
args = parser.parse_args(sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else [])
bpy.ops.wm.open_mainfile(filepath=str(args.blend.resolve()))
graph = bpy.context.evaluated_depsgraph_get()
parts = []
for obj in bpy.context.scene.objects:
    if obj.type != "MESH":
        continue
    evaluated = obj.evaluated_get(graph)
    mesh = evaluated.to_mesh()
    mesh.calc_loop_triangles()
    uv = mesh.uv_layers.active.data
    surface_area = uv_area = 0.0
    for triangle in mesh.loop_triangles:
        a, b, c = [obj.matrix_world @ mesh.vertices[i].co for i in triangle.vertices]
        surface_area += (b - a).cross(c - a).length / 2
        a, b, c = [uv[i].uv for i in triangle.loops]
        ab, ac = b - a, c - a
        uv_area += abs(ab.x * ac.y - ab.y * ac.x) / 2
    parts.append({"name": obj.name, "pixels_per_unit": 4096 * math.sqrt(uv_area / surface_area)})
    evaluated.to_mesh_clear()

densities = [part["pixels_per_unit"] for part in parts]
ratio = max(densities) / min(densities)
report = {
    "texture_size": 4096,
    "measurement": "4096 * sqrt(evaluated UV triangle area / world-space triangle area)",
    "median_pixels_per_unit": statistics.median(densities),
    "maximum_to_minimum_ratio": ratio,
    "maximum_allowed_ratio": 1.25,
    "parts": parts,
}
if args.report:
    args.report.write_text(json.dumps(report, indent=2) + "\n")
print(json.dumps({key: value for key, value in report.items() if key != "parts"}))
assert len(parts) == 39, f"Expected 39 mesh parts, found {len(parts)}"
assert ratio <= 1.25, f"Uneven texel density: {ratio:.3f}x maximum/minimum"
