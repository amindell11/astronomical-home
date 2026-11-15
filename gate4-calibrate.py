"""Calibrate linear emission inputs so the in-game (ACES, bloom) screen colour hits the accepted light colours."""
import json
import re
import subprocess
import sys
from pathlib import Path

from PIL import Image

SP = Path(r"C:/Users/amind/AppData/Local/Temp/claude/882050741e06a72d/e76a9849-d0da-48a5-b840-7ee52bdc2c2b/scratchpad")
PROJECT = "D:/amind/git/agent-1/src/Asteroids3D"
TARGETS = {"window": "#FEAC39", "prize": "#FFE25A", "gimbal": "#5BFF86", "drum": "#FF3B52"}


def lin(c):
    c = c / 255
    return c / 12.92 if c <= 0.04045 else ((c + 0.055) / 1.055) ** 2.4


def hexrgb(h):
    return [int(h[i:i + 2], 16) for i in (1, 3, 5)]


def run(inputs, out):
    vecs = ", ".join(f"new UnityEngine.Vector4({r:.5f}f, {g:.5f}f, {b:.5f}f, 1f)" for r, g, b in inputs)
    src = (SP / "tools/swatch.cs").read_text().replace("__INPUTS__", vecs).replace("__OUT__", str(out).replace("\\", "/"))
    (SP / "tools/swatch_run.cs").write_text(src)
    res = subprocess.run(["unity", "command", "eval_file", "--file", str(SP / "tools/swatch_run.cs"),
                          "--project-path", PROJECT, "--result-only"], capture_output=True, text=True, shell=True)
    m = re.search(r'"result":\s*"([^"]*)"', res.stdout)
    if not m:
        sys.exit("eval failed: " + res.stdout + res.stderr)
    return [tuple(int(v) for v in p.split(",")) for p in m.group(1).split(";") if p]


def sample(png, points):
    im = Image.open(png).convert("RGB")
    out = []
    for x, y in points:
        px = [im.getpixel((x + dx, y + dy)) for dx in range(-3, 4) for dy in range(-3, 4)]
        out.append([sum(p[i] for p in px) / len(px) for i in range(3)])
    return out


names = list(TARGETS)
targets = [hexrgb(TARGETS[n]) for n in names]
inputs = [[lin(c) * 1.0 for c in t] for t in targets]
for it in range(int(sys.argv[1]) if len(sys.argv) > 1 else 5):
    png = SP / f"shots/swatch-{it}.png"
    pts = run(inputs, png)
    got = sample(png, pts)
    err = []
    for i, (t, g) in enumerate(zip(targets, got)):
        e = max(abs(a - b) for a, b in zip(t, g))
        err.append(e)
        # Damped per-channel correction in linear light.
        inputs[i] = [max(1e-4, v * ((lin(min(255, max(1, tc))) / max(lin(max(1, gc)), 1e-4)) ** 0.7))
                     for v, tc, gc in zip(inputs[i], t, g)]
    print(f"iter {it}: " + "; ".join(f"{n} got {[round(c) for c in g]} want {t} err {e:.0f}" for n, g, t, e in zip(names, got, targets, err)))
(SP / "shots/calibrated.json").write_text(json.dumps(dict(zip(names, inputs)), indent=1))
print(json.dumps(dict(zip(names, [[round(v, 4) for v in i] for i in inputs]))))
