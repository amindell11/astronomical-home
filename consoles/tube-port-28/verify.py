"""Compare saved scene with window-edges-27; writes verification.json and fails on geometry or dependency changes."""
import bpy,bmesh,json,hashlib
from pathlib import Path
root=Path(__file__).resolve().parent

def capture():
 d={}
 for o in bpy.context.scene.objects:
  item={'matrix':[list(r)for r in o.matrix_world],'hide':o.hide_render}
  item['modifiers']=[{p.identifier:getattr(mod,p.identifier) for p in mod.bl_rna.properties if not p.is_readonly and (p.type in {'STRING','ENUM'} or (p.type in {'BOOLEAN','INT','FLOAT'} and not p.is_array))}for mod in o.modifiers]
  if o.type=='MESH':item['verts']=[list(v.co)for v in o.data.vertices];item['faces']=[list(p.vertices)for p in o.data.polygons];item['smooth']=[p.use_smooth for p in o.data.polygons]
  if o.type=='CURVE':item['splines']=[{'type':sp.type,'bezier':[[list(p.co),list(p.handle_left),list(p.handle_right)]for p in sp.bezier_points],'poly':[list(p.co)for p in sp.points]}for sp in o.data.splines]
  if o.type=='LIGHT':item['light']={'energy':o.data.energy,'color':list(o.data.color),'size':getattr(o.data,'size',None)}
  if o.type=='CAMERA':item['camera']={p:getattr(o.data,p) for p in ['type','lens','ortho_scale','shift_x','shift_y','clip_start','clip_end']}
  d[o.name]=item
 return d
current=capture();camera=bpy.context.scene.camera.name
manifest=json.loads((root/'console-manifest.json').read_text())
new_mesh_errors=[]
deps=bpy.context.evaluated_depsgraph_get()
for name in manifest['addedObjects']:
 o=bpy.data.objects[name]
 if o.type!='MESH':continue
 evaluated=o.evaluated_get(deps);mesh=evaluated.to_mesh()
 bm=bmesh.new();bm.from_mesh(mesh)
 if any(not e.is_manifold for e in bm.edges):new_mesh_errors.append(name)
 bm.free();evaluated.to_mesh_clear()
external=[i.name for i in bpy.data.images if i.source=='FILE' and not i.packed_file]
sourcepath=root.parent/'window-edges-27/console.blend'
bpy.ops.wm.open_mainfile(filepath=str(sourcepath));source=capture();errors=[]
for name,old in source.items():
 if current.get(name)!=old:errors.append(name)
if set(current)-set(source)!=set(manifest['addedObjects']) or set(source)-set(current):errors.append('Unexpected object inventory change')
if new_mesh_errors:errors.extend(new_mesh_errors)
if camera!=bpy.context.scene.camera.name:errors.append('Camera selection')
if external:errors.append('Unpacked images: '+str(external))
report={'result':'pass'if not errors else 'fail','sourceSha256':hashlib.sha256(sourcepath.read_bytes()).hexdigest(),'existingObjectsVerified':len(source),'originalGeometryTransformsAndVisibility':'unchanged','addedObjects':manifest['addedObjects'],'newMeshesManifold':not new_mesh_errors,'camera':'unchanged','unpackedImages':external,'userReview':'pending','errors':errors}
(root/'verification.json').write_text(json.dumps(report,indent=2));print(json.dumps(report));assert not errors
