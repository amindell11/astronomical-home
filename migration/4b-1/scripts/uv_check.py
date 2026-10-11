"""4b-1 UV check on evaluated parts (world space; the mirror plane is world x = 0).

blender -b --factory-startup --python-exit-code 1 -P uv_check.py -- <tools dir> <before.blend> <after.blend> <out.json>
- copy: each part is evaluated up to and including its Mirror (later modifiers off). The Mirror's
  output is the native faces then their mirrored copies; every mirrored corner must sit at the
  world mirror of its native corner and carry the native UV shifted by the Mirror's offset_v
  exactly, and the native faces must lie wholly in one atlas half (upper before, lower after).
- swap: with the full stack, loop by loop, after = before - 0.5 on upper-half loops and
  before + 0.5 on lower-half loops, exactly (float32). Evaluated topology must match.
Exit 0 pass, 3 any failure.
"""
import json
import sys

import bpy
import numpy as np

tools, before_path, after_path, out = sys.argv[sys.argv.index("--") + 1:]
sys.path.insert(0, tools)
import ship_source

POSITION = 1e-5


def evaluate(path, mirror_only):
    scene = ship_source.open_source(path)
    evaluation = ship_source.Evaluation(scene)
    offsets = {}
    for name, copy in evaluation.copies.items():
        types = [m.type for m in copy.modifiers]
        if copy.type != "MESH" or "MIRROR" not in types:
            continue
        mirror = types.index("MIRROR")
        offsets[name] = (copy.modifiers[mirror].offset_u, copy.modifiers[mirror].offset_v)
        if mirror_only:
            for modifier in list(copy.modifiers)[mirror + 1:]:
                modifier.show_viewport = False
    evaluation.depsgraph.update()
    parts = {}
    for name in offsets:
        copy = evaluation.copies[name]
        mesh = evaluation.mesh(name)
        world = np.array(evaluation.world(copy), np.float64)
        co = ship_source.read_array(mesh.vertices, "co", np.float32, 3).reshape(-1, 3).astype(np.float64)
        corners = ship_source.read_array(mesh.loops, "vertex_index", np.int32)
        layer = mesh.uv_layers.active
        uv = ship_source.read_array(layer.uv, "vector", np.float32, 2).reshape(-1, 2)
        parts[name] = {"layer": layer.name, "points": (co @ world[:3, :3].T + world[:3, 3])[corners], "uv": uv,
                       "offset": offsets[name][:2],
                       "starts": ship_source.read_array(mesh.polygons, "loop_start", np.int32),
                       "totals": ship_source.read_array(mesh.polygons, "loop_total", np.int32)}
        bpy.data.meshes.remove(mesh)
    return parts


def copy_check(part, native_upper):
    """Mirror output: faces [0, P) are native, [P, 2P) their mirrored copies with reversed winding.
    Corners pair by world mirror position inside each face pair."""
    points, uv, starts, totals = part["points"], part["uv"], part["starts"], part["totals"]
    if len(starts) % 2:
        return {"ok": False, "reason": f"mirror output has an odd face count {len(starts)}"}
    faces = len(starts) // 2
    shift = np.array(part["offset"], np.float32)
    native_loops = np.concatenate([np.arange(starts[f], starts[f] + totals[f]) for f in range(faces)])
    native_v = uv[native_loops, 1]
    in_half = bool(np.all(native_v >= 0.5)) if native_upper else bool(np.all(native_v < 0.5))
    unpaired, uv_mismatches, gap = 0, 0, 0.0
    for face in range(faces):
        twin = face + faces
        if totals[face] != totals[twin]:
            unpaired += totals[face]
            continue
        native = np.arange(starts[face], starts[face] + totals[face])
        mirrored = np.arange(starts[twin], starts[twin] + totals[twin])
        for loop in native:
            target = points[loop] * (-1.0, 1.0, 1.0)
            distance = np.abs(points[mirrored] - target).max(axis=1)
            best = mirrored[distance <= POSITION]
            if not len(best):
                unpaired += 1
                gap = max(gap, float(distance.min()))
                continue
            if not any(np.all(uv[candidate] == uv[loop] + shift) for candidate in best):
                uv_mismatches += 1
    return {"ok": in_half and unpaired == 0 and uv_mismatches == 0, "native_in_half": in_half,
            "offset_v": float(shift[1]), "faces": faces, "unpaired": unpaired, "unpaired_gap": gap,
            "uv_mismatches": uv_mismatches}


def symmetry_gap(part):
    """Largest distance from a loop to the nearest loop at its world mirror position (full stack)."""
    points = part["points"]
    mirrored = points * (-1.0, 1.0, 1.0)
    gaps = np.sqrt(((points[:, None, :] - mirrored[None, :, :]) ** 2).sum(axis=2)).min(axis=1)
    return float(gaps.max())


before_mirror, after_mirror = evaluate(before_path, True), evaluate(after_path, True)
before, after = evaluate(before_path, False), evaluate(after_path, False)
assert sorted(before) == sorted(after) and len(before) == 30, (len(before), len(after))
results, failed = {}, []
for name in sorted(before):
    b, a = before[name], after[name]
    entry = {"layer_before": b["layer"], "layer_after": a["layer"],
             "copy_before": copy_check(before_mirror[name], True),
             "copy_after": copy_check(after_mirror[name], False),
             "full_stack_symmetry_gap": symmetry_gap(b)}
    if b["uv"].shape == a["uv"].shape:
        expected = b["uv"].copy()
        upper = expected[:, 1] >= 0.5
        expected[upper, 1] -= np.float32(0.5)
        expected[~upper, 1] += np.float32(0.5)
        entry["swap_mismatches"] = int(np.any(a["uv"] != expected, axis=1).sum())
        entry["loops"] = len(b["uv"])
    else:
        entry["swap_mismatches"] = -1
    entry["ok"] = (entry["copy_before"]["ok"] and entry["copy_after"]["ok"] and entry["swap_mismatches"] == 0
                   and a["layer"] == "PaintUV")
    if not entry["ok"]:
        failed.append(name)
    results[name] = entry
report = {"before": before_path, "after": after_path, "parts": results, "failed": failed,
          "verdict": "pass" if not failed else "fail"}
with open(out, "w", encoding="utf-8") as handle:
    json.dump(report, handle, indent=2)
gaps = {n: round(r["full_stack_symmetry_gap"], 6) for n, r in results.items() if r["full_stack_symmetry_gap"] > POSITION}
print(f"UVCHECK verdict={report['verdict']} parts={len(results)} failed={failed} asymmetric_full_stack={gaps}",
      file=sys.stderr)
sys.exit(0 if not failed else 3)
