"""Authored palette coverage on locked surfaces, independent of palette colours and paint masks.

Marks use signed symmetry-origin coordinates; optional X folding authors mirrored livery.
Rasterisation follows PaintUV, so markings can cross faces without cutting the source mesh.
"""

import json
import math
from pathlib import Path

import numpy as np

MAX_REGIONS = 16
PREFIX = "Region_"


def parse(value):
    if (not isinstance(value, dict) or set(value) != {"schema_version", "marks"}
            or type(value["schema_version"]) is not int or value["schema_version"] != 1):
        raise ValueError("authored regions: expected schema_version 1 and marks")
    if not isinstance(value["marks"], list):
        raise ValueError("authored regions: marks must be a list")
    for index, mark in enumerate(value["marks"]):
        path = f"marks[{index}]"
        if not isinstance(mark, dict) or set(mark) != {"parts", "region", "mirror_x", "where", "shape", "opacity"}:
            raise ValueError(f"{path}: expected parts, region, mirror_x, where, shape and opacity")
        parts = mark["parts"]
        if (not isinstance(parts, list) or not parts or any(not isinstance(p, str) or not p.strip() for p in parts)
                or len(set(parts)) != len(parts)):
            raise ValueError(f"{path}.parts: expected distinct part names")
        if not isinstance(mark["region"], str) or not mark["region"].strip():
            raise ValueError(f"{path}.region: expected a region name")
        if type(mark["mirror_x"]) is not bool:
            raise ValueError(f"{path}.mirror_x: expected true or false")
        _number(mark["opacity"], f"{path}.opacity", 0, 1)
        where = mark["where"]
        if not isinstance(where, dict) or not set(where) <= {"x", "y", "z", "nz"}:
            raise ValueError(f"{path}.where: expected bounds on x, y, z or nz")
        for axis, bounds in where.items():
            if not isinstance(bounds, list) or len(bounds) != 2 or bounds == [None, None]:
                raise ValueError(f"{path}.where.{axis}: expected [low, high] with at least one bound")
            low, high = bounds
            for bound in bounds:
                if bound is not None:
                    _number(bound, f"{path}.where.{axis}")
            if low is not None and high is not None and low >= high:
                raise ValueError(f"{path}.where.{axis}: low must be below high")
        shape = mark["shape"]
        if not isinstance(shape, dict):
            raise ValueError(f"{path}.shape: expected a shape")
        kind = shape.get("kind")
        fields = {"box": {"kind"}, "polygon": {"kind", "points"},
                  "path": {"kind", "points", "width", "closed"}, "circle": {"kind", "center", "radius"}}
        if kind not in fields or set(shape) != fields[kind]:
            raise ValueError(f"{path}.shape: expected box, polygon, path or circle with its fields")
        if kind in ("polygon", "path"):
            points = shape["points"]
            minimum = 3 if kind == "polygon" else 2
            if not isinstance(points, list) or len(points) < minimum:
                raise ValueError(f"{path}.shape.points: expected at least {minimum} points")
            for point in points:
                _point(point, f"{path}.shape.points")
            if any(a == b for a, b in zip(points, points[1:])):
                raise ValueError(f"{path}.shape.points: consecutive points must differ")
            if kind == "polygon":
                area = sum(a[0] * b[1] - b[0] * a[1] for a, b in zip(points, points[1:] + points[:1]))
                if abs(area) < 1e-12:
                    raise ValueError(f"{path}.shape.points: polygon has zero area")
            else:
                _number(shape["width"], f"{path}.shape.width", 0, exclusive=True)
                if type(shape["closed"]) is not bool or (shape["closed"] and points[0] == points[-1]):
                    raise ValueError(f"{path}.shape.closed: expected a flag; do not repeat the closing point")
        elif kind == "circle":
            _point(shape["center"], f"{path}.shape.center")
            _number(shape["radius"], f"{path}.shape.radius", 0, exclusive=True)
    return value


def _number(value, path, low=None, high=None, exclusive=False):
    if type(value) not in (int, float) or not math.isfinite(value):
        raise ValueError(f"{path}: expected a finite number")
    if low is not None and (value <= low if exclusive else value < low):
        raise ValueError(f"{path}: number is below its allowed range")
    if high is not None and value > high:
        raise ValueError(f"{path}: number is above its allowed range")


def _point(value, path):
    if not isinstance(value, list) or len(value) != 2:
        raise ValueError(f"{path}: expected [x, y]")
    for coordinate in value:
        _number(coordinate, path)


def load(path):
    return parse(json.loads(Path(path).read_text(encoding="utf-8-sig")))


def coverage(mark, points, normal_z):
    p = points.copy() if mark["mirror_x"] else points
    if mark["mirror_x"]:
        p[:, 0] = np.abs(p[:, 0])
    hit = np.ones(len(p), bool)
    for axis, (low, high) in mark["where"].items():
        values = normal_z if axis == "nz" else p[:, {"x": 0, "y": 1, "z": 2}[axis]]
        if low is not None:
            hit &= values > low
        if high is not None:
            hit &= values < high
    shape = mark["shape"]
    if shape["kind"] == "circle":
        hit &= np.sum((p[:, :2] - shape["center"]) ** 2, axis=1) < shape["radius"] ** 2
    elif shape["kind"] == "polygon":
        polygon, inside = shape["points"], np.zeros(len(p), bool)
        for a, b in zip(polygon, polygon[1:] + polygon[:1]):
            if abs(b[1] - a[1]) < 1e-12:
                continue
            inside ^= ((a[1] > p[:, 1]) != (b[1] > p[:, 1])) & (
                p[:, 0] < (b[0] - a[0]) * (p[:, 1] - a[1]) / (b[1] - a[1]) + a[0])
        hit &= inside
    elif shape["kind"] == "path":
        path = shape["points"] + (shape["points"][:1] if shape["closed"] else [])
        distance = np.full(len(p), np.inf)
        for a, b in zip(path[:-1], path[1:]):
            delta = np.asarray(b) - a
            t = np.clip(((p[:, :2] - a) @ delta) / (delta @ delta), 0, 1)
            distance = np.minimum(distance, np.linalg.norm(p[:, :2] - a - t[:, None] * delta, axis=1))
        hit &= distance < shape["width"] / 2
    return hit


def surface_samples(part, size, uv_name, tolerance=-1e-6):
    mesh = part.mesh
    mesh.calc_loop_triangles()
    uv = mesh.uv_layers[uv_name].uv
    for triangle in mesh.loop_triangles:
        coords = np.array([uv[i].vector[:] for i in triangle.loops]) * size
        low = np.maximum(np.floor(coords.min(axis=0)).astype(int), 0)
        high = np.minimum(np.ceil(coords.max(axis=0)).astype(int), size - 1)
        if np.any(high < low):
            continue
        gx, gy = np.meshgrid(np.arange(low[0], high[0] + 1) + .5, np.arange(low[1], high[1] + 1) + .5)
        a, b, c = coords
        den = (b[1] - c[1]) * (a[0] - c[0]) + (c[0] - b[0]) * (a[1] - c[1])
        if abs(den) < 1e-12:
            continue
        u = ((b[1] - c[1]) * (gx - c[0]) + (c[0] - b[0]) * (gy - c[1])) / den
        v = ((c[1] - a[1]) * (gx - c[0]) + (a[0] - c[0]) * (gy - c[1])) / den
        inside = (u >= tolerance) & (v >= tolerance) & (1 - u - v >= tolerance)
        if not inside.any():
            continue
        points = np.column_stack([u[inside], v[inside], (1 - u - v)[inside]]) @ part.co[list(triangle.vertices)]
        yield triangle, gx[inside].astype(int), gy[inside].astype(int), points


def require_independent_sides(parts, size, uv_name):
    occupied = np.zeros((size, size), np.uint8)
    for name, part in parts.items():
        for triangle, xs, ys, points in surface_samples(part, size, uv_name, tolerance=1e-6):
            sides = (points[:, 0] < 0).astype(np.uint8) + 2 * (points[:, 0] > 0).astype(np.uint8)
            combined = occupied[ys, xs] | sides
            if (combined == 3).any():
                raise ValueError(f"paint_space signed requires independent sides: {name} face {triangle.polygon_index} shares UVs")
            occupied[ys, xs] = combined


def rasterize(parts, marks, names, size, margin, uv_name):
    indices = {name: index for index, name in enumerate(names)}
    packed = [np.zeros((size, size, 4), np.uint8) for _ in range((len(names) + 3) // 4)]
    filled = np.zeros((size, size), bool)
    for name, part in parts.items():
        authored = [mark for mark in marks if name in mark["parts"]]
        for triangle, xs, ys, points in surface_samples(part, size, uv_name):
            weights = np.zeros((len(points), len(names)), np.float32)
            region = part.materials[int(part.material_index[triangle.polygon_index])].name
            weights[:, indices[region]] = 1
            normal_z = part.normals[triangle.polygon_index, 2]
            for mark in authored:
                hit = coverage(mark, points, normal_z)
                weights[hit] *= 1 - mark["opacity"]
                weights[hit, indices[mark["region"]]] += mark["opacity"]
            quantized = np.rint(weights * 255).astype(np.uint8)
            for index in range(len(names)):
                packed[index // 4][ys, xs, index % 4] = quantized[:, index]
            filled[ys, xs] = True
    for _ in range(margin):
        added = np.zeros_like(filled)
        for axis, shift in ((0, 1), (0, -1), (1, 1), (1, -1)):
            valid = np.roll(filled, shift, axis=axis) & ~filled & ~added
            for image in packed:
                image[valid] = np.roll(image, shift, axis=axis)[valid]
            added |= valid
        filled |= added
    return {PREFIX + name: packed[index // 4][..., index % 4] for index, name in enumerate(names)}


def pack(masks, names):
    shape = masks[PREFIX + names[0]].shape
    return [np.stack([masks[PREFIX + names[index + channel]] if index + channel < len(names)
                      else np.zeros(shape, np.uint8) for channel in range(4)], axis=2)
            for index in range(0, len(names), 4)]


def compose(masks, names, colours):
    shape = masks[PREFIX + names[0]].shape
    color, total = np.zeros((*shape, 3), np.float32), np.zeros((*shape, 1), np.float32)
    for name in names:
        weight = masks[PREFIX + name][..., None].astype(np.float32) / 255
        color += weight * np.asarray(colours[name][:3], np.float32)
        total += weight
    return color / np.maximum(total, 1e-4)
