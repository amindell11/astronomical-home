"""Bake the authored window etch/cloud mask and export its real pane. --assets names the Unity visuals directory."""
import argparse
import sys
from pathlib import Path

import bpy

parser = argparse.ArgumentParser()
parser.add_argument('--assets', required=True)
args = parser.parse_args(sys.argv[sys.argv.index('--') + 1:])
assets = Path(args.assets).resolve()
glass = bpy.data.objects['25 | Viewport glass']
material = glass.data.materials[0]
nodes = material.node_tree.nodes
links = material.node_tree.links
mix = nodes['Only the perimeter catches light']
mask = mix.inputs[0].links[0].from_socket
shader = nodes.new('ShaderNodeEmission')
links.new(mask, shader.inputs['Color'])
links.new(shader.outputs[0], nodes['Material Output'].inputs['Surface'])
image = bpy.data.images.new('WindowEtching', width=4096, height=2048, alpha=False)
image.colorspace_settings.name = 'Non-Color'
image.filepath_raw = str(assets / 'WindowEtching.png')
image.file_format = 'PNG'
target = nodes.new('ShaderNodeTexImage')
target.image = image
nodes.active = target
bpy.ops.object.select_all(action='DESELECT')
glass.hide_set(False)
glass.select_set(True)
bpy.context.view_layer.objects.active = glass
bpy.context.scene.cycles.samples = 1
bpy.context.scene.render.bake.margin = 2
bpy.context.scene.render.bake.use_selected_to_active = False
bpy.ops.object.bake(type='EMIT')
image.save()
world = glass.matrix_world.copy()
glass.parent = None
glass.matrix_world = world
glass.name = 'ViewportGlass'
glass.data.materials.clear()
bpy.ops.export_scene.fbx(filepath=str(assets / 'ViewportGlass.fbx'), use_selection=True,
                         object_types={'MESH'}, apply_unit_scale=True,
                         apply_scale_options='FBX_SCALE_ALL', bake_space_transform=True,
                         axis_forward='-Z', axis_up='Y', add_leaf_bones=False, bake_anim=False)
print('WINDOW_EXPORT=complete')
