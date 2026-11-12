"""Blender background export of console.blend.

--assets is the Unity visuals directory; --scratch holds disposable bake intermediates.
Writes Console.fbx, ConsolePaint.png, ConsoleEmission.png, both static screen PNGs,
and console-export.json beside this script. Exit 0 means every output was written.
The source scene is never saved by this exporter.
"""
import argparse
import json
import math
import sys
from pathlib import Path

import bpy
from mathutils import Matrix, Vector


parser = argparse.ArgumentParser()
parser.add_argument('--assets', required=True)
parser.add_argument('--scratch', required=True)
parser.add_argument('--size', type=int, default=8192)
parser.add_argument('--reuse-ink', action='store_true')
args = parser.parse_args(sys.argv[sys.argv.index('--') + 1:])
assets = Path(args.assets).resolve()
scratch = Path(args.scratch).resolve()
assets.mkdir(parents=True, exist_ok=True)
scratch.mkdir(parents=True, exist_ok=True)
scene = bpy.context.scene
camera = scene.camera
source_camera = camera.matrix_world.copy()
aspect = 16 / 9
vertical_fov = math.degrees(2 * math.atan(camera.data.sensor_width / (2 * camera.data.lens * aspect)))

excluded = ('07 reuse | Vanguard', '19 | Vanguard', '19 | Distant space',
            '11 | Neutral projector frustum', '25 | Viewport glass')
hardware = [o for o in scene.objects if not o.hide_render and o.type in {'MESH', 'CURVE', 'FONT'}
            and not o.name.startswith(excluded)]
for obj in scene.objects:
    if obj.type in {'MESH', 'CURVE', 'FONT'} and obj not in hardware:
        obj.hide_render = True

part_names = {
    'MainScreenGlass': ['07 | Main rectangular display'],
    'DetailScreenGlass': ['07 | Detail rectangular display'],
    'SelectorKnob': ['08 | Selector faceted grip', '08 | Selector pointer'],
    'PreviousButton': ['08 | Previous chunky keycap'],
    'PreviousLegend': ['08 | Previous inlaid chevron'],
    'NextButton': ['08 | Next chunky keycap'],
    'NextLegend': ['08 | Next inlaid chevron'],
    'LaunchButton': ['08 | Launch broad beveled button'],
    'LaunchLegend': ['19 | Launch legend'],
    'ProjectorLens': ['09 | Projector lens'],
}
moving_parts = ('SelectorKnob', 'PreviousButton', 'NextButton', 'LaunchButton')
legend_parts = ('PreviousLegend', 'NextLegend', 'LaunchLegend')
pivot_names = {
    'SelectorKnob': '08 | Selector faceted grip', 'PreviousButton': '08 | Previous chunky keycap',
    'NextButton': '08 | Next chunky keycap', 'LaunchButton': '08 | Launch broad beveled button',
}
groups = {name: [bpy.data.objects[n] for n in names] for name, names in part_names.items()}
pivots = {name: bpy.data.objects[pivot_names.get(name, names[0])].matrix_world.copy()
          for name, names in part_names.items()}
screen_objects = groups['MainScreenGlass'] + groups['DetailScreenGlass']
paint_objects = [o for o in hardware if o not in screen_objects]

for name, material_name in [('MainScreenReference', '19 | Main rectangular display'),
                            ('DetailScreenReference', '19 | Detail rectangular display')]:
    image = next(n.image for n in bpy.data.materials[material_name].node_tree.nodes if n.type == 'TEX_IMAGE')
    image.filepath_raw = str(assets / (name + '.png'))
    image.file_format = 'PNG'
    image.save()

# Freestyle is evaluated on the authored geometry before applying the modifiers.
white = bpy.data.materials.new('Bake white')
white.use_nodes = True
nodes = white.node_tree.nodes
nodes.clear()
emission = nodes.new('ShaderNodeEmission')
emission.inputs['Color'].default_value = (1, 1, 1, 1)
out = nodes.new('ShaderNodeOutputMaterial')
white.node_tree.links.new(emission.outputs[0], out.inputs['Surface'])
bpy.context.view_layer.material_override = white
scene.render.resolution_x = 4096
scene.render.resolution_y = 2304
scene.render.resolution_percentage = 100
scene.render.film_transparent = False
scene.world.color = (1, 1, 1)
for node in scene.world.node_tree.nodes:
    if node.type == 'BACKGROUND':
        node.inputs['Color'].default_value = (1, 1, 1, 1)
        node.inputs['Strength'].default_value = 1
scene.cycles.samples = 1
scene.cycles.use_denoising = False
scene.render.image_settings.file_format = 'PNG'
scene.render.image_settings.color_mode = 'RGBA'
scene.view_settings.view_transform = 'Standard'
scene.view_settings.look = 'None'
for lines in bpy.context.view_layer.freestyle_settings.linesets:
    lines.linestyle.color = (.009, .004, .002)
    lines.linestyle.thickness *= 4096 / 1672


def render_ink(name):
    scene.render.filepath = str(scratch / name)
    if not args.reuse_ink:
        bpy.ops.render.render(write_still=True)
    return bpy.data.images.load(str(scratch / name), check_existing=False)


# A control that moves wears only its own outline, drawn inside its edge, so the ink travels
# with it. The body is inked with the controls away, so nothing of theirs stays behind.
moving = [o for name in moving_parts for o in groups[name]]
carried = moving + [o for name in legend_parts for o in groups[name]]
for obj in carried:
    obj.hide_render = True
body_ink = render_ink('console-ink-body.png')
for obj in carried:
    obj.hide_render = False
linesets = bpy.context.view_layer.freestyle_settings.linesets
rim = linesets['Lit legend rim']
own_ink = bpy.data.collections.new('Moving control ink')
for obj in moving:
    if obj.name not in rim.collection.objects:
        own_ink.objects.link(obj)
for lines in linesets:
    if lines == rim:
        continue
    lines.collection = own_ink
    if lines.select_silhouette:
        lines.linestyle.thickness_position = 'INSIDE'
control_ink = render_ink('console-ink-controls.png')
bpy.context.view_layer.material_override = None
scene.render.use_freestyle = False

worlds = {obj: obj.matrix_world.copy() for obj in hardware}
for obj in hardware:
    obj.parent = None
    obj.matrix_world = worlds[obj]
for obj in list(bpy.data.objects):
    if obj not in hardware and obj != camera:
        bpy.data.objects.remove(obj, do_unlink=True)
print('Converting', len(hardware), 'hardware objects', flush=True)
for index, obj in enumerate(hardware):
    for slot in obj.material_slots:
        actual = slot.material
        slot.link = 'DATA'
        slot.material = actual
    bpy.ops.object.select_all(action='DESELECT')
    obj.hide_set(False)
    obj.select_set(True)
    bpy.context.view_layer.objects.active = obj
    bpy.ops.object.convert(target='MESH')
    if index % 20 == 0:
        print('Converted', index, flush=True)

# Keep original generated paint coordinates while giving every object disjoint atlas UVs.
bpy.ops.object.select_all(action='DESELECT')
for obj in paint_objects:
    obj.select_set(True)
    obj.data.uv_layers.new(name='ConsoleAtlas')
    obj.data.uv_layers.active = obj.data.uv_layers['ConsoleAtlas']
    obj.data.uv_layers['ConsoleAtlas'].active_render = True
bpy.context.view_layer.objects.active = paint_objects[0]
bpy.ops.object.mode_set(mode='EDIT')
bpy.ops.mesh.select_all(action='SELECT')
bpy.ops.uv.smart_project(angle_limit=math.radians(66), island_margin=.003,
                         area_weight=0, correct_aspect=True, scale_to_bounds=True)
bpy.ops.object.mode_set(mode='OBJECT')
print('Atlas UVs packed', flush=True)

for obj in paint_objects:
    coordinates = obj.data.attributes.new('ConsoleGenerated', 'FLOAT_VECTOR', 'POINT')
    object_coordinates = obj.data.attributes.new('ConsoleObject', 'FLOAT_VECTOR', 'POINT')
    minimum = Vector(tuple(min(v.co[axis] for v in obj.data.vertices) for axis in range(3)))
    maximum = Vector(tuple(max(v.co[axis] for v in obj.data.vertices) for axis in range(3)))
    size = maximum - minimum
    own = obj.data.attributes.new('ConsoleCarriedInk', 'FLOAT', 'POINT')
    for vertex, value in zip(obj.data.vertices, coordinates.data):
        value.vector = tuple((vertex.co[axis] - minimum[axis]) / size[axis] if size[axis] else 0
                             for axis in range(3))
        object_coordinates.data[vertex.index].vector = vertex.co
        own.data[vertex.index].value = 1 if obj in carried else 0
    for polygon in obj.data.polygons:
        if not obj.data.materials[polygon.material_index]:
            raise RuntimeError('Unassigned source material: ' + obj.name)

materials = set(m for obj in paint_objects for m in obj.data.materials if m)
original_outputs = {}
paint_inputs = {}
emission_inputs = {}


def color_socket(material, purpose):
    nodes = material.node_tree.nodes
    painted = nodes.get('Painted color independent of smooth reflections')
    if purpose == 'paint' and painted:
        return painted.inputs['Color'].links[0].from_socket, None
    principled = next((n for n in nodes if n.type == 'BSDF_PRINCIPLED'), None)
    if principled:
        socket = principled.inputs['Base Color' if purpose == 'paint' else 'Emission Color']
        strength = 1 if purpose == 'paint' else principled.inputs['Emission Strength'].default_value
        if socket.is_linked:
            return socket.links[0].from_socket, strength
        value = nodes.new('ShaderNodeRGB')
        value.outputs[0].default_value = socket.default_value if strength else (0, 0, 0, 1)
        return value.outputs[0], strength
    value = nodes.new('ShaderNodeRGB')
    value.outputs[0].default_value = (0, 0, 0, 1) if purpose == 'emission' else material.diffuse_color
    return value.outputs[0], 1


def projected_ink(material):
    nodes = material.node_tree.nodes
    links = material.node_tree.links
    geometry = nodes.new('ShaderNodeNewGeometry')
    subtract = nodes.new('ShaderNodeVectorMath')
    subtract.operation = 'SUBTRACT'
    subtract.inputs[1].default_value = camera.matrix_world.translation
    links.new(geometry.outputs['Position'], subtract.inputs[0])
    axes = [camera.matrix_world.to_3x3().col[i] for i in range(3)]
    dots = []
    for axis in (axes[0], axes[1], -axes[2]):
        dot = nodes.new('ShaderNodeVectorMath')
        dot.operation = 'DOT_PRODUCT'
        dot.inputs[1].default_value = axis
        links.new(subtract.outputs['Vector'], dot.inputs[0])
        dots.append(dot.outputs['Value'])
    combine = nodes.new('ShaderNodeCombineXYZ')
    for i, scale in enumerate((camera.data.lens / camera.data.sensor_width,
                              camera.data.lens * aspect / camera.data.sensor_width)):
        divide = nodes.new('ShaderNodeMath')
        divide.operation = 'DIVIDE'
        links.new(dots[i], divide.inputs[0])
        links.new(dots[2], divide.inputs[1])
        multiply_add = nodes.new('ShaderNodeMath')
        multiply_add.operation = 'MULTIPLY_ADD'
        multiply_add.inputs[1].default_value = scale
        multiply_add.inputs[2].default_value = .5
        links.new(divide.outputs[0], multiply_add.inputs[0])
        links.new(multiply_add.outputs[0], combine.inputs[i])
    layers = []
    for image in (body_ink, control_ink):
        texture = nodes.new('ShaderNodeTexImage')
        texture.image = image
        texture.extension = 'EXTEND'
        links.new(combine.outputs[0], texture.inputs['Vector'])
        layers.append(texture.outputs['Color'])
    own = nodes.new('ShaderNodeAttribute')
    own.attribute_name = 'ConsoleCarriedInk'
    pick = nodes.new('ShaderNodeMixRGB')
    links.new(own.outputs['Fac'], pick.inputs[0])
    links.new(layers[0], pick.inputs[1])
    links.new(layers[1], pick.inputs[2])
    return pick.outputs[0]


for material in materials:
    generated = material.node_tree.nodes.new('ShaderNodeAttribute')
    generated.attribute_name = 'ConsoleGenerated'
    for link in list(material.node_tree.links):
        if link.from_node.type == 'TEX_COORD' and link.from_socket.name == 'Generated':
            material.node_tree.links.new(generated.outputs['Vector'], link.to_socket)
        elif link.from_node.type == 'TEX_COORD' and link.from_socket.name == 'Object':
            coordinates = material.node_tree.nodes.new('ShaderNodeAttribute')
            coordinates.attribute_name = 'ConsoleObject'
            material.node_tree.links.new(coordinates.outputs['Vector'], link.to_socket)
    output = next(n for n in material.node_tree.nodes if n.type == 'OUTPUT_MATERIAL')
    original_outputs[material] = output
    paint, _ = color_socket(material, 'paint')
    glow, strength = color_socket(material, 'emission')
    if 'Painted color independent of smooth reflections' in material.node_tree.nodes:
        glow = material.node_tree.nodes.new('ShaderNodeRGB').outputs[0]
        glow.default_value = (0, 0, 0, 1)
        strength = 0
    multiply = material.node_tree.nodes.new('ShaderNodeMixRGB')
    multiply.blend_type = 'MULTIPLY'
    multiply.inputs[0].default_value = 1
    material.node_tree.links.new(paint, multiply.inputs[1])
    material.node_tree.links.new(projected_ink(material), multiply.inputs[2])
    paint_inputs[material] = multiply.outputs[0]
    emission_inputs[material] = (glow, strength or 0)

bpy.ops.object.select_all(action='DESELECT')
copies = []
for original in paint_objects:
    duplicate = original.copy()
    duplicate.data = original.data.copy()
    scene.collection.objects.link(duplicate)
    duplicate.parent = None
    duplicate.matrix_world = original.matrix_world
    duplicate.select_set(True)
    original.hide_render = True
    copies.append(duplicate)
bpy.context.view_layer.objects.active = copies[0]
bpy.ops.object.join()
bake_object = bpy.context.object
for index in reversed(range(len(bake_object.data.materials))):
    if not any(p.material_index == index for p in bake_object.data.polygons):
        bake_object.data.materials.pop(index=index)
print('Joined bake mesh', len(bake_object.data.vertices), 'vertices', flush=True)


def bake(name, size, glow=False):
    image = bpy.data.images.new(name, width=size, height=size, alpha=False, float_buffer=glow)
    image.filepath_raw = str(assets / (name + '.png'))
    image.file_format = 'PNG'
    image.colorspace_settings.name = 'sRGB'
    for material in materials:
        nodes = material.node_tree.nodes
        links = material.node_tree.links
        shader = nodes.new('ShaderNodeEmission')
        socket, strength = emission_inputs[material] if glow else (paint_inputs[material], 1)
        links.new(socket, shader.inputs['Color'])
        shader.inputs['Strength'].default_value = strength
        links.new(shader.outputs[0], original_outputs[material].inputs['Surface'])
        target = nodes.new('ShaderNodeTexImage')
        target.image = image
        nodes.active = target
    scene.render.bake.use_clear = True
    scene.render.bake.margin = 12 if size >= 4096 else 6
    scene.render.bake.use_selected_to_active = False
    bpy.ops.object.select_all(action='DESELECT')
    bake_object.select_set(True)
    bpy.context.view_layer.objects.active = bake_object
    print('Baking', name, flush=True)
    bpy.ops.object.bake(type='EMIT')
    if glow:
        scene.view_settings.view_transform = 'AgX'
        scene.view_settings.look = 'AgX - Medium High Contrast'
        image.save_render(image.filepath_raw, scene=scene)
    else:
        image.save()
    print('Saved', name, flush=True)
    return image


bake('ConsolePaint', args.size)
bake('ConsoleEmission', args.size // 2, glow=True)
bpy.data.objects.remove(bake_object, do_unlink=True)
for obj in paint_objects:
    obj.hide_render = False

exported = []
assigned = set(o for group in groups.values() for o in group)
groups['ConsoleBody'] = [o for o in hardware if o not in assigned]
pivots['ConsoleBody'] = Matrix.Identity(4)
for name, objects in groups.items():
    bpy.ops.object.select_all(action='DESELECT')
    for obj in objects:
        obj.select_set(True)
    bpy.context.view_layer.objects.active = objects[0]
    if len(objects) > 1:
        bpy.ops.object.join()
    obj = bpy.context.object
    world = obj.matrix_world.copy()
    pivot = pivots[name]
    position = pivot.translation
    rotation = pivot.to_quaternion().to_matrix().to_4x4()
    rotation.translation = position
    obj.data.transform(rotation.inverted() @ world)
    obj.parent = None
    obj.matrix_world = rotation
    obj.name = name
    obj.data.name = name
    if name not in {'MainScreenGlass', 'DetailScreenGlass'}:
        atlas = obj.data.uv_layers['ConsoleAtlas']
        for uv in list(obj.data.uv_layers):
            if uv != atlas:
                obj.data.uv_layers.remove(uv)
    obj.data.materials.clear()
    material_name = name if name in {'MainScreenGlass', 'DetailScreenGlass'} else 'ConsolePaint'
    material = bpy.data.materials.get('Export ' + material_name) or bpy.data.materials.new('Export ' + material_name)
    obj.data.materials.append(material)
    for polygon in obj.data.polygons:
        polygon.material_index = 0
    exported.append(obj)

bpy.ops.object.select_all(action='DESELECT')
for obj in exported:
    obj.select_set(True)
bpy.context.view_layer.objects.active = exported[-1]
bpy.ops.export_scene.fbx(filepath=str(assets / 'Console.fbx'), use_selection=True,
                         object_types={'MESH'}, apply_unit_scale=True,
                         apply_scale_options='FBX_SCALE_ALL', bake_space_transform=True,
                         axis_forward='-Z', axis_up='Y', use_mesh_modifiers=True,
                         mesh_smooth_type='FACE', add_leaf_bones=False, bake_anim=False)

manifest = {
    'source': 'console.blend', 'sourceEvidenceCommit': '3b14f5d5a51d86463aba0922497b6377865ab38d',
    'camera': {'position': list(source_camera.translation),
               'forward': list(-source_camera.to_3x3().col[2]),
               'up': list(source_camera.to_3x3().col[1]),
               'verticalFieldOfView': vertical_fov, 'referenceAspect': aspect},
    'parts': [{'name': obj.name, 'vertices': len(obj.data.vertices),
               'triangles': sum(len(p.vertices) - 2 for p in obj.data.polygons),
               'pivot': list(obj.matrix_world.translation)} for obj in exported],
    'paint': 'Authored color graph and fixed-camera Freestyle ink, one layer for the body and one the '
             'moving controls carry. No scene lighting, sky, ship or cone.',
    'screens': 'Static reference artwork; no live UI.',
}
(Path(__file__).parent / 'console-export.json').write_text(json.dumps(manifest, indent=2))
print('CONSOLE_EXPORT=' + json.dumps(manifest))
