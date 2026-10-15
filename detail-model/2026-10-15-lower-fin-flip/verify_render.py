import bpy
import json
import runpy
from pathlib import Path
from mathutils import Vector

stage=Path(__file__).parent
def corners():
    mesh=bpy.data.objects['Lower flaps - Fins'].data
    return {u.name:{str((p.index,mesh.loops[i].vertex_index)):list(u.data[i].uv)
                   for p in mesh.polygons for i in p.loop_indices} for u in mesh.uv_layers}
bpy.ops.wm.open_mainfile(filepath=str(stage/'before.blend'))
old_uv=corners()
bpy.ops.wm.open_mainfile(filepath='D:/amind/git/agent-3/art/ships/bulwark/Bulwark.blend')
assert old_uv==corners(), 'UV corner mismatch'
capture=runpy.run_path('D:/amind/git/agent-3/results/bulwark/detail-model/2026-10-14-organization/organize.py')['capture']
images=capture({})['images']
expected=json.loads((stage/'verification.json').read_text())['after_images']
assert all(images[n]['pixels']==v['pixels'] for n,v in expected.items()), 'Packed paint mismatch'
obj=bpy.data.objects['Lower flaps - Fins']
normal_matrix=obj.matrix_world.to_3x3().inverted().transposed()
normals=[(normal_matrix@obj.data.polygons[i].normal).normalized().z for i in [0,8,9,10]]
assert max(normals)<-.5
(stage/'reload.json').write_text(json.dumps({'passed':True,'uv_corner_association_preserved':True,'paint_pixels_preserved':True,'painted_normal_z':normals},indent=2))
scene=bpy.context.scene
scene.render.engine='BLENDER_EEVEE'
scene.render.resolution_x=1100
scene.render.resolution_y=850
scene.render.resolution_percentage=100
scene.render.image_settings.file_format='PNG'
scene.render.film_transparent=True
scene.view_settings.view_transform='Standard'
scene.view_settings.look='None'
world=bpy.data.worlds.new('Fin review world');world.use_nodes=True
env=world.node_tree.nodes.new('ShaderNodeTexEnvironment')
env.image=bpy.data.images.load('C:/Program Files/Blender Foundation/Blender 5.1/5.1/datafiles/studiolights/world/forest.exr')
world.node_tree.links.new(env.outputs['Color'],world.node_tree.nodes['Background'].inputs[0])
scene.world=world
for obj in scene.objects:
    if obj.type=='MESH':obj.hide_render=not obj.visible_get()
camera=bpy.data.objects.new('Fin review camera',bpy.data.cameras.new('Fin review camera'))
scene.collection.objects.link(camera);scene.camera=camera;camera.data.type='ORTHO'
for name,direction,center,scale in [
    ('underside',(1,.5,-.7),(0,-.3,1.7),11.5),
    ('side',(1,.1,.05),(0,-.3,1.7),11.5),
]:
    center=Vector(center)
    camera.location=center+Vector(direction).normalized()*25
    camera.rotation_euler=(center-camera.location).to_track_quat('-Z','Y').to_euler()
    camera.data.ortho_scale=scale
    scene.render.filepath=str(stage/(name+'.png'))
    bpy.ops.render.render(write_still=True)
