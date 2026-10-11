"""4b-1 shape check: every part's evaluated world-space vertices equal the pre-edit source's
after undoing the 180-degree turn about Z, within 1e-5.

blender -b --factory-startup --python-exit-code 1 -P shape_check.py -- <tools dir> <before.blend> <after.blend> <out.json>
Evaluates both files with the ship tools' Evaluation (unhidden copies, live modifiers). Compares
every MESH and EMPTY present in both files; objects only in the after file are listed as added.
Exit 0 pass, 3 any part differs.
"""
import json
import sys

import bpy
import numpy as np

tools, before_path, after_path, out = sys.argv[sys.argv.index("--") + 1:]
sys.path.insert(0, tools)
import ship_source

TOLERANCE = 1e-5
UNTURN = np.diag([-1.0, -1.0, 1.0])


def snapshot(path):
    scene = ship_source.open_source(path)
    evaluation = ship_source.Evaluation(scene)
    parts = {}
    for name, copy in evaluation.copies.items():
        world = np.array(evaluation.world(copy), np.float64)
        record = {"type": copy.type, "world": world}
        if copy.type == "MESH":
            mesh = evaluation.mesh(name)
            co = ship_source.read_array(mesh.vertices, "co", np.float32, 3).reshape(-1, 3).astype(np.float64)
            record["points"] = co @ world[:3, :3].T + world[:3, 3]
            record["loops"] = len(mesh.loops)
            bpy.data.meshes.remove(mesh)
        parts[name] = record
    return parts


before, after = snapshot(before_path), snapshot(after_path)
results, failed = {}, []
for name in sorted(before):
    if name not in after:
        results[name] = {"status": "missing"}
        failed.append(name)
        continue
    b, a = before[name], after[name]
    entry = {"type": b["type"]}
    if b["type"] == "MESH":
        if b["points"].shape != a["points"].shape or b["loops"] != a["loops"]:
            entry.update(status="topology", before=list(b["points"].shape), after=list(a["points"].shape))
            failed.append(name)
            results[name] = entry
            continue
        diff = float(np.abs(a["points"] @ UNTURN.T - b["points"]).max()) if len(b["points"]) else 0.0
        entry.update(vertices=len(b["points"]), max_abs_diff=diff)
    else:
        origin_b, origin_a = b["world"][:3, 3], UNTURN @ a["world"][:3, 3]
        axes = float(np.abs(UNTURN @ a["world"][:3, :3] - b["world"][:3, :3]).max())
        diff = max(float(np.abs(origin_a - origin_b).max()), axes)
        entry.update(max_abs_diff=diff)
    entry["status"] = "ok" if diff <= TOLERANCE else "differs"
    if diff > TOLERANCE:
        failed.append(name)
    results[name] = entry
added = sorted(set(after) - set(before))
report = {"tolerance": TOLERANCE, "before": before_path, "after": after_path, "compared": len(results),
          "meshes": sum(r.get("type") == "MESH" for r in results.values()), "added": added,
          "failed": failed, "verdict": "pass" if not failed else "fail",
          "max_abs_diff": max((r.get("max_abs_diff", 0.0) for r in results.values()), default=0.0),
          "parts": results}
with open(out, "w", encoding="utf-8") as handle:
    json.dump(report, handle, indent=2)
print(f"SHAPE verdict={report['verdict']} compared={report['compared']} meshes={report['meshes']} "
      f"max_abs_diff={report['max_abs_diff']:.3g} added={added} failed={failed}", file=sys.stderr)
sys.exit(0 if not failed else 3)
