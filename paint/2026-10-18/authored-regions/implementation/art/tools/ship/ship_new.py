"""Scaffold a new ship source at ship.json's `source`; refuses an existing file.

CLI: blender -b --factory-startup --python-exit-code 1 -P ship_new.py -- --ship SHIP_JSON --out DIR
[--top IMAGE] [--side IMAGE]. Creates the SymmetryOrigin root empty, the role and ignore
collections, optional top/side reference images (in `ignore`), standard cameras and the flat
review render preset. Nose +Y, Z up, mirror across X. Report: <out>/ship_new.json.
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


def scaffold(ship, top=None, side=None):
    bpy.ops.wm.read_factory_settings(use_empty=True)
    scene = bpy.context.scene
    scene.name = ship["name"]
    scene.unit_settings.system = "METRIC"
    scene.unit_settings.scale_length = 1.0
    origin = bpy.data.objects.new(contract.SYMMETRY_ORIGIN, None)
    origin.empty_display_type = "PLAIN_AXES"
    scene.collection.objects.link(origin)
    collections = {}
    for name in [f"role.{role}" for role in contract.STATIC_ROLES] + [contract.IGNORE, "cameras"]:
        collections[name] = bpy.data.collections.new(name)
        scene.collection.children.link(collections[name])
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
    for name, (location, rotation) in CAMERAS.items():
        camera = bpy.data.objects.new(name, bpy.data.cameras.new(name))
        camera.data.type = "ORTHO"
        camera.data.ortho_scale = 10.0
        camera.location = location
        camera.rotation_euler = rotation
        collections["cameras"].objects.link(camera)
    scene.camera = bpy.data.objects["camera.top"]
    ship_source.apply_review_preset(scene)


def main(argv):
    parser = contract.tool_parser("Scaffold a new ship source.")
    parser.add_argument("--top", help="Top reference image")
    parser.add_argument("--side", help="Side reference image")
    args = parser.parse_args(argv)
    ship, source = contract.load_ship(args.ship)
    report = contract.Report("ship_new", args.out, ship_source.blender_version(), [])
    report.data["source"] = str(source)
    if source.exists():
        raise report.refuse(f"{source} already exists; ship_new never overwrites a source")
    for image in (args.top, args.side):
        if image is not None and not Path(image).is_file():
            raise report.refuse(f"Reference image not found: {image}")
    source.parent.mkdir(parents=True, exist_ok=True)
    scaffold(ship, args.top, args.side)
    bpy.ops.wm.save_as_mainfile(filepath=str(source), check_existing=False)
    report.data["source_sha256"] = contract.sha256_file(source)
    return report.finish(True, created=str(source))


if __name__ == "__main__":
    contract.run(main)
