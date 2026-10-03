"""Shared Blender layer for the ship tools: open a source, find roles, fingerprint, evaluate.

A source is never saved: tools open it, read stored data, and do all evaluation on copies
in a temporary scene. The ship scene is the file's active scene.
"""

import hashlib
import json
from pathlib import Path
import re
import sys

import bpy
from mathutils import Matrix
import numpy as np

sys.path.insert(0, str(Path(__file__).resolve().parent))
import ship_contract as contract

GEOMETRY_PARAMETERS = {
    "MIRROR": ("use_axis", "use_bisect_axis", "use_bisect_flip_axis", "use_clip", "use_mirror_merge",
               "merge_threshold", "bisect_threshold", "mirror_object"),
    "SOLIDIFY": ("solidify_mode", "thickness", "thickness_clamp", "use_thickness_angle_clamp", "offset",
                 "use_rim", "use_rim_only", "use_even_offset", "use_quality_normals", "use_flip_normals",
                 "use_flat_faces", "nonmanifold_thickness_mode", "nonmanifold_boundary_mode",
                 "nonmanifold_merge_threshold", "bevel_convex", "vertex_group", "thickness_vertex_group",
                 "invert_vertex_group"),
    "BEVEL": ("width", "width_pct", "segments", "affect", "limit_method", "angle_limit", "offset_type",
              "profile_type", "profile", "use_clamp_overlap", "loop_slide", "harden_normals", "miter_outer",
              "miter_inner", "spread", "vmesh_method", "vertex_group", "invert_vertex_group", "edge_weight",
              "vertex_weight", "face_strength_mode", "mark_sharp"),
    "SUBSURF": ("subdivision_type", "levels", "quality", "boundary_smooth", "use_creases",
                "use_custom_normals", "use_limit_surface"),
}
UV_PARAMETERS = {
    "MIRROR": ("use_mirror_u", "use_mirror_v", "use_mirror_udim", "mirror_offset_u", "mirror_offset_v",
               "offset_u", "offset_v"),
    "BEVEL": ("mark_seam",),
    "SUBSURF": ("uv_smooth",),
}


def blender_version():
    return bpy.app.version_string


def open_source(path):
    bpy.ops.wm.open_mainfile(filepath=str(path), load_ui=False, use_scripts=False)
    return bpy.context.scene


def ship_objects(scene):
    """Every MESH and EMPTY in the ship scene, hidden, excluded and ignored ones included, sorted by name."""
    return sorted((o for o in scene.objects if o.type in ("MESH", "EMPTY")), key=lambda o: o.name)


def _collections(scene):
    found, stack = [], list(scene.collection.children)
    while stack:
        collection = stack.pop()
        if collection not in found:
            found.append(collection)
            stack.extend(collection.children)
    return found


class Roles:
    def __init__(self, scene):
        self.errors = []
        self.members = {}
        self.reserved = []
        self.ignored = set()
        for collection in _collections(scene):
            if collection.name == contract.IGNORE:
                self.ignored |= {o.name for o in collection.all_objects}
                continue
            try:
                parsed = contract.parse_role(collection.name)
            except ValueError as error:
                self.errors.append((collection.name, str(error)))
                continue
            if parsed is None:
                continue
            kind, ident = parsed
            if ident is not None:
                self.reserved.append(collection.name)
            key = kind if ident is None else f"{kind}.{ident}"
            self.members.setdefault(key, set()).update(o.name for o in collection.all_objects)

    def of(self, name):
        return {key for key, names in self.members.items() if name in names}

    def visual_of(self, name):
        return self.of(name) & set(contract.VISUAL_ROLES)


def stored_world(obj):
    """World matrix from stored transforms only, so visibility and depsgraph state never matter."""
    local = obj.matrix_parent_inverse @ obj.matrix_basis if obj.parent else obj.matrix_basis.copy()
    return stored_world(obj.parent) @ local if obj.parent else local


def _encode(value):
    if hasattr(value, "name") and hasattr(value, "bl_rna"):
        return value.name
    if isinstance(value, str) or isinstance(value, (bool, int, float)) or value is None:
        return value
    return [_encode(item) for item in value]


def _modifier_record(modifier, table):
    return [modifier.type, modifier.show_viewport,
            [_encode(getattr(modifier, name)) for name in table.get(modifier.type, ())]]


def read_array(collection, attribute, dtype, width=1):
    values = np.empty(len(collection) * width, dtype)
    collection.foreach_get(attribute, values)
    return values


def _matrix(matrix):
    return np.array(matrix, np.float32).tobytes()


def part_fingerprint(obj):
    geometry, uv, materials = hashlib.sha256(), hashlib.sha256(), hashlib.sha256()
    geometry.update(obj.type.encode())
    geometry.update(_matrix(obj.matrix_basis))
    parent = [obj.parent.name, obj.parent_type, obj.parent_bone] if obj.parent else None
    geometry.update(json.dumps(parent).encode())
    geometry.update(_matrix(obj.matrix_parent_inverse))
    geometry.update(json.dumps([_modifier_record(m, GEOMETRY_PARAMETERS) for m in obj.modifiers]).encode())
    uv.update(json.dumps([_modifier_record(m, UV_PARAMETERS) for m in obj.modifiers]).encode())
    materials.update(json.dumps([slot.material.name if slot.material else None
                                 for slot in obj.material_slots]).encode())
    if obj.type == "MESH":
        mesh = obj.data
        geometry.update(read_array(mesh.vertices, "co", np.float32, 3).tobytes())
        geometry.update(read_array(mesh.loops, "vertex_index", np.int32).tobytes())
        geometry.update(read_array(mesh.polygons, "loop_start", np.int32).tobytes())
        geometry.update(read_array(mesh.polygons, "loop_total", np.int32).tobytes())
        if mesh.shape_keys:
            keys = mesh.shape_keys
            geometry.update(json.dumps([keys.use_relative, keys.reference_key.name]).encode())
            for block in keys.key_blocks:
                geometry.update(json.dumps([block.name, block.relative_key.name, block.value, block.mute,
                                            block.slider_min, block.slider_max, block.interpolation,
                                            block.vertex_group]).encode())
                geometry.update(read_array(block.data, "co", np.float32, 3).tobytes())
        for layer in mesh.uv_layers:
            uv.update(layer.name.encode())
            uv.update(read_array(layer.uv, "vector", np.float32, 2).tobytes())
        materials.update(read_array(mesh.polygons, "material_index", np.int32).tobytes())
    return {"geometry": geometry.hexdigest(), "uv": uv.hexdigest(), "materials": materials.hexdigest()}


def fingerprint(scene):
    parts = {obj.name: part_fingerprint(obj) for obj in ship_objects(scene)}
    components = {}
    for component in ("geometry", "uv", "materials"):
        digest = hashlib.sha256()
        for name, record in parts.items():
            digest.update(json.dumps([name, record[component]]).encode())
        components[component] = digest.hexdigest()
    return {"recipe": contract.FINGERPRINT_RECIPE, "components": components, "parts": parts}


class Evaluation:
    """Unhidden copies of every ship object in a temporary scene, evaluated with live modifiers.

    Copies keep their parent chain and mirror objects (remapped to copies), so evaluation
    matches the source regardless of visibility or collection exclusion. `space` maps world
    to symmetry-origin space (identity when the source has no origin).
    """

    def __init__(self, scene):
        self.scene = bpy.data.scenes.new("ship evaluation")
        self.scene.unit_settings.scale_length = scene.unit_settings.scale_length
        self.copies = {}
        for obj in ship_objects(scene):
            copy = obj.copy()
            copy.hide_viewport = copy.hide_render = copy.hide_select = False
            self.scene.collection.objects.link(copy)
            self.copies[obj.name] = copy
        for name, copy in self.copies.items():
            source_parent = copy.parent
            if source_parent is not None:
                inverse = copy.matrix_parent_inverse.copy()
                copy.parent = self.copies.get(source_parent.name)
                copy.matrix_parent_inverse = inverse
            for modifier in copy.modifiers:
                if modifier.type == "MIRROR" and modifier.mirror_object is not None:
                    modifier.mirror_object = self.copies.get(modifier.mirror_object.name)
        bpy.context.window.scene = self.scene
        self.depsgraph = bpy.context.evaluated_depsgraph_get()
        self.depsgraph.update()
        origin = self.copies.get(contract.SYMMETRY_ORIGIN)
        self.has_origin = origin is not None and origin.type == "EMPTY"
        self.space = self.world(origin).inverted() if self.has_origin else Matrix.Identity(4)

    def world(self, copy):
        return copy.evaluated_get(self.depsgraph).matrix_world.copy()

    def to_space(self, name):
        return self.space @ self.world(self.copies[name])

    def mesh(self, name):
        """Evaluated mesh of a part in its own local space; caller removes it."""
        evaluated = self.copies[name].evaluated_get(self.depsgraph)
        return bpy.data.meshes.new_from_object(evaluated, preserve_all_data_layers=True, depsgraph=self.depsgraph)

    def materials(self, name):
        return [slot.material for slot in self.copies[name].material_slots]

    def show_only(self, names):
        for name, copy in self.copies.items():
            copy.hide_render = name not in names


def mesh_arrays(mesh):
    """Positions (V,3), corner vertices (L,), corner normals (L,3) and triangle corners (T,3)."""
    mesh.calc_loop_triangles()
    co = read_array(mesh.vertices, "co", np.float32, 3).reshape(-1, 3)
    corners = read_array(mesh.loops, "vertex_index", np.int32)
    normals = read_array(mesh.corner_normals, "vector", np.float32, 3).reshape(-1, 3)
    triangles = read_array(mesh.loop_triangles, "loops", np.int32, 3).reshape(-1, 3)
    return co, corners, normals, triangles


def bounds(points):
    return np.min(points, axis=0), np.max(points, axis=0)


DEFAULT_NAME = re.compile(r"(Cube|Plane|Sphere|Cylinder|Cone|Torus|Circle|Icosphere|Grid|Monkey|Suzanne|"
                          r"Empty|Mesh|Object|Text|Curve|BezierCurve|NurbsPath)(\.\d{3})?")


def apply_review_preset(scene, size=1024):
    """Flat review render preset: Workbench, flat material colours, black object outlines."""
    scene.render.engine = "BLENDER_WORKBENCH"
    scene.render.resolution_x = scene.render.resolution_y = size
    scene.render.resolution_percentage = 100
    scene.render.film_transparent = False
    scene.render.image_settings.file_format = "PNG"
    scene.render.image_settings.color_mode = "RGBA"
    scene.display.shading.light = "FLAT"
    scene.display.shading.color_type = "MATERIAL"
    scene.display.shading.show_object_outline = True
    scene.display.shading.object_outline_color = (0.0, 0.0, 0.0)
    scene.display.render_aa = "8"
    scene.view_settings.view_transform = "Standard"
    scene.view_settings.look = "None"
    if scene.world is None:
        scene.world = bpy.data.worlds.new("Review background")
    scene.world.color = (0.18, 0.19, 0.21)
