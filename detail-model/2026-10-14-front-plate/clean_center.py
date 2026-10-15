import bpy
import bmesh
import json
import sys
from pathlib import Path
from mathutils import Vector

stage=Path(__file__).parent
if bpy.context.mode!='OBJECT':bpy.ops.object.mode_set(mode='OBJECT')
target=bpy.data.objects['09 Side armor']
matrix=target.matrix_world.copy()
bm=bmesh.new();bm.from_mesh(target.data)
plane_co=matrix.inverted()@Vector((0,0,0))
plane_no=matrix.to_3x3().transposed()@Vector((1,0,0))
bmesh.ops.bisect_plane(bm,geom=list(bm.verts)+list(bm.edges)+list(bm.faces),
                      dist=1e-6,plane_co=plane_co,plane_no=plane_no,
                      clear_outer=True,clear_inner=False)
for v in bm.verts:
    world=matrix@v.co
    if abs(world.x)<1e-5:
        world.x=0
        v.co=matrix.inverted()@world
bmesh.ops.recalc_face_normals(bm,faces=list(bm.faces))
bm.to_mesh(target.data);bm.free();target.data.update()
mirror=next(m for m in target.modifiers if m.type=='MIRROR')
mirror.use_clip=True
mirror.use_mirror_merge=True
mirror.merge_threshold=.0001
target.name='15 Front armor'
bpy.context.view_layer.update()
eo=target.evaluated_get(bpy.context.evaluated_depsgraph_get());evaluated=eo.to_mesh()
bm=bmesh.new();bm.from_mesh(evaluated)
assert all(e.is_manifold for e in bm.edges), 'Plate is not closed'
remaining=set(bm.verts);components=0
while remaining:
    components+=1;queue=[remaining.pop()]
    while queue:
        v=queue.pop()
        for e in v.link_edges:
            other=e.other_vert(v)
            if other in remaining:remaining.remove(other);queue.append(other)
assert components==1,components
assert bm.calc_volume(signed=True)>0
geometry={'vertices':len(bm.verts),'edges':len(bm.edges),'faces':len(bm.faces),'components':components,'closed':True}
bm.free();eo.to_mesh_clear()
payload={'vertices':[list(v.co) for v in target.data.vertices],
         'faces':[list(p.vertices) for p in target.data.polygons],
         'materials':[p.material_index for p in target.data.polygons],
         'uv':{u.name:[list(d.uv) for d in u.data] for u in target.data.uv_layers},
         'matrix_basis':[list(row) for row in target.matrix_basis],
         'matrix_parent_inverse':[list(row) for row in target.matrix_parent_inverse],
         'topology':geometry,
         'modifiers':[]}
sys.path.insert(0,'D:/amind/git/agent-3/art/tools/ship')
import ship_source
for m in target.modifiers:
    values={}
    for prop in ship_source.GEOMETRY_PARAMETERS[m.type]+ship_source.UV_PARAMETERS[m.type]:
        value=getattr(m,prop)
        if isinstance(value,bpy.types.Object):value={'object':value.name}
        elif not isinstance(value,(bool,int,float,str,type(None))):value=list(value)
        values[prop]=value
    values['material']=m.material if m.type=='BEVEL' else None
    payload['modifiers'].append({'name':m.name,'type':m.type,'properties':values})
(stage/'plate-correction.json').write_text(json.dumps(payload,indent=2),encoding='utf-8')
exec((stage/'render_plate.py').read_text(),{'__name__':'__main__','__file__':str(stage/'render_plate.py'),'variant':'corrected-draft'})
