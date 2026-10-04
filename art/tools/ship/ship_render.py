"""Render review images of a ship's visual role parts with the flat review preset.

CLI: blender -b --factory-startup --python-exit-code 1 -P ship_render.py -- --ship SHIP_JSON --out DIR
--set ortho|turnaround|sheet|scale|compare [--size N] [--before BLEND]
  ortho       top, side, front and back orthographic views
  turnaround  eight views around Z at 20 degrees elevation
  sheet       ortho and turnaround tiles composed into sheet.png
  scale       the top view at game scales, upscaled nearest-neighbour into scale.png
  compare     top and side of --before beside the source with a difference tile; the report
              counts changed pixels per view. Framing covers both ships.
Report <out>/ship_render.json lists the images. Refuses a source with no visual role parts.
"""

import math
from pathlib import Path
import sys

import bpy
from mathutils import Matrix, Vector
import numpy as np

sys.path.insert(0, str(Path(__file__).resolve().parent))
import ship_contract as contract
import ship_source

ORTHO = {"top": ((0, 0, 1), (0, 1, 0)), "side": ((1, 0, 0), (0, 0, 1)),
         "front": ((0, 1, 0), (0, 0, 1)), "back": ((0, -1, 0), (0, 0, 1))}
TURNAROUND = {f"turn{index * 45:03d}": ((math.cos(math.radians(index * 45 - 90)) * math.cos(math.radians(20)),
                                         math.sin(math.radians(index * 45 - 90)) * math.cos(math.radians(20)),
                                         math.sin(math.radians(20))), (0, 0, 1)) for index in range(8)}
GAME_SCALES = (32, 48, 64, 96)
STRIP_HEIGHT = 192


def prepare(source):
    """Open a source and evaluate its visual role parts; returns (evaluation, origin-space bounds)."""
    scene = ship_source.open_source(source)
    roles = ship_source.Roles(scene)
    parts = sorted(name for role in contract.VISUAL_ROLES for name in roles.members.get(role, ())
                   if scene.objects[name].type == "MESH")
    if not parts:
        return None, None
    evaluation = ship_source.Evaluation(scene)
    evaluation.show_only(parts)
    points = []
    for name in parts:
        mesh = evaluation.mesh(name)
        co = ship_source.mesh_arrays(mesh)[0]
        bpy.data.meshes.remove(mesh)
        matrix = np.array(evaluation.to_space(name), np.float64)
        points.append(co @ matrix[:3, :3].T + matrix[:3, 3])
    return evaluation, ship_source.bounds(np.concatenate(points))


def camera_matrix(direction, up, center, distance):
    back = Vector(direction).normalized()
    right = Vector(up).cross(back).normalized()
    return Matrix.Translation(Vector(center) + back * distance) @ Matrix(
        (right, back.cross(right), back)).transposed().to_4x4()


def render(evaluation, framing, views, size, out, prefix=""):
    scene = evaluation.scene
    ship_source.apply_review_preset(scene, size)
    low, high = framing
    center = (low + high) / 2
    radius = float(np.linalg.norm(high - low)) / 2 + 1e-3
    corners = np.array([[x, y, z] for x in (low[0], high[0]) for y in (low[1], high[1]) for z in (low[2], high[2])])
    camera = bpy.data.objects.new("review camera", bpy.data.cameras.new("review camera"))
    camera.data.type = "ORTHO"
    camera.data.clip_start = 0.01
    camera.data.clip_end = radius * 4 + 1
    scene.collection.objects.link(camera)
    scene.camera = camera
    written = {}
    for name, (direction, up) in views.items():
        camera.matrix_world = evaluation.space.inverted() @ camera_matrix(direction, up, center, radius * 2 + 0.5)
        local = np.array(camera_matrix(direction, up, center, 0).to_3x3(), np.float64)
        extent = (corners - center) @ local
        camera.data.ortho_scale = float(max(np.ptp(extent[:, 0]), np.ptp(extent[:, 1]))) * 1.1 + 1e-3
        path = out / f"{prefix}{name}.png"
        scene.render.filepath = str(path)
        bpy.ops.render.render(write_still=True)
        written[name] = path
    return written


def load_pixels(path):
    image = bpy.data.images.load(str(path), check_existing=False)
    pixels = np.empty(len(image.pixels), np.float32)
    image.pixels.foreach_get(pixels)
    shape = (image.size[1], image.size[0], 4)
    bpy.data.images.remove(image)
    return pixels.reshape(shape)


def save_pixels(path, pixels):
    image = bpy.data.images.new(Path(path).stem, pixels.shape[1], pixels.shape[0], alpha=True)
    image.pixels.foreach_set(np.ascontiguousarray(pixels, np.float32).ravel())
    image.filepath_raw = str(path)
    image.file_format = "PNG"
    image.save()
    bpy.data.images.remove(image)


def grid(tiles, columns):
    """Compose equally sized tiles top-left first; Blender pixel rows run bottom-up."""
    height, width = tiles[0].shape[:2]
    rows = math.ceil(len(tiles) / columns)
    sheet = np.zeros((rows * height, columns * width, 4), np.float32)
    sheet[..., 3] = 1
    for index, tile in enumerate(tiles):
        row, column = divmod(index, columns)
        top = (rows - 1 - row) * height
        sheet[top:top + height, column * width:(column + 1) * width] = tile
    return sheet


def main(argv):
    parser = contract.tool_parser("Render review images of a ship.")
    parser.add_argument("--set", required=True, choices=("ortho", "turnaround", "sheet", "scale", "compare"))
    parser.add_argument("--size", type=int, default=512)
    parser.add_argument("--before", help="Earlier .blend for --set compare")
    args = parser.parse_args(argv)
    if (args.set == "compare") != (args.before is not None):
        raise contract.UsageError("--before is required with, and only with, --set compare")
    if not 16 <= args.size <= 4096:
        raise contract.UsageError("--size must be between 16 and 4096")
    ship, source = contract.load_ship(args.ship)
    before = Path(args.before).resolve() if args.before else None
    report = contract.Report("ship_render", args.out, ship_source.blender_version(),
                             [source] + ([before] if before else []))
    out = report.path.parent
    for path in [source] + ([before] if before else []):
        if not path.is_file():
            raise report.refuse(f"Source not found: {path}")
    images, fields = {}, {"set": args.set}
    if args.set == "compare":
        report.data["before_sha256"] = contract.sha256_file(before)
        _, before_bounds = prepare(before)
        evaluation, after_bounds = prepare(source)
        if before_bounds is None or after_bounds is None:
            raise report.refuse("No visual role parts")
        framing = (np.minimum(before_bounds[0], after_bounds[0]), np.maximum(before_bounds[1], after_bounds[1]))
        views = {name: ORTHO[name] for name in ("top", "side")}
        after = render(evaluation, framing, views, args.size, out, "after_")
        evaluation, _ = prepare(before)
        earlier = render(evaluation, framing, views, args.size, out, "before_")
        fields["changed_pixels"] = {}
        for name in views:
            old, new = load_pixels(earlier[name]), load_pixels(after[name])
            changed = np.any(old != new, axis=2)
            difference = np.zeros_like(new)
            difference[..., 3] = 1
            difference[changed, 0] = 1
            images[f"compare_{name}"] = out / f"compare_{name}.png"
            save_pixels(images[f"compare_{name}"], grid([old, new, difference], 3))
            fields["changed_pixels"][name] = int(changed.sum())
            images.update({f"before_{name}": earlier[name], f"after_{name}": after[name]})
    else:
        evaluation, framing = prepare(source)
        if evaluation is None:
            raise report.refuse("No visual role parts")
        if args.set in ("ortho", "sheet"):
            images.update(render(evaluation, framing, ORTHO, args.size, out))
        if args.set in ("turnaround", "sheet"):
            images.update(render(evaluation, framing, TURNAROUND, args.size, out))
        if args.set == "sheet":
            images["sheet"] = out / "sheet.png"
            save_pixels(images["sheet"], grid([load_pixels(images[n]) for n in [*ORTHO, *TURNAROUND]], 4))
        if args.set == "scale":
            tiles = []
            for size in GAME_SCALES:
                small = load_pixels(render(evaluation, framing, {"top": ORTHO["top"]}, size, out, f"scale{size}_")["top"])
                factor = STRIP_HEIGHT // size
                tiles.append(small.repeat(factor, axis=0).repeat(factor, axis=1))
            images["scale"] = out / "scale.png"
            save_pixels(images["scale"], np.concatenate(tiles, axis=1))
    return report.finish(True, images={name: str(path) for name, path in images.items()}, **fields)


if __name__ == "__main__":
    contract.run(main)
