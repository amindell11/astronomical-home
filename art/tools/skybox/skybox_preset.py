"""Versioned, validated authoring settings shared by Blender and the CLI."""

import copy
import json
import math
from pathlib import Path

SCHEMA_VERSION = 1
DEFAULT = {
    "schema_version": SCHEMA_VERSION,
    "nebula": {
        "variation": 0,
        "scale": 1.0,
        "stretch": [0.62, 1.05, 0.62],
        "rotation": [0.20, -0.35, 0.58],
        "coverage": 0.0,
        "core_emission": 1.0,
        "palette": [[0.015, 0.001, 0.055], [0.35, 0.008, 0.52],
                    [0.018, 0.18, 0.72], [0.75, 0.025, 0.16]],
    },
    "tiny_stars": {"brightness": 1.0, "legacy_seed": 7319.0},
    "anchor_brightness": 1.0,
    "anchors": [
        {"direction": [0.78, -0.36, 0.51], "radius": 0.055, "color": [0.55, 0.72, 1.0], "strength": 420.0},
        {"direction": [-0.43, -0.81, 0.40], "radius": 0.070, "color": [1.0, 0.47, 0.17], "strength": 260.0},
        {"direction": [-0.70, 0.31, -0.64], "radius": 0.050, "color": [0.52, 0.66, 1.0], "strength": 600.0},
        {"direction": [0.18, 0.91, 0.37], "radius": 0.062, "color": [1.0, 0.78, 0.45], "strength": 360.0},
        {"direction": [0.52, 0.43, -0.74], "radius": 0.045, "color": [0.70, 0.82, 1.0], "strength": 850.0},
    ],
}


def defaults(glow=False):
    value = copy.deepcopy(DEFAULT)
    if glow:
        value["tiny_stars"]["brightness"] = 0.0
        value["nebula"]["core_emission"] = 1.5
    return value


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
    tiny = value["tiny_stars"]
    _keys(tiny, DEFAULT["tiny_stars"], "tiny_stars")
    _number(tiny["brightness"], "tiny_stars.brightness", 0.0)
    _number(tiny["legacy_seed"], "tiny_stars.legacy_seed")
    _number(value["anchor_brightness"], "anchor_brightness", 0.0)
    if not isinstance(value["anchors"], list) or len(value["anchors"]) != 5:
        raise ValueError("anchors: expected five focal stars")
    for index, star in enumerate(value["anchors"]):
        path = f"anchors[{index}]"
        _keys(star, DEFAULT["anchors"][0], path)
        _vector(star["direction"], f"{path}.direction", -1.0, 1.0)
        if sum(x*x for x in star["direction"]) < 1e-12:
            raise ValueError(f"{path}.direction: expected a nonzero direction")
        _number(star["radius"], f"{path}.radius", 0.001, 1.0)
        _vector(star["color"], f"{path}.color", 0.0, 1.0)
        _number(star["strength"], f"{path}.strength", 0.0)
    return copy.deepcopy(value)


def load(path):
    with open(path, encoding="utf-8-sig") as handle:
        return parse(json.load(handle))


def save(path, value):
    value = parse(value)
    Path(path).write_text(json.dumps(value, indent=2, allow_nan=False) + "\n", encoding="utf-8")
