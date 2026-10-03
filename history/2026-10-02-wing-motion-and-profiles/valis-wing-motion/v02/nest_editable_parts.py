import bpy
import hashlib
import json
from pathlib import Path
from mathutils import Matrix, Vector

OUT = Path('D:/amind/git/astronomical-home/results/valis-wing-motion/v02')
assert bpy.context.mode == 'OBJECT'
assert all(json.loads(OUT.joinpath('assembly-edits-check.json').read_text()).values())
root = bpy.data.objects['Valis symmetry origin']
editable = bpy.data.collections['Valis - assemblies']
reference = bpy.data.collections['Reference - pre-assembly parts']
parts = list(reference.objects)
world_before = {o.name:o.matrix_world.copy() for o in parts}
def geometry_hash(obj):
    keys = [(k.name, [tuple(v.co) for v in k.data]) for k in obj.data.shape_keys.key_blocks] if obj.data.shape_keys else []
    return hashlib.sha256(json.dumps([[tuple(v.co) for v in obj.data.vertices], [tuple(p.vertices) for p in obj.data.polygons], [tuple(v.uv) for v in obj.data.uv_layers['ValisPaintUV'].data], keys]).encode()).hexdigest()
geometry_before = {o.name:geometry_hash(o) for o in parts}
joined = [bpy.data.objects[n] for n in ['Body', 'Aft prongs', 'Wing upper.R', 'Wing upper.L', 'Wing lower.R', 'Wing lower.L']]
upper_pivot = joined[2].matrix_world.translation.copy()
lower_pivot = joined[4].matrix_world.translation.copy()
for obj in joined:
    obj.name = 'Joined preview - ' + obj.name
    reference.objects.link(obj)
    editable.objects.unlink(obj)
    obj.hide_render = True
for obj in parts:
    editable.objects.link(obj)
    reference.objects.unlink(obj)
    obj.hide_render = False
reference.name = 'Reference - joined preview'
editable.name = 'Valis - editable hierarchy'
bpy.context.view_layer.update()
for obj in parts:
    obj.hide_set(False)

orientation = root.matrix_world.to_3x3().normalized().to_4x4()
groups = {}
def group(name, parent, pivot=(0, 0, 0)):
    obj = bpy.data.objects.new(name, None)
    editable.objects.link(obj)
    obj.empty_display_type = 'PLAIN_AXES'
    obj.empty_display_size = .18
    obj.parent = parent
    obj.matrix_parent_inverse = parent.matrix_world.inverted()
    obj.matrix_basis = Matrix.Translation(Vector(pivot)) @ orientation
    obj['group_control'] = 'Move this parent to edit the complete assembly; expand it to edit individual parts.'
    bpy.context.view_layer.update()
    groups[name] = obj
    return obj

def attach(names, parent):
    for name in names:
        obj = bpy.data.objects[name]
        old_parent = obj.parent.matrix_world.copy()
        old_inverse = obj.matrix_parent_inverse.copy()
        obj.parent = parent
        obj.matrix_parent_inverse = parent.matrix_world.inverted() @ old_parent @ old_inverse

body = group('Body', root)
attach(['01 central fuselage', '08 ventral armor'], group('Fuselage', body))
attach(['02 black canopy', '03 canopy side cheeks'], group('Canopy', body))
attach(['04 dorsal armor', '05 dorsal shoulder bevel', '06 dorsal lavender rails', '07 rear crest'], group('Dorsal armor', body))
attach(['60 engine spine', '61 engine side fairings', '62 engine raised ribs', '64 exhaust rim'], group('Engine', body))
attach(['40 forward outriggers', '41 outrigger lavender toes', '42 outrigger inner sockets'], group('Forward outriggers', body))
attach(['51 midship auxiliary vanes', '52 auxiliary vane feet', '53 auxiliary vane roots'], group('Auxiliary vanes', body))
flight = group('Flight surfaces', root)
upper = group('Upper wings', flight, upper_pivot)
attach(['10 upper swept wings', '11 upper wing gray planes', '12 upper wing lavender tips'], group('Wing panels', upper, upper_pivot))
attach(['13 charcoal trailing spars', '12 upper wing lavender tips underside'], group('Trailing spars', upper, upper_pivot))
attach(['70 shoulder pivot housing', '71 shoulder lavender cap'], group('Shoulder pivots', upper, upper_pivot))
attach(['20 lower swept blades', '21 lower blade lavender tips', '21 lower blade lavender tips underside', '22 lower blade dark inner facets'], group('Lower wings', flight, lower_pivot))
attach(['50 aft split prongs'], group('Aft prongs', root))
bpy.context.view_layer.update()
errors = {o.name:max(abs(o.matrix_world[r][c] - world_before[o.name][r][c]) for r in range(4) for c in range(4)) for o in parts}
assert max(errors.values()) < 2e-6, errors
assert all(geometry_hash(o) == geometry_before[o.name] for o in parts)
assert all(m.mirror_object == root for o in parts for m in o.modifiers if m.type == 'MIRROR')
assert len(parts) == 30
bpy.ops.object.select_all(action='DESELECT')
groups['Trailing spars'].select_set(True)
bpy.context.view_layer.objects.active = groups['Trailing spars']
report = {'visible_mesh_parts':len(parts), 'parent_groups':{n:[o.name for o in g.children] for n,g in groups.items()}, 'restored_parts':['13 charcoal trailing spars', '12 upper wing lavender tips underside'], 'max_transform_error':max(errors.values()), 'geometry_uv_and_profile_keys_unchanged':True, 'live_mirror_modifiers_preserved':True}
OUT.joinpath('nested-groups-proof.json').write_text(json.dumps(report, indent=2))
bpy.ops.wm.save_as_mainfile(filepath=str(OUT / 'Valis-profile-nested.blend'), copy=True)
result = report
