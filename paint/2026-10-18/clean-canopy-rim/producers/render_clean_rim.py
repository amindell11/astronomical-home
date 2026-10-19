import bpy,sys,hashlib
from pathlib import Path
from mathutils import Vector,Matrix
SOURCE=Path('D:/amind/git/agent-4/art/ships/vanguard/Vanguard.blend')
before=hashlib.sha256(SOURCE.read_bytes()).hexdigest()
bpy.ops.wm.open_mainfile(filepath=str(SOURCE))
scene=bpy.context.scene
visible=[o for o in scene.objects if o.type=='MESH' and not o.hide_render]
painted=[o for o in visible if o.name not in ['Vanguard structural ink','MVP Canopy','MVP Power Nacelle Core']]
OUT=Path('D:/amind/git/agent-4/results/vanguard-redo/clean-canopy-rim/preview');OUT.mkdir(exist_ok=True)
base=bpy.data.images.load('D:/amind/git/agent-4/results/vanguard-redo/clean-canopy-rim/paint/preview/Composite.png')
def review_material(name,image):
    mat=bpy.data.materials.new(name);mat.use_nodes=True
    tex=mat.node_tree.nodes.new('ShaderNodeTexImage');tex.image=image
    mat.node_tree.links.new(tex.outputs['Color'],mat.node_tree.nodes.get('Principled BSDF').inputs['Base Color'])
    mat.node_tree.nodes.active=tex
    return mat

points=[obj.matrix_world@Vector(p) for obj in visible for p in obj.bound_box]
center=Vector([(min(p[i] for p in points)+max(p[i] for p in points))/2 for i in range(3)])
data=bpy.data.cameras.new('preserved paint review');camera=bpy.data.objects.new('preserved paint review',data)
scene.collection.objects.link(camera);scene.camera=camera;data.type='ORTHO';data.clip_start=.01;data.clip_end=100

def render(name,size,axis,up,focus=None,span=None):
    scene.render.engine='BLENDER_WORKBENCH';scene.render.resolution_x=scene.render.resolution_y=size
    scene.render.resolution_percentage=100;scene.render.image_settings.file_format='PNG'
    scene.render.image_settings.color_mode='RGBA';scene.render.film_transparent=False
    sh=scene.display.shading;sh.light='FLAT';sh.color_type='TEXTURE';sh.show_shadows=False;sh.show_cavity=False
    sh.show_object_outline=True;sh.object_outline_color=(.008,.01,.016);sh.background_type='WORLD'
    scene.world.color=(.045,.055,.08);scene.view_settings.view_transform='Standard';scene.view_settings.look='None'
    scene.display.render_aa='32'
    z=Vector(axis).normalized();x=Vector(up).cross(z).normalized();y=z.cross(x)
    focus=Vector(focus) if focus is not None else center
    camera.rotation_euler=Matrix((x,y,z)).transposed().to_euler();camera.location=focus+z*8
    if span is None:span=max(max((p-focus).dot(v) for p in points)-min((p-focus).dot(v) for p in points) for v in [x,y])*1.15
    data.ortho_scale=span
    path=OUT/(name+'.png');scene.render.filepath=str(path);bpy.ops.render.render(write_still=True)
    return path


for obj in painted:
 obj.data.materials.clear();obj.data.materials.append(review_material(obj.name,base))
 for face in obj.data.polygons:face.material_index=0
 obj.data.uv_layers.active=obj.data.uv_layers['PaintUV'];obj.data.uv_layers['PaintUV'].active_render=True
render('top',1024,(0,0,1),(0,1,0))
render('hero',1024,(1,1,1.5),(0,0,1))
assert hashlib.sha256(SOURCE.read_bytes()).hexdigest()==before


