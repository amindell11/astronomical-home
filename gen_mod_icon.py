# Generates the mod station find icon: the repair icon's diamond with a four-point star in dark ink.
import sys
import numpy as np
from PIL import Image

FIND = (0.72, 0.48, 1.0)       # find colour (violet), shared by every find kind
INK = (0.015, 0.045, 0.065)    # BerthGraphic ink
SIZE, SS = 128, 8

n = SIZE * SS
y, x = np.mgrid[0:n, 0:n]
u = (x + .5) / n * 2 - 1
v = (y + .5) / n * 2 - 1
diamond = (np.abs(u) + np.abs(v)) <= .97
radius, pinch = .62, .62  # star tip radius; exponent < 1 pinches the arms
star = (np.abs(u) / radius) ** pinch + (np.abs(v) / radius) ** pinch <= 1

rgb = np.zeros((n, n, 3))
rgb[:] = FIND
rgb[star] = INK
alpha = diamond.astype(float)
img = np.concatenate([rgb * alpha[..., None], alpha[..., None]], axis=2)
img = img.reshape(SIZE, SS, SIZE, SS, 4).mean(axis=(1, 3))
a = img[..., 3:4]
col = np.where(a > 0, img[..., :3] / np.maximum(a, 1e-6), np.array(FIND))
out = np.concatenate([col, a], axis=2)
Image.fromarray((out * 255 + .5).astype(np.uint8), "RGBA").save(sys.argv[1])
