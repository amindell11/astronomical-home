"""4b-1 collider check: map the source collider and hull back to today's Unity hull space and compare
the collider's bounds with Valis collider.asset's stored AABB; report hull bounds alongside.

blender -b --factory-startup --python-exit-code 1 -P collider_check.py -- <tools dir> <Valis.blend> <out.json>
Unity space from new symmetry space: u = (-x, y - centre_y, -z) * scale.
"""
import json
import sys

import bpy
import numpy as np

tools, path, out = sys.argv[sys.argv.index("--") + 1:]
sys.path.insert(0, tools)
import ship_source

SCALE, CENTRE_Y = 0.228625789, -0.335398436
ASSET_CENTER, ASSET_EXTENT = np.array([0, 0, -0.06524506]), np.array([1.067495, 1.0163327, 0.3505249])
scene = ship_source.open_source(path)
roles = ship_source.Roles(scene)
evaluation = ship_source.Evaluation(scene)


def unity_points(name):
    mesh = evaluation.mesh(name)
    co = ship_source.read_array(mesh.vertices, "co", np.float32, 3).reshape(-1, 3).astype(np.float64)
    bpy.data.meshes.remove(mesh)
    m = np.array(evaluation.to_space(name), np.float64)
    p = co @ m[:3, :3].T + m[:3, 3]
    return np.stack([-p[:, 0], p[:, 1] - CENTRE_Y, -p[:, 2]], axis=1) * SCALE


(collider,) = roles.members["collider"]
c = unity_points(collider)
hull = np.concatenate([unity_points(n) for n in sorted(roles.members["hull"])])
lo, hi = c.min(0), c.max(0)
report = {"collider": collider, "collider_center": ((lo + hi) / 2).tolist(), "collider_extent": ((hi - lo) / 2).tolist(),
          "asset_center": ASSET_CENTER.tolist(), "asset_extent": ASSET_EXTENT.tolist(),
          "aabb_max_abs_diff": float(max(np.abs((lo + hi) / 2 - ASSET_CENTER).max(), np.abs((hi - lo) / 2 - ASSET_EXTENT).max())),
          "hull_min": hull.min(0).tolist(), "hull_max": hull.max(0).tolist()}
json.dump(report, open(out, "w"), indent=2)
print("COLLIDER", json.dumps(report), file=sys.stderr)
