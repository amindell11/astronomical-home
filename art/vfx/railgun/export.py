# /// script
# requires-python = ">=3.11"
# dependencies = ["pillow==12.1.1"]
# ///
"""Rasterize the saved SVG lance into four-frame atlases and a muzzle texture.

    uv run art/vfx/railgun/export.py
    uv run art/vfx/railgun/export.py --output <texture-directory>

The default output is Assets/Visuals/Vfx/Railgun/Textures in this checkout.
Writes Railgun_Body.png, Railgun_Fringe.png and Railgun_Muzzle.png.
Exit 0 means all textures were exported; errors exit nonzero. Requires Pillow.
"""
import argparse
import re
import xml.etree.ElementTree as ET
from pathlib import Path

from PIL import Image, ImageColor, ImageDraw

HERE = Path(__file__).resolve().parent
DEFAULT_OUTPUT = HERE.parents[2] / "src/Asteroids3D/Assets/Visuals/Vfx/Railgun/Textures"
FRAME = (512, 64)
SUPERSAMPLE = 4


def rasterize(path, size):
    root = ET.parse(path).getroot()
    _, _, width, height = map(float, root.attrib["viewBox"].split())
    scale_x = size[0] * SUPERSAMPLE / width
    scale_y = size[1] * SUPERSAMPLE / height
    image = Image.new("RGBA", (size[0] * SUPERSAMPLE, size[1] * SUPERSAMPLE))
    for element in root:
        tag = element.tag.split("}")[-1]
        if tag == "defs":
            continue
        attrs = element.attrib
        layer = Image.new("RGBA", image.size)
        draw = ImageDraw.Draw(layer)
        alpha = round(float(attrs.get("opacity", 1)) * 255)
        fill = attrs.get("fill", "none")
        color = (*ImageColor.getrgb(fill), alpha) if fill != "none" else None
        if tag == "path":
            data = attrs["d"]
            if re.search(r"[A-KN-Yac-z]", data):
                raise ValueError(f"Unsupported SVG path command in {path}")
            coordinates = list(map(float, re.findall(r"-?\d+(?:\.\d+)?", data)))
            points = [(x * scale_x, y * scale_y) for x, y in zip(coordinates[::2], coordinates[1::2])]
            if color:
                draw.polygon(points, fill=color)
            if "stroke" in attrs:
                draw.line(points, fill=(*ImageColor.getrgb(attrs["stroke"]), alpha),
                          width=max(1, round(float(attrs["stroke-width"]) * scale_y)), joint="curve")
        elif tag == "circle":
            cx, cy, radius = (float(attrs[key]) for key in ("cx", "cy", "r"))
            draw.ellipse(((cx - radius) * scale_x, (cy - radius) * scale_y,
                          (cx + radius) * scale_x, (cy + radius) * scale_y), fill=color)
        else:
            raise ValueError(f"Unsupported SVG element {tag} in {path}")
        image = Image.alpha_composite(image, layer)
    return image.resize(size, Image.Resampling.LANCZOS)


def export(output):
    output.mkdir(parents=True, exist_ok=True)
    for layer, source in (("Body", "blue_1_body"), ("Fringe", "blue_2_fringe")):
        atlas = Image.new("RGBA", (FRAME[0] * 4, FRAME[1]))
        for frame in range(4):
            atlas.paste(rasterize(HERE / "source" / f"{source}_f{frame}.svg", FRAME), (frame * FRAME[0], 0))
        atlas.save(output / f"Railgun_{layer}.png")
    rasterize(HERE / "source/blue_4_muzzle.svg", (256, 256)).save(output / "Railgun_Muzzle.png")
    print(f"Exported body, fringe and muzzle to {output}")


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument("--output", type=Path, default=DEFAULT_OUTPUT)
    export(parser.parse_args().output)
