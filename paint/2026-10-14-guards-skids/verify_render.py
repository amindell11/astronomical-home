import bpy
import json
import runpy
from pathlib import Path
from mathutils import Vector

stage = Path(__file__).parent
bpy.ops.wm.open_mainfile(filepath='D:/amind/git/agent-3/art/ships/bulwark/Bulwark.blend')
capture = runpy.run_path('D:/amind/git/agent-3/results/bulwark/detail-model/2026-10-14-organization/organize.py')['capture']
expected = json.loads((stage/'verification.json').read_text())['after']
actual = json.loads(json.dumps(capture({o.name:o for o in bpy.context.scene.objects})))
assert expected == actual, 'Reload mismatch'
(stage/'reload.json').write_text(json.dumps({'passed':True,'objects':len(actual['objects']),'images':len(actual['images'])}))
scene = bpy.context.scene
scene.render.engine = 'BLENDER_EEVEE'
scene.render.resolution_x = 1100
scene.render.resolution_y = 850
scene.render.resolution_percentage = 100
scene.render.image_settings.file_format = 'PNG'
scene.render.film_transparent = True
scene.view_settings.view_transform = 'Standard'
scene.view_settings.look = 'None'
world = bpy.data.worlds.new('Paint review world')
world.use_nodes = True
env = world.node_tree.nodes.new('ShaderNodeTexEnvironment')
env.image = bpy.data.images.load('C:/Program Files/Blender Foundation/Blender 5.1/5.1/datafiles/studiolights/world/forest.exr')
world.node_tree.links.new(env.outputs['Color'],world.node_tree.nodes['Background'].inputs[0])
scene.world = world
for obj in scene.objects:
    if obj.type == 'MESH':obj.hide_render = not obj.visible_get()
camera = bpy.data.objects.new('Paint review camera',bpy.data.cameras.new('Paint review camera'))
scene.collection.objects.link(camera)
scene.camera = camera
camera.data.type = 'ORTHO'
for name, direction, center, scale in [
    ('ship',(1,.7,.5),(0,.1,1.7),11.5),
    ('guards-skids',(1,.3,.15),(2.2,-.4,1.6),4.8),
]:
    center=Vector(center)
    camera.location=center+Vector(direction).normalized()*25
    camera.rotation_euler=(center-camera.location).to_track_quat('-Z','Y').to_euler()
    camera.data.ortho_scale=scale
    scene.render.filepath=str(stage/(name+'.png'))
    bpy.ops.render.render(write_still=True)
