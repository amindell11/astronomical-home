"""Scratch: render a ship with another ship's framing, so the two compare at one scale."""
import math, sys
from pathlib import Path
sys.path.insert(0, r"D:/amind/git/agent-1/art/tools/ship")
import ship_render as R
argv = sys.argv[sys.argv.index("--") + 1:]
frame, src, out, size = Path(argv[0]), Path(argv[1]), Path(argv[2]), int(argv[3])
out.mkdir(parents=True, exist_ok=True)
_, bounds = R.prepare(frame)
evaluation, _ = R.prepare(src)
def view(az, el):
    a, e = math.radians(az), math.radians(el)
    return ((math.cos(a) * math.cos(e), math.sin(a) * math.cos(e), math.sin(e)), (0, 0, 1))
views = {"top": R.ORTHO["top"], "three": view(-45, 40), "front": view(135, 40), "side": R.ORTHO["side"]}
R.render(evaluation, bounds, views, size, out, "built_", textured=False)
