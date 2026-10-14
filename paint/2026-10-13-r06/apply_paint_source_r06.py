import bpy, sys, json, hashlib
from pathlib import Path

sys.path.insert(0, 'D:/amind/git/agent-3/art/tools/ship')
import ship_source

source = Path('D:/amind/git/agent-3/art/ships/bulwark/Bulwark.blend')
folder = source.parent
stage = Path('D:/amind/git/agent-3/results/bulwark/paint/2026-10-13-r06')
baseline = '057bc5e39d37d00c172aea4c52c35b7374f098b23b199d0b517f82b6b6bc1e45'
assert hashlib.sha256(source.read_bytes()).hexdigest() == baseline
src = ship_source.open_source(source)
before = ship_source.fingerprint(src)
glass_color = bpy.data.materials['Bulwark Glass'].node_tree.nodes['Base coat'].inputs[1].default_value[:]
obj = src.objects['07 Side insets']
assert len(obj.data.materials) == 5
assert {p.material_index for p in obj.data.polygons} == {3}
assert 'Bulwark Metal' not in bpy.data.materials
mask_hashes = {name: hashlib.sha256((folder/'layers'/(name+'.png')).read_bytes()).hexdigest()
               for name in ('Shadow', 'Light', 'Ink', 'BaseRegion')}

def rgba(h):
    values = [int(h[i:i+2], 16)/255 for i in (1, 3, 5)]
    return tuple(v/12.92 if v <= .04045 else ((v+.055)/1.055)**2.4 for v in values)+(1,)

metal = bpy.data.materials['Bulwark Glass'].copy()
metal.name = 'Bulwark Metal'
metal.diffuse_color = rgba('#999999')
base = metal.node_tree.nodes['Base coat']
base.inputs[1].default_value = rgba('#999999')
base.label = 'Recolourable neutral metal canisters'
bsdf = next(n for n in metal.node_tree.nodes if n.type == 'BSDF_PRINCIPLED')
bsdf.inputs['Roughness'].default_value = .6
bsdf.inputs['Metallic'].default_value = .75
obj.data.materials.append(metal)
for face in obj.data.polygons:
    face.material_index = 5

after = ship_source.fingerprint(src)
for component in ('geometry', 'uv'):
    assert before['components'][component] == after['components'][component]
for name in ('Upper flap deployment', 'Lower flap deployment'):
    assert src.objects[name].rotation_euler.x == 0
assert bpy.data.materials['Bulwark Glass'].node_tree.nodes['Base coat'].inputs[1].default_value[:] == glass_color
bpy.context.preferences.filepaths.save_version = 0
bpy.ops.wm.save_as_mainfile(filepath=str(source), check_existing=False)
sha = hashlib.sha256(source.read_bytes()).hexdigest()

palette = json.loads((folder/'palettes.json').read_text())
palette['palettes']['plum-bone']['Metal'] = '#999999'
palette['material_roles'] = {'Metal': 'Side canisters: neutral metal hardware', 'Glass': 'Cockpit glazing only'}
(folder/'palettes.json').write_text(json.dumps(palette, indent=2))
settings = json.loads((folder/'paint-settings.json').read_text())
settings['material_finishes'] = {'Metal': {'blender_metallic': .75, 'blender_roughness': .6,
    'unity_specular_strength': .2, 'unity_shadow_color': [.74, .74, .74]}}
(folder/'paint-settings.json').write_text(json.dumps(settings, indent=2))
for name, digest in mask_hashes.items():
    assert hashlib.sha256((folder/'layers'/(name+'.png')).read_bytes()).hexdigest() == digest
audit = {'source_sha256': sha, 'previous_paint_source_sha256': baseline,
    'baseline_commit': 'c6768e32db3b8bd0cd89e92998d24012bdbcae7b',
    'geometry_unchanged': True, 'uvs_unchanged': True,
    'geometry_fingerprint': after['components']['geometry'], 'uv_fingerprint': after['components']['uv'],
    'materials_changed': ['07 Side insets: Glass -> Metal'], 'cockpit_material_unchanged': True,
    'paint_masks_unchanged': True, 'mask_sha256': mask_hashes,
    'separate_editable_base_colours': palette['palettes']['plum-bone'],
    'approval': 'UV and plum direction approved; paint revision pending review'}
(stage/'paint-source-audit.json').write_text(json.dumps(audit, indent=2))
(stage/'mask-audit.json').write_text(json.dumps({'source_sha256': sha, 'masks_unchanged_from': baseline,
    'texture_size': 2048, 'mask_sha256': mask_hashes, 'colour_baked': False}, indent=2))
print(json.dumps(audit))
