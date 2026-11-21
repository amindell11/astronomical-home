"""Check glazing geometry, baked mask and camera coverage without saving the scene.

Run headless Blender after the mask bake. Optional --baseline names a pre-glass
object snapshot. Writes previews/window-etching-audit.json; assertions exit nonzero.
"""
from pathlib import Path
import argparse
import hashlib
import json
import sys

import bpy
import numpy as np
from mathutils import Vector
from mathutils.bvhtree import BVHTree
from mathutils.geometry import barycentric_transform
from bpy_extras.object_utils import world_to_camera_view

ROOT = Path(__file__).resolve().parent
sys.path.insert(0, str(ROOT))
from deck_projection import game_projection

parser = argparse.ArgumentParser()
parser.add_argument('--baseline', type=Path)
args = parser.parse_args(sys.argv[sys.argv.index('--') + 1:] if '--' in sys.argv else [])
bpy.ops.wm.open_mainfile(filepath=str(ROOT / 'deck.blend'))
scene = bpy.context.scene
glass = bpy.data.objects['25 | Viewport glass']
mesh = glass.data
mesh.calc_loop_triangles()
assert len(mesh.uv_layers) == 1 and len(mesh.materials) == 1
assert not glass.get('preview_only')
mask_socket = mesh.materials[0].node_tree.nodes['Only the perimeter catches light'].inputs[0].links[0].from_socket
assert mask_socket.type == 'VALUE'
uv = mesh.uv_layers[0]
uvs = [Vector((d.uv.x, d.uv.y, 0)) for d in uv.data]
assert all(0 <= p.x <= 1 and 0 <= p.y <= 1 for p in uvs)
vertices = [glass.matrix_world @ v.co for v in mesh.vertices]
triangles = [tuple(t.vertices) for t in mesh.loop_triangles]
tree = BVHTree.FromPolygons(vertices, triangles, all_triangles=True)


def overlap_area(a, b):
    result = [p.copy() for p in a]
    cross = lambda x, y: x.x * y.y - x.y * y.x
    if cross(b[1] - b[0], b[2] - b[0]) < 0:
        b = b[::-1]
    for p, q in zip(b, b[1:] + b[:1]):
        clipped = []
        for x, y in zip(result, result[1:] + result[:1]):
            dx, dy = cross(q - p, x - p), cross(q - p, y - p)
            if dx >= 0:
                clipped.append(x)
            if (dx >= 0) != (dy >= 0):
                clipped.append(x.lerp(y, dx / (dx - dy)))
        result = clipped
    return abs(sum(cross(p, q) for p, q in zip(result, result[1:] + result[:1]))) / 2


uv_triangles = [[uvs[i] for i in t.loops] for t in mesh.loop_triangles]
overlaps = [overlap_area(a, b) for i, a in enumerate(uv_triangles) for b in uv_triangles[i + 1:]]
assert max(overlaps, default=0) < 1e-9
pane_records = []
for face in mesh.polygons:
    points = [vertices[i] for i in face.vertices]
    normal = (points[1] - points[0]).cross(points[2] - points[0]).normalized()
    deviation = max(abs((p - points[0]).dot(normal)) for p in points)
    assert deviation < 1e-5
    texels = [Vector((uvs[i].x * 4096, uvs[i].y * 2048, 0)) for i in face.loop_indices]
    densities = [(b - a).length / (q - p).length
                 for i, (p, a) in enumerate(zip(points, texels))
                 for q, b in zip(points[i + 1:], texels[i + 1:])]
    assert max(densities) / min(densities) < 1.0001, (face.index, min(densities), max(densities))
    pane_records.append({'vertices': len(points), 'planarity_error': deviation,
                         'texels_per_unit': sum(densities) / len(densities)})

image = bpy.data.images.load(str(ROOT / 'previews/deck-window-etching-mask.png'))
image.colorspace_settings.name = 'Non-Color'
assert tuple(image.size) == (4096, 2048)
pixels = np.empty(2048 * 4096 * 4, dtype=np.float32)
image.pixels.foreach_get(pixels)
pixels = pixels.reshape(2048, 4096, 4)
assert np.max(np.abs(pixels[:, :, 0] - pixels[:, :, 1])) == 0
assert np.max(np.abs(pixels[:, :, 0] - pixels[:, :, 2])) == 0
eye = scene.camera.matrix_world.translation


def sample_mask(hit, index):
    t = mesh.loop_triangles[index]
    coord = barycentric_transform(hit, *(vertices[i] for i in t.vertices), *(uvs[i] for i in t.loops))
    return float(pixels[min(2047, int(coord.y * 2048)), min(4095, int(coord.x * 4096)), 0])


hardware = [o for o in scene.objects if o.type == 'MESH' and not o.get('preview_only') and o != glass]
hw_vertices, hw_faces = [], []
for obj in hardware:
    offset = len(hw_vertices)
    hw_vertices.extend(obj.matrix_world @ v.co for v in obj.data.vertices)
    hw_faces.extend(tuple(offset + i for i in p.vertices) for p in obj.data.polygons)
hardware_tree = BVHTree.FromPolygons(hw_vertices, hw_faces)
coverage = {}
for width, label in [(1920, '16x9'), (2560, '21x9'), (3840, '32x9')]:
    game_projection(scene, scene.camera, width, 1080)
    bpy.context.view_layer.update()
    scale = scene.camera.data.sensor_width / scene.camera.data.lens
    rotation = scene.camera.matrix_world.to_3x3()
    sky_samples, missed = 0, []
    for y in range(6, 1080, 12):
        for x in range(6, width, 12):
            ray = (rotation @ Vector(((x / width - .5) * scale,
                                     (.5 - y / 1080) * scale * 1080 / width, -1))).normalized()
            if hardware_tree.ray_cast(eye, ray)[0] is not None:
                continue
            sky_samples += 1
            if tree.ray_cast(eye, ray)[0] is None:
                missed.append([x, y])
    coverage[label] = {'open_window_samples': sky_samples, 'uncovered': missed}
    assert not missed, (label, missed[:20])

game_projection(scene, scene.camera, 1920, 1080)
corners = [world_to_camera_view(scene, scene.camera, Vector((x, y, z)))
           for x in (-2.5, 2.5) for y in (-2, 2) for z in (2.5, 6.5)]
left, right = min(p.x for p in corners), max(p.x for p in corners)
bottom, top = min(p.y for p in corners), max(p.y for p in corners)
central_values = []
scale = scene.camera.data.sensor_width / scene.camera.data.lens
for x in np.linspace(left, right, 100):
    for y in np.linspace(bottom, top, 80):
        ray = (rotation @ Vector(((x - .5) * scale, (y - .5) * scale * 1080 / 1920, -1))).normalized()
        hit, _, index, _ = tree.ray_cast(eye, ray)
        assert hit is not None
        central_values.append(sample_mask(hit, index))
assert max(central_values) == 0, max(central_values)

preservation = None
if args.baseline:
    baseline = json.loads(args.baseline.read_text())
    current = {}
    for obj in scene.objects:
        if obj == glass:
            continue
        record = {'type': obj.type, 'matrix': [list(row) for row in obj.matrix_world], 'properties': dict(obj.items())}
        if obj.type == 'MESH':
            record.update(vertices=[list(v.co) for v in obj.data.vertices], faces=[list(p.vertices) for p in obj.data.polygons],
                          materials=[m.name for m in obj.data.materials], indices=[p.material_index for p in obj.data.polygons])
        current[obj.name] = hashlib.sha256(json.dumps(record, sort_keys=True, default=str).encode()).hexdigest()
    preservation = {name: current.get(name) == value for name, value in baseline.items()}
    assert all(preservation.values()), [name for name, passed in preservation.items() if not passed]
    assert set(current) == set(baseline)

report = {'object': glass.name, 'panes': pane_records, 'triangles': len(triangles),
          'uv_map': uv.name, 'maximum_uv_overlap': max(overlaps, default=0),
          'mask_size': list(image.size), 'mask_maximum': float(pixels[:, :, 0].max()),
          'nonzero_mask_fraction': float(np.mean(pixels[:, :, 0] > 0)),
          'hologram_samples': len(central_values), 'hologram_mask_maximum': max(central_values),
          'coverage': coverage, 'original_objects_unchanged': preservation}
(ROOT / 'previews/window-etching-audit.json').write_text(json.dumps(report, indent=2))
print('GLASS_VERIFICATION=complete', flush=True)
