import bpy
import sys
import json
import numpy as np
from pathlib import Path
from mathutils import Vector

stage=Path(__file__).parent
variant=sys.argv[sys.argv.index('--')+1]
scene=bpy.context.scene
if variant=='saved':
    import hashlib
    sys.path.insert(0,'D:/amind/git/agent-3/art/tools/ship')
    import ship_source
    expected=json.loads((stage/'cleanup-result.json').read_text())
    actual=ship_source.fingerprint(scene)['components']
    assert actual==expected['after']
    checks={}
    for key,info in expected['images'].items():
        im=next(x for x in bpy.data.images if x.name.startswith('Bulwark '+key))
        packed_hash=hashlib.sha256(im.packed_file.data).hexdigest()
        assert packed_hash==info['file_sha256']
        values=np.empty(len(im.pixels),dtype=np.float32);im.pixels.foreach_get(values)
        pixel_hash=hashlib.sha256(values.tobytes()).hexdigest()
        assert pixel_hash==info['pixel_sha256'], key+' pixel mismatch after load'
        checks[key]={'packed_sha256':packed_hash,'pixel_sha256':pixel_hash}
    (stage/'saved-verification.json').write_text(json.dumps({'fingerprint':actual,'images':checks,'passed':True},indent=2),encoding='utf-8')
if variant=='clean':
    replacements={'BaseRegion':stage/'clean-BaseRegion.png','Light':stage.parent/'2026-10-13-owner-setup/layers/Light.png','Ink':stage.parent/'2026-10-13-owner-setup/layers/Ink.png'}
    for key,path in replacements.items():
        im=bpy.data.images.load(str(path),check_existing=False)
        im.colorspace_settings.name='Non-Color'
        for material in bpy.data.materials:
            if material.use_nodes and material.node_tree.nodes.get(key):
                material.node_tree.nodes[key].image=im
scene.render.engine='BLENDER_EEVEE'
scene.render.resolution_x=1100
scene.render.resolution_y=1100
scene.render.resolution_percentage=100
scene.render.image_settings.file_format='PNG'
scene.render.film_transparent=True
scene.view_settings.view_transform='Standard'
scene.view_settings.look='None'
scene.view_settings.exposure=0
scene.view_settings.gamma=1
world=bpy.data.worlds.new('Review world')
world.use_nodes=True
environment=world.node_tree.nodes.new('ShaderNodeTexEnvironment')
environment.image=bpy.data.images.load('C:/Program Files/Blender Foundation/Blender 5.1/5.1/datafiles/studiolights/world/forest.exr')
world.node_tree.links.new(environment.outputs['Color'],world.node_tree.nodes['Background'].inputs[0])
world.node_tree.nodes['Background'].inputs[1].default_value=1
scene.world=world
parts=json.loads((stage/'owner-mesh.json').read_text())['parts']
coords=np.concatenate([np.array(p['vertices']) for p in parts])
low=coords.min(axis=0);high=coords.max(axis=0);center=Vector((low+high)/2);span=max(high-low)
for ob in scene.objects:
    if ob.type=='LIGHT':ob.hide_render=True
for name,offset,energy in []:
    light=bpy.data.lights.new(name,'AREA');light.energy=energy;light.shape='DISK';light.size=span*1.5
    ob=bpy.data.objects.new(name,light);scene.collection.objects.link(ob);ob.location=center+Vector(offset)*span*.6
    ob.rotation_euler=(center-ob.location).to_track_quat('-Z','Y').to_euler()
camera=bpy.data.objects.new('Review camera',bpy.data.cameras.new('Review camera'))
scene.collection.objects.link(camera);scene.camera=camera;camera.data.type='ORTHO';camera.data.ortho_scale=span*1.13
views={'top':(0,0,1),'quarter':(1,-1.5,1.4),'front-quarter':(1,1.7,1.2),'side':(1,0,.25)}
for name,vector in views.items():
    camera.location=center+Vector(vector).normalized()*span*2
    camera.rotation_euler=(center-camera.location).to_track_quat('-Z','Y').to_euler()
    scene.render.filepath=str(stage/(variant+'-'+name+'.png'))
    bpy.ops.render.render(write_still=True)
