"""Load a scene and write object geometry/state to --out JSON, without changing the file."""
import bpy,json,sys,hashlib
from pathlib import Path
from mathutils import Vector
args=sys.argv[sys.argv.index('--')+1:]
out=Path(args[args.index('--out')+1]);s=bpy.context.scene;bpy.context.view_layer.update()
objects={}
for o in s.objects:
    verts=[list(v.co)for v in o.data.vertices]if o.type=='MESH'else []
    faces=[list(p.vertices)for p in o.data.polygons]if o.type=='MESH'else []
    objects[o.name]={'type':o.type,'parent':o.parent.name if o.parent else None,'matrix':[list(r)for r in o.matrix_world],'local':[list(r)for r in o.matrix_local],'hideRender':o.hide_render,'hideViewport':o.hide_get(),'vertices':verts,'faces':faces,'bounds':[list(o.matrix_world@Vector(v))for v in o.bound_box],'modifiers':[(m.name,m.type)for m in o.modifiers]}
    if o.type=='CURVE':objects[o.name]['curves']=[[[list(p.co),list(p.handle_left),list(p.handle_right)]for p in sp.bezier_points]for sp in o.data.splines]
out.write_text(json.dumps({'scene':s.name,'camera':s.camera.name if s.camera else None,'objects':objects,'file':bpy.data.filepath,'sha256':hashlib.sha256(Path(bpy.data.filepath).read_bytes()).hexdigest()}))
print(json.dumps({'scene':s.name,'camera':s.camera.name if s.camera else None,'objects':len(objects),'report':str(out)}))
