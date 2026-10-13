import sys
import hashlib
import json
from pathlib import Path
import bpy
from mathutils import Vector

sys.path.insert(0, 'D:/amind/git/agent-3/art/tools/ship')
import ship_render
import ship_source

args = sys.argv[sys.argv.index('--') + 1:]
source, destination = [Path(arg).resolve() for arg in args[:2]]
destination.mkdir(parents=True, exist_ok=True)
source_hash = hashlib.sha256(source.read_bytes()).hexdigest()
evaluation, bounds = ship_render.prepare(source)
scene = evaluation.scene
visible_parts = [o.name for o in bpy.data.scenes[0].objects if o.type == 'MESH' and not o.hide_render]
evaluation.show_only(visible_parts)
ship_source.apply_review_preset(scene, 1000)
shading = scene.display.shading
shading.light = 'STUDIO'
shading.studiolight_rotate_z = 0.3
shading.show_shadows = True
shading.show_cavity = True
shading.cavity_type = 'BOTH'
shading.curvature_ridge_factor = 1.1
shading.curvature_valley_factor = 1.0
shading.show_object_outline = False
shading.background_type = 'WORLD'
scene.world.color = (0.11, 0.12, 0.14)
scene.display.render_aa = '32'
camera = bpy.data.objects.new('Review only camera', bpy.data.cameras.new('Review only camera'))
scene.collection.objects.link(camera)
scene.camera = camera
camera.data.type = 'ORTHO'
camera.data.clip_start = 0.01
camera.data.clip_end = 100
views = [
    ('side-aligned', (-1, 0, 0), (0, 0, 1), (0, 0, 1.8), 11.1, 1400, 700),
    ('top-aligned', (0, 0, 1), (1, 0, 0), (0, 0, 1.5), 11.1, 1400, 820),
    ('hero', (10, 15, 8.5), (0, 0, 1), (0, 0, 1.5), 14.0, 1400, 1050),
    ('rear-quarter', (9, -16, 7), (0, 0, 1), (0, -0.4, 1.5), 13.5, 1400, 1050),
    ('rear', (0, -1, 0), (0, 0, 1), (0, 0, 1.7), 7.5, 1100, 750),
    ('side-studio', (1, 0, 0), (0, 0, 1), (0, 0, 1.5), 11.6, 1100, 750),
    ('front-studio', (0, 1, 0), (0, 0, 1), (0, 0, 1.6), 7, 1000, 750),
    ('top-studio', (0, 0, 1), (0, 1, 0), (0, 0, 1.5), 11.5, 1000, 1000),
]
views = [v for v in views if v[0] in ['top-studio', 'side-aligned', 'hero']]
for name, direction, up, center, scale, width, height in views:
    camera.matrix_world = ship_render.camera_matrix(direction, up, center, 30)
    camera.data.ortho_scale = scale
    scene.render.resolution_x = width
    scene.render.resolution_y = height
    scene.render.filepath = str(destination / (name + '.png'))
    bpy.ops.render.render(write_still=True)
assert hashlib.sha256(source.read_bytes()).hexdigest() == source_hash
(destination / 'studio-report.json').write_text(json.dumps({'source_sha256': source_hash, 'source_unchanged': True, 'views': [x[0] for x in views]}, indent=2))

