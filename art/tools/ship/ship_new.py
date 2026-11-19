"""Scaffold a new ship source at ship.json's `source`; refuses an existing file.

CLI: blender -b --factory-startup --python-exit-code 1 -P ship_new.py -- --ship SHIP_JSON --out DIR
[--top IMAGE] [--side IMAGE] | [--base BASE_SHIP_JSON [--omit PART ...]]. Creates the role, ignore
and camera collections, standard cameras and the flat review render preset. Without --base it adds
the SymmetryOrigin root empty and optional top/side reference images (in `ignore`). With --base it
library-links (relative path) every part of the base's visual and socket roles except --omit, each
into the local role collection of the same name, plus the base's SymmetryOrigin, parent chains and
mirror objects. Nose +Y, Z up, mirror across X. Report: <out>/ship_new.json.
"""

from pathlib import Path
import math
import sys

import bpy

sys.path.insert(0, str(Path(__file__).resolve().parent))
import ship_contract as contract
import ship_source

CAMERAS = {
    "camera.top": ((0.0, 0.0, 10.0), (0.0, 0.0, 0.0)),
    "camera.side": ((10.0, 0.0, 0.0), (math.pi / 2, 0.0, math.pi / 2)),
    "camera.front": ((0.0, 10.0, 0.0), (math.pi / 2, 0.0, math.pi)),
}


def scaffold(ship):
    bpy.ops.wm.read_factory_settings(use_empty=True)
    scene = bpy.context.scene
    scene.name = ship["name"]
    scene.unit_settings.system = "METRIC"
    scene.unit_settings.scale_length = 1.0
    collections = {}
    for name in [f"role.{role}" for role in contract.STATIC_ROLES] + [contract.IGNORE, "cameras"]:
        collections[name] = bpy.data.collections.new(name)
        scene.collection.children.link(collections[name])
    for name, (location, rotation) in CAMERAS.items():
        camera = bpy.data.objects.new(name, bpy.data.cameras.new(name))
        camera.data.type = "ORTHO"
        camera.data.ortho_scale = 10.0
        camera.location = location
        camera.rotation_euler = rotation
        collections["cameras"].objects.link(camera)
    scene.camera = bpy.data.objects["camera.top"]
    ship_source.apply_review_preset(scene)
    return scene, collections


def new_source(ship, top=None, side=None):
    scene, collections = scaffold(ship)
    origin = bpy.data.objects.new(contract.SYMMETRY_ORIGIN, None)
    origin.empty_display_type = "PLAIN_AXES"
    scene.collection.objects.link(origin)
    for name, image, rotation in (("reference.top", top, (0.0, 0.0, 0.0)),
                                  ("reference.side", side, (math.pi / 2, 0.0, math.pi / 2))):
        if image is None:
            continue
        empty = bpy.data.objects.new(name, None)
        empty.empty_display_type = "IMAGE"
        empty.data = bpy.data.images.load(str(Path(image).resolve()))
        empty.empty_display_size = 10.0
        empty.empty_image_depth = "BACK"
        empty.show_empty_image_only_axis_aligned = True
        empty.rotation_euler = rotation
        empty.parent = origin
        collections[contract.IGNORE].objects.link(empty)


def base_parts(scene):
    """Each linkable part of a base scene with the role collections it sits in."""
    roles = ship_source.Roles(scene)
    parts = {}
    for role in contract.LINKED_ROLES:
        for name in roles.members.get(role, ()):
            parts.setdefault(name, []).append(f"role.{role}")
    return parts


def supports(scene, names):
    """SymmetryOrigin plus every parent and mirror object the named parts need, transitively."""
    needed, stack = set(), [scene.objects[contract.SYMMETRY_ORIGIN]] + [scene.objects[n] for n in names]
    while stack:
        obj = stack.pop()
        if obj.name in needed:
            continue
        needed.add(obj.name)
        stack.extend(o for o in [obj.parent, *(m.mirror_object for m in obj.modifiers if m.type == "MIRROR")] if o)
    return sorted(needed - set(names))


def derive(ship, source, base_source, parts, support):
    """Save the scaffold first so the library path is stored relative to the new source."""
    scene, collections = scaffold(ship)
    bpy.ops.wm.save_as_mainfile(filepath=str(source), check_existing=False)
    with bpy.data.libraries.load(str(base_source), link=True, relative=True) as (_, linked):
        linked.objects = sorted(parts) + support
    for obj in linked.objects:
        for name in parts.get(obj.name, ()):
            collections[name].objects.link(obj)
        if obj.name in support:
            scene.collection.objects.link(obj)
    bpy.context.preferences.filepaths.save_version = 0
    bpy.ops.wm.save_mainfile()
    return [library.filepath for library in bpy.data.libraries]


def main(argv):
    parser = contract.tool_parser("Scaffold a new ship source.")
    parser.add_argument("--top", help="Top reference image")
    parser.add_argument("--side", help="Side reference image")
    parser.add_argument("--base", help="Base ship.json whose parts the new source links")
    parser.add_argument("--omit", nargs="+", default=[], help="Base parts the new source leaves out")
    args = parser.parse_args(argv)
    ship, source = contract.load_ship(args.ship)
    report = contract.Report("ship_new", args.out, ship_source.blender_version(), [])
    report.data["source"] = str(source)
    if source.exists():
        raise report.refuse(f"{source} already exists; ship_new never overwrites a source")
    for image in (args.top, args.side):
        if image is not None and not Path(image).is_file():
            raise report.refuse(f"Reference image not found: {image}")
    parts = {}
    if args.base is not None:
        _, base_source = contract.load_ship(args.base)
        if not base_source.is_file():
            raise report.refuse(f"Base source not found: {base_source}")
        base_scene = ship_source.open_source(base_source)
        if contract.SYMMETRY_ORIGIN not in base_scene.objects:
            raise report.refuse(f"Base has no {contract.SYMMETRY_ORIGIN}")
        parts = base_parts(base_scene)
    reason = contract.derive_refusal(args.base, args.omit, (args.top, args.side), parts)
    if reason is not None:
        raise report.refuse(reason)
    source.parent.mkdir(parents=True, exist_ok=True)
    if args.base is None:
        new_source(ship, args.top, args.side)
        bpy.ops.wm.save_as_mainfile(filepath=str(source), check_existing=False)
        fields = {}
    else:
        kept = {name: roles for name, roles in parts.items() if name not in args.omit}
        support = supports(base_scene, kept)
        libraries = derive(ship, source, base_source, kept, support)
        fields = {"base": str(base_source), "libraries": libraries, "linked": sorted(kept),
                  "supports": support, "omitted": sorted(args.omit)}
    report.data["source_sha256"] = contract.sha256_file(source)
    return report.finish(True, created=str(source), **fields)


if __name__ == "__main__":
    contract.run(main)
