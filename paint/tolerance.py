"""Changed-pixel fraction inside the ship silhouette between two review renders (the brief's measure).

blender -b --factory-startup --python-exit-code 1 -P tolerance.py -- BEFORE.png AFTER.png [THRESHOLD/255]
"""

import sys
from pathlib import Path

import numpy as np

TOOLS = Path(r"D:/amind/git/agent-5/art/tools/ship")
sys.path.insert(0, str(TOOLS))
import ship_source

args = sys.argv[sys.argv.index("--") + 1:]
before, after = (ship_source.load_pixels(path)[..., :3] for path in args[:2])
threshold = (float(args[2]) if len(args) > 2 else 16.0) / 255.0
background = before[0, 0]
silhouette = (np.abs(before - background).max(axis=2) > 0.02) | (np.abs(after - background).max(axis=2) > 0.02)
delta = np.abs(before - after).max(axis=2)
changed = (delta > threshold) & silhouette
print(f"TOLERANCE silhouette={int(silhouette.sum())} changed={int(changed.sum())} "
      f"fraction={changed.sum() / max(1, silhouette.sum()):.4f} threshold={threshold * 255:.0f}/255 "
      f"mean_delta={float(delta[silhouette].mean() * 255):.2f}/255", file=sys.stderr)
