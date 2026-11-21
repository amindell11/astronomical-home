"""Render a neutral-material ink inspection of the saved deck at 1920 by 1080.

Run with headless Blender. Writes previews/deck-finish5-geometry.png without
saving the scene or changing its palette. Blender errors exit nonzero.
"""
from pathlib import Path
import sys

import bpy

ROOT = Path(__file__).resolve().parent
sys.path.insert(0, str(ROOT))
from deck_projection import game_projection

bpy.ops.wm.open_mainfile(filepath=str(ROOT / 'deck.blend'))
scene = bpy.context.scene
game_projection(scene, scene.camera, 1920, 1080)
scene.render.threads_mode = 'FIXED'
scene.render.threads = 10
scene.cycles.samples = 16
scene.render.use_freestyle = True
for obj in scene.objects:
    if obj.get('preview_only') or obj.name == '25 | Viewport glass':
        obj.hide_render = True
material = bpy.data.materials.new('Review only | neutral geometry')
material.use_nodes = True
shader = material.node_tree.nodes.get('Principled BSDF')
shader.inputs['Base Color'].default_value = (.38, .43, .50, 1)
shader.inputs['Roughness'].default_value = 1
bpy.context.view_layer.material_override = material
scene.world.node_tree.nodes.get('Background').inputs[0].default_value = (.18, .18, .18, 1)
scene.render.filepath = str(ROOT / 'previews/deck-finish5-geometry.png')
bpy.ops.render.render(write_still=True)
