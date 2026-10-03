import bpy,math,json
from pathlib import Path
from mathutils import Vector,Matrix
BASE=Path('D:/amind/git/agent-7/art/ships/crimson')
WORK=Path('C:/Users/amind/.codex/artifact-archives/crimson-turntable-20260928')
DONOR=Path('D:/amind/git/agent-7/src/Asteroids3D/Assets/Visuals/Ships/Ship2/CrimsonVortex0613023558TextureObj')
bpy.ops.wm.open_mainfile(filepath=str(BASE/'Crimson.blend'))
s=bpy.context.scene;dg=bpy.context.evaluated_depsgraph_get()
# Freeze evaluation only in this presentation scene; the editing source stays untouched.
new=[]
for o in list(s.objects):
 if o.type=='MESH':
  me=bpy.data.meshes.new_from_object(o.evaluated_get(dg),preserve_all_data_layers=True,depsgraph=dg);me.transform(o.matrix_world)
  copy=bpy.data.objects.new('New - '+o.name,me);s.collection.objects.link(copy);new.append(copy)
for o in list(s.objects):
 if o not in new:bpy.data.objects.remove(o,do_unlink=True)
bpy.ops.wm.obj_import(filepath=str(DONOR/'crimsonVortex0613023558Texture.obj'))
old=[o for o in bpy.context.selected_objects if o.type=='MESH']
rot=Matrix.Rotation(-math.pi/2,4,'Z')
for o in old:
 o.data.transform(rot@o.matrix_world);o.matrix_world=Matrix.Identity(4)
img=bpy.data.images.load(str(DONOR/'crimsonVortex0613023558Texture.png'));img.pack()
mat=bpy.data.materials.new('Original AI texture');mat.use_nodes=True;tex=mat.node_tree.nodes.new('ShaderNodeTexImage');tex.image=img;mat.node_tree.nodes.active=tex;mat.node_tree.links.new(tex.outputs['Color'],mat.node_tree.nodes.get('Principled BSDF').inputs['Base Color'])
for o in old:
 o.data.materials.clear();o.data.materials.append(mat)
 for p in o.data.polygons:p.material_index=0

def bounds(group):
 vs=[v.co for o in group for v in o.data.vertices]
 lo=Vector([min(v[i] for v in vs) for i in range(3)]);hi=Vector([max(v[i] for v in vs) for i in range(3)])
 return lo,hi
nlo,nhi=bounds(new);olo,ohi=bounds(old);length=nhi.y-nlo.y;oldscale=length/(ohi.y-olo.y)
view=Vector((.0,-1,1.0)).normalized();right=Vector((1,0,0));up=view.cross(right)
for group,label,side,scale,lo,hi in [(old,'Original AI',-1,oldscale,olo,ohi),(new,'New hand-painted',1,1,nlo,nhi)]:
 center=(lo+hi)/2
 root=bpy.data.objects.new(label+' turntable',None);s.collection.objects.link(root);root.location=right*side*1.48
 for o in group:
  for v in o.data.vertices:v.co=(v.co-center)*scale
  o.parent=root
 root.rotation_euler.z=math.radians(-25);root.keyframe_insert(data_path='rotation_euler',index=2,frame=1)
 root.rotation_euler.z=math.radians(335);root.keyframe_insert(data_path='rotation_euler',index=2,frame=181)
 for layer in root.animation_data.action.layers:
  for strip in layer.strips:
   for bag in strip.channelbags:
    for fc in bag.fcurves:
     for key in fc.keyframe_points:key.interpolation='LINEAR'
camdata=bpy.data.cameras.new('Comparison camera');cam=bpy.data.objects.new('Comparison camera',camdata);s.collection.objects.link(cam);s.camera=cam
cam.location=view*10;cam.rotation_euler=(-cam.location).to_track_quat('-Z','Y').to_euler();camdata.type='ORTHO';camdata.ortho_scale=6.1
s.render.engine='BLENDER_WORKBENCH';s.render.resolution_x=1280;s.render.resolution_y=720;s.render.resolution_percentage=100;s.render.fps=18
sh=s.display.shading;sh.light='FLAT';sh.color_type='TEXTURE';sh.show_shadows=False;sh.show_cavity=True;sh.cavity_type='BOTH';sh.show_object_outline=True;sh.object_outline_color=(.008,.01,.016);sh.background_type='WORLD';s.world.color=(.025,.035,.055)
s.view_settings.view_transform='Standard';s.render.image_settings.file_format='PNG';s.render.film_transparent=False
s.frame_start=1;s.frame_end=180
s.render.filepath=str(WORK/'frames'/'frame-')
s.frame_set(1);s.render.filepath=str(WORK/'preview.png');bpy.ops.render.render(write_still=True)
(WORK/'comparison.json').write_text(json.dumps({'original':str(DONOR),'new':str(BASE/'Crimson.blend'),'normalization':'same nose-to-tail length','old_uniform_scale':oldscale,'frames':180,'fps':18},indent=2))
bpy.ops.wm.save_as_mainfile(filepath=str(WORK/'Comparison.blend'))
print('PREVIEW READY',flush=True)
