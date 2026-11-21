"""Verify the two-strip finish without saving Blender data.

Run headless Blender with -- --capture once before editing. The default checks
the frozen scene, lamp visibility and solids, then writes finish7-audit.json.
Failed assertions exit nonzero with --python-exit-code 1.
"""
from pathlib import Path
import json
import shutil
import sys

import bpy
import bmesh
from mathutils import Vector
from mathutils.bvhtree import BVHTree
from bpy_extras.object_utils import world_to_camera_view

ROOT = Path(__file__).resolve().parent
OUT = ROOT / 'previews'
BODY = 'Deck | joined closed structure'
source = (ROOT / 'verify_finish4.py').read_text(encoding='utf-8')
scope = {'__file__': str(ROOT / 'verify_finish4.py')}
exec(compile(source.split('current = json.loads')[0], str(ROOT / 'verify_finish4.py'), 'exec'), scope)
scene = bpy.context.scene
current = json.loads(json.dumps(scope['snapshot']()))
baseline_path = OUT / 'finish7-baseline.json'
if '--capture' in sys.argv:
    assert not baseline_path.exists(), 'The finish 7 baseline is already frozen.'
    baseline_path.write_text(json.dumps(current, indent=2), encoding='utf-8')
    shutil.copyfile(OUT / 'deck-camera.png', OUT / 'deck-finish7-original.png')
    print('FINISH7_BASELINE=complete', flush=True)
    sys.exit(0)

baseline = json.loads(baseline_path.read_text(encoding='utf-8'))
def lighting_change(name):
    return 'Header downlight' in name or 'cyan strip' in name or name.startswith('Preview | header ')

preserved = {name: value == current['objects'].get(name)
             for name, value in baseline['objects'].items()
             if name != BODY and not lighting_change(name)}
unchanged_palette = {name: value == current['materials'].get(name)
                     for name, value in baseline['materials'].items()
                     if name != 'Deck | warm white downlight emission'}
checks = {
    'unrelated_objects_exact': all(preserved.values()),
    'unrelated_materials_exact': all(unchanged_palette.values()),
    'only_requested_object_changes': all(lighting_change(name) for name in
        set(baseline['objects']) ^ set(current['objects'])),
    'no_structural_dashes': not any('cyan strip' in name for name in current['objects']),
    'linesets_exact': current['lines'] == baseline['lines'] and current['ink'] == baseline['ink'],
    'glass_shader_exact': current['glass_shader'] == baseline['glass_shader'],
}
hardware = [o for o in scene.objects if o.type == 'MESH' and not o.get('preview_only')
            and o.name != '25 | Viewport glass']
meshes = []
trees = []
for obj in hardware:
    bm = bmesh.new()
    bm.from_mesh(obj.data)
    meshes.append({'name': obj.name, 'planar': bool(obj.get('planar_screen')),
                   'closed': all(e.is_manifold and e.is_contiguous for e in bm.edges),
                   'volume': bm.calc_volume(signed=True),
                   'clean': not any(not v.link_faces for v in bm.verts)
                            and not any(f.calc_area() == 0 for f in bm.faces) and not obj.modifiers})
    bm.transform(obj.matrix_world)
    trees.append((obj, BVHTree.FromBMesh(bm)))
    bm.free()
checks['closed_solids'] = all(m['planar'] or m['closed'] and m['volume'] > 0 for m in meshes)
checks['clean_meshes'] = all(m['clean'] for m in meshes)
lamps = []
eye = scene.camera.matrix_world.translation
for obj in hardware:
    if 'Header downlight' not in obj.name:
        continue
    shader = obj.active_material.node_tree.nodes.get('Principled BSDF')
    size = current['objects'][obj.name]['size']
    front = max(v.co.z for v in obj.data.vertices)
    failures = []
    pixels = []
    for x_fraction in (-.495, -.4, -.25, 0, .25, .4, .495):
        for y_fraction in (-.28, 0, .28):
            point = obj.matrix_world @ Vector((size[0] * x_fraction, size[1] * y_fraction, front))
            projected = world_to_camera_view(scene, scene.camera, point)
            pixels.append([projected.x * 1920, (1 - projected.y) * 1080])
            if not (0 < projected.x < 1 and 0 < projected.y < 1 and projected.z > 0):
                failures.append('outside camera')
            direction = point - eye
            distance = direction.length
            for other, tree in trees:
                if other == obj:
                    continue
                hit = tree.ray_cast(eye, direction.normalized(), distance - .0001)
                if hit[0] is not None:
                    failures.append(other.name)
    lamps.append({'name': obj.name, 'position': current['objects'][obj.name]['centre'],
                  'size': size, 'linear_color': list(shader.inputs['Emission Color'].default_value[:3]),
                  'strength': shader.inputs['Emission Strength'].default_value,
                  'unlinked_emission': not shader.inputs['Emission Color'].is_linked,
                  'pixel_bounds': [[min(p[i] for p in pixels), max(p[i] for p in pixels)] for i in (0, 1)],
                  'visibility_failures': sorted(set(failures))})
checks['exactly_two_downlights'] = len(lamps) == 2
checks['lens_contract'] = all(l['name'].endswith(' lens') and l['unlinked_emission']
    and 5 <= l['size'][0] <= 6 and .12 <= l['size'][1] <= .18
    and max(abs(a-b) for a, b in zip(l['linear_color'], (.28, .36, .48))) < 1e-6 for l in lamps)
checks['fully_visible_lenses'] = all(not l['visibility_failures'] for l in lamps)
if len(lamps) == 2:
    a, b = lamps
    checks['mirrored_lenses'] = abs(a['position'][0] + b['position'][0]) < 1e-6 and all(
        abs(a['position'][i] - b['position'][i]) < 1e-6 for i in (1, 2)) and a['size'] == b['size']
report = {'checks': checks, 'preserved_objects': preserved, 'preserved_materials': unchanged_palette,
          'lamps': lamps, 'meshes': meshes}
(OUT / 'finish7-audit.json').write_text(json.dumps(report, indent=2), encoding='utf-8')
print(json.dumps(checks), flush=True)
assert all(checks.values()), [name for name, passed in checks.items() if not passed]
print('FINISH7_AUDIT=complete', flush=True)
