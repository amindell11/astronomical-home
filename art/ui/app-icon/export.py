# /// script
# requires-python = ">=3.11"
# dependencies = ["pillow==11.3.0", "numpy==2.3.2"]
# ///
"""Render the icon SVGs to PNGs.

Reads only the subset these SVGs use: filled <path> elements made of straight
`M x,y x,y ... Z` subpaths with fill-rule evenodd. Usage: uv run art/ui/app-icon/export.py
"""

import re
from pathlib import Path

import numpy as np
from PIL import Image, ImageDraw

HERE = Path(__file__).resolve().parent
REPO = HERE.parents[2]
UNITY_ICON = REPO / "src/Asteroids3D/Assets/Visuals/Ui/AppIcon/AppIcon.png"
SUPERSAMPLE = 4

# (svg, output png, size)
EXPORTS = [
    ("icon.svg", UNITY_ICON, 1024),
    ("icon.svg", HERE / "export/icon-512.png", 512),
    ("icon.svg", HERE / "export/icon-256.png", 256),
    ("mark-mono.svg", HERE / "export/mark-mono-512.png", 512),
]

PATH = re.compile(r'<path fill="(#[0-9A-Fa-f]{6})" fill-rule="evenodd" d="([^"]*)"')


def parse(svg_text):
    view = float(re.search(r'viewBox="0 0 ([\d.]+) [\d.]+"', svg_text).group(1))
    layers = []
    for colour, d in PATH.findall(svg_text):
        rings = []
        for sub in re.findall(r"M([^Z]*)Z", d):
            rings.append([tuple(map(float, p.split(","))) for p in sub.split()])
        layers.append((colour, rings))
    return view, layers


def render(svg_path, size):
    view, layers = parse(svg_path.read_text())
    s = size * SUPERSAMPLE
    k = s / view
    img = Image.new("RGBA", (s, s), (0, 0, 0, 0))
    for colour, rings in layers:
        mask = np.zeros((s, s), bool)
        for ring in rings:
            m = Image.new("1", (s, s), 0)
            ImageDraw.Draw(m).polygon([(x * k, y * k) for x, y in ring], fill=1)
            mask ^= np.array(m)  # evenodd: overlapping subpaths cancel
        img.paste(Image.new("RGBA", (s, s), colour), (0, 0), Image.fromarray(mask.astype(np.uint8) * 255))
    return img.resize((size, size), Image.LANCZOS)


if __name__ == "__main__":
    for svg, out, size in EXPORTS:
        out.parent.mkdir(parents=True, exist_ok=True)
        render(HERE / svg, size).save(out)
        print(out.relative_to(REPO))
