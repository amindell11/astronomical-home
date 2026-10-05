import bpy, math, json
from pathlib import Path
from mathutils import Vector
from io_scene_fbx import import_fbx

root = Path('D:/amind/git/agent-2')
out = root / 'results/nightshade-concept/concept/2026-10-05-r01/references/native'
out.mkdir(parents=True, exist_ok=True)
import_fbx.blen_read_light = lambda *args: bpy.data.lights.new('unused embedded light', 'POINT')
bpy.ops.object.select_all(action='SELECT')
bpy.ops.object.delete(use_global=False)
bpy.ops.import_scene.fbx(filepath=str(root / 'src/Asteroids3D/Assets/Visuals/Ships/Nightshade/cruiserUpdate1.fbx'))
meshes = [o for o in bpy.context.scene.objects if o.type == 'MESH']
for o in list(bpy.context.scene.objects):
    if o.type != 'MESH':
        bpy.data.objects.remove(o, do_unlink=True)
mat = bpy.data.materials.new('Current Nightshade albedo')
mat.use_nodes = True
bsdf = mat.node_tree.nodes.get('Principled BSDF')
bsdf.inputs['Roughness'].default_value = 0.65
tex = mat.node_tree.nodes.new('ShaderNodeTexImage')
tex.image = bpy.data.images.load(str(root / 'src/Asteroids3D/Assets/Visuals/Ships/Nightshade/Nightshade_Albedo.png'))
tex.image.pack()
mat.node_tree.links.new(tex.outputs['Color'], bsdf.inputs['Base Color'])
for o in meshes:
    o.scale = (1, 1, 1)
    o.data.materials.clear()
    o.data.materials.append(mat)
pts = [o.matrix_world @ Vector(c) for o in meshes for c in o.bound_box]
bpy.context.view_layer.update()
pts = [o.matrix_world @ Vector(c) for o in meshes for c in o.bound_box]
lo = Vector(tuple(min(p[i] for p in pts) for i in range(3)))
hi = Vector(tuple(max(p[i] for p in pts) for i in range(3)))
center = (lo + hi) / 2
span = max(hi-lo)
scene = bpy.context.scene
scene.render.engine = 'CYCLES'
scene.cycles.samples = 24
scene.render.resolution_x = scene.render.resolution_y = 1000
scene.render.resolution_percentage = 100
scene.world.color = (0.25, 0.25, 0.25)
scene.view_settings.view_transform = 'Standard'
for name, direction, power, size in [('key',(-3,-4,6),450,5),('fill',(4,3,3),300,4),('below',(0,2,-5),200,4)]:
    data = bpy.data.lights.new(name, 'AREA')
    data.energy, data.shape, data.size = power, 'DISK', size
    lamp = bpy.data.objects.new(name, data)
    scene.collection.objects.link(lamp)
    lamp.location = center + Vector(direction)
    lamp.rotation_euler = (center-lamp.location).to_track_quat('-Z','Y').to_euler()
camera = bpy.data.objects.new('reference camera', bpy.data.cameras.new('reference camera'))
scene.collection.objects.link(camera)
scene.camera = camera
camera.data.type = 'ORTHO'
camera.data.ortho_scale = span*1.2
views = {'top':(0,0,1), 'side':(1,0,0), 'quarter':(1,-1,1)}
for name, direction in views.items():
    camera.location = center + Vector(direction).normalized()*span*3
    camera.rotation_euler = (center-camera.location).to_track_quat('-Z','Y').to_euler()
    scene.render.filepath = str(out / f'mesh-{name}.png')
    bpy.ops.render.render(write_still=True)
bpy.ops.wm.save_as_mainfile(filepath=str(out / 'Nightshade-reference.blend'))
(out/'capture.json').write_text(json.dumps({'source':'src/Asteroids3D/Assets/Visuals/Ships/Nightshade/cruiserUpdate1.fbx','texture':'src/Asteroids3D/Assets/Visuals/Ships/Nightshade/Nightshade_Albedo.png','prefab_scale_xyz':[127,110,180],'blender_proportion_xyz':[1,1,1],'views':list(views),'note':'Imported current mesh; embedded lights omitted. Geometry unchanged; native FBX proportions; nonuniform prefab scale omitted for concept reference. Reference lighting differs from Unity.'},indent=2)+'\n')

