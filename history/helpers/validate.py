import bpy,json,math
from pathlib import Path
bpy.ops.wm.open_mainfile(filepath='D:/amind/git/agent-2/art/ships/valis/Valis.blend')
root=bpy.data.objects['Valis symmetry origin']
objects=[o for o in bpy.context.scene.objects if o.type=='MESH']
rows=[]
dg=bpy.context.evaluated_depsgraph_get()
for obj in objects:
    assert len([m for m in obj.modifiers if m.type=='MIRROR' and m.mirror_object==root and m.show_viewport and m.show_render])==1,obj.name
    assert all(math.isfinite(v) for vert in obj.data.vertices for v in vert.co),obj.name
    evaluated=obj.evaluated_get(dg).to_mesh()
    coords={tuple(round(float(c),4) for c in v.co) for v in evaluated.vertices}
    assert all((-x,y,z) in coords for x,y,z in coords),obj.name
    rows.append({'part':obj.name,'source_vertices':len(obj.data.vertices),'evaluated_faces':len(evaluated.polygons),'live_symmetry':True})
    obj.evaluated_get(dg).to_mesh_clear()
assert not bpy.data.libraries
assert not bpy.data.images
Path('D:/amind/git/agent-2/results/valis-geometry/validation.json').write_text(json.dumps({'verdict':'pass','mesh_parts':len(rows),'external_dependencies':0,'checks':['finite geometry','live mirrors anchored to shared origin','evaluated bilateral symmetry','self-contained source'],'parts':rows},indent=2))
print('VALIDATED',len(rows),'editable mirrored mesh parts')
