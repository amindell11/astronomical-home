import bpy,json
from pathlib import Path
out=Path('D:/amind/git/agent-2/results/valis-integration')
bpy.ops.wm.open_mainfile(filepath='D:/amind/git/agent-2/art/ships/valis/Valis.blend')
dg=bpy.context.evaluated_depsgraph_get();parts=[]
for o in bpy.context.scene.objects:
 if o.type!='MESH':continue
 e=o.evaluated_get(dg);m=e.to_mesh();m.calc_loop_triangles();verts=[];norms=[];uv=[];indices=[];normalmat=o.matrix_world.to_3x3().inverted().transposed()
 for t in m.loop_triangles:
  normal=(normalmat@t.normal).normalized()
  for vi in t.vertices:
   p=o.matrix_world@m.vertices[vi].co;verts.append({'x':p.x,'y':-p.y,'z':-p.z});norms.append({'x':normal.x,'y':-normal.y,'z':-normal.z});uv.append({'x':p.x*.1+.5,'y':p.y*.1+.5});indices.append(len(indices))
 parts.append({'name':o.name,'material':o.data.materials[0].name,'vertices':verts,'normals':norms,'uv':uv,'triangles':indices});e.to_mesh_clear()
(out/'valis-meshes.json').write_text(json.dumps({'parts':parts},separators=(',',':')))
print('Exported',len(parts),'parts;',sum(len(p['triangles'])//3 for p in parts),'triangles')
