"""Check finish 5 against the saved finish 4 scene without saving Blender data.

Run headless Blender with -- --capture before editing to freeze the snapshot and
six existing masks. The default writes finish5-audit.json; failed checks exit 1.
--glass-placement allows only the canopy transform and its world-space centre to change.
"""
from pathlib import Path
import hashlib
import json
import shutil
import sys

import bpy
import bmesh

ROOT = Path(__file__).resolve().parent
OUT = ROOT / 'previews'
BODY = 'Deck | joined closed structure'
source = (ROOT / 'verify_finish4.py').read_text(encoding='utf-8')
scope = {'__file__': str(ROOT / 'verify_finish4.py')}
exec(compile(source.split('current = json.loads')[0], str(ROOT / 'verify_finish4.py'), 'exec'), scope)


def components():
    obj = bpy.data.objects[BODY]
    bm = bmesh.new()
    bm.from_mesh(obj.data)
    unseen = set(bm.verts)
    result = []
    while unseen:
        pending = [unseen.pop()]
        vertices = set(pending)
        while pending:
            v = pending.pop()
            for edge in v.link_edges:
                other = edge.other_vert(v)
                if other in unseen:
                    unseen.remove(other)
                    vertices.add(other)
                    pending.append(other)
        faces = {f for v in vertices for f in v.link_faces}
        geometry = sorted((sorted(tuple(round(c, 6) for c in v.co) for v in f.verts),
                           obj.data.materials[f.material_index].name) for f in faces)
        volume = sum(f.verts[0].co.dot(f.verts[i].co.cross(f.verts[i+1].co)) / 6
                     for f in faces for i in range(1, len(f.verts)-1))
        result.append({'hash': scope['digest'](geometry), 'faces': len(faces),
                       'volume': volume,
                       'bounds': [[min(v.co[i] for v in vertices), max(v.co[i] for v in vertices)]
                                  for i in range(3)]})
    bm.free()
    return sorted(result, key=lambda c: c['hash'])


snapshot = json.loads(json.dumps(scope['snapshot']()))
snapshot['body_components'] = components()
baseline_path = OUT / 'finish5-baseline.json'
if '--capture' in sys.argv:
    baseline_path.write_text(json.dumps(snapshot, indent=2), encoding='utf-8')
    for suffix in ('', '-16x10', '-3x2', '-4x3', '-21x9', '-32x9'):
        shutil.copyfile(OUT / f'deck-mask{suffix}.png', OUT / f'deck-finish5-original-mask{suffix}.png')
    print('FINISH5_BASELINE=complete', flush=True)
    sys.exit(0)

baseline = json.loads(baseline_path.read_text(encoding='utf-8'))
preserved = {n: scope['object_matches'](n, value, snapshot['objects'].get(n))
             for n, value in baseline['objects'].items() if n != BODY}
checks = {'all_other_objects_exact': all(preserved.values()),
          'object_names_exact': set(snapshot['objects']) == set(baseline['objects']),
          'palette_exact': snapshot['materials'] == baseline['materials'],
          'ink_exact': snapshot['lines'] == baseline['lines'] and snapshot['ink'] == baseline['ink'],
          'glass_shader_exact': snapshot['glass_shader'] == baseline['glass_shader']}
meshes = []
for obj in bpy.context.scene.objects:
    if obj.type != 'MESH' or obj.get('preview_only') or obj.name == '25 | Viewport glass':
        continue
    bm = bmesh.new()
    bm.from_mesh(obj.data)
    obj.data.calc_loop_triangles()
    meshes.append({'name': obj.name, 'triangles': len(obj.data.loop_triangles),
                   'planar_screen': bool(obj.get('planar_screen')),
                   'closed': all(e.is_manifold and e.is_contiguous for e in bm.edges),
                   'volume': bm.calc_volume(signed=True),
                   'clean': not any(not v.link_faces for v in bm.verts)
                            and not any(f.calc_area() == 0 for f in bm.faces),
                   'modifiers': [m.type for m in obj.modifiers]})
    bm.free()
checks['closed_solids'] = all(m['planar_screen'] or m['closed'] and m['volume'] > 0 for m in meshes)
checks['clean_meshes'] = all(m['clean'] for m in meshes)
checks['no_modifiers'] = all(not m['modifiers'] for m in meshes)
old_hashes = {c['hash'] for c in baseline['body_components']}
new_hashes = {c['hash'] for c in snapshot['body_components']}
checks['new_body_components_positive'] = all(c['volume'] > 0 for c in snapshot['body_components']
                                           if c['hash'] not in old_hashes)
checks['inward_cavity_surfaces_unchanged'] = {c['hash'] for c in snapshot['body_components'] if c['volume'] < 0} == {
    c['hash'] for c in baseline['body_components'] if c['volume'] < 0}
report = {'checks': checks, 'preserved_objects': preserved,
          'glass_placement_allowed': '--glass-placement' in sys.argv,
          'meshes': meshes, 'hardware_triangles': sum(m['triangles'] for m in meshes),
          'body_components_before': len(old_hashes), 'body_components_after': len(new_hashes),
          'unchanged_body_components': len(old_hashes & new_hashes),
          'removed_components': [c for c in baseline['body_components'] if c['hash'] not in new_hashes],
          'added_components': [c for c in snapshot['body_components'] if c['hash'] not in old_hashes],
          'scene_sha256': hashlib.sha256((ROOT / 'deck.blend').read_bytes()).hexdigest()}
(OUT / 'finish5-audit.json').write_text(json.dumps(report, indent=2), encoding='utf-8')
print(json.dumps(checks), flush=True)
assert all(checks.values()), [key for key, value in checks.items() if not value]
print('FINISH5_AUDIT=complete', flush=True)
