import bpy,bmesh,sys,json,hashlib,math
from pathlib import Path
from mathutils import Matrix,Vector
sys.path.insert(0,'D:/amind/git/agent-3/art/tools/ship')
import ship_source
source=Path('D:/amind/git/agent-3/art/ships/bulwark/Bulwark.blend')
out=Path('D:/amind/git/agent-3/results/bulwark/coherent-cleanup/refined')
out.mkdir(parents=True,exist_ok=True)
sha=hashlib.sha256(source.read_bytes()).hexdigest()
assert sha=='9a3ccb8dfcd23f6a628826bf51336ea526ac8fed67169c46c280fb5257df948e'
bpy.ops.wm.open_mainfile(filepath=str(source),load_ui=False,use_scripts=False)
original_scene=bpy.context.scene
original_parts={o.name:o for o in original_scene.objects if o.type=='MESH' and not o.hide_render}
gray=bpy.data.materials['Bulwark Blockout Gray']
def mesh_stats(mesh):
 bm=bmesh.new();bm.from_mesh(mesh)
 remaining=set(bm.verts);components=[]
 while remaining:
  todo=[remaining.pop()];comp=[]
  while todo:
   v=todo.pop();comp.append(v)
   for e in v.link_edges:
    other=e.other_vert(v)
    if other in remaining:remaining.remove(other);todo.append(other)
  components.append({'vertices':len(comp),'bounds':[[min(v.co[a] for v in comp),max(v.co[a] for v in comp)] for a in range(3)]})
 result={'vertices':len(bm.verts),'faces':len(bm.faces),'boundary_edges':sum(e.is_boundary for e in bm.edges),'nonmanifold_edges':sum(not e.is_manifold for e in bm.edges),'volume':bm.calc_volume(signed=True),'components':components}
 bm.free();return result
def repair(mesh,fill=True):
 bm=bmesh.new();bm.from_mesh(mesh)
 bmesh.ops.remove_doubles(bm,verts=list(bm.verts),dist=1e-6)
 bmesh.ops.dissolve_degenerate(bm,edges=list(bm.edges),dist=1e-7)
 if fill:
  edges=[e for e in bm.edges if e.is_boundary]
  if edges:bmesh.ops.holes_fill(bm,edges=edges,sides=0)
 bmesh.ops.recalc_face_normals(bm,faces=list(bm.faces))
 bmesh.ops.triangulate(bm,faces=list(bm.faces),quad_method='BEAUTY',ngon_method='BEAUTY')
 bm.to_mesh(mesh);bm.free();mesh.update()
 if mesh_stats(mesh)['volume']<0:
  bm=bmesh.new();bm.from_mesh(mesh);bmesh.ops.reverse_faces(bm,faces=list(bm.faces));bm.to_mesh(mesh);bm.free()
roles={}
new_scene=bpy.data.scenes.new('Bulwark - refined kitbash')
new_scene.unit_settings.scale_length=1
for name in ['Ship','role.hull','role.canopy','role.cores','ignore']:
 c=bpy.data.collections.new('Refinement '+name);new_scene.collection.children.link(c);roles[name]=c
root=bpy.data.objects.new('Refinement SymmetryOrigin',None);roles['Ship'].objects.link(root)
root.empty_display_type='PLAIN_AXES';root.empty_display_size=.3
structural=[
 ('01 Ram nose','Ram bulkhead'),
 ('03 Lower hull','Lower hull'),
 ('02 Upper hull.001','Main deck'),
 ('02 Upper hull','Aft bridge'),
 ('04 Tail booms','Engine shoulders'),
 ('05 Tail tips','Engine shrouds'),
 ('09 Side armor','Flank armor'),
 ('09 Side armor.001','Dorsal spine'),
 ('09 Side armor.002','Engine root collars'),
 ('05 Tail tips.002','Upper shoulder guards'),
 ('05 Tail tips.003','Lower shoulder skids')]
operands=[];audit={'original_source_sha256':sha,'source_checkpoint':'66952107','parts':{},'unions':[]}
for name,meaning in structural:
 original=original_parts[name]
 temp=original.copy();temp.data=original.data.copy();original_scene.collection.objects.link(temp)
 temp.hide_set(False);temp.hide_render=False
 if name in ['05 Tail tips.002','05 Tail tips.003']:
  bm=bmesh.new();bm.from_mesh(temp.data);bm.verts.ensure_lookup_table()
  bmesh.ops.delete(bm,geom=[v for v in bm.verts if v.index>=21],context='VERTS')
  bm.verts.ensure_lookup_table()
  bm.faces.new([bm.verts[i] for i in range(14,21)])
  bmesh.ops.recalc_face_normals(bm,faces=list(bm.faces))
  bm.to_mesh(temp.data);bm.free()
 bpy.context.view_layer.update()
 dg=bpy.context.evaluated_depsgraph_get()
 mesh=bpy.data.meshes.new_from_object(temp.evaluated_get(dg),preserve_all_data_layers=True,depsgraph=dg)
 mesh.transform(temp.matrix_world);repair(mesh)
 mat=gray.copy();mat.name='Region '+meaning;mesh.materials.clear();mesh.materials.append(mat)
 for p in mesh.polygons:p.material_index=0;p.use_smooth=False
 operand=bpy.data.objects.new('Working '+meaning,mesh);roles['Ship'].objects.link(operand)
 operands.append(operand);audit['parts'][meaning]={'source':name,'prepared':mesh_stats(mesh)}
 bpy.data.objects.remove(temp,do_unlink=True)
bpy.context.window.scene=new_scene
hull=operands[0]
for operand in operands[1:]:
 mod=hull.modifiers.new('Weld '+operand.name,'BOOLEAN')
 mod.operation='UNION';mod.solver='EXACT';mod.object=operand;mod.use_self=True;mod.use_hole_tolerant=True;mod.material_mode='TRANSFER'
 bpy.context.view_layer.update()
 dg=bpy.context.evaluated_depsgraph_get()
 merged=bpy.data.meshes.new_from_object(hull.evaluated_get(dg),preserve_all_data_layers=True,depsgraph=dg)
 assert len(merged.vertices)>0,operand.name
 old=hull.data;hull.modifiers.clear();hull.data=merged
 bpy.data.meshes.remove(old)
 audit['unions'].append({'part':operand.name,'result':mesh_stats(merged)})
 bpy.data.objects.remove(operand,do_unlink=True)
bm=bmesh.new();bm.from_mesh(hull.data)
bmesh.ops.remove_doubles(bm,verts=list(bm.verts),dist=1e-5)
bmesh.ops.dissolve_degenerate(bm,edges=list(bm.edges),dist=1e-6)
bmesh.ops.dissolve_limit(bm,angle_limit=math.radians(.08),use_dissolve_boundaries=False,verts=list(bm.verts),edges=list(bm.edges),delimit={'MATERIAL'})
bmesh.ops.recalc_face_normals(bm,faces=list(bm.faces))
bad=[f for f in bm.faces if len(f.verts)>4 and any(abs((v.co-f.verts[0].co).dot(f.normal))>1e-5 for v in f.verts)]
if bad:bmesh.ops.triangulate(bm,faces=bad)
bm.to_mesh(hull.data);bm.free();hull.data.update()
bm=bmesh.new();bm.from_mesh(hull.data)
remaining=set(bm.verts);islands=[]
while remaining:
 todo=[remaining.pop()];part=[]
 while todo:
  v=todo.pop();part.append(v)
  for e in v.link_edges:
   other=e.other_vert(v)
   if other in remaining:remaining.remove(other);todo.append(other)
 islands.append(part)
internal=[part for part in islands if all(abs(v.co.x)<.476 and 2.738<v.co.y<2.742 and 1.545<v.co.z<1.574 for v in part)]
assert len(internal)==1 and len(internal[0])==17
bmesh.ops.delete(bm,geom=internal[0],context='VERTS')
audit['removed_internal_sliver']='17-vertex enclosed island at the nose/deck overlap, y=2.74'
bm.to_mesh(hull.data);bm.free();hull.data.update()
audit['full_hull']=mesh_stats(hull.data)
assert audit['full_hull']['nonmanifold_edges']==0,audit['full_hull']
assert len(audit['full_hull']['components'])==1,audit['full_hull']
# Keep the authored half editable after joining the structural surfaces.
bm=bmesh.new();bm.from_mesh(hull.data)
bmesh.ops.bisect_plane(bm,geom=list(bm.verts)+list(bm.edges)+list(bm.faces),dist=1e-6,plane_co=(0,0,0),plane_no=(1,0,0),clear_inner=True,use_snap_center=True)
for v in bm.verts:
 if abs(v.co.x)<1e-5:v.co.x=0
center_faces=[f for f in bm.faces if all(abs(v.co.x)<1e-6 for v in f.verts)]
if center_faces:bmesh.ops.delete(bm,geom=center_faces,context='FACES')
bmesh.ops.remove_doubles(bm,verts=list(bm.verts),dist=1e-5)
bmesh.ops.recalc_face_normals(bm,faces=list(bm.faces))
bm.to_mesh(hull.data);bm.free()
hull.name='Hull - coherent armored shell';hull.data.name='Bulwark structural surface'
region_names=[m.name.removeprefix('Region ') for m in hull.data.materials]
for i,name in enumerate(region_names):
 ids={v for p in hull.data.polygons if p.material_index==i for v in p.vertices}
 if ids:
  group=hull.vertex_groups.new(name=name);group.add(sorted(ids),1.0,'REPLACE')
hull.data.materials.clear();hull.data.materials.append(gray)
for p in hull.data.polygons:p.material_index=0;p.use_smooth=False
if 'PaintUV' not in hull.data.uv_layers:hull.data.uv_layers.new(name='PaintUV')
hull.parent=root;roles['role.hull'].objects.link(hull)
mir=hull.modifiers.new('Linked left / right','MIRROR');mir.mirror_object=root;mir.use_clip=True;mir.merge_threshold=.0001
hull['regions']='Select original functional regions with the named vertex groups.'
for old_name,new_name,role in [('06 Cockpit dome','Cockpit - teardrop canopy','role.canopy'),('07 Side insets','Flank modules - round armor interfaces','role.canopy'),('08 Nested engines','Engines - protected twin nozzles','role.cores')]:
 original=original_parts[old_name]
 mesh=original.data.copy();mesh.transform(original.matrix_world)
 o=bpy.data.objects.new(new_name,mesh);roles['Ship'].objects.link(o);roles[role].objects.link(o);o.parent=root
 bm=bmesh.new();bm.from_mesh(mesh);bmesh.ops.remove_doubles(bm,verts=list(bm.verts),dist=1e-6);bmesh.ops.recalc_face_normals(bm,faces=list(bm.faces))
 bad=[f for f in bm.faces if len(f.verts)>4 and any(abs((v.co-f.verts[0].co).dot(f.normal))>1e-5 for v in f.verts)]
 if bad:bmesh.ops.triangulate(bm,faces=bad)
 bm.to_mesh(mesh);bm.free()
 m=o.modifiers.new('Linked left / right','MIRROR');m.mirror_object=root;m.use_clip=True
 if old_name=='06 Cockpit dome':
  sub=o.modifiers.new('Dome smoothing (editable cage)','SUBSURF');sub.levels=2;sub.render_levels=2
 if 'PaintUV' not in mesh.uv_layers:mesh.uv_layers.new(name='PaintUV')
 o['interpreted_from']=old_name
for o in list(bpy.data.objects):
 if o.name not in new_scene.objects:bpy.data.objects.remove(o,do_unlink=True)
for s in list(bpy.data.scenes):
 if s!=new_scene:bpy.data.scenes.remove(s)
for c in list(bpy.data.collections):
 if c not in roles.values():bpy.data.collections.remove(c)
for key,c in roles.items():c.name=key
root.name='SymmetryOrigin'
bpy.context.view_layer.update()
dg=bpy.context.evaluated_depsgraph_get()
evaluated=bpy.data.meshes.new_from_object(hull.evaluated_get(dg),depsgraph=dg)
audit['final_hull_evaluated']=mesh_stats(evaluated);bpy.data.meshes.remove(evaluated)
assert audit['final_hull_evaluated']['nonmanifold_edges']==0
assert len(audit['final_hull_evaluated']['components'])==1
audit['regions']=[g.name for g in hull.vertex_groups]
audit['objects']=[o.name for o in new_scene.objects if o.type=='MESH']
new_scene['cleanup_basis']='Owner-approved kitbash at 66952107; separate refinement, original file unchanged.'
new_scene['stage']='Shape cleanup for review'
for o in new_scene.objects:o.select_set(False)
hull.select_set(True);bpy.context.view_layer.objects.active=hull
for screen in bpy.data.screens:
 for area in screen.areas:
  if area.type=='VIEW_3D':
   area.spaces.active.overlay.show_overlays=False
bpy.ops.wm.save_as_mainfile(filepath=str(out/'Bulwark-refined.blend'))
assert hashlib.sha256(source.read_bytes()).hexdigest()==sha
audit['original_unchanged']=True
audit['refined_sha256']=hashlib.sha256((out/'Bulwark-refined.blend').read_bytes()).hexdigest()
(out/'cleanup-audit.json').write_text(json.dumps(audit,indent=2))
ship=json.loads((source.parent/'ship.json').read_text());ship['source']='Bulwark-refined.blend'
(out/'ship.json').write_text(json.dumps(ship,indent=2))
print(json.dumps({'result':'saved','source_unchanged':True,'stats':audit['final_hull_evaluated'],'file':str(out/'Bulwark-refined.blend')}))
