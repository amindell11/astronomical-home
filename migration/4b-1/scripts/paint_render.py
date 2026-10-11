"""4b-1 paint render: EEVEE top view of a Valis source with its own materials, nose up on screen.

blender -b --factory-startup --python-exit-code 1 -P paint_render.py -- <Valis.blend> <out.png> [size]
Never saves the source. The nose is +Y when the file has a SymmetryOrigin root, else -Y (pre-edit);
the camera's up vector follows the nose, so screen right is the pilot's right in both. Only objects
with render visibility draw (the joined previews and the collider are render-hidden).
"""
import sys

import bpy
from mathutils import Matrix, Vector
import numpy as np

args = sys.argv[sys.argv.index("--") + 1:]
path, out = args[0], args[1]
size = int(args[2]) if len(args) > 2 else 768
bpy.ops.wm.open_mainfile(filepath=path, load_ui=False, use_scripts=False)
scene = bpy.context.scene
nose = 1.0 if "SymmetryOrigin" in bpy.data.objects else -1.0
depsgraph = bpy.context.evaluated_depsgraph_get()
points = []
for obj in scene.objects:
    if obj.type != "MESH" or obj.hide_render:
        continue
    evaluated = obj.evaluated_get(depsgraph)
    mesh = evaluated.to_mesh()
    co = np.array([v.co for v in mesh.vertices], np.float64)
    m = np.array(obj.matrix_world, np.float64)
    points.append(co @ m[:3, :3].T + m[:3, 3])
    evaluated.to_mesh_clear()
points = np.concatenate(points)
low, high = points.min(0), points.max(0)
center = (low + high) / 2
camera = bpy.data.objects.new("paint top camera", bpy.data.cameras.new("paint top camera"))
camera.data.type = "ORTHO"
camera.data.ortho_scale = float(max(high[0] - low[0], high[1] - low[1])) * 1.06
camera.data.clip_end = 1000
up = Vector((0, nose, 0))
right = up.cross(Vector((0, 0, 1)))
camera.matrix_world = Matrix.Translation(Vector((center[0], center[1], high[2] + 20))) @ Matrix(
    (right, up, Vector((0, 0, 1)))).transposed().to_4x4()
scene.collection.objects.link(camera)
scene.camera = camera
sun = bpy.data.objects.new("paint sun", bpy.data.lights.new("paint sun", "SUN"))
sun.data.energy = 3.0
sun.rotation_euler = (0.35, 0.2, 0.0)
scene.collection.objects.link(sun)
for engine in ("BLENDER_EEVEE", "BLENDER_EEVEE_NEXT"):
    try:
        scene.render.engine = engine
        break
    except TypeError:
        continue
if scene.world is None:
    scene.world = bpy.data.worlds.new("paint world")
scene.world.use_nodes = False
scene.world.color = (0.045, 0.05, 0.075)
scene.render.resolution_x = scene.render.resolution_y = size
scene.render.resolution_percentage = 100
scene.render.film_transparent = False
scene.render.image_settings.file_format = "PNG"
scene.view_settings.view_transform = "Standard"
scene.render.filepath = out
bpy.ops.render.render(write_still=True)
print(f"PAINT nose={nose:+.0f} engine={scene.render.engine} out={out}", file=sys.stderr)
