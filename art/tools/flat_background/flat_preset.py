"""Versioned, validated authoring settings shared by Blender and the CLI."""

import colorsys
import copy
import json
import math
import random
from pathlib import Path

SCHEMA_VERSION = 2
DEFAULT = {
    "schema_version": SCHEMA_VERSION,
    "nebula": {
        "variation": 0,
        "scale": 1.0,
        "stretch": [0.62, 1.05, 0.62],
        "rotation": [0.20, -0.35, 0.58],
        "coverage": 0.0,
        "core_emission": 1.5,
        "palette": [[0.015, 0.001, 0.055], [0.35, 0.008, 0.52],
                    [0.018, 0.18, 0.72], [0.75, 0.025, 0.16]],
    },
}

V1_KEYS = ("schema_version", "nebula", "tiny_stars", "anchor_brightness", "anchors")
MIGRATION = [
    "Read a schema_version 1 preset as schema_version 2.",
    "Dropped tiny_stars, anchor_brightness and anchors; stars belong to Unity.",
    "Palette, variation, scale, coverage and core emission retain their authoring roles.",
    "Stretch X/Y control torus radii; stretch Z controls cloud detail.",
    "Rotation X/Y shift periodic phases; rotation Z offsets the 4D field.",
]

PALETTES = {
    "GLOW": ("Nebula Glow", DEFAULT["nebula"]["palette"]),
    "ICE": ("Azure & Ice", [[0.002, 0.008, 0.04], [0.015, 0.12, 0.48],
                             [0.035, 0.42, 0.58], [0.30, 0.62, 0.85]]),
    "TEAL": ("Teal & Amber", [[0.002, 0.025, 0.03], [0.012, 0.30, 0.38],
                               [0.025, 0.52, 0.28], [0.85, 0.22, 0.03]]),
    "EMBER": ("Ember & Violet", [[0.035, 0.002, 0.008], [0.62, 0.015, 0.04],
                                  [0.30, 0.018, 0.52], [0.90, 0.34, 0.06]]),
    "ROSE": ("Rose & Gold", [[0.04, 0.002, 0.025], [0.48, 0.024, 0.22],
                              [0.68, 0.09, 0.38], [0.90, 0.42, 0.12]]),
}


def make_palette(scheme, seed=0, variation=0):
    base = PALETTES[scheme][1]
    if variation == 0:
        return copy.deepcopy(base)
    rng = random.Random(seed)
    shift = rng.uniform(-0.07, 0.07) * variation
    result = []
    for color in base:
        hue, saturation, value = colorsys.rgb_to_hsv(*color)
        hue = (hue + shift + rng.uniform(-0.02, 0.02) * variation) % 1
        saturation = max(0.35, min(1, saturation + rng.uniform(-0.12, 0.12) * variation))
        result.append(list(colorsys.hsv_to_rgb(hue, saturation, value)))
    return result


def adjust_palette(colors, channel, decrease=False):
    step = -1 if decrease else 1
    result = []
    for color in colors:
        hue, saturation, value = colorsys.rgb_to_hsv(*color)
        if channel == "HUE":
            hue = (hue + step / 36) % 1
        elif channel == "SATURATION":
            saturation = max(0, min(1, saturation + step * 0.05))
        elif channel == "VALUE":
            value = min(1, value * 1.1 ** step)
        else:
            raise ValueError(f"Unknown color adjustment: {channel}")
        result.append(list(colorsys.hsv_to_rgb(hue, saturation, value)))
    return result


def defaults():
    return copy.deepcopy(DEFAULT)


def _keys(value, expected, path):
    if not isinstance(value, dict) or set(value) != set(expected):
        raise ValueError(f"{path}: expected fields {', '.join(expected)}")


def _number(value, path, low=None, high=None):
    if isinstance(value, bool) or not isinstance(value, (float, int)) or not math.isfinite(value):
        raise ValueError(f"{path}: expected a finite number")
    if (low is not None and value < low) or (high is not None and value > high):
        raise ValueError(f"{path}: expected a number between {low} and {high}")


def _vector(value, path, low=None, high=None):
    if not isinstance(value, list) or len(value) != 3:
        raise ValueError(f"{path}: expected three numbers")
    for index, item in enumerate(value):
        _number(item, f"{path}[{index}]", low, high)


def parse(value):
    _keys(value, DEFAULT, "preset")
    if type(value["schema_version"]) is not int or value["schema_version"] != SCHEMA_VERSION:
        raise ValueError(f"Unsupported schema_version; expected {SCHEMA_VERSION}")
    nebula = value["nebula"]
    _keys(nebula, DEFAULT["nebula"], "nebula")
    if type(nebula["variation"]) is not int or not 0 <= nebula["variation"] <= 2147483647:
        raise ValueError("nebula.variation: expected an integer from 0 to 2147483647")
    _number(nebula["scale"], "nebula.scale", 0.1, 10.0)
    _vector(nebula["stretch"], "nebula.stretch", 0.05, 10.0)
    _vector(nebula["rotation"], "nebula.rotation")
    _number(nebula["coverage"], "nebula.coverage", -0.2, 0.2)
    _number(nebula["core_emission"], "nebula.core_emission", 0.0)
    if not isinstance(nebula["palette"], list) or len(nebula["palette"]) != 4:
        raise ValueError("nebula.palette: expected four RGB colors")
    for color in nebula["palette"]:
        _vector(color, "nebula.palette color", 0.0, 1.0)
    if not any(channel > 0 for color in nebula["palette"] for channel in color):
        raise ValueError("nebula.palette: at least one color must emit light")
    return copy.deepcopy(value)


def upgrade(value):
    """Return (schema 2 preset, migration report); the report is empty for a schema 2 input."""
    if isinstance(value, dict) and type(value.get("schema_version")) is int and value["schema_version"] == 1:
        _keys(value, V1_KEYS, "preset")
        value = {"schema_version": SCHEMA_VERSION, "nebula": value["nebula"]}
        return parse(value), list(MIGRATION)
    return parse(value), []


def load(path):
    with open(path, encoding="utf-8-sig") as handle:
        return upgrade(json.load(handle))


def save(path, value):
    value = parse(value)
    Path(path).write_text(json.dumps(value, indent=2, allow_nan=False) + "\n", encoding="utf-8")
