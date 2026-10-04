"""End-to-end smoke of every ship tool on the generated fixture; prints SHIP_SMOKE_PASS.

Run: blender -b --factory-startup --python-exit-code 1 -P blender_ship_smoke.py -- --out DIR
"""

import argparse
import json
import os
from pathlib import Path
import re
import shutil
import subprocess
import sys

import bpy
import numpy as np

TESTS = Path(__file__).resolve().parent
TOOLS = TESTS.parent
sys.path.insert(0, str(TOOLS))
import ship_contract as contract

parser = argparse.ArgumentParser()
parser.add_argument("--out", required=True)
out = Path(parser.parse_args(sys.argv[sys.argv.index("--") + 1:]).out).resolve()
shutil.rmtree(out, ignore_errors=True)
out.mkdir(parents=True)
results = {}


CHILD_ENV = {key: value for key, value in os.environ.items() if key != "OCIO"}


def blender(script, *args):
    """Child Blender without the OCIO variable this Blender exports, which it would log on stdout."""
    return subprocess.run([bpy.app.binary_path, "-b", "--factory-startup", "--python-exit-code", "1",
                           "-P", str(script), "--", *map(str, args)], capture_output=True, text=True, env=CHILD_ENV)


def ship(name, variant=None, **overrides):
    folder = out / name
    folder.mkdir()
    spec = {"schema_version": 1, "name": "Fixture", "source": "Fixture.blend", "texture_size": 1024,
            "uv_density_max_ratio": 2.0, "contour_roles": ["hull", "canopy"], "exemptions": [], **overrides}
    path = folder / "ship.json"
    path.write_text(json.dumps(spec), encoding="utf-8")
    if variant is not False:
        result = blender(TESTS / "fixture_ship.py", "--ship", path, *(["--variant", variant] if variant else []))
        assert result.returncode == 0, result.stderr[-3000:]
    return path


def tool(name, ship_json, *args, expect=0):
    """Run a tool; assert its exit code and that stdout holds only trailers. Returns (trailers, report)."""
    result = blender(TOOLS / f"ship_{name}.py", "--ship", ship_json, "--out", ship_json.parent / f"out_{name}", *args)
    assert result.returncode == expect, (name, args, result.returncode, result.stdout, result.stderr[-3000:])
    lines = result.stdout.splitlines()
    assert all(re.fullmatch(r"SHIP_[A-Z]+=.+", line) for line in lines), (name, result.stdout)
    trailers = dict(line.split("=", 1) for line in lines)
    assert trailers["SHIP_VERDICT"] == ("pass" if expect == 0 else "fail"), trailers
    report = json.loads(Path(trailers["SHIP_REPORT"]).read_text(encoding="utf-8"))
    for key in ("schema_version", "recipe", "blender_version", "source_sha256"):
        assert key in report, (name, key)
    return trailers, report


def rules(report):
    return {(finding["rule"], finding["part"]) for finding in report["findings"]}


base = ship("base")
source = base.parent / "Fixture.blend"
source_sha = contract.sha256_file(source)

# ship_new refuses an existing source.
tool("new", base, expect=3)

# ship_check passes; the fingerprint is stable across runs and a save-as copy.
first = tool("check", base)[1]
second = tool("check", base)[1]
assert first["findings"] == [], first["findings"]
assert first["fingerprint"] == second["fingerprint"]
copy = ship("copy", variant=False)
bpy.ops.wm.open_mainfile(filepath=str(source))
bpy.ops.wm.save_as_mainfile(filepath=str(copy.parent / "Fixture.blend"), copy=True)
bpy.ops.wm.read_factory_settings(use_empty=True)
assert tool("check", copy)[1]["fingerprint"] == first["fingerprint"]
assert {"Fin", "Scratch", "Spine"} <= set(first["fingerprint"]["parts"])
results["fingerprint"] = first["fingerprint"]["components"]["geometry"]

# ship_lock: lock, verify, then reopen reports nothing invalidated.
trailers, _ = tool("lock", base, "--mode", "lock")
lock = Path(trailers["SHIP_LOCK"])
assert lock.is_file() and lock.parent == base.parent
tool("lock", base, "--mode", "verify")
assert tool("lock", base, "--mode", "reopen")[1]["invalidated"] == []

# ship_uv passes within the ratio.
results["uv_ratio"] = tool("uv", base)[1]["ratio"]

# ship_render: every set writes its images; comparing against the copy changes nothing.
for render_set in ("ortho", "turnaround", "sheet", "scale"):
    images = tool("render", base, "--set", render_set, "--size", "128")[1]["images"]
    assert images and all(Path(path).is_file() for path in images.values()), images
compare = tool("render", base, "--set", "compare", "--size", "128", "--before", copy.parent / "Fixture.blend")[1]
assert compare["changed_pixels"] == {"top": 0, "side": 0}, compare["changed_pixels"]

# ship_export, then re-import the FBX.
trailers, export = tool("export", base)
sidecar = json.loads(Path(export["sidecar"]).read_text(encoding="utf-8"))
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.wm.fbx_import(filepath=trailers["SHIP_FBX"])
objects = {obj.name: obj for obj in bpy.context.scene.objects}
meshes = {name for name, obj in objects.items() if obj.type == "MESH"}
assert meshes == {"Hull", "Canopy", "Cores", "Collider"}, meshes
assert set(objects) == meshes | {"Fixture", "Sockets", "engine.L", "engine.R"}, set(objects)
for name, matrix in sidecar["sockets"].items():
    expected = np.array(matrix)[:3, 3]
    assert np.abs(np.array(objects[name].matrix_world.translation) - expected).max() < 1e-5, name
for name in sorted(meshes):
    mesh = objects[name].data
    assert all(polygon.loop_total == 3 for polygon in mesh.polygons), name
    if name == "Collider":
        continue
    role = sidecar["roles"][name]
    assert [material.name for material in mesh.materials] == role["material_slots"], name
    co = np.array([v.co for v in mesh.vertices], np.float64)
    corners = np.array([loop.vertex_index for loop in mesh.loops])
    normals = np.array([n.vector for n in mesh.corner_normals], np.float64)
    a, b, c = (co[corners[i::3]] for i in range(3))
    face = np.cross(b - a, c - a)
    face /= np.linalg.norm(face, axis=1, keepdims=True)
    slot = np.array([polygon.material_index for polygon in mesh.polygons])
    contour = slot == len(role["material_slots"]) - 1 if role["contour"] else np.zeros(len(slot), bool)
    surface = np.repeat(~contour, 3)
    assert np.abs(normals[surface] - np.repeat(face, 3, axis=0)[surface]).max() < 1e-4, name
    if role["contour"]:
        assert contour.sum() == (~contour).sum() > 0, (name, contour.sum(), (~contour).sum())
        joined = {}
        for vertex, normal in zip(corners[~surface], normals[~surface]):
            joined.setdefault(tuple(co[vertex]), []).append(normal)
        spread = max(np.abs(np.array(group) - group[0]).max() for group in joined.values())
        assert spread < 1e-4, (name, spread)
        assert np.abs(normals[~surface] - np.repeat(face, 3, axis=0)[~surface]).max() > 0.1, name
    results[f"{name}_triangles"] = len(mesh.polygons)
hull = np.array([v.co for v in objects["Hull"].data.vertices])
assert abs(hull[:, 2].max() - 0.5) < 1e-5, "hidden Fin missing from Hull"
everything = np.concatenate([[v.co for v in objects[n].data.vertices] for n in meshes])
assert "Scratch" not in objects and everything[:, 0].max() < 4, "ignored Scratch exported"

# One failure case per tool.
assert ("modifier_order", "Wing") in rules(tool("check", ship("solidify_first", "solidify_first"), expect=3)[1])
assert ("name", "Cube") in rules(tool("check", ship("cube_name", "cube_name"), expect=3)[1])
assert ("uv_density", "Canopy") in rules(tool("uv", ship("scaled_uv", "scaled_uv"), expect=3)[1])
moved = ship("moved_hidden_vertex", "moved_hidden_vertex")
shutil.copy(lock, moved.parent / "lock.json")
assert tool("lock", moved, "--mode", "verify", expect=3)[1]["changed"] == ["Fin"]
reopened = tool("lock", moved, "--mode", "reopen")[1]
assert reopened["changed"] == ["Fin"] and reopened["invalidated"] == list(contract.INVALIDATED_BY_GEOMETRY)
tool("lock", moved, "--mode", "verify")
repainted = ship("repainted_weight", "repainted_weight")
shutil.copy(lock, repainted.parent / "lock.json")
assert tool("lock", repainted, "--mode", "verify", expect=3)[1]["changed"] == ["Wing"]

assert contract.sha256_file(source) == source_sha, "a tool changed the fixture source"
assert not list(out.rglob("*.blend1")), list(out.rglob("*.blend1"))
print("SHIP_SMOKE_PASS", json.dumps(results))
