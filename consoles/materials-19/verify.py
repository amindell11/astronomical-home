"""Read final material scene and geometry-18; verify existing transforms, vertices, polygon indices and curves. Writes verification.json; nonzero on mismatch."""
import bpy,json
from pathlib import Path
root=Path(__file__).resolve().parent

def capture():
 d={}
 for o in bpy.context.scene.objects:
  item={'matrix':[list(r)for r in o.matrix_world],'hide':o.hide_render}
  if o.type=='MESH':item['verts']=[list(v.co)for v in o.data.vertices];item['faces']=[list(p.vertices)for p in o.data.polygons]
  if o.type=='CURVE':item['splines']=[{'type':sp.type,'bezier':[[list(p.co),list(p.handle_left),list(p.handle_right)]for p in sp.bezier_points],'poly':[list(p.co)for p in sp.points]}for sp in o.data.splines]
  d[o.name]=item
 return d
current=capture();camera=bpy.context.scene.camera.name
external=[i.name for i in bpy.data.images if i.source=='FILE' and not i.packed_file]
bpy.ops.wm.open_mainfile(filepath=str(root.parent/'geometry-18/console.blend'));source=capture();errors=[]
for name,old in source.items():
 if current.get(name)!=old:errors.append(name)
if camera!=bpy.context.scene.camera.name:errors.append('camera')
if external:errors.append('Unpacked images: '+str(external))
report={'result':'pass'if not errors else 'fail','existingObjectsVerified':len(source),'geometryTransformsAndVisibility':'unchanged','newObjects':[n for n in current if n not in source],'unpackedImages':external,'geometryApproval':'provisional','materialsApproval':'pending','errors':errors}
(root/'verification.json').write_text(json.dumps(report,indent=2));print(json.dumps(report));assert not errors
