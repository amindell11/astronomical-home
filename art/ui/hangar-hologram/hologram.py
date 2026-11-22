# /// script
# requires-python = ">=3.11"
# dependencies = ["numpy>=2", "pillow>=10", "scipy>=1.13"]
# ///
"""The hangar hologram's tuner: turns a capture of the game's hangar into layers, and bakes the tuner page.

  uv run hologram.py --layers <probe dir>     read the probe's buffers, write layers/
  uv run hologram.py --tuner out/tuner.html   bake the page from layers/ and settings.json
"""
import argparse
import base64
import io
import json
from pathlib import Path

import numpy as np
from PIL import Image
from scipy import ndimage

HERE = Path(__file__).parent
LAYERS = HERE / "layers"
SETTINGS = HERE / "settings.json"
TEMPLATE = HERE / "tuner.template.html"
# Screen pixels kept around each hull, so glow and halo have room.
MARGIN = 72
# The contour is searched this far out from the hull, in stage pixels.
CONTOUR_REACH = 7
# Distance outside the hull is stored over this many pixels, so the page can draw any outline width.
DISTANCE_RANGE = 8


def srgb(linear):
    linear = np.clip(linear, 0, 1)
    return np.where(linear <= 0.0031308, linear * 12.92, 1.055 * np.power(linear, 1 / 2.4) - 0.055)


def u8(values):
    return (np.clip(values, 0, 1) * 255 + 0.5).astype(np.uint8)


def write_layers(probe: Path):
    """Per ship: paint (straight sRGB, alpha = hull), masks (R outside distance, G panel ink, B facing,
    A crease), aux (R glass, G depth, B non-hull renderers) and livery (the ship as the game draws it)."""
    LAYERS.mkdir(exist_ok=True)
    meta = json.loads((probe / "meta.json").read_text())
    width, height = meta["stage"]
    screen_w, screen_h = meta["screen"]
    quad = np.array(meta["quad"])
    # Where the stage image sits on screen, top-left origin.
    quad_x, quad_w = quad[:, 0].min(), quad[:, 0].max() - quad[:, 0].min()
    quad_top, quad_h = screen_h - quad[:, 1].max(), quad[:, 1].max() - quad[:, 1].min()
    frame_x, frame_y, frame_w, frame_h = meta["frame"]

    def load(ship, label):
        data = np.fromfile(probe / f"{ship}-{label}.bin", dtype=np.float16).astype(np.float32)
        return data.reshape(height, width, 4)[::-1]

    layers = {
        "quad": [float(quad_x), float(quad_top), float(quad_w), float(quad_h)],
        # The settled ship's framing square: centre x, centre y, side.
        "frame": [float(quad_x + (frame_x + frame_w / 2) * quad_w),
                  float(quad_top + (1 - frame_y - frame_h / 2) * quad_h), float(frame_w * quad_w)],
        "projector": [meta["projector"][0], screen_h - meta["projector"][1]],
        "ships": {},
    }
    for ship in meta["ships"]:
        livery, paint, normal, aux = (load(ship, label) for label in ("livery", "paint", "normal", "aux"))
        hull = np.clip(aux[..., 3], 0, 1)
        paint_cov = np.clip(paint[..., 3], 0, 1)
        straight = paint[..., :3] / np.maximum(paint_cov[..., None], 1e-4)
        outside = np.clip(livery[..., 3] - paint_cov, 0, 1)
        near_hull = ndimage.binary_dilation(paint_cov > 0.05, iterations=CONTOUR_REACH)
        extras = outside * ~near_hull
        reach = ndimage.gaussian_filter(ndimage.distance_transform_edt(paint_cov < 0.5).astype(np.float32), 0.6)
        reach = np.clip((reach - 0.5) / DISTANCE_RANGE, 0, 1)

        n = normal[..., :3] / np.maximum(hull[..., None], 1e-4)
        facing = np.clip(normal[..., 3] / np.maximum(hull, 1e-4), 0, 1)
        depth = aux[..., 0] / np.maximum(hull, 1e-4)
        solid = hull > 0.5
        ink = np.clip(aux[..., 1] / np.maximum(hull, 1e-4), 0, 1) * solid
        glass = np.clip(aux[..., 2] / np.maximum(hull, 1e-4), 0, 1) * solid
        inside = (hull > 0.98) & (depth > 0.5)
        near, far = np.percentile(depth[inside], 0.5), np.percentile(depth[inside], 99.5)
        depth01 = np.clip((depth - near) / max(far - near, 1e-4), 0, 1) * inside

        # Crease: neighbouring hull pixels whose normals disagree, or whose depth jumps.
        crease = np.zeros((height, width), np.float32)
        for dy, dx in ((0, 1), (1, 0), (1, 1), (1, -1)):
            both = inside & np.roll(inside, (dy, dx), axis=(0, 1))
            bend = 1 - np.sum(n * np.roll(n, (dy, dx), axis=(0, 1)), axis=-1)
            jump = np.abs(depth - np.roll(depth, (dy, dx), axis=(0, 1))) / max(far - near, 1e-4)
            crease = np.maximum(crease, both * np.clip(np.maximum((bend - 0.03) / 0.15, (jump - 0.015) / 0.03), 0, 1))
        crease = np.clip(ndimage.gaussian_filter(crease, 0.55) * 1.35, 0, 1)

        ys, xs = np.nonzero(livery[..., 3] > 0.02)
        x0, x1 = max(xs.min() - MARGIN, 0), min(xs.max() + MARGIN, width)
        y0, y1 = max(ys.min() - MARGIN, 0), min(ys.max() + MARGIN, height)
        crop = (slice(y0, y1), slice(x0, x1))
        livery_a = np.clip(livery[..., 3], 0, 1)
        livery_rgb = livery[..., :3] / np.maximum(livery_a[..., None], 1e-4)
        images = {
            "paint": np.dstack([srgb(straight), paint_cov[..., None]]),
            "masks": np.dstack([reach, ink, facing * solid, crease]),
            "aux": np.dstack([glass, depth01, extras, np.ones_like(hull)]),
            "livery": np.dstack([srgb(livery_rgb), livery_a[..., None]]),
        }
        for name, image in images.items():
            Image.fromarray(u8(image[crop]), "RGBA").save(LAYERS / f"{ship}-{name}.png")
        layers["ships"][ship] = {
            "box": [float(quad_x + x0 * quad_w / width), float(quad_top + y0 * quad_h / height),
                    float((x1 - x0) * quad_w / width), float((y1 - y0) * quad_h / height)],
            "centre": [float(quad_x + xs.mean() * quad_w / width), float(quad_top + ys.mean() * quad_h / height)],
        }
    Image.open(probe / "plate.png").convert("RGB").save(LAYERS / "plate.png")
    (LAYERS / "layers.json").write_text(json.dumps(layers, indent=2))
    print(f"layers for {', '.join(layers['ships'])} in {LAYERS}")


def bake_tuner(target: Path):
    layers = json.loads((LAYERS / "layers.json").read_text())

    def uri(path):
        return "data:image/png;base64," + base64.b64encode(path.read_bytes()).decode()

    plate = io.BytesIO()
    Image.open(LAYERS / "plate.png").save(plate, "JPEG", quality=93, subsampling=0)
    data = {"layers": {key: layers[key] for key in ("quad", "frame", "projector")},
            "plate": "data:image/jpeg;base64," + base64.b64encode(plate.getvalue()).decode(), "ships": {}}
    for ship, info in layers["ships"].items():
        data["ships"][ship] = {**info, **{k: uri(LAYERS / f"{ship}-{k}.png") for k in ("paint", "masks", "aux", "livery")}}
    html = TEMPLATE.read_text(encoding="utf-8")
    html = html.replace("/*DATA*/", json.dumps(data)).replace("/*SETTINGS*/", SETTINGS.read_text(encoding="utf-8"))
    target.parent.mkdir(parents=True, exist_ok=True)
    target.write_text(html, encoding="utf-8")
    print(f"{target} {len(html) / 1e6:.2f} MB")


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument("--layers", type=Path, metavar="PROBE_DIR")
    parser.add_argument("--tuner", type=Path, metavar="HTML")
    args = parser.parse_args()
    if args.layers:
        write_layers(args.layers)
    if args.tuner:
        bake_tuner(args.tuner)
    if not args.layers and not args.tuner:
        parser.print_help()
