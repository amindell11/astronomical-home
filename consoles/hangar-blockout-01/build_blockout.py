from pathlib import Path
import json
import math
import bpy
from mathutils import Vector

ROOT = Path(__file__).resolve().parent
DATA = json.loads((ROOT / 'inputs/hangar-data.json').read_text())
OUT = ROOT / 'hangar-blockout.blend'
if OUT.exists():
    raise RuntimeError('The editable source already exists. Do not overwrite owner edits; use a new output folder.')

bpy.ops.wm.read_factory_settings(use_empty=True)
scene = bpy.context.scene
scene.name = 'Hangar dispatch blockout'
scene.unit_settings.system = 'METRIC'
scene.render.engine = 'CYCLES'
scene.cycles.samples = 16
scene.cycles.use_denoising = True
scene.render.resolution_x = 1920
scene.render.resolution_y = 1080
scene.render.resolution_percentage = 100
scene.render.image_settings.file_format = 'PNG'
scene.render.fps = 30
scene.frame_start = 1
scene.frame_end = 180
scene.view_settings.view_transform = 'Standard'
scene.view_settings.look = 'None'
scene.world = bpy.data.worlds.new('Dim ambient for grey blockout')
scene.world.use_nodes = True
scene.world.node_tree.nodes['Background'].inputs['Color'].default_value = (.12, .14, .18, 1)
scene.world.node_tree.nodes['Background'].inputs['Strength'].default_value = .25

def collection(name):
    c = bpy.data.collections.new(name)
    scene.collection.children.link(c)
    return c

camera_col = collection('00 Camera')
dock_col = collection('01 Station dock')
terminal_col = collection('02 Dispatch terminal')
screen_col = collection('03 Editable screen data')
projection_col = collection('04 Hologram capture')
call_col = collection('05 Physical Call control')
light_col = collection('06 Blockout illumination')
ref_col = collection('07 Source references - not rendered')
ref_col.hide_render = True

def move(obj, col):
    for old in list(obj.users_collection):
        old.objects.unlink(obj)
    col.objects.link(obj)
    return obj

def material(name, color, emission=0):
    mat = bpy.data.materials.new(name)
    mat.diffuse_color = (*color, 1)
    bs = mat.node_tree.nodes.get('Principled BSDF')
    bs.inputs['Base Color'].default_value = (*color, 1)
    bs.inputs['Roughness'].default_value = .82
    if emission:
        bs.inputs['Emission Color'].default_value = (*color, 1)
        bs.inputs['Emission Strength'].default_value = emission
    return mat

grey = material('Grey blockout - housing', (.28, .28, .28))
dark = material('Grey blockout - recess', (.065, .065, .065))
floor = material('Grey blockout - dock', (.15, .15, .15))
white = material('Screen ink - shared warm off-white', (.88, .85, .76), 1)
black = material('Screen backing', (.012, .014, .018))
button_mat = material('Call cap - neutral grey', (.52, .52, .52))
lamp = material('Call status light', (.8, .8, .8), 2)

def empty(name, pos, col):
    obj = bpy.data.objects.new(name, None)
    col.objects.link(obj)
    obj.location = pos
    obj.empty_display_type = 'PLAIN_AXES'
    obj.empty_display_size = .2
    return obj

def cube(name, pos, size, mat, col, bevel=.015, parent=None):
    bpy.ops.mesh.primitive_cube_add(size=1, location=pos)
    obj = move(bpy.context.object, col)
    obj.name = name
    obj.dimensions = size
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    obj.data.materials.append(mat)
    if bevel:
        mod = obj.modifiers.new('Editable corner radius', 'BEVEL')
        mod.width = bevel
        mod.segments = 1
        obj.modifiers.new('Flat face normals', 'WEIGHTED_NORMAL')
    if parent:
        obj.parent = parent
    return obj

def text(name, body, pos, size, mat, col, font, parent=None, align='LEFT'):
    curve = bpy.data.curves.new(name, 'FONT')
    curve.body = body
    curve.font = font
    curve.size = size
    curve.align_x = align
    curve.align_y = 'CENTER'
    curve.resolution_u = 4
    obj = bpy.data.objects.new(name, curve)
    col.objects.link(obj)
    obj.location = pos
    obj.data.materials.append(mat)
    if parent:
        obj.parent = parent
    return obj

font_dir = ROOT.parents[1] / 'combat-hud/shared/fonts'
sans = bpy.data.fonts.load(str(font_dir / 'plex-sans.ttf'))
mono = bpy.data.fonts.load(str(font_dir / 'plex-mono.ttf'))
sans.pack()
mono.pack()

cam_data = bpy.data.cameras.new('Standing at the dispatch terminal')
cam = bpy.data.objects.new('CAMERA - single review view', cam_data)
camera_col.objects.link(cam)
cam.location = (0, -3.9, 2.05)
cam.rotation_euler = (Vector((0, .40, 1.18)) - cam.location).to_track_quat('-Z', 'Y').to_euler()
cam.data.lens = 32
cam.data.clip_end = 100
scene.camera = cam

dock = empty('DOCK - move the dock here', (0, 0, 0), dock_col)
cube('Dock slab', (0, -.5, -.14), (9, 4.4, .28), floor, dock_col, .025, dock)
cube('Dock edge facing open space', (0, 1.68, .035), (9, .13, .07), grey, dock_col, .015, dock)
cube('Dock edge fascia', (0, 1.78, -.16), (9, .10, .38), dark, dock_col, .01, dock)
for x in (-3.3, -1.65, 0, 1.65, 3.3):
    cube('Dock slab seam', (x, -.45, .002), (.012, 4.1, .002), dark, dock_col, 0, dock)

terminal = empty('TERMINAL - move the complete console here', (0, 0, 0), terminal_col)
terminal['Purpose'] = 'Station dispatch terminal. Grey massing only; no painted hardware.'
cube('Console pedestal', (-.25, .20, .48), (1.05, .66, .90), dark, terminal_col, .045, terminal)
cube('Console foot', (-.25, .15, .07), (1.55, .90, .14), grey, terminal_col, .025, terminal)
cube('Shared control worktop', (0, .15, .99), (2.85, 1.10, .17), grey, terminal_col, .06, terminal)

monitor = empty('SCREEN - move or tilt monitor here', (-.74, .24, 1.52), terminal_col)
monitor.parent = terminal
monitor.rotation_euler.x = math.radians(72)
cube('Monitor housing', (0, 0, 0), (1.38, .95, .18), grey, terminal_col, .065, monitor)
cube('Screen recess', (0, 0, .101), (1.24, .80, .026), dark, terminal_col, .025, monitor)
cube('Screen face', (0, 0, .118), (1.17, .73, .008), black, screen_col, 0, monitor)
text('Title HANGAR', DATA['title'], (-.51, .28, .127), .072, white, screen_col, sans, monitor)
cube('Title divider', (0, .20, .128), (1.03, .004, .002), white, screen_col, 0, monitor)

def visibility(obj, nightshade):
    for frame, hidden in [(1, nightshade), (60, nightshade), (61, not nightshade),
                          (120, not nightshade), (121, nightshade), (180, nightshade)]:
        obj.hide_render = hidden
        obj.hide_viewport = hidden
        obj.keyframe_insert(data_path='hide_render', frame=frame)
        obj.keyframe_insert(data_path='hide_viewport', frame=frame)

for label, key, y in [('SHIP', 'ship', .10), ('PRIMARY', 'primary', -.055), ('SECONDARY', 'secondary', -.21)]:
    text('Label ' + label, label, (-.51, y, .129), .042, white, screen_col, sans, monitor)
    for ship in ('Valis', 'Nightshade'):
        obj = text(ship + ' ' + label, DATA['samples'][ship][key], (-.21, y, .129), .054,
                   white, screen_col, mono, monitor)
        visibility(obj, ship == 'Nightshade')
    text(label + ' previous', '\u2039', (.40, y, .129), .075, white, screen_col, sans, monitor)
    text(label + ' next', '\u203a', (.51, y, .129), .075, white, screen_col, sans, monitor)
    cube(label + ' row rule', (0, y-.067, .128), (1.03, .002, .001), grey, screen_col, 0, monitor)

projector = empty('PROJECTOR - hologram origin', (.65, .30, 1.09), projection_col)
projector.parent = terminal
cube('Projector base', (0, 0, 0), (.67, .58, .075), dark, projection_col, .03, projector)
cube('Projector aperture', (0, 0, .045), (.50, .42, .018), grey, projection_col, .035, projector)
cube('Projector lit slot', (0, 0, .057), (.34, .025, .008), lamp, projection_col, .004, projector)

call = empty('CALL - physical control assembly', (1.08, -.20, 1.095), call_col)
call.parent = terminal
call.rotation_euler.x = math.radians(25)
cube('Call button socket', (0, 0, 0), (.38, .32, .06), dark, call_col, .025, call)
cap = cube('CALL - movable button cap', (0, 0, .042), (.26, .23, .065), button_mat, call_col, .02, call)
text('CALL engraved label', 'CALL', (0, -.01, .077), .060, black, call_col, sans, call, align='CENTER')
cube('Call indicator', (0, .145, .036), (.18, .012, .012), lamp, call_col, .002, call)
cap['Function'] = 'Physical Call control. Arrival transition follows layout approval.'

holo = empty('HOLOGRAM - move or scale capture volume', (.65, .40, 1.60), projection_col)
holo['Opacity'] = .7
holo['Capture'] = 'Actual Unity preview flights, PR 1076. Frames 1-180 at 30 fps.'
holo['Note'] = 'A captured image plane for the blockout, not replacement 3D ship art.'
holo.rotation_euler = cam.rotation_euler
bpy.ops.mesh.primitive_plane_add(size=1)
plate = move(bpy.context.object, projection_col)
plate.name = 'Actual preview flights - keyed RGB sequence'
plate.parent = holo
plate.location = (-.55, 0, 0)
plate.scale = (3.0, 3.0 * 720 / 1280, 1)

mat = bpy.data.materials.new('Hologram - real capture key and transparency')
mat.surface_render_method = 'DITHERED'
nodes = mat.node_tree.nodes
nodes.clear()
links = mat.node_tree.links
output = nodes.new('ShaderNodeOutputMaterial')
output.location = (650, 0)
texture = nodes.new('ShaderNodeTexImage')
texture.location = (-650, 80)
texture.name = 'Actual Unity preview flight frames'
texture.image = bpy.data.images.load(str(ROOT / 'inputs/preview-flight/frame-0000.png'))
texture.image.source = 'SEQUENCE'
texture.image_user.frame_duration = 180
texture.image_user.frame_start = 1
texture.image_user.use_cyclic = True
texture.image_user.use_auto_refresh = True
distance = nodes.new('ShaderNodeVectorMath')
distance.operation = 'DISTANCE'
distance.location = (-400, -150)
srgb = lambda x: (x / 255 / 12.92) if x / 255 <= .04045 else ((x / 255 + .055) / 1.055) ** 2.4
distance.inputs[1].default_value = tuple(srgb(x) for x in (9, 13, 23))
links.new(texture.outputs['Color'], distance.inputs[0])
key = nodes.new('ShaderNodeMapRange')
key.location = (-200, -150)
key.inputs['From Min'].default_value = .0003
key.inputs['From Max'].default_value = .005
key.inputs['To Min'].default_value = 0
key.inputs['To Max'].default_value = 1
links.new(distance.outputs['Value'], key.inputs['Value'])
opacity = nodes.new('ShaderNodeMath')
opacity.operation = 'MULTIPLY'
opacity.location = (0, -160)
links.new(key.outputs['Result'], opacity.inputs[0])
driver = opacity.inputs[1].driver_add('default_value').driver
driver.expression = 'opacity'
var = driver.variables.new()
var.name = 'opacity'
var.type = 'SINGLE_PROP'
var.targets[0].id = holo
var.targets[0].data_path = '["Opacity"]'
emission = nodes.new('ShaderNodeEmission')
emission.location = (0, 100)
emission.inputs['Strength'].default_value = 1.7
links.new(texture.outputs['Color'], emission.inputs['Color'])
transparent = nodes.new('ShaderNodeBsdfTransparent')
transparent.location = (0, 260)
mix = nodes.new('ShaderNodeMixShader')
mix.location = (390, 60)
links.new(opacity.outputs[0], mix.inputs[0])
links.new(transparent.outputs[0], mix.inputs[1])
links.new(emission.outputs[0], mix.inputs[2])
links.new(mix.outputs[0], output.inputs['Surface'])
plate.data.materials.append(mat)
plate.visible_shadow = False

sky = bpy.data.images.load(str(ROOT / 'inputs/real-hangar-reference.png'))
sky.pack()
bpy.ops.mesh.primitive_plane_add(size=1, location=(0, 8, 2.3), rotation=(math.pi / 2, 0, 0))
backdrop = move(bpy.context.object, dock_col)
backdrop.name = 'Distant sky - empty region of real hangar capture'
backdrop.scale = (20, 11.25, 1)
uvs = backdrop.data.uv_layers.active.data
for loop in uvs:
    u, v = loop.uv
    loop.uv = (.375 + .2775 * u, .7222222222 + .2777777778 * v)
sky_mat = bpy.data.materials.new('Dim captured sky - background placeholder')
sky_nodes = sky_mat.node_tree.nodes
sky_nodes.clear()
sky_texture = sky_nodes.new('ShaderNodeTexImage')
sky_texture.image = sky
sky_emission = sky_nodes.new('ShaderNodeEmission')
sky_emission.inputs['Strength'].default_value = .35
sky_output = sky_nodes.new('ShaderNodeOutputMaterial')
sky_mat.node_tree.links.new(sky_texture.outputs['Color'], sky_emission.inputs['Color'])
sky_mat.node_tree.links.new(sky_emission.outputs[0], sky_output.inputs['Surface'])
backdrop.data.materials.append(sky_mat)
backdrop['Source crop'] = 'Pixels x 600-1044, y 0-250. Empty 16:9 sky crop; no ship or UI.'

def area(name, pos, target, energy, size):
    data = bpy.data.lights.new(name, 'AREA')
    data.energy = energy
    data.shape = 'DISK'
    data.size = size
    obj = bpy.data.objects.new(name, data)
    light_col.objects.link(obj)
    obj.location = pos
    obj.rotation_euler = (Vector(target)-obj.location).to_track_quat('-Z', 'Y').to_euler()
    return obj

area('Neutral blockout key', (-3, -4, 5), (0, .2, 1), 500, 5)
area('Neutral edge fill', (3, 1, 4), (0, 0, 1), 250, 4)

for name, path, x in [('Lighting C - mood only', ROOT.parent / 'lighting-01/electronic-light-contrast-nb2.jpg', -5),
                      ('Ink finish B - later paint-over', ROOT.parent / 'concepts/02-ink-drawn.png', -9)]:
    obj = empty(name, (x, 0, 2), ref_col)
    obj.empty_display_type = 'IMAGE'
    obj.data = bpy.data.images.load(str(path))
    obj.data.pack()
    obj.empty_display_size = 3
    obj.hide_render = True

for frame, title in [(1, 'Captured entry: Valis'), (46, 'Review still: Valis'),
                     (61, 'Next: Nightshade'), (106, 'Review still: Nightshade'),
                     (121, 'Previous: Valis'), (166, 'Return settled')]:
    scene.timeline_markers.new(title, frame=frame)

notes = bpy.data.texts.new('START HERE - blockout guide')
notes.write('''EDITABLE HANGAR BLOCKOUT / issue 1249\n\nOne camera. Grey hardware only. No image-generation paint-over yet.\n\nMove TERMINAL to move the console. SCREEN moves/tilts the monitor.\nPROJECTOR positions the aperture. HOLOGRAM moves/scales the real capture.\nHOLOGRAM custom property Opacity changes projection transparency.\nCALL is a physical button assembly, not a second screen.\n\nTimeline: 1-180 at 30 fps, actual captured entry and Next/Previous flights.\n46 is Valis, 106 is Nightshade, 166 is Valis returned.\nShip names and authored weapon values change at the recorded selection frames.\n\nThe flight is a keyed image sequence from the shipped Unity preview review.\nKeep inputs/preview-flight beside this file. Other images and fonts are packed.\nNative mesh, text, modifiers, camera and lights remain editable.\nThe sky is an empty crop of an actual hangar capture, for background context.\n\nAfter the owner edits this .blend, this file is the source of record.\nDo not rerun build_blockout.py over it. F12 renders the current view.\nPaint-over waits for explicit owner approval of this blockout.\n''')
scene['Approval state'] = 'Blockout awaiting owner review. No paint-over authorized yet.'
scene['Data source commit'] = DATA['sourceCommit']
scene.frame_set(46)
bpy.context.view_layer.update()
for obj in bpy.context.selected_objects:
    obj.select_set(False)
monitor.select_set(True)
bpy.context.view_layer.objects.active = monitor
for screen in bpy.data.screens:
    for area_ui in screen.areas:
        if area_ui.type == 'VIEW_3D':
            area_ui.spaces.active.region_3d.view_perspective = 'CAMERA'
            area_ui.spaces.active.shading.type = 'MATERIAL'
            area_ui.spaces.active.overlay.show_extras = False
scene.render.filepath = str(ROOT / 'renders/blockout-valis.png')
bpy.ops.wm.save_as_mainfile(filepath=str(OUT))
bpy.ops.file.make_paths_relative()
bpy.ops.wm.save_as_mainfile(filepath=str(OUT))
bpy.ops.render.render(write_still=True)
print(json.dumps({'blend':str(OUT), 'cameraCount':len([o for o in scene.objects if o.type=='CAMERA']),
                  'objects':len(scene.objects), 'frame':scene.frame_current}))
