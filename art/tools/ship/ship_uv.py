"""Check texel density across a ship's visual parts on the PaintUV layer.

CLI: blender -b --factory-startup --python-exit-code 1 -P ship_uv.py -- --ship SHIP_JSON --out DIR
Density per part is sqrt(UV area / surface area) * texture_size, in texels per unit, on the
evaluated mesh in symmetry-origin space. Exit 3 when max/min density across non-exempt parts
exceeds uv_density_max_ratio (findings name the densest and sparsest part) or a part has no
usable PaintUV layer. Report: <out>/ship_uv.json with every part's density.
"""

from pathlib import Path
import sys

import bpy
import numpy as np

sys.path.insert(0, str(Path(__file__).resolve().parent))
import ship_contract as contract
import ship_source


def density(mesh, matrix, texture_size):
    layer = mesh.uv_layers.get(contract.PAINT_UV)
    if layer is None:
        return None
    co, corners, _, triangles = ship_source.mesh_arrays(mesh)
    if not len(triangles):
        return None
    points = co @ np.array(matrix.to_3x3(), np.float64).T
    uv = ship_source.read_array(layer.uv, "vector", np.float64, 2).reshape(-1, 2)
    a, b, c = (points[corners[triangles[:, i]]] for i in range(3))
    surface = 0.5 * np.linalg.norm(np.cross(b - a, c - a), axis=1).sum()
    p, q, r = (uv[triangles[:, i]] for i in range(3))
    texture = 0.5 * np.abs((q - p)[:, 0] * (r - p)[:, 1] - (q - p)[:, 1] * (r - p)[:, 0]).sum()
    if surface <= 0 or texture <= 0:
        return None
    return float(np.sqrt(texture / surface) * texture_size)


def main(argv):
    args = contract.tool_parser("Check texel density across a ship's parts.").parse_args(argv)
    ship, source = contract.load_ship(args.ship)
    report = contract.Report("ship_uv", args.out, ship_source.blender_version(), [source])
    if not source.is_file():
        raise report.refuse(f"Source not found: {source}")
    scene = ship_source.open_source(source)
    roles = ship_source.Roles(scene)
    parts = sorted(name for role in contract.VISUAL_ROLES for name in roles.members.get(role, ())
                   if scene.objects[name].type == "MESH")
    if not parts:
        raise report.refuse("No visual role parts")
    evaluation = ship_source.Evaluation(scene)
    densities, findings = {}, []
    for name in parts:
        mesh = evaluation.mesh(name)
        try:
            densities[name] = density(mesh, evaluation.to_space(name), ship["texture_size"])
        finally:
            bpy.data.meshes.remove(mesh)
        if densities[name] is None:
            findings.append({"rule": "uv_layer", "part": name,
                             "message": f"No {contract.PAINT_UV} layer with UV area"})
    exempt = {part for e in ship["exemptions"] if e["rule"] == "uv_density" for part in e["parts"]}
    measured = {name: value for name, value in densities.items() if value is not None and name not in exempt}
    ratio = None
    if measured:
        low, high = min(measured, key=measured.get), max(measured, key=measured.get)
        ratio = measured[high] / measured[low]
        if ratio > ship["uv_density_max_ratio"]:
            for name in (high, low):
                findings.append({"rule": "uv_density", "part": name,
                                 "message": f"Density ratio {ratio:.3f} exceeds {ship['uv_density_max_ratio']}"})
    findings, exempted = contract.apply_exemptions(findings, ship["exemptions"])
    for finding in findings:
        print(f"{finding['rule']}: {finding['part']}: {finding['message']}", file=sys.stderr)
    return report.finish(not findings, densities=densities, ratio=ratio,
                         max_ratio=ship["uv_density_max_ratio"], findings=findings, exempted=exempted)


if __name__ == "__main__":
    contract.run(main)
