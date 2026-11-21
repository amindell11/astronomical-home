"""Render the exported mask with the game's flat colour and mask-times-.6 alpha.

Run headless Blender after export_glass.py and copy its PNG to previews.
Writes two etching previews and three bright-backdrop checks under .temp.
Run assemble_glass.py with ordinary Python for the labelled sheet. Errors exit nonzero.
"""
from pathlib import Path
import sys

import bpy
from mathutils import Matrix

ROOT = Path(__file__).resolve().parent
sys.path.insert(0, str(ROOT))
from deck_projection import game_projection

bpy.ops.wm.open_mainfile(filepath=str(ROOT / 'deck.blend'))
scene = bpy.context.scene
glass = bpy.data.objects['25 | Viewport glass']
material = bpy.data.materials.new('Preview | game window etching')
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
strength.inputs[1].default_value = .6
links.new(texture.outputs['Color'], strength.inputs[0])
emission = nodes.new('ShaderNodeEmission')
emission.inputs['Color'].default_value = (.30, .38, .42, 1)
emission.inputs['Strength'].default_value = 1
transparent = nodes.new('ShaderNodeBsdfTransparent')
mix = nodes.new('ShaderNodeMixShader')
links.new(strength.outputs[0], mix.inputs[0])
links.new(transparent.outputs[0], mix.inputs[1])
links.new(emission.outputs[0], mix.inputs[2])
output = nodes.new('ShaderNodeOutputMaterial')
links.new(mix.outputs[0], output.inputs['Surface'])
glass.data.materials.clear()
glass.data.materials.append(material)
glass.visible_shadow = False
glass.visible_diffuse = False
glass.visible_glossy = False
glass.visible_transmission = False

backdrop = bpy.data.objects['19 | Distant space']
eye = scene.camera.matrix_world.translation.copy()
backdrop.matrix_world = Matrix.Translation(eye) @ Matrix.Scale(4, 4) @ Matrix.Translation(-eye) @ backdrop.matrix_world
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
cloud.color_ramp.elements[0].color = (.35, .55, .8, 1)
cloud.color_ramp.elements[1].color = (.85, .95, 1, 1)
scene.view_settings.look = 'None'
(ROOT / '.temp').mkdir(exist_ok=True)
for label, width in [('16x9', 1920), ('21x9', 2560), ('32x9', 3840)]:
    game_projection(scene, scene.camera, width, 1080)
    scene.render.filepath = str(ROOT / '.temp' / f'deck-glass-{label}.png')
    bpy.ops.render.render(write_still=True)
print('GLASS_PREVIEWS=complete', flush=True)
