import bpy
import json
import math
from pathlib import Path
from mathutils import Vector

out=Path(__file__).resolve().parent
scene=bpy.data.scenes['Vanguard - Texture MVP']
bpy.context.window.scene=scene
paint=bpy.data.images['Vanguard MVP - Base Color']
paint.reload()
assert tuple(paint.size)==(4096,4096)
assert Path(bpy.path.abspath(paint.filepath)).is_file()
for obj in scene.objects:
    if obj.type=='MESH' and 'MVP_Atlas' in obj.data.uv_layers:
        assert all(-.00001<=c<=1.00001 for item in obj.data.uv_layers['MVP_Atlas'].data for c in item.uv)

# Preview reflections are shading, not highlights painted into the base-color map.
glass=bpy.data.materials['Vanguard | Canopy navy']
nodes=glass.node_tree.nodes;links=glass.node_tree.links
ink=next(n for n in nodes if n.bl_idname=='ShaderNodeMixRGB' and n.blend_type=='MIX')
original=ink.inputs[1].links[0].from_socket
geometry=nodes.new('ShaderNodeNewGeometry');geometry.location=(-690,480)
dot=nodes.new('ShaderNodeVectorMath');dot.operation='DOT_PRODUCT';dot.location=(-470,480)
dot.inputs[1].default_value=Vector((-.2,-.35,1)).normalized()
links.new(geometry.outputs['Normal'],dot.inputs[0])
highlight=nodes.new('ShaderNodeValToRGB');highlight.location=(-260,480)
highlight.color_ramp.interpolation='EASE'
highlight.color_ramp.elements[0].position=.90
highlight.color_ramp.elements[0].color=(0,0,0,1)
highlight.color_ramp.elements[1].position=.993
highlight.color_ramp.elements[1].color=(.08,.23,.37,1)
links.new(dot.outputs['Value'],highlight.inputs[0])
add=nodes.new('ShaderNodeMixRGB');add.blend_type='ADD';add.inputs[0].default_value=1;add.location=(180,300)
links.new(original,add.inputs[1]);links.new(highlight.outputs[0],add.inputs[2]);links.new(add.outputs[0],ink.inputs[1])

scene.objects['Large soft key'].data.energy=220
scene.objects['Cool fill'].data.energy=90

group=scene.compositing_node_group
nodes=group.nodes;links=group.links
render=next(n for n in nodes if n.bl_idname=='CompositorNodeRLayers')
glare=next(n for n in nodes if n.bl_idname=='CompositorNodeGlare')
output=next(n for n in nodes if n.bl_idname=='NodeGroupOutput')
separate=nodes.new('CompositorNodeSeparateColor');separate.mode='RGB'
links.new(render.outputs['Image'],separate.inputs[0])
mask=nodes.new('ShaderNodeMath');mask.operation='GREATER_THAN';mask.inputs[1].default_value=1.05
links.new(separate.outputs[2],mask.inputs[0])
isolate=nodes.new('ShaderNodeMixRGB');isolate.blend_type='MULTIPLY';isolate.inputs[0].default_value=1
links.new(render.outputs['Image'],isolate.inputs[1]);links.new(mask.outputs[0],isolate.inputs[2])
links.new(isolate.outputs[0],glare.inputs['Image'])
glare.inputs['Threshold'].default_value=0
glare.inputs['Strength'].default_value=.5
glare.inputs['Size'].default_value=.18
combine=nodes.new('ShaderNodeMixRGB');combine.blend_type='ADD';combine.inputs[0].default_value=1
links.new(render.outputs['Image'],combine.inputs[1]);links.new(glare.outputs['Glare'],combine.inputs[2])
links.new(combine.outputs[0],output.inputs['Image'])
render.location=(-650,100);separate.location=(-650,-140);mask.location=(-430,-140)
isolate.location=(-220,40);glare.location=(10,40);combine.location=(240,150);output.location=(460,150)

camera=scene.camera
points=[obj.matrix_world@v.co for obj in scene.objects if obj.type=='MESH' for v in obj.data.vertices]
minimum=Vector(tuple(min(p[i] for p in points) for i in range(3)))
maximum=Vector(tuple(max(p[i] for p in points) for i in range(3)))
center=(minimum+maximum)/2
def pose(direction,roll=0):
    direction=Vector(direction).normalized()
    camera.location=center+direction*8
    camera.rotation_euler=(-direction).to_track_quat('-Z','Y').to_euler()
    if roll: camera.rotation_euler.rotate_axis('Z',roll)
    bpy.context.view_layer.update()
    inverse=camera.matrix_world.inverted()
    projected=[inverse@p for p in points]
    width=max(p.x for p in projected)-min(p.x for p in projected)
    height=max(p.y for p in projected)-min(p.y for p in projected)
    camera.data.ortho_scale=max(width,height*scene.render.resolution_x/scene.render.resolution_y)*1.12
    shift=Vector(((max(p.x for p in projected)+min(p.x for p in projected))/2,
                  (max(p.y for p in projected)+min(p.y for p in projected))/2,0))
    camera.location+=camera.matrix_world.to_3x3()@shift

renders=out/'renders-final';renders.mkdir(exist_ok=True)
views=[('front-quarter',(1.15,1.6,1.25)),('top',(0,0,1)),
       ('rear-quarter',(-1.15,-1.6,1.05)),('underside',(0,0,-1)),('front',(0,1,.12))]
scene.render.use_compositing=True
for label,direction in views:
    pose(direction)
    scene.render.filepath=str(renders/(label+'.png'))
    bpy.ops.render.render(write_still=True)
pose((1.15,1.6,1.25))
scene.render.use_compositing=False
scene.render.filepath=str(renders/'front-quarter-no-bloom.png')
bpy.ops.render.render(write_still=True)
scene.render.use_compositing=True
scene.render.filepath='//renders-final/front-quarter.png'
for screen in bpy.data.screens:
    for area in screen.areas:
        if area.type=='VIEW_3D':
            area.spaces.active.shading.use_scene_world=True
            area.spaces.active.shading.use_scene_lights=True
            area.spaces.active.region_3d.view_perspective='CAMERA'

scene['Preview controls']='Compositor glare strength controls bloom; core material controls emission; Freestyle controls rendered outlines.'
scene['Texture edit']='MVP_Atlas is the new paint UV. External texture: textures/vanguard-basecolor-v2.png. Unpacked for texture painting.'
scene['Geometry note']='Review meshes are evaluated copies; original editable geometry remains in the retained source scene and source-live.blend.'
bpy.ops.wm.save_as_mainfile(filepath=str(out/'vanguard-textured-mvp.blend'),relative_remap=True)
report={'file':str(out/'vanguard-textured-mvp.blend'),'texture':str(out/'textures/vanguard-basecolor-v2.png'),
        'active_scene':scene.name,'texture_size':list(paint.size),'verified_uv_range':True,
        'source_scene_retained':bpy.data.scenes.get('Scene') is not None,
        'renders':[str(renders/(label+'.png')) for label,_ in views],
        'preview_only':['Eevee cel-shading nodes','Freestyle ink contours','Compositor bloom'],
        'unity_modified':False}
(out/'delivery-manifest.json').write_text(json.dumps(report,indent=2))
print(json.dumps(report),flush=True)
