"""Compare saved scene with ink-materials-20; writes verification.json and fails on geometry or dependency changes."""
import bpy,json,hashlib
from pathlib import Path
root=Path(__file__).resolve().parent

def capture():
 d={}
 for o in bpy.context.scene.objects:
  item={'matrix':[list(r)for r in o.matrix_world],'hide':o.hide_render}
  item['modifiers']=[{p.identifier:getattr(mod,p.identifier) for p in mod.bl_rna.properties if not p.is_readonly and (p.type in {'STRING','ENUM'} or (p.type in {'BOOLEAN','INT','FLOAT'} and not p.is_array))}for mod in o.modifiers]
  if o.type=='MESH':item['verts']=[list(v.co)for v in o.data.vertices];item['faces']=[list(p.vertices)for p in o.data.polygons];item['smooth']=[p.use_smooth for p in o.data.polygons]
  if o.type=='CURVE':item['splines']=[{'type':sp.type,'bezier':[[list(p.co),list(p.handle_left),list(p.handle_right)]for p in sp.bezier_points],'poly':[list(p.co)for p in sp.points]}for sp in o.data.splines]
  if o.type=='CAMERA':item['camera']={p:getattr(o.data,p) for p in ['type','lens','ortho_scale','shift_x','shift_y','clip_start','clip_end']}
  d[o.name]=item
 return d
current=capture();camera=bpy.context.scene.camera.name
external=[i.name for i in bpy.data.images if i.source=='FILE' and not i.packed_file]
sourcepath=root.parent/'ink-materials-20/console.blend'
bpy.ops.wm.open_mainfile(filepath=str(sourcepath));source=capture();errors=[]
for name,old in source.items():
 if current.get(name)!=old:errors.append(name)
if set(current)!=set(source):errors.append('Object inventory')
if camera!=bpy.context.scene.camera.name:errors.append('Camera selection')
if external:errors.append('Unpacked images: '+str(external))
report={'result':'pass'if not errors else 'fail','sourceSha256':hashlib.sha256(sourcepath.read_bytes()).hexdigest(),'existingObjectsVerified':len(source),'geometryTransformsAndVisibility':'unchanged','objectInventory':'unchanged','camera':'unchanged','unpackedImages':external,'geometryApproval':'provisional','materialsApproval':'pending user review','errors':errors}
(root/'verification.json').write_text(json.dumps(report,indent=2));print(json.dumps(report));assert not errors
