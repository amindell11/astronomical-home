import bpy
import bmesh
import json
import runpy
from pathlib import Path
from mathutils import Matrix, Vector

stage = Path(__file__).parent
capture = runpy.run_path('D:/amind/git/agent-3/results/bulwark/detail-model/2026-10-14-organization/organize.py')['capture']
assert not bpy.data.is_dirty, 'Owner has new unsaved edits'
objects = {o.name: o for o in bpy.context.scene.objects}
obj = objects['Lower flaps - Fins']
mesh = obj.data
assert obj.mode == 'OBJECT' and len(mesh.vertices) == 12 and len(mesh.polygons) == 14
assert mesh.users == 1 and not mesh.shape_keys and not mesh.has_custom_normals

def edit_state():
    result = {}
    for ob in bpy.context.objects_in_mode:
        if ob.mode != 'EDIT':
            continue
        bm = bmesh.from_edit_mesh(ob.data)
        bm.verts.ensure_lookup_table()
        result[ob.name] = {
            'vertices': [list(v.co) for v in bm.verts],
            'faces': [[v.index for v in f.verts] for f in bm.faces],
            'uv': {u.name: [[list(loop[u].uv) for loop in f.loops] for f in bm.faces]
                   for u in bm.loops.layers.uv.values()},
        }
    return result

def corners():
    return {u.name: {str((p.index, mesh.loops[i].vertex_index)): list(u.data[i].uv)
                    for p in mesh.polygons for i in p.loop_indices}
            for u in mesh.uv_layers}

def world_vertices():
    return [obj.matrix_world @ v.co for v in mesh.vertices]

def painted_normals(indices):
    normal_matrix = obj.matrix_world.to_3x3().inverted().transposed()
    return [(normal_matrix @ mesh.polygons[i].normal).normalized().z for i in indices]

before = capture(objects)
editing_before = edit_state()
uv_before = corners()
vertices_before = world_vertices()
painted_faces = [p.index for p in mesh.polygons if p.material_index == 4 and abs(p.normal.z) > .5]
normal_before = painted_normals(painted_faces)
root_a = (vertices_before[0] + vertices_before[6]) / 2
root_b = (vertices_before[4] + vertices_before[10]) / 2
axis = (root_a - root_b).normalized()
normal = (Vector((0, 0, 1)) - axis * axis.z).normalized()
center = (root_a + root_b) / 2
reflection = Matrix.Identity(4)
for i in range(3):
    for j in range(3):
        reflection[i][j] -= 2 * normal[i] * normal[j]
reflection.translation = normal * (2 * normal.dot(center))
local_reflection = obj.matrix_world.inverted() @ reflection @ obj.matrix_world
(stage / 'before-data.json').write_text(json.dumps(before))
(stage / 'original-vertices.json').write_text(json.dumps([list(v.co) for v in mesh.vertices]))
mesh.transform(local_reflection)
mesh.flip_normals()
mesh.update()
bpy.context.view_layer.update()
after = capture(objects)
normal_after = painted_normals(painted_faces)
vertices_after = world_vertices()
changed = {n: [k for k in before['objects'][n] if before['objects'][n][k] != after['objects'][n][k]]
           for n in before['objects'] if before['objects'][n] != after['objects'][n]}
assert set(changed) == {obj.name}, changed
assert corners() == uv_before, 'Paint UV association changed'
assert before['images'] == after['images'], 'Paint pixels changed'
assert edit_state() == editing_before, 'Owner edit mesh changed'
assert max(normal_after) < -.5, normal_after
attachment_error = max(((vertices_after[0] + vertices_after[6]) / 2 - root_a).length,
                       ((vertices_after[4] + vertices_after[10]) / 2 - root_b).length)
distance_error = max(abs((vertices_after[i] - vertices_after[j]).length -
                         (vertices_before[i] - vertices_before[j]).length)
                     for i in range(12) for j in range(i))
assert attachment_error < 2e-6 and distance_error < 2e-6
report = {'changed': changed, 'painted_normal_z_before': normal_before,
          'painted_normal_z_after': normal_after, 'attachment_error': attachment_error,
          'shape_distance_error': distance_error, 'uv_corner_association_preserved': True,
          'paint_pixels_preserved': True, 'owner_edit_mesh_preserved': True,
          'after_images': after['images']}
(stage / 'verification.json').write_text(json.dumps(report, indent=2))
bpy.ops.wm.save_as_mainfile(filepath=bpy.data.filepath)
