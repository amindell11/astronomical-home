"""4b-1 paint side score: does a Blender top render's paint sit on the same side as the game's?

python paint_side_score.py <game.png> <render.png> [<render.png> ...]
Each image is cropped to the ship (pixels unlike the corner background), resized to 512 px, and
reduced to high-passed luminance (brush strokes, not lighting). Scores are Pearson correlations,
inside both ships' masks, with the game image and with the game mirrored left-right. The geometry
is symmetric, so the paint that sits on the game's sides scores higher unmirrored.
"""
import sys

import numpy as np
from PIL import Image, ImageFilter

SIZE = 512


def prepare(path):
    image = np.asarray(Image.open(path).convert("RGB"), np.float64) / 255
    background = image[2, 2]
    mask = np.abs(image - background).max(axis=2) > 0.08
    rows, cols = np.flatnonzero(mask.any(axis=1)), np.flatnonzero(mask.any(axis=0))
    crop = image[rows[0]:rows[-1] + 1, cols[0]:cols[-1] + 1]
    crop_mask = mask[rows[0]:rows[-1] + 1, cols[0]:cols[-1] + 1]
    luminance = Image.fromarray(np.uint8(255 * (crop @ [0.2126, 0.7152, 0.0722]))).resize((SIZE, SIZE), Image.BICUBIC)
    blurred = luminance.filter(ImageFilter.GaussianBlur(5))
    high = np.asarray(luminance, np.float64) - np.asarray(blurred, np.float64)
    shape_mask = np.asarray(Image.fromarray(np.uint8(crop_mask * 255)).resize((SIZE, SIZE), Image.NEAREST)) > 0
    eroded = np.asarray(Image.fromarray(np.uint8(shape_mask * 255)).filter(ImageFilter.MinFilter(9))) > 0
    return high, eroded


def corr(a, b, mask):
    a, b = a[mask], b[mask]
    a, b = a - a.mean(), b - b.mean()
    return float((a * b).sum() / np.sqrt((a * a).sum() * (b * b).sum()))


game, game_mask = prepare(sys.argv[1])
for path in sys.argv[2:]:
    render, render_mask = prepare(path)
    same = corr(render, game, render_mask & game_mask)
    mirrored = corr(render, game[:, ::-1], render_mask & game_mask[:, ::-1])
    verdict = "game sides" if same > mirrored else "mirrored sides"
    print(f"{path.split('/')[-1]}: same={same:.3f} mirrored={mirrored:.3f} -> paint on {verdict}")
