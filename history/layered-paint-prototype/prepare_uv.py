import bpy,json,math
from pathlib import Path
from mathutils import Vector
out=Path('D:/amind/git/agent-2/results/valis-paint-v2')
bpy.ops.wm.open_mainfile(filepath='D:/amind/git/agent-2/art/ships/valis/Valis.blend')
objs=sorted([o for o in bpy.context.scene.objects if o.type=='MESH'],key=lambda o:o.name)
bpy.ops.object.select_all(action='DESELECT')
for o in objs:
 o.hide_set(False);o.select_set(True)
 if not o.data.uv_layers.get('ValisPaintUV'):o.data.uv_layers.new(name='ValisPaintUV')
 o.data.uv_layers.active=o.data.uv_layers['ValisPaintUV']
bpy.context.view_layer.objects.active=objs[0]
bpy.ops.object.mode_set(mode='EDIT');bpy.ops.mesh.select_all(action='SELECT')
bpy.ops.uv.smart_project(angle_limit=math.radians(66),island_margin=.006,area_weight=1,correct_aspect=True)
bpy.ops.object.mode_set(mode='OBJECT')
parts=[]
for o in objs:
 m=o.data;m.calc_loop_triangles();uv=m.uv_layers.active.data; nm=o.matrix_world.to_3x3().inverted().transposed()
 tris=[]
 for t in m.loop_triangles:
  tris.append({'p':[list(o.matrix_world@m.vertices[i].co) for i in t.vertices],'uv':[list(uv[i].uv) for i in t.loops],'n':list((nm@t.normal).normalized())})
 edgefaces={}
 for p in m.polygons:
  for i,li in enumerate(p.loop_indices):
   lj=p.loop_indices[(i+1)%len(p.loop_indices)];key=tuple(sorted((m.loops[li].vertex_index,m.loops[lj].vertex_index)));edgefaces.setdefault(key,[]).append((p,li,lj))
 edges=[]
 for key,entries in edgefaces.items():
  a,b=[o.matrix_world@m.vertices[i].co for i in key]
  if abs(a.x)<.001 and abs(b.x)<.001:continue
  if len(entries)==1 or min(p.normal.dot(q.normal) for p,_,_ in entries for q,_,_ in entries)<.65:
   for p,li,lj in entries:
    if (nm@p.normal).z>.2:edges.append([list(uv[li].uv),list(uv[lj].uv),list(sum((uv[i].uv for i in p.loop_indices),Vector((0,0)))/len(p.loop_indices))])
 parts.append({'name':o.name,'material':m.materials[0].name,'triangles':tris,'edges':edges,'polygons':[{'uv':[list(uv[i].uv) for i in p.loop_indices],'normal':list(nm@p.normal),'area':p.area} for p in m.polygons]})
(out/'uv-layout.json').write_text(json.dumps({'parts':parts}))
bpy.ops.wm.save_as_mainfile(filepath=str(out/'Valis-UV.blend'))
print('UV atlas prepared',len(parts),'parts')


