import bpy, bmesh, sys, json, hashlib
from pathlib import Path
from mathutils import Vector

sys.path.insert(0, 'D:/amind/git/agent-3/art/tools/ship')
import ship_source

root = Path('D:/amind/git/agent-3/results/bulwark/straight-tail')
source = root/'owner/Bulwark.blend'
out = root/'refined'
sha = hashlib.sha256(source.read_bytes()).hexdigest()
assert sha == '29da5d3819d1c5e916a8e2627ede6ae674436aa0be042fb23cc9aa08fb9737d5'
bpy.ops.wm.open_mainfile(filepath=str(source), load_ui=False, use_scripts=False)
scene = bpy.context.scene
before = ship_source.fingerprint(scene)
boom = bpy.data.objects['04 Tail booms']
tip = bpy.data.objects['05 Tail tips']
assert len(boom.data.vertices) == 37 and len(tip.data.vertices) == 35
assert boom.data.users == tip.data.users == 1
bv = [boom.matrix_world@v.co for v in boom.data.vertices]
tv = [tip.matrix_world@v.co for v in tip.data.vertices]
original_bv = [v.copy() for v in bv]
original_tv = [v.copy() for v in tv]
rails = [
    (24, [1,17,3,30], 7),
    (16, [7,15,0,31], 8),
    (19, [8,14,25,32], 9),
    (18, [9,13,26,33], 10),
    (20, [10,12,27,34], 11),
    (23, [2,11,28,35], 12),
    (23, [6,29,36], 13),
]
errors = []
for column, (root_index, boom_indices, tip_index) in enumerate(rails):
    a, b = bv[root_index].copy(), tv[tip_index].copy()
    def straight_x(y):
        return a.x+(b.x-a.x)*(y-a.y)/(b.y-a.y)
    for index in boom_indices:
        bv[index].x = straight_x(bv[index].y)
        errors.append(abs(bv[index].x-straight_x(bv[index].y)))
    tv[column].x = straight_x(tv[column].y)
    errors.append(abs(tv[column].x-straight_x(tv[column].y)))

for obj, points in [(boom,bv),(tip,tv)]:
    inverse = obj.matrix_world.inverted()
    for vertex, point in zip(obj.data.vertices, points):
        vertex.co = inverse@point
    obj.data.update()
    bm = bmesh.new()
    bm.from_mesh(obj.data)
    bmesh.ops.recalc_face_normals(bm, faces=list(bm.faces))
    nonplanar = [f for f in bm.faces if len(f.verts)>4 and any(abs((v.co-f.verts[0].co).dot(f.normal))>1e-5 for v in f.verts)]
    if nonplanar:
        bmesh.ops.triangulate(bm, faces=nonplanar)
    bm.to_mesh(obj.data)
    bm.free()
    obj.data.update()

bpy.context.view_layer.update()
after = ship_source.fingerprint(scene)
changed = [n for n,p in before['parts'].items() if p != after['parts'].get(n)]
assert set(changed) == {'04 Tail booms','05 Tail tips'}, changed
assert before['parts'].keys() == after['parts'].keys()
for old, new in [(original_bv,bv),(original_tv,tv)]:
    assert all(abs(a.y-b.y)<1e-6 and abs(a.z-b.z)<1e-6 for a,b in zip(old,new))
assert all((tv[i]-original_tv[i]).length<1e-6 for i in range(7,35))
for screen in bpy.data.screens:
    for area in screen.areas:
        if area.type == 'VIEW_3D':
            area.spaces.active.overlay.show_overlays = True
for obj in scene.objects:
    obj.select_set(False)
boom.select_set(True)
bpy.context.view_layer.objects.active = boom
scene['review_stage'] = 'Straight tail edges and widened housing shoulders'
target = out/'Bulwark.blend'
bpy.ops.wm.save_as_mainfile(filepath=str(target))
audit = {
    'source_sha256':sha,
    'owner_checkpoint':'9eff188d81390e80de3fc0202a0bde35796c9791',
    'output_sha256':hashlib.sha256(target.read_bytes()).hexdigest(),
    'changed_existing_parts':changed,
    'new_parts':[],
    'unchanged_parts':[n for n in before['parts'] if n not in changed],
    'straight_rail_max_error':max(errors),
    'side_profile_unchanged':True,
    'engines_and_housing_rear_unchanged':True,
    'changes':{
        '04 Tail booms':'Straight longitudinal top-view edges from the boom root to the wide housing shoulder; removes the inward pinching curve.',
        '05 Tail tips':'Forward housing shoulders widen to meet the same straight edges. The engine angle, rear housing, cavity, and side profile remain.'
    }
}
assert hashlib.sha256(source.read_bytes()).hexdigest() == sha
(out/'shape-audit.json').write_text(json.dumps(audit,indent=2))
ship = json.loads((root/'owner/ship.json').read_text())
ship['source'] = target.name
(out/'ship.json').write_text(json.dumps(ship,indent=2))
print(json.dumps(audit))
