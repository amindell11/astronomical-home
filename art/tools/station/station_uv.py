"""Unwrap a station's hull meshes into one PaintUV atlas at one texel density, and save the .blend.

CLI: blender -b --factory-startup --python-exit-code 1 -P station_uv.py -- --station BLEND [--redo]
Hull meshes are the mesh objects whose `key` custom property is a hull colour. They are unwrapped
together in world space (angle-based islands, then packed at one scale with a mip-safe margin), so
islands keep their relative world size. Other meshes get no UV layer. Refuses a station that
already has PaintUV unless --redo. Prose to stderr; stdout trailers: STATION_TEXELS_PER_UNIT,
STATION_FILL, STATION_DENSITY_RATIO, STATION_ISLANDS. Exit 0 saved, 1 error.
"""

import math
from pathlib import Path
import sys

import bmesh
import bpy
from bpy_extras import bmesh_utils
import numpy as np

sys.path.insert(0, str(Path(__file__).resolve().parents[1] / "ship"))
import ship_contract as contract
import ship_source

HULL_KEYS = ("bone", "canvas", "slate", "ochre", "brick", "blue")
TEXTURE_SIZE = 2048
PACK_MARGIN = 5 / TEXTURE_SIZE  # the packer leaves at least 8 texels between islands: room for the mips
ANGLE_LIMIT = 66.0


def hull_objects(scene):
    return sorted((o for o in scene.objects if o.type == "MESH" and o.get("key") in HULL_KEYS), key=lambda o: o.name)


def unwrap(scene, objects):
    """PaintUV per object, computed on a world-space joined copy so one pack covers the whole station."""
    selected, active = [o for o in scene.objects if o.select_get()], bpy.context.view_layer.objects.active
    sync = scene.tool_settings.use_uv_select_sync
    copies = []
    for index, obj in enumerate(objects):
        mesh = obj.data.copy()
        mesh.transform(obj.matrix_world)
        for name, values in (("station_part", np.full(len(mesh.loops), index)),
                             ("station_corner", np.arange(len(mesh.loops)))):
            mesh.attributes.new(name, "INT", "CORNER").data.foreach_set("value", values.astype(np.int32))
        copy = bpy.data.objects.new(f"unwrap {obj.name}", mesh)
        scene.collection.objects.link(copy)
        copies.append(copy)
    for obj in scene.objects:
        obj.select_set(obj in copies)
    bpy.context.view_layer.objects.active = copies[0]
    bpy.ops.object.join()
    joined = bpy.context.view_layer.objects.active
    scene.tool_settings.use_uv_select_sync = True
    bpy.ops.object.mode_set(mode="EDIT")
    bpy.ops.mesh.select_all(action="SELECT")
    bpy.ops.uv.smart_project(angle_limit=math.radians(ANGLE_LIMIT), island_margin=0.0, area_weight=0.0,
                             correct_aspect=True, scale_to_bounds=False)
    bpy.ops.uv.seams_from_islands()
    bpy.ops.uv.unwrap(method="ANGLE_BASED", fill_holes=True, correct_aspect=True)
    bpy.ops.uv.average_islands_scale()
    bpy.ops.uv.pack_islands(rotate=True, scale=True, margin_method="FRACTION", margin=PACK_MARGIN,
                            shape_method="CONCAVE")
    bpy.ops.object.mode_set(mode="OBJECT")
    mesh = joined.data
    part = ship_source.read_array(mesh.attributes["station_part"].data, "value", np.int32)
    corner = ship_source.read_array(mesh.attributes["station_corner"].data, "value", np.int32)
    uv = ship_source.read_array(mesh.uv_layers.active.uv, "vector", np.float32, 2).reshape(-1, 2)
    bm = bmesh.new()
    bm.from_mesh(mesh)
    islands = len(bmesh_utils.bmesh_linked_uv_islands(bm, bm.loops.layers.uv.active))
    bm.free()
    for index, obj in enumerate(objects):
        mine = part == index
        values = np.empty((len(obj.data.loops), 2), np.float32)
        values[corner[mine]] = uv[mine]
        layer = obj.data.uv_layers.new(name=contract.PAINT_UV, do_init=False)
        layer.uv.foreach_set("vector", values.ravel())
        obj.data.uv_layers.active = layer
        layer.active_render = True
    bpy.data.objects.remove(joined)
    bpy.data.meshes.remove(mesh)
    scene.tool_settings.use_uv_select_sync = sync
    for obj in scene.objects:
        obj.select_set(obj in selected)
    bpy.context.view_layer.objects.active = active
    return islands


def measure(objects):
    """Texels per unit over the whole hull, UV fill, and the max/min of per-face density."""
    uv_area = surface = 0.0
    densities = []
    for obj in objects:
        mesh = obj.data
        co, corners, _, triangles = ship_source.mesh_arrays(mesh)
        points = co @ np.array(obj.matrix_world.to_3x3(), np.float64).T
        uv = ship_source.read_array(mesh.uv_layers[contract.PAINT_UV].uv, "vector", np.float64, 2).reshape(-1, 2)
        a, b, c = (points[corners[triangles[:, i]]] for i in range(3))
        p, q, r = (uv[triangles[:, i]] for i in range(3))
        polygon = ship_source.read_array(mesh.loop_triangles, "polygon_index", np.int32)
        world = np.bincount(polygon, 0.5 * np.linalg.norm(np.cross(b - a, c - a), axis=1), len(mesh.polygons))
        texture = np.bincount(polygon, 0.5 * np.abs((q - p)[:, 0] * (r - p)[:, 1] - (q - p)[:, 1] * (r - p)[:, 0]),
                              len(mesh.polygons))
        faces = world > 1e-6
        densities.append(np.sqrt(texture[faces] / world[faces]))
        surface += world.sum()
        uv_area += texture.sum()
    density = np.concatenate(densities)
    return {"texels_per_unit": math.sqrt(uv_area / surface) * TEXTURE_SIZE, "fill": uv_area,
            "ratio": float(density.max() / density.min())}


def main(argv):
    parser = contract.ArgumentParser(description="Unwrap a station's hull meshes into one PaintUV atlas.")
    parser.add_argument("--station", required=True, help="Path to the station .blend (saved in place)")
    parser.add_argument("--redo", action="store_true", help="Replace an existing PaintUV layer")
    args = parser.parse_args(argv)
    source = Path(args.station).resolve()
    if not source.is_file():
        raise contract.UsageError(f"--station: not found: {source}")
    bpy.ops.wm.open_mainfile(filepath=str(source), load_ui=True, use_scripts=False)
    scene = bpy.context.scene
    objects = hull_objects(scene)
    if not objects:
        raise contract.UsageError(f"No hull meshes (key in {HULL_KEYS}) in {source}")
    existing = [o.name for o in objects if contract.PAINT_UV in o.data.uv_layers]
    if existing and not args.redo:
        raise contract.UsageError(f"{len(existing)} hull meshes already have {contract.PAINT_UV}; pass --redo to replace")
    for obj in objects:
        if contract.PAINT_UV in obj.data.uv_layers:
            obj.data.uv_layers.remove(obj.data.uv_layers[contract.PAINT_UV])
    islands = unwrap(scene, objects)
    stats = measure(objects)
    bpy.context.preferences.filepaths.save_version = 0
    bpy.ops.wm.save_mainfile(filepath=str(source), compress=True)
    print(f"{source.name}: {len(objects)} hull meshes, {islands} islands, "
          f"{stats['texels_per_unit']:.1f} texels/unit at {TEXTURE_SIZE}, fill {stats['fill']:.3f}, "
          f"face density max/min {stats['ratio']:.3f}", file=sys.stderr)
    return contract.EXIT_PASS, [("STATION_TEXELS_PER_UNIT", f"{stats['texels_per_unit']:.2f}"),
                                ("STATION_FILL", f"{stats['fill']:.4f}"),
                                ("STATION_DENSITY_RATIO", f"{stats['ratio']:.4f}"),
                                ("STATION_ISLANDS", islands)]


if __name__ == "__main__":
    contract.run(main)
