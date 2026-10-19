import bpy
import sys
from pathlib import Path
from mathutils import Vector

stage=Path(__file__).parent
args=sys.argv[sys.argv.index('--')+1:]
variant=args[0]
bpy.ops.wm.open_mainfile(filepath='D:/amind/git/agent-3/art/ships/bulwark/Bulwark.blend')
if len(args)>1:
    mask=bpy.data.images.load(args[1],check_existing=False)
    mask.colorspace_settings.name='Non-Color'
    original=bpy.data.images['Bulwark BaseRegion']
    for mat in bpy.data.materials:
        if mat.use_nodes:
            for node in mat.node_tree.nodes:
                if node.type=='TEX_IMAGE' and node.image==original:
                    node.image=mask


scene=bpy.context.scene
scene.render.engine='BLENDER_EEVEE'
scene.render.resolution_x=1100;scene.render.resolution_y=900;scene.render.resolution_percentage=100
scene.render.image_settings.file_format='PNG';scene.render.film_transparent=True
scene.view_settings.view_transform='Standard';scene.view_settings.look='None'
world=bpy.data.worlds.new('Paint review');world.use_nodes=True
env=world.node_tree.nodes.new('ShaderNodeTexEnvironment')
env.image=bpy.data.images.load('C:/Program Files/Blender Foundation/Blender 5.1/5.1/datafiles/studiolights/world/forest.exr')
world.node_tree.links.new(env.outputs['Color'],world.node_tree.nodes['Background'].inputs[0]);scene.world=world
for o in scene.objects:
    if o.type=='MESH':o.hide_render=not o.visible_get()
camera=bpy.data.objects.new('Paint review',bpy.data.cameras.new('Paint review'))
scene.collection.objects.link(camera);scene.camera=camera;camera.data.type='ORTHO'
center=Vector((0,-.2,1.7))
for name,direction,scale in [('opposite',(-1,1,.5),14.5),('top',(0,0,1),14.5),('front',(1,1,.7),14.5),('side',(1,.1,.15),14.5),('rear',(1,-1,.5),14.5),('underside',(1,.5,-.7),14.5)]:
    camera.location=center+Vector(direction).normalized()*25
    camera.rotation_euler=(center-camera.location).to_track_quat('-Z','Y').to_euler()
    camera.data.ortho_scale=scale
    scene.render.filepath=str(stage/(variant+'-'+name+'.png'))
    bpy.ops.render.render(write_still=True)


scene.render.resolution_percentage=25
scene.render.filepath=str(stage/"game-scale.png")
bpy.ops.render.render(write_still=True)

scene.render.resolution_percentage=100
camera.location=center+Vector((0,0,25))
camera.rotation_euler=(center-camera.location).to_track_quat('-Z','Y').to_euler()
scene.render.filepath=str(stage/'final-top.png')
bpy.ops.render.render(write_still=True)

scene.render.resolution_percentage=25
scene.render.filepath=str(stage/"game-scale-top.png")
bpy.ops.render.render(write_still=True)
