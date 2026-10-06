from pathlib import Path
import hashlib
import json
import sys

import bpy
import numpy as np

ROOT = Path('D:/amind/git/agent-1')
OUT = Path(__file__).parent / 'paint-round-01'
SOURCE = ROOT / 'art/ships/nightshade/Nightshade.blend'
sys.path.insert(0, str(ROOT / 'art/tools/ship'))
import ship_render
import ship_source

OUT.mkdir(exist_ok=True)
source_hash = hashlib.sha256(SOURCE.read_bytes()).hexdigest()
assert source_hash == 'bf15def6d4d91f4c9744262f3920b0f9e0ac58466a9aa362993ef9f8f61cf4d2'
scene = ship_source.open_source(SOURCE)
fingerprint = ship_source.fingerprint(scene)
visible = {obj.name for obj in ship_source.ship_objects(scene)
           if not obj.hide_get(view_layer=scene.view_layers[0]) and not obj.hide_render}
evaluation, bounds = ship_render.prepare(SOURCE)
names = [name for name, obj in evaluation.copies.items() if not obj.hide_render and name in visible]
if 'Upper small swept fins.001' in visible and 'Upper small swept fins.001' not in names:
    names.append('Upper small swept fins.001')
evaluation.show_only(names)
base_preset = ship_source.apply_review_preset

def preset(scene, size):
    base_preset(scene, size)
    shading = scene.display.shading
    shading.light = 'STUDIO'
    shading.studiolight_rotate_z = .25
    shading.show_cavity = True
    shading.cavity_type = 'WORLD'
    shading.cavity_ridge_factor = .25
    shading.cavity_valley_factor = .65
    shading.show_shadows = False
    shading.show_specular_highlight = True
    scene.world.color = (.47, .48, .50)

ship_source.apply_review_preset = preset
views = {'isometric': ((-1.5, 1.3, 2.3), (0, 0, 1)),
         'top': ((0, 0, 1), (0, 1, 0)),
         'side': ((-1, 0, 0), (0, 0, 1)),
         'underside': ((-1.5, 1.3, -2.3), (0, 0, -1))}
written = ship_render.render(evaluation, bounds, views, 1100, OUT)
ship_render.save_pixels(OUT / 'flat-contact-sheet.png', ship_render.grid(
    [ship_render.load_pixels(path) for path in written.values()], 2))
assert hashlib.sha256(SOURCE.read_bytes()).hexdigest() == source_hash
(OUT / 'render-audit.json').write_text(json.dumps({
    'source_commit': 'c1fdb2746421408761f4202dfdef32422c554fdc',
    'source_sha256_before': source_hash, 'source_sha256_after': source_hash,
    'geometry_unchanged': True, 'source_saved': False,
    'blender_version': bpy.app.version_string, 'visible_parts': sorted(names),
    'views': list(views), 'fingerprint': fingerprint,
    'prerequisites': {'detail_model_approved': True, 'flight_approved': False,
                      'geometry_lock_written': False, 'uv_approved': False,
                      'paint_concept_approved': False}}, indent=2), encoding='utf-8')
print('PAINT_REFERENCE_RENDERED', flush=True)
