import ast
import bpy
from pathlib import Path

OUT = Path('D:/amind/git/astronomical-home/results/valis-wing-motion/v02')
bpy.ops.wm.save_as_mainfile(filepath=str(OUT / 'Valis-restored-before-alignment.blend'), copy=True)
tree = ast.parse(OUT.joinpath('shape_profiles.py').read_text())
exec(compile(ast.Module(body=[n for n in tree.body if isinstance(n, (ast.Import, ast.ImportFrom, ast.FunctionDef))], type_ignores=[]), 'profile-functions', 'exec'))
main = bpy.data.objects['10 upper swept wings']
tip = bpy.data.objects['12 upper wing lavender tips underside']
spar = bpy.data.objects['13 charcoal trailing spars']
relative_spar = tip.matrix_world.inverted() @ spar.matrix_world
targets = [(tip, main.matrix_world.copy(), bpy.context.scene.objects['Wing panels']), (spar, main.matrix_world @ relative_spar, spar.parent)]
for obj, target, parent in targets:
    obj.parent = parent
    obj.matrix_parent_inverse = parent.matrix_world.inverted() @ target @ obj.matrix_basis.inverted()
bpy.context.view_layer.update()
frames = {}
for name, mesh_name, thickness in [('main', '10 upper swept wings', .14), ('spar', '13 charcoal trailing spars', .23)]:
    obj = main if name == 'main' else spar
    reference = bpy.data.objects.new('Profile reference', bpy.data.meshes[mesh_name])
    reference.matrix_world = obj.matrix_world
    reference.modifiers.new('Reference thickness', 'SOLIDIFY').thickness = thickness
    frames[name] = frame(reference)
    bpy.data.objects.remove(reference)
for obj, frame_name in [(tip, 'main'), (spar, 'spar')]:
    inverse = obj.matrix_world.inverted()
    profile = obj.data.shape_keys.key_blocks['Thin sculpted profile']
    for original, shaped in zip(obj.data.vertices, profile.data):
        p = obj.matrix_world @ original.co
        shaped.co = inverse @ Vector((p.x, p.y, section(np.array(p), frames[frame_name], False)))
    profile.value = 1
    obj.data.update()
bpy.context.view_layer.update()
bpy.ops.wm.save_as_mainfile(filepath=str(OUT / 'Valis-profile-nested.blend'), copy=True)
result = {'aligned_parts':[tip.name, spar.name], 'underside_tips_parent':tip.parent.name, 'mirrors_preserved':all(any(m.type=='MIRROR' for m in o.modifiers) for o in [main, tip, spar]), 'saved':str(OUT / 'Valis-profile-nested.blend')}
