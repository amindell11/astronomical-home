"""Export a ship's role collections to <Name>.fbx plus <Name>.export.json.

CLI: blender -b --factory-startup --python-exit-code 1 -P ship_export.py -- --ship SHIP_JSON --out DIR
FBX: root empty <Name> in symmetry-origin space (-Z forward, Y up); one triangulated mesh per
present visual role (Hull, Canopy, Cores, Ink) with material slots sorted by name, Collider
without materials, and socket empties verbatim under Sockets. Roles in contour_roles carry a
contour: a separate vertex range with joined normals, in a last slot named Contour.
The sidecar records parts, slots, triangle and vertex ranges, sockets and the fingerprint.
Refuses (exit 3) a source without SymmetryOrigin, with role grammar errors, reserved
move/debris roles, an empty hull, or faces without a material. Prints SHIP_FBX=<fbx path>.
"""

import json
from pathlib import Path
import sys

import bmesh
import bpy
import numpy as np

sys.path.insert(0, str(Path(__file__).resolve().parent))
import ship_contract as contract
import ship_source


class Part:
    """One part's evaluated, triangulated corners in part-local space; `placed` maps them to symmetry-origin space."""

    def __init__(self, evaluation, name, uv_names):
        mesh = evaluation.mesh(name)
        try:
            corner_ids = mesh.attributes.new("ship_corner", "INT", "CORNER")
            corner_ids.data.foreach_set("value", np.arange(len(mesh.loops), dtype=np.int32))
            normals = ship_source.read_array(mesh.corner_normals, "vector", np.float32, 3).reshape(-1, 3)
            work = bmesh.new()
            work.from_mesh(mesh)
            bmesh.ops.triangulate(work, faces=work.faces[:], quad_method="BEAUTY", ngon_method="BEAUTY")
            work.to_mesh(mesh)
            work.free()
            original = ship_source.read_array(mesh.attributes["ship_corner"].data, "value", np.int32)
            self.co = ship_source.read_array(mesh.vertices, "co", np.float32, 3).reshape(-1, 3)
            self.corners = ship_source.read_array(mesh.loops, "vertex_index", np.int32)
            self.normals = normals[original]
            self.material_index = ship_source.read_array(mesh.polygons, "material_index", np.int32)
            self.uvs = {}
            for uv_name in uv_names:
                layer = mesh.uv_layers.get(uv_name)
                self.uvs[uv_name] = (ship_source.read_array(layer.uv, "vector", np.float32, 2).reshape(-1, 2)
                                     if layer else np.zeros((len(self.corners), 2), np.float32))
        finally:
            bpy.data.meshes.remove(mesh)
        self.materials = evaluation.materials(name)
        self.matrix = np.array(evaluation.to_space(name), np.float64)

    def contour_normals(self):
        """Per position within the part, sum the normals of its distinct split vertices, then normalize."""
        position = self.co[self.corners]
        split = np.concatenate([position, self.normals, *self.uvs.values()], axis=1)
        _, first = np.unique(np.ascontiguousarray(split).view(np.dtype((np.void, split.dtype.itemsize * split.shape[1]))),
                             return_index=True)
        keys = np.ascontiguousarray(position).view(np.dtype((np.void, 12))).ravel()
        _, group = np.unique(keys, return_inverse=True)
        joined = np.zeros((group.max() + 1, 3), np.float64)
        np.add.at(joined, group[first], self.normals[first].astype(np.float64))
        joined /= np.linalg.norm(joined, axis=1, keepdims=True)
        return joined[group].astype(np.float32)

    def placed(self, normals):
        linear, offset = self.matrix[:3, :3], self.matrix[:3, 3]
        co = (self.co.astype(np.float64) @ linear.T + offset).astype(np.float32)
        turned = normals.astype(np.float64) @ np.linalg.inv(linear)
        turned /= np.linalg.norm(turned, axis=1, keepdims=True)
        return co, turned.astype(np.float32)


def build_mesh(name, pieces, uv_names, slot_count):
    """Join pieces (co, corners, normals, material_index, uvs) as separate vertex ranges."""
    mesh = bpy.data.meshes.new(name)
    co = np.concatenate([p[0] for p in pieces])
    offsets = np.cumsum([0] + [len(p[0]) for p in pieces[:-1]])
    corners = np.concatenate([p[1] + offset for p, offset in zip(pieces, offsets)]).astype(np.int32)
    mesh.vertices.add(len(co))
    mesh.vertices.foreach_set("co", co.ravel())
    mesh.loops.add(len(corners))
    mesh.loops.foreach_set("vertex_index", corners)
    mesh.polygons.add(len(corners) // 3)
    mesh.polygons.foreach_set("loop_start", np.arange(0, len(corners), 3, dtype=np.int32))
    mesh.update(calc_edges=True)
    if slot_count:
        mesh.polygons.foreach_set("material_index", np.concatenate([p[3] for p in pieces]).astype(np.int32))
    for uv_name in uv_names:
        mesh.uv_layers.new(name=uv_name).uv.foreach_set("vector", np.concatenate([p[4][uv_name] for p in pieces]).ravel())
    mesh.attributes.new("custom_normal", "FLOAT_VECTOR", "CORNER").data.foreach_set(
        "vector", np.concatenate([p[2] for p in pieces]).ravel())
    mesh.update()
    return mesh


def export_role(evaluation, report, role, names, contour):
    uv_names = sorted({layer.name for name in names for layer in evaluation.copies[name].data.uv_layers})
    parts = {name: Part(evaluation, name, uv_names) for name in names}
    slots = sorted({m.name for part in parts.values() for m in part.materials if m is not None})
    for name, part in parts.items():
        used = np.unique(part.material_index)
        if len(used) and (used.max() >= len(part.materials) or any(part.materials[i] is None for i in used)):
            raise report.refuse(f"{name} has faces without a material")
    pieces, contour_pieces = [], []
    for name, part in parts.items():
        remap = np.array([slots.index(m.name) if m is not None else -1 for m in part.materials], np.int32)
        co, normals = part.placed(part.normals)
        pieces.append((co, part.corners, normals, remap[part.material_index], part.uvs))
        if contour:
            co, normals = part.placed(part.contour_normals())
            contour_pieces.append((co, part.corners, normals, np.full(len(part.material_index), len(slots), np.int32),
                                   part.uvs))
    mesh = build_mesh(role, pieces + contour_pieces, uv_names, len(slots) + bool(contour))
    for slot in slots:
        mesh.materials.append(bpy.data.materials[slot])
    if contour:
        mesh.materials.append(bpy.data.materials.get(contract.CONTOUR) or bpy.data.materials.new(contract.CONTOUR))
    return mesh, {"parts": list(parts), "material_slots": slots + ([contract.CONTOUR] if contour else []),
                  "uv_layers": uv_names, "surface": ranges(parts, pieces, 0, 0),
                  "contour": ranges(parts, contour_pieces, *(sum(len(p[i]) for p in pieces) for i in (0, 1)))}


def ranges(names, pieces, vertex, corner):
    """Per part [first vertex, vertex count, first triangle, triangle count] of one vertex range."""
    record = {}
    for name, piece in zip(names, pieces):
        record[name] = [int(vertex), len(piece[0]), int(corner) // 3, len(piece[1]) // 3]
        vertex, corner = vertex + len(piece[0]), corner + len(piece[1])
    return record


def export_collider(evaluation, names):
    pieces = []
    for name in names:
        part = Part(evaluation, name, [])
        co, normals = part.placed(part.normals)
        pieces.append((co, part.corners, normals, np.zeros(len(part.material_index), np.int32), {}))
    mesh = build_mesh("Collider", pieces, [], 0)
    return mesh, {"parts": list(names), "triangles": len(mesh.polygons)}


def main(argv):
    args = contract.tool_parser("Export a ship's role collections to FBX.").parse_args(argv)
    ship, source = contract.load_ship(args.ship)
    out = Path(args.out).resolve()
    fbx = out / f"{ship['name']}.fbx"
    trailers = [("SHIP_FBX", str(fbx))]
    report = contract.Report("ship_export", out, ship_source.blender_version(), [source])
    if not source.is_file():
        raise report.refuse(f"Source not found: {source}")
    scene = ship_source.open_source(source)
    roles = ship_source.Roles(scene)
    if roles.errors:
        raise report.refuse("; ".join(error for _, error in roles.errors))
    if roles.reserved:
        raise report.refuse(f"{', '.join(sorted(roles.reserved))}: not supported in this version")
    meshes = {role: sorted(n for n in roles.members.get(role, ()) if scene.objects[n].type == "MESH")
              for role in contract.STATIC_ROLES}
    if not meshes["hull"]:
        raise report.refuse("role.hull has no mesh parts")
    visual = [name for role in contract.VISUAL_ROLES for name in meshes[role]]
    if len(visual) != len(set(visual)):
        raise report.refuse("A part sits in more than one visual role")
    fingerprint = ship_source.fingerprint(scene)
    evaluation = ship_source.Evaluation(scene)
    if not evaluation.has_origin:
        raise report.refuse(f"No {contract.SYMMETRY_ORIGIN} empty")

    built, sidecar_roles = [], {}
    for role, export_name in contract.VISUAL_ROLES.items():
        if meshes[role]:
            mesh, record = export_role(evaluation, report, export_name, meshes[role], role in ship["contour_roles"])
            built.append((export_name, mesh))
            sidecar_roles[export_name] = record
    if meshes["collider"]:
        mesh, record = export_collider(evaluation, meshes["collider"])
        built.append(("Collider", mesh))
        sidecar_roles["Collider"] = record
    sockets = {name: evaluation.to_space(name) for name in sorted(roles.members.get("sockets", ()))
               if scene.objects[name].type == "EMPTY"}

    for block in list(bpy.data.objects) + list(bpy.data.meshes):
        if block not in [mesh for _, mesh in built]:
            block.name = "~source~" + block.name
    export_scene = bpy.data.scenes.new("ship export")
    root = bpy.data.objects.new(ship["name"], None)
    export_scene.collection.objects.link(root)
    for name, mesh in built:
        mesh.name = name
        obj = bpy.data.objects.new(name, mesh)
        obj.parent = root
        export_scene.collection.objects.link(obj)
    if sockets:
        group = bpy.data.objects.new("Sockets", None)
        group.parent = root
        export_scene.collection.objects.link(group)
        for name, matrix in sockets.items():
            socket = bpy.data.objects.new(name, None)
            socket.parent = group
            socket.matrix_basis = matrix
            export_scene.collection.objects.link(socket)
    bpy.context.window.scene = export_scene
    for obj in export_scene.objects:
        obj.select_set(True)
    bpy.ops.export_scene.fbx(filepath=str(fbx), use_selection=True, object_types={"EMPTY", "MESH"},
                             use_mesh_modifiers=False, use_triangles=False, mesh_smooth_type="OFF",
                             add_leaf_bones=False, bake_anim=False, use_custom_props=False, path_mode="STRIP",
                             axis_forward="-Z", axis_up="Y", global_scale=1.0, apply_unit_scale=True)
    sidecar = {"schema_version": contract.SCHEMA_VERSION, "recipe": contract.FINGERPRINT_RECIPE,
               "blender_version": ship_source.blender_version(), "source_sha256": report.data["source_sha256"],
               "name": ship["name"], "fbx": fbx.name, "fbx_sha256": contract.sha256_file(fbx),
               "axes": {"forward": "-Z", "up": "Y"}, "roles": sidecar_roles,
               "sockets": {name: [list(row) for row in matrix] for name, matrix in sockets.items()},
               "fingerprint": fingerprint["components"]}
    sidecar_path = out / f"{ship['name']}.export.json"
    sidecar_path.write_text(json.dumps(sidecar, indent=2) + "\n", encoding="utf-8")
    return report.finish(True, trailers, fbx=str(fbx), sidecar=str(sidecar_path))


if __name__ == "__main__":
    contract.run(main)
