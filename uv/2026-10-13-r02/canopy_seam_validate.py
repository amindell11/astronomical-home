import bpy,sys,json,hashlib
import numpy as np
from pathlib import Path
sys.path.insert(0,'D:/amind/git/agent-3/art/tools/ship')
import ship_source,ship_uv
root=Path('D:/amind/git/agent-3/results/bulwark/uv/2026-10-13-r02')
source=Path('D:/amind/git/agent-3/art/ships/bulwark/Bulwark.blend')
sha=hashlib.sha256(source.read_bytes()).hexdigest();src=ship_source.open_source(source);ev=ship_source.Evaluation(src)
names=[o.name for o in src.objects if o.type=='MESH' and not o.hide_render];guide=[];densities={}
for name in names:
 mesh=ev.mesh(name);_,_,_,tri=ship_source.mesh_arrays(mesh)
 uv=ship_source.read_array(mesh.uv_layers['PaintUV'].uv,'vector',np.float32,2).reshape(-1,2)
 assert uv.min()>=0 and uv.max()<=1
 guide.append({'name':name,'uv':uv.tolist(),'triangles':tri.tolist()});densities[name]=ship_uv.density(mesh,ev.to_space(name),2048)
 if name=='06 Cockpit dome':
  edges={}
  for face in mesh.polygons:
   loops=list(face.loop_indices)
   for i,a in enumerate(loops):
    b=loops[(i+1)%len(loops)];va=mesh.loops[a].vertex_index;vb=mesh.loops[b].vertex_index
    key=tuple(sorted((va,vb)));mapping={va:uv[a],vb:uv[b]};edges.setdefault(key,[]).append(mapping)
  seams=[];boundary=[];maximum=0
  for key,refs in edges.items():
   if len(refs)==1:boundary.append(key);continue
   assert len(refs)==2
   gap=max(float(np.linalg.norm(refs[0][v]-refs[1][v])) for v in key);maximum=max(maximum,gap)
   if gap>1e-6:seams.append(key)
  assert not seams,seams
  # Subdivision repeats shared UV calculations with subpixel float differences.
  canonical={}
  for loop in mesh.loops:canonical.setdefault(loop.vertex_index,uv[loop.index].copy())
  welded=np.array([canonical[l.vertex_index] for l in mesh.loops])
  assert float(np.max(np.linalg.norm(welded-uv,axis=1)))<1e-6
  mesh.uv_layers['PaintUV'].uv.foreach_set('vector',welded.astype(np.float32).ravel());mesh.update()
  canopy=bpy.data.objects.new('Evaluated canopy UV overlap check',mesh);ev.scene.collection.objects.link(canopy)
  for o in ev.scene.objects:o.select_set(False)
  canopy.select_set(True);bpy.context.view_layer.objects.active=canopy;ev.scene.tool_settings.use_uv_select_sync=False
  bpy.ops.object.mode_set(mode='EDIT');bpy.ops.mesh.select_all(action='SELECT');bpy.ops.uv.select_all(action='DESELECT');bpy.ops.uv.select_overlap();bpy.ops.object.mode_set(mode='OBJECT')
  overlaps=sum(bool(l.value) for l in mesh.attributes['.uv_select_vert'].data)
  assert overlaps==0,overlaps
  (root/'canopy-evaluated.json').write_text(json.dumps({'uv':welded.tolist(),'triangles':tri.tolist()},separators=(',',':')))
  report={'source_sha256':sha,'evaluated_canopy_interior_uv_seams':len(seams),'maximum_interior_uv_gap':maximum,
          'evaluated_canopy_overlap_selected_loops':overlaps,'overlap_shared_uv_tolerance':1e-6,
          'open_underside_boundary_edges':len(boundary),'source_unchanged':True}
  bpy.data.objects.remove(canopy,do_unlink=True)
 bpy.data.meshes.remove(mesh)
(root/'uv-triangles.json').write_text(json.dumps(guide,separators=(',',':')))
(root/'canopy-seam-check.json').write_text(json.dumps(report,indent=2))
assert hashlib.sha256(source.read_bytes()).hexdigest()==sha
print(json.dumps(report));print('DENSITY_RATIO',max(densities.values())/min(densities.values()))
