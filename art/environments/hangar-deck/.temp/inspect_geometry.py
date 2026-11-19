from pathlib import Path
import bpy
from mathutils import Vector
from bpy_extras.object_utils import world_to_camera_view

root = Path(__file__).resolve().parents[1]
bpy.ops.wm.open_mainfile(filepath=str(root / 'deck.blend'))
scene = bpy.context.scene
body = bpy.data.objects['Deck | joined closed structure']
for label, condition in [
    ('outer pod', lambda v: 8.9 < v.x < 11.8 and 0 < v.z < 6),
    ('header end', lambda v: v.x > 12 and v.z > 8),
    ('band end', lambda v: v.x > 8.8 and v.z < 1.5),
]:
    points = {tuple(round(c, 4) for c in body.matrix_world @ v.co)
              for v in body.data.vertices if condition(body.matrix_world @ v.co)}
    print(label)
    for p in sorted(points):
        screen = world_to_camera_view(scene, scene.camera, Vector(p))
        print(p, 'pixel', round(screen.x * 1920, 1), round((1-screen.y)*1080,1))
