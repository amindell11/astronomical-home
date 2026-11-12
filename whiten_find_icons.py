# Re-derives the find icons as white diamonds with the same dark glyph, so a per-kind tint colours them.
# Every icon pixel lies on the line from the HUD ink to the old violet; each keeps its position on that line,
# with white in place of violet. Alpha is untouched.
import sys
import numpy as np
from PIL import Image

VIOLET = np.array([184, 122, 255], float)
INK = np.array([4, 11, 17], float)
WHITE = np.array([255, 255, 255], float)

for path in sys.argv[1:]:
    rgba = np.asarray(Image.open(path).convert("RGBA")).astype(float)
    rgb, a = rgba[..., :3], rgba[..., 3:]
    axis = VIOLET - INK
    t = np.clip(((rgb - INK) @ axis) / (axis @ axis), 0, 1)[..., None]
    off = np.abs(rgb - (INK + t * axis)).max(axis=-1)[a[..., 0] > 0]
    assert off.max() <= 3, f"{path}: a pixel off the ink-violet line by {off.max()}"
    out = INK + t * (WHITE - INK)
    Image.fromarray(np.concatenate([out, a], -1).round().astype(np.uint8), "RGBA").save(path)
    print(path, "max off-line", off.max())
