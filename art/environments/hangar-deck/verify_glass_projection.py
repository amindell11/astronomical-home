"""Check every game-camera pixel, mask background and glazing depth without saving.

Run headless Blender. Writes previews/glass-projection-audit.json and exits 1
for glass ahead of hardware, stray mask values or reversed faces.
"""
from pathlib import Path
import json
import sys

import bpy
import numpy as np
from mathutils import Vector

ROOT = Path(__file__).resolve().parent
sys.path.insert(0, str(ROOT))
from deck_projection import game_projection

bpy.ops.wm.open_mainfile(filepath=str(ROOT / 'deck.blend'))
scene = bpy.context.scene
glass = bpy.data.objects['25 | Viewport glass']
hardware = [o for o in scene.objects if o.type == 'MESH' and not o.get('preview_only') and o != glass]
eye = scene.camera.matrix_world.translation.copy()
forward = scene.camera.matrix_world.to_3x3() @ Vector((0, 0, -1))
near = min((glass.matrix_world @ v.co - eye).dot(forward) for v in glass.data.vertices)
rear = max((o.matrix_world @ v.co - eye).dot(forward) for o in hardware for v in o.data.vertices)
normal_matrix = glass.matrix_world.to_3x3().inverted().transposed()
facing = [float((normal_matrix @ p.normal).normalized().dot(eye - glass.matrix_world @ p.center))
          for p in glass.data.polygons]


def rasterize(objects, width, height, with_uv=False):
    depth = np.full((height, width), np.inf, dtype=np.float64)
    uv_image = np.zeros((height, width, 2), dtype=np.float64) if with_uv else None
    scale = scene.camera.data.sensor_width / scene.camera.data.lens
    inverse = scene.camera.matrix_world.inverted()
    for obj in objects:
        obj.data.calc_loop_triangles()
        camera = np.array([inverse @ (obj.matrix_world @ v.co) for v in obj.data.vertices])
        z = -camera[:, 2]
        assert np.all(z > 0), obj.name
        xy = camera[:, :2] / z[:, None] / scale * width
        xy[:, 0] += width / 2
        xy[:, 1] = height / 2 - xy[:, 1]
        for triangle in obj.data.loop_triangles:
            ids = list(triangle.vertices)
            a, b, c = xy[ids]
            left, top = np.maximum(np.ceil(np.min(xy[ids], axis=0) - .5), 0).astype(int)
            right, bottom = np.minimum(np.floor(np.max(xy[ids], axis=0) - .5), (width-1, height-1)).astype(int)
            determinant = (b[1]-c[1])*(a[0]-c[0]) + (c[0]-b[0])*(a[1]-c[1])
            if right < left or bottom < top or abs(determinant) < 1e-12:
                continue
            y, x = np.ogrid[top:bottom+1, left:right+1]
            wa = ((b[1]-c[1])*(x+.5-c[0]) + (c[0]-b[0])*(y+.5-c[1])) / determinant
            wb = ((c[1]-a[1])*(x+.5-c[0]) + (a[0]-c[0])*(y+.5-c[1])) / determinant
            wc = 1-wa-wb
            inside = (wa >= -1e-9) & (wb >= -1e-9) & (wc >= -1e-9)
            inverse_depth = wa/z[ids[0]] + wb/z[ids[1]] + wc/z[ids[2]]
            candidate = np.divide(1, inverse_depth, where=inverse_depth > 0,
                                  out=np.full_like(inverse_depth, np.inf))
            target = depth[top:bottom+1, left:right+1]
            nearer = inside & (candidate < target)
            target[nearer] = candidate[nearer]
            if with_uv:
                uv = np.array([obj.data.uv_layers[0].data[i].uv[:] for i in triangle.loops])
                value = (wa[..., None]*uv[0]/z[ids[0]] + wb[..., None]*uv[1]/z[ids[1]]
                         + wc[..., None]*uv[2]/z[ids[2]]) * candidate[..., None]
                uv_image[top:bottom+1, left:right+1][nearer] = value[nearer]
    return depth, uv_image


image = bpy.data.images.load(str(ROOT / 'previews/deck-window-etching-mask.png'))
image.colorspace_settings.name = 'Non-Color'
pixels = np.empty(len(image.pixels), dtype=np.float32)
image.pixels.foreach_get(pixels)
mask = pixels.reshape(image.size[1], image.size[0], 4)[:, :, 0]
layout = json.loads((ROOT / 'previews/window-etching-layout.json').read_text())
y, x = np.nonzero(mask)
atlas = np.array(layout['atlas_physical_size'])
points = np.column_stack((x+.5, y+.5)) * atlas / np.array(image.size)
outside = np.ones(len(x), dtype=bool)
for stroke in layout['strokes']:
    a, b = np.array(stroke['a']), np.array(stroke['b'])
    delta = b-a
    t = np.clip((points-a) @ delta / (delta @ delta), 0, 1)
    distance = np.linalg.norm(points-a-t[:, None]*delta, axis=1)
    outside &= distance > stroke['half_width']*1.25 + np.linalg.norm(atlas / np.array(image.size))
island_interior = np.zeros(mask.shape, dtype=bool)
glass.data.calc_loop_triangles()
for triangle in glass.data.loop_triangles:
    uv = np.array([glass.data.uv_layers[0].data[i].uv[:] for i in triangle.loops])
    polygon_pixels = uv * np.array(image.size)
    left, top = np.floor(polygon_pixels.min(axis=0)).astype(int)
    right, bottom = np.ceil(polygon_pixels.max(axis=0)).astype(int)
    yy, xx = np.ogrid[top:bottom+1, left:right+1]
    orientation = np.sign(sum(a[0]*b[1]-b[0]*a[1] for a,b in zip(polygon_pixels, np.roll(polygon_pixels,-1,axis=0))))
    inside = np.ones((bottom-top+1, right-left+1), dtype=bool)
    for a, b in zip(polygon_pixels, np.roll(polygon_pixels,-1,axis=0)):
        d = b-a
        inside &= orientation*(d[0]*(yy+.5-a[1])-d[1]*(xx+.5-a[0])) >= 0
    island_interior[top:bottom+1, left:right+1] |= inside
for _ in range(3):
    eroded = island_interior.copy()
    for dx, dy in [(x,y) for x in (-1,0,1) for y in (-1,0,1)]:
        eroded &= np.roll(island_interior, (dy,dx), (0,1))
    island_interior = eroded
report = {'hardware_rear_depth': rear, 'glass_near_depth': near, 'depth_clearance': near-rear,
          'face_toward_eye': facing, 'nonzero_texels': len(x),
          'nonzero_texels_outside_strokes': int(np.sum(outside)),
          'island_margins_and_unused_atlas_maximum': float(mask[~island_interior].max()), 'views': {}}
for width, label in [(1920, '16x9'), (2560, '21x9'), (3840, '32x9')]:
    game_projection(scene, scene.camera, width, 1080)
    hardware_depth, _ = rasterize(hardware, width, 1080)
    glass_depth, uv = rasterize([glass], width, 1080, True)
    in_front = np.isfinite(hardware_depth) & (glass_depth < hardware_depth-1e-5)
    uncovered = ~np.isfinite(hardware_depth) & ~np.isfinite(glass_depth)
    texel = uv*np.array(image.size)-.5
    integer = np.floor(texel).astype(int)
    blend = texel-integer
    sampled = np.zeros(glass_depth.shape)
    for dx, dy in [(0,0), (1,0), (0,1), (1,1)]:
        sampled += mask[np.clip(integer[:,:,1]+dy,0,image.size[1]-1),
                        np.clip(integer[:,:,0]+dx,0,image.size[0]-1)] * (
                            blend[:,:,0] if dx else 1-blend[:,:,0]) * (blend[:,:,1] if dy else 1-blend[:,:,1])
    report['views'][label] = {'resolution': [width,1080], 'pixels_checked': width*1080,
                             'glass_ahead_of_hardware_pixels': int(in_front.sum()),
                             'tinted_hardware_pixels': int(np.sum(in_front & (sampled > 0))),
                             'uncovered_window_pixels': int(uncovered.sum())}
    print(label, json.dumps(report['views'][label]), flush=True)
(ROOT / 'previews/glass-projection-audit.json').write_text(json.dumps(report, indent=2))
assert near > rear, report['depth_clearance']
assert min(facing) > 0, facing
assert not np.any(outside), report['nonzero_texels_outside_strokes']
assert report['island_margins_and_unused_atlas_maximum'] == 0
assert all(not v['glass_ahead_of_hardware_pixels']
           for v in report['views'].values()), report['views']
print('GLASS_PROJECTION=complete', flush=True)
