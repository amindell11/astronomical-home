"""Scratch: sample a concept's colour per palette region by aligning it to the idmap render.

python sample_regions.py CONCEPT_JPG SIDE(left|right) IDMAP_PNG [--debug OUT_PNG]
The concept holds two ships side by side on a grey card; SIDE picks the half. Each half's ship
bounding box is fitted onto the idmap ship's bounding box, then every idmap region (pixels
nearest its false colour, eroded) reports the median concept colour as sRGB hex and linear.
"""
import json
import sys
import numpy as np
from PIL import Image


def bbox(mask):
    ys, xs = np.nonzero(mask)
    return xs.min(), ys.min(), xs.max(), ys.max()


def background(arr, ref):
    return np.all(np.abs(arr - ref) < 18, axis=2)


def lin(c):
    c = c / 255.0
    return np.where(c <= 0.04045, c / 12.92, ((c + 0.055) / 1.055) ** 2.4)


concept_path, side, idmap_path = sys.argv[1:4]
debug = sys.argv[sys.argv.index("--debug") + 1] if "--debug" in sys.argv else None
concept = np.asarray(Image.open(concept_path).convert("RGB")).astype(float)
h, w = concept.shape[:2]
half = concept[:, : w // 2] if side == "left" else concept[:, w // 2:]
bg = np.median(half[:20, :20].reshape(-1, 3), axis=0)
cx0, cy0, cx1, cy1 = bbox(~background(half, bg))

idmap = np.asarray(Image.open(idmap_path).convert("RGB")).astype(float)
ibg = np.median(idmap[:10, :10].reshape(-1, 3), axis=0)
ix0, iy0, ix1, iy1 = bbox(~background(idmap, ibg))
regions = json.load(open(idmap_path + ".json"))
targets = {name: np.array([v if v <= 0.0031308 else 1.055 * v ** (1 / 2.4) - 0.055 for v in rgb]) * 255
           for name, rgb in regions.items()}
names = list(targets)
stack = np.stack([targets[n] for n in names])
dist = np.linalg.norm(idmap[:, :, None, :] - stack[None, None], axis=3)
label = dist.argmin(axis=2)
ok = dist.min(axis=2) < 30
# erode: keep pixels whose 5x5 neighbourhood shares the label (drops outlines and seams)
from scipy.ndimage import minimum_filter, maximum_filter  # noqa: E402
same = (minimum_filter(label, 7) == maximum_filter(label, 7)) & minimum_filter(ok.astype(np.uint8), 7).astype(bool)

ys, xs = np.nonzero(same)
u = (xs - ix0) / max(ix1 - ix0, 1)
v = (ys - iy0) / max(iy1 - iy0, 1)
px = np.clip((cx0 + u * (cx1 - cx0)).round().astype(int), 0, half.shape[1] - 1)
py = np.clip((cy0 + v * (cy1 - cy0)).round().astype(int), 0, half.shape[0] - 1)
samples = half[py, px]
result = {}
for i, name in enumerate(names):
    sel = label[ys, xs] == i
    if sel.sum() < 30:
        result[name] = None
        continue
    med = np.median(samples[sel], axis=0)
    result[name] = {"hex": "#%02x%02x%02x" % tuple(int(round(c)) for c in med),
                    "linear": [round(float(c), 4) for c in lin(med)] + [1.0], "pixels": int(sel.sum())}
print(json.dumps(result, indent=1))
if debug:
    out = np.zeros_like(idmap)
    for i, name in enumerate(names):
        if result[name]:
            out[(label == i) & ok] = [int(result[name]["hex"][k:k + 2], 16) for k in (1, 3, 5)]
    out[~ok] = ibg
    Image.fromarray(out.astype(np.uint8)).save(debug)
