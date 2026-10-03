import bpy
import json
from pathlib import Path
from mathutils import Vector

out = Path(__file__).resolve().parent
manifest = json.loads((out / 'source-manifest.json').read_text(encoding='utf-8'))
source = bpy.data.scenes[manifest['source']['scene']]
bpy.context.window.scene = source
bpy.context.view_layer.update()
depsgraph = bpy.context.evaluated_depsgraph_get()
captured = []
for name in manifest['visible_meshes']:
    obj = source.objects[name]
    mesh = bpy.data.meshes.new_from_object(obj.evaluated_get(depsgraph), depsgraph=depsgraph)
    captured.append((name, mesh, obj.matrix_world.copy()))

scene = bpy.data.scenes.new('Neutral geometry captures')
bpy.context.window.scene = scene
points = []
for name, mesh, matrix in captured:
    obj = bpy.data.objects.new(name + ' capture', mesh)
    scene.collection.objects.link(obj)
    obj.matrix_world = matrix
    obj.color = (0.55, 0.57, 0.61, 1)
    if name == 'Canopy':
        obj.color = (0.12, 0.15, 0.20, 1)
    elif name == 'Power Nacelle Core':
        obj.color = (0.32, 0.40, 0.47, 1)
    points.extend(matrix @ v.co for v in mesh.vertices)

minimum = Vector(tuple(min(p[i] for p in points) for i in range(3)))
maximum = Vector(tuple(max(p[i] for p in points) for i in range(3)))
center = (minimum + maximum) / 2
diameter = (maximum - minimum).length
scene.render.engine = 'BLENDER_WORKBENCH'
scene.render.resolution_x = 1400
scene.render.resolution_y = 1100
scene.render.resolution_percentage = 100
scene.render.image_settings.file_format = 'PNG'
scene.render.film_transparent = False
scene.display.shading.light = 'STUDIO'
scene.display.shading.color_type = 'OBJECT'
scene.display.shading.background_type = 'WORLD'
scene.world = bpy.data.worlds.new('Capture background')
scene.world.color = (0.065, 0.075, 0.095)
scene.display.shading.show_shadows = True
scene.display.shading.show_cavity = True
scene.display.shading.cavity_type = 'BOTH'
scene.display.shading.curvature_ridge_factor = 1.0
scene.display.shading.curvature_valley_factor = 1.0
scene.display.shading.show_object_outline = True
scene.display.shading.object_outline_color = (0.025, 0.03, 0.04)
scene.display.shading.show_specular_highlight = False
scene.view_settings.view_transform = 'Standard'
scene.view_settings.look = 'None'
camera_data = bpy.data.cameras.new('Capture camera')
camera = bpy.data.objects.new('Capture camera', camera_data)
scene.collection.objects.link(camera)
scene.camera = camera
camera_data.type = 'ORTHO'
camera_data.clip_start = 0.001
camera_data.clip_end = diameter * 20

views = [
    ('01-top', (0, 0, 1), 'Y'),
    ('02-front-quarter', (1.15, 1.6, 1.25), 'Y'),
    ('03-rear-quarter', (-1.15, -1.6, 1.05), 'Y'),
    ('04-underside', (0, 0, -1), 'Y'),
    ('05-side', (1, 0, 0), 'Y'),
    ('06-front', (0, 1, 0), 'Y'),
]
records = []
aspect = scene.render.resolution_x / scene.render.resolution_y
for label, direction, up in views:
    direction = Vector(direction).normalized()
    camera.location = center + direction * diameter * 3
    camera.rotation_euler = (-direction).to_track_quat('-Z', up).to_euler()
    bpy.context.view_layer.update()
    inverse = camera.matrix_world.inverted()
    projected = [inverse @ p for p in points]
    width = max(p.x for p in projected) - min(p.x for p in projected)
    height = max(p.y for p in projected) - min(p.y for p in projected)
    camera_data.ortho_scale = max(width, height * aspect) * 1.18
    midpoint = Vector(((max(p.x for p in projected) + min(p.x for p in projected))/2,
                       (max(p.y for p in projected) + min(p.y for p in projected))/2, 0))
    camera.location += camera.matrix_world.to_3x3() @ midpoint
    path = out / (label + '.png')
    scene.render.filepath = str(path)
    bpy.ops.render.render(write_still=True)
    records.append({'view':label,'path':str(path),'direction':list(direction),'orthographic_scale':camera_data.ortho_scale})

(out / 'capture-manifest.json').write_text(json.dumps({'source':manifest,'bounds':[list(minimum),list(maximum)],'views':records},indent=2),encoding='utf-8')
print(json.dumps({'captures':records}))
