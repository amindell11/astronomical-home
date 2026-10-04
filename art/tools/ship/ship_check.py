"""Lint a ship source and record its per-part fingerprints.

CLI: blender -b --factory-startup --python-exit-code 1 -P ship_check.py -- --ship SHIP_JSON --out DIR
Report <out>/ship_check.json: findings and exempted findings ({rule, part, message[, reason]})
and the ship-fp-1 fingerprint. Exit 3 when any finding is not exempted in ship.json.
"""

import os
from pathlib import Path
import sys

import bpy
import numpy as np

sys.path.insert(0, str(Path(__file__).resolve().parent))
import ship_contract as contract
import ship_source

TOLERANCE = 1e-5
NGON_TOLERANCE = 1e-4
COLLIDER_TRIANGLES = 255
SUPPORTED_MODIFIERS = set(ship_source.GEOMETRY_PARAMETERS)


def _mirror_findings(obj, origin_world):
    findings = []
    for modifier in (m for m in obj.modifiers if m.type == "MIRROR"):
        if not modifier.use_axis[0]:
            findings.append(f"Mirror {modifier.name!r} does not mirror across X")
            continue
        if origin_world is None:
            continue
        frame = ship_source.stored_world(modifier.mirror_object or obj)
        offset = (frame.translation - origin_world.translation).length
        axis = frame.to_3x3().col[0].normalized().cross(origin_world.to_3x3().col[0].normalized()).length
        if offset > TOLERANCE or axis > TOLERANCE:
            findings.append(f"Mirror {modifier.name!r} plane is not on the {contract.SYMMETRY_ORIGIN} plane")
    return findings


def _nonplanar_ngons(mesh):
    co = np.array([v.co for v in mesh.vertices], np.float64).reshape(-1, 3)
    count = 0
    for polygon in mesh.polygons:
        if polygon.loop_total <= 4:
            continue
        points = co[list(polygon.vertices)]
        normal = np.array(polygon.normal, np.float64)
        if np.abs((points - points.mean(axis=0)) @ normal).max() > NGON_TOLERANCE:
            count += 1
    return count


def _inward(mesh):
    """True only for a closed mesh whose signed volume is negative; open meshes are not judged."""
    co, corners, _, triangles = ship_source.mesh_arrays(mesh)
    if not len(triangles):
        return False
    edge_use = np.bincount(ship_source.read_array(mesh.loops, "edge_index", np.int32), minlength=len(mesh.edges))
    if np.any(edge_use != 2):
        return False
    a, b, c = (co[corners[triangles[:, i]]].astype(np.float64) for i in range(3))
    return float(np.einsum("ij,ij->i", a, np.cross(b, c)).sum()) < 0


def _missing_files():
    missing = []
    for collection in (bpy.data.images, bpy.data.libraries, bpy.data.fonts, bpy.data.sounds,
                       bpy.data.movieclips, bpy.data.volumes, bpy.data.cache_files):
        for block in collection:
            if getattr(block, "packed_file", None) is not None or getattr(block, "packed_files", None):
                continue
            if collection == bpy.data.images and block.source not in ("FILE", "SEQUENCE", "MOVIE", "TILED"):
                continue
            path = block.filepath
            if not path or path == "<builtin>":
                continue
            if not os.path.exists(bpy.path.abspath(path, library=block.library)):
                missing.append((block.name, path))
    return missing


def check(scene, ship):
    findings = []

    def find(rule, part, message):
        findings.append({"rule": rule, "part": part, "message": message})

    roles = ship_source.Roles(scene)
    for collection, error in roles.errors:
        find("roles", collection, error)
    if not roles.members.get("hull"):
        find("roles", "role.hull", "role.hull is missing or empty")
    objects = {o.name: o for o in ship_source.ship_objects(scene)}
    for name in sorted(roles.members.get("sockets", ())):
        if scene.objects[name].type != "EMPTY":
            find("roles", name, "role.sockets holds only empties")

    origin = objects.get(contract.SYMMETRY_ORIGIN)
    origin_world = None
    if origin is None or origin.type != "EMPTY" or origin.parent is not None:
        find("origin", contract.SYMMETRY_ORIGIN, f"No parentless {contract.SYMMETRY_ORIGIN} empty")
    else:
        origin_world = ship_source.stored_world(origin)

    evaluation = ship_source.Evaluation(scene)
    collider_triangles = 0
    for name, obj in objects.items():
        if name in roles.ignored or obj is origin:
            continue
        ancestor = obj.parent
        while ancestor is not None and ancestor is not origin:
            ancestor = ancestor.parent
        if origin_world is not None and ancestor is None:
            find("origin", name, f"Not parented under {contract.SYMMETRY_ORIGIN}")
        if ship_source.DEFAULT_NAME.fullmatch(name):
            find("name", name, "Default-named part")
        if any(abs(s - 1.0) > TOLERANCE for s in obj.matrix_basis.to_scale()):
            find("scale", name, f"Unapplied scale {tuple(round(s, 6) for s in obj.matrix_basis.to_scale())}")
        types = [m.type for m in obj.modifiers]
        for kind in sorted(set(types) - SUPPORTED_MODIFIERS):
            find("modifier_type", name, f"Unsupported modifier type {kind}")
        mirrors = [i for i, kind in enumerate(types) if kind == "MIRROR"]
        if "SOLIDIFY" in types and mirrors and types.index("SOLIDIFY") < mirrors[-1]:
            find("modifier_order", name, "Solidify precedes Mirror")
        for message in _mirror_findings(obj, origin_world):
            find("mirror", name, message)
        if obj.type != "MESH":
            continue
        ngons = _nonplanar_ngons(obj.data)
        if ngons:
            find("ngon", name, f"{ngons} non-planar n-gon(s)")
        visual = roles.visual_of(name)
        in_collider = name in roles.members.get("collider", ())
        if in_collider and visual:
            find("visual_role", name, f"Collider part also in {sorted(visual)}")
        elif not in_collider and len(visual) != 1:
            find("visual_role", name, f"In {len(visual)} visual roles; expected exactly one")
        mesh = evaluation.mesh(name)
        try:
            if _inward(mesh):
                find("normals", name, "Closed mesh with inward normals")
            if in_collider:
                mesh.calc_loop_triangles()
                collider_triangles += len(mesh.loop_triangles)
        finally:
            bpy.data.meshes.remove(mesh)
    if collider_triangles > COLLIDER_TRIANGLES:
        find("collider", "role.collider", f"{collider_triangles} triangles; at most {COLLIDER_TRIANGLES}")
    for block, path in _missing_files():
        find("missing_file", block, f"Missing external file {path}")
    if scene.unit_settings.scale_length != 1.0:
        find("unit_scale", scene.name, f"Unit scale {scene.unit_settings.scale_length}; expected 1")
    return contract.apply_exemptions(findings, ship["exemptions"])


def main(argv):
    args = contract.tool_parser("Lint a ship source.").parse_args(argv)
    ship, source = contract.load_ship(args.ship)
    report = contract.Report("ship_check", args.out, ship_source.blender_version(), [source])
    if not source.is_file():
        raise report.refuse(f"Source not found: {source}")
    scene = ship_source.open_source(source)
    fingerprint = ship_source.fingerprint(scene)
    findings, exempted = check(scene, ship)
    for finding in findings:
        print(f"{finding['rule']}: {finding['part']}: {finding['message']}", file=sys.stderr)
    return report.finish(not findings, findings=findings, exempted=exempted, fingerprint=fingerprint)


if __name__ == "__main__":
    contract.run(main)
