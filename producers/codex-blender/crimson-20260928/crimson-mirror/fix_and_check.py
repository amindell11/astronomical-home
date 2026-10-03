import bpy,json
from pathlib import Path
from mathutils import Vector
from mathutils.kdtree import KDTree
root=Path('D:/amind/git/agent-7/art/ships/crimson/mesh-study')
source=root/'top-match/CrimsonTopMatch.blend'
target=root/'top-match/CrimsonTopMatch-CenteredMirror.blend'
assert not target.exists()
bpy.ops.wm.open_mainfile(filepath=str(source))
pairs=[(o,m) for o in bpy.context.scene.objects for m in o.modifiers if m.type=='MIRROR']
assert len(pairs)==29 and all(m.mirror_object is None for o,m in pairs)
def coords(o):
    bpy.context.view_layer.update()
    e=o.evaluated_get(bpy.context.evaluated_depsgraph_get())
    return [e.matrix_world @ v.co for v in e.data.vertices]
before={o.name:coords(o) for o,m in pairs}
col=bpy.data.collections.new('00 Symmetry controls')
bpy.context.scene.collection.children.link(col)
anchor=bpy.data.objects.new('SHIP CENTER - mirror plane',None)
col.objects.link(anchor)
anchor.empty_display_type='PLAIN_AXES'
anchor.empty_display_size=.15
anchor.lock_location=(True,True,True)
anchor.lock_rotation=(True,True,True)
anchor.lock_scale=(True,True,True)
anchor.hide_select=True
anchor.hide_render=True
for o,m in pairs:
    m.mirror_object=anchor
bpy.context.view_layer.update()
unchanged=max((a-b).length for o,m in pairs for a,b in zip(before[o.name],coords(o)))
assert unchanged<1e-6,unchanged
bpy.ops.wm.save_as_mainfile(filepath=str(target))
reports=[]
for path in [target,root/'CrimsonMesh-CenteredMirror.blend']:
    bpy.ops.wm.open_mainfile(filepath=str(path))
    pairs=[(o,m) for o in bpy.context.scene.objects for m in o.modifiers if m.type=='MIRROR']
    worst=0
    for o,m in pairs:
        original=o.matrix_world.copy()
        try:
            o.location += Vector((.075,.033,.021))
            o.rotation_euler.z += .12
            verts=coords(o)
            tree=KDTree(len(verts))
            for i,v in enumerate(verts): tree.insert(v,i)
            tree.balance()
            error=max(tree.find(Vector((-v.x,v.y,v.z)))[2] for v in verts)
            assert error<1e-6,(o.name,error)
            worst=max(worst,error)
        finally:
            o.matrix_world=original
    reports.append({'file':str(path),'tested_parts':len(pairs),'translation_and_rotation_symmetry_error':worst})
report={'top_draft_geometry_change':unchanged,'checks':reports}
Path('D:/amind/git/agent-7/results/crimson-mirror/verification.json').write_text(json.dumps(report,indent=2))
print(json.dumps(report))
