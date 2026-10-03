import bpy,json,hashlib
from pathlib import Path
from mathutils import Vector
from mathutils.kdtree import KDTree
folder=Path('D:/amind/git/agent-7/results/crimson-mirror')
report=json.loads((folder/'live-preservation-check.json').read_text())
bpy.ops.wm.open_mainfile(filepath=report['corrected_copy'])
results=[]
for name in ('Wing root beam','Cube'):
    o=bpy.data.objects[name]
    o.location+=Vector((.075,.02,.03))
    o.rotation_euler.z+=.12
    bpy.context.view_layer.update()
    e=o.evaluated_get(bpy.context.evaluated_depsgraph_get())
    verts=[e.matrix_world @ v.co for v in e.data.vertices]
    tree=KDTree(len(verts))
    for i,v in enumerate(verts): tree.insert(v,i)
    tree.balance()
    error=max(tree.find(Vector((-v.x,v.y,v.z)))[2] for v in verts)
    assert error<1e-6,(name,error)
    results.append({'part':name,'symmetry_error':error})
print(json.dumps({'saved_copy_readable':True,'movement_checks':results}))
