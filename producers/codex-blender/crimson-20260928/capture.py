import bpy,json,hashlib
from pathlib import Path
from mathutils import Vector,Matrix
base=Path('D:/amind/git/agent-7/art/ships/crimson')
bpy.ops.wm.open_mainfile(filepath=str(base/'Crimson.blend'))
s=bpy.context.scene;s.render.engine='BLENDER_WORKBENCH'
s.render.resolution_x=1536;s.render.resolution_y=1536;s.render.resolution_percentage=100
sh=s.display.shading;sh.light='STUDIO';sh.color_type='SINGLE';sh.single_color=(.65,.65,.65);sh.show_shadows=True;sh.show_cavity=True;sh.cavity_type='BOTH';sh.show_object_outline=True;sh.background_type='WORLD';s.world.color=(.16,.16,.16)
s.view_settings.view_transform='Standard';s.render.image_settings.file_format='PNG';s.render.film_transparent=False
meshes=[o for o in s.objects if o.type=='MESH' and not o.hide_get()]
points=[o.matrix_world@Vector(p) for o in meshes for p in o.bound_box]
center=Vector([(min(p[i] for p in points)+max(p[i] for p in points))/2 for i in range(3)])
d=bpy.data.cameras.new('Capture');cam=bpy.data.objects.new('Capture',d);s.collection.objects.link(cam);s.camera=cam;d.type='ORTHO'
views=[('top',(0,0,1),(0,1,0)),('bottom',(0,0,-1),(0,1,0)),('side-right',(1,0,0),(0,0,1)),('side-left',(-1,0,0),(0,0,1)),('front',(0,1,0),(0,0,1)),('rear',(0,-1,0),(0,0,1)),('three-quarter',(1,1,1.1),(0,0,1))]
for name,axis,up in views:
 z=Vector(axis).normalized();x=Vector(up).cross(z).normalized();y=z.cross(x)
 cam.rotation_euler=Matrix((x,y,z)).transposed().to_euler();cam.location=center+z*8
 span=max(max((p-center).dot(v) for p in points)-min((p-center).dot(v) for p in points) for v in [x,y]);d.ortho_scale=span*1.12
 s.render.filepath=str(base/'orthographic'/(name+'.png'));bpy.ops.render.render(write_still=True)
report={'source':'Crimson.blend','source_sha256':hashlib.sha256((base/'Crimson.blend').read_bytes()).hexdigest(),'parts':len(meshes),'views':[v[0] for v in views]}
(base/'orthographic'/'capture-manifest.json').write_text(json.dumps(report,indent=2))
print(json.dumps(report))
