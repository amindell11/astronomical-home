"""Render the exported etch mask at 35% additive strength without saving the scene.

Run headless Blender after export_glass.py and copy its PNG to previews.
Writes deck-window-etching.png and deck-window-etching-21x9.png. Errors exit nonzero.
"""
from pathlib import Path
import sys

import bpy

ROOT = Path(__file__).resolve().parent
sys.path.insert(0, str(ROOT))
from deck_projection import game_projection

bpy.ops.wm.open_mainfile(filepath=str(ROOT / 'deck.blend'))
scene = bpy.context.scene
glass = bpy.data.objects['25 | Viewport glass']
material = bpy.data.materials.new('Preview | baked additive window etching')
material.use_nodes = True
nodes, links = material.node_tree.nodes, material.node_tree.links
nodes.clear()
uv = nodes.new('ShaderNodeUVMap')
uv.uv_map = glass.data.uv_layers[0].name
texture = nodes.new('ShaderNodeTexImage')
texture.image = bpy.data.images.load(str(ROOT / 'previews/deck-window-etching-mask.png'))
texture.image.colorspace_settings.name = 'Non-Color'
texture.extension = 'CLIP'
links.new(uv.outputs['UV'], texture.inputs['Vector'])
strength = nodes.new('ShaderNodeMath')
strength.operation = 'MULTIPLY'
strength.inputs[1].default_value = .35
links.new(texture.outputs['Color'], strength.inputs[0])
emission = nodes.new('ShaderNodeEmission')
emission.inputs['Color'].default_value = (.72, .86, 1, 1)
links.new(strength.outputs[0], emission.inputs['Strength'])
transparent = nodes.new('ShaderNodeBsdfTransparent')
add = nodes.new('ShaderNodeAddShader')
links.new(transparent.outputs[0], add.inputs[0])
links.new(emission.outputs[0], add.inputs[1])
output = nodes.new('ShaderNodeOutputMaterial')
links.new(add.outputs[0], output.inputs['Surface'])
glass.data.materials.clear()
glass.data.materials.append(material)
glass.visible_shadow = False
glass.visible_diffuse = False
glass.visible_glossy = False
glass.visible_transmission = False

backdrop = bpy.data.objects['19 | Distant space']
sky = bpy.data.materials.new('Preview | distant blue starfield')
sky.use_nodes = True
nodes, links = sky.node_tree.nodes, sky.node_tree.links
nodes.clear()
position = nodes.new('ShaderNodeNewGeometry')
xyz = nodes.new('ShaderNodeSeparateXYZ')
links.new(position.outputs['Position'], xyz.inputs[0])
plane = nodes.new('ShaderNodeCombineXYZ')
links.new(xyz.outputs['X'], plane.inputs['X'])
links.new(xyz.outputs['Z'], plane.inputs['Y'])
noise = nodes.new('ShaderNodeTexNoise')
noise.inputs['Scale'].default_value = .14
noise.inputs['Detail'].default_value = 3
links.new(plane.outputs[0], noise.inputs['Vector'])
cloud = nodes.new('ShaderNodeValToRGB')
cloud.color_ramp.elements[0].position = .25
cloud.color_ramp.elements[0].color = (.0018, .004, .014, 1)
cloud.color_ramp.elements[1].position = .78
cloud.color_ramp.elements[1].color = (.009, .025, .065, 1)
links.new(noise.outputs['Fac'], cloud.inputs[0])
stars = nodes.new('ShaderNodeTexVoronoi')
stars.voronoi_dimensions = '2D'
stars.inputs['Scale'].default_value = 1.15
links.new(plane.outputs[0], stars.inputs['Vector'])
disc = nodes.new('ShaderNodeMapRange')
disc.clamp = True
disc.inputs['From Min'].default_value = .002
disc.inputs['From Max'].default_value = .018
disc.inputs['To Min'].default_value = .48
disc.inputs['To Max'].default_value = 0
links.new(stars.outputs['Distance'], disc.inputs['Value'])
colour = nodes.new('ShaderNodeMixRGB')
colour.blend_type = 'ADD'
colour.inputs[0].default_value = 1
links.new(cloud.outputs[0], colour.inputs[1])
links.new(disc.outputs[0], colour.inputs[2])
emission = nodes.new('ShaderNodeEmission')
links.new(colour.outputs[0], emission.inputs['Color'])
output = nodes.new('ShaderNodeOutputMaterial')
links.new(emission.outputs[0], output.inputs['Surface'])
backdrop.data.materials.clear()
backdrop.data.materials.append(sky)
scene.cycles.samples = 48
scene.cycles.use_denoising = False
scene.render.threads_mode = 'FIXED'
scene.render.threads = 10
for suffix, width in [('', 1920), ('-21x9', 2560)]:
    game_projection(scene, scene.camera, width, 1080)
    scene.render.filepath = str(ROOT / 'previews' / ('deck-window-etching' + suffix + '.png'))
    bpy.ops.render.render(write_still=True)
print('GLASS_PREVIEWS=complete', flush=True)
