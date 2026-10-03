import bpy
import json
from mathutils import Matrix, Vector
from pathlib import Path

OUT = Path('D:/amind/git/astronomical-home/results/valis-wing-motion/v02')
assert bpy.context.mode == 'OBJECT'
sources = [o for o in bpy.context.scene.objects if o.type == 'MESH']
visible = [o for o in sources if not o.hide_get() and not o.hide_viewport]
root = bpy.data.objects['Valis symmetry origin']
collection = bpy.data.collections['Valis - editable parts']
upper = ['10 upper swept wings', '11 upper wing gray planes', '12 upper wing lavender tips', '70 shoulder pivot housing', '71 shoulder lavender cap']
lower = ['20 lower swept blades', '21 lower blade lavender tips', '21 lower blade lavender tips underside', '22 lower blade dark inner facets']
aft = ['50 aft split prongs']
groups = [('Body', [o.name for o in visible if o.name not in upper + lower + aft], 0)]
groups += [(f'Wing {level}.{side}', names, sign) for level, names in [('upper', upper), ('lower', lower)] for side, sign in [('R', 1), ('L', -1)]]
groups += [('Aft prongs', aft, 0)]
bpy.ops.wm.save_as_mainfile(filepath=str(OUT / 'Valis-before-grouping.blend'), copy=True)

cache = {}
for obj in visible:
    deps = bpy.context.evaluated_depsgraph_get()
    shaped = bpy.data.meshes.new_from_object(obj.evaluated_get(deps), preserve_all_data_layers=True, depsgraph=deps)
    original = None
    if obj.data.shape_keys:
        key = obj.data.shape_keys.key_blocks['Thin sculpted profile']
        value = key.value
        key.value = 0
        bpy.context.view_layer.update()
        deps = bpy.context.evaluated_depsgraph_get()
        original = bpy.data.meshes.new_from_object(obj.evaluated_get(deps), preserve_all_data_layers=True, depsgraph=deps)
        key.value = value
        bpy.context.view_layer.update()
        assert len(original.vertices) == len(shaped.vertices)
    cache[obj.name] = (obj, shaped, original)

report = []
new_objects = []
for name, names, sign in groups:
    vertices, originals, faces, mat_ids, smooth, uv, normals = [], [], [], [], [], [], []
    materials, part_vertices = [], {}
    for source_name in names:
        obj, mesh, old = cache[source_name]
        matrix = obj.matrix_world
        normal_matrix = matrix.to_3x3().inverted().transposed()
        mapping = {}
        part_vertices[source_name] = []
        for face in mesh.polygons:
            center = matrix @ face.center
            if sign and center.x * sign <= 0:
                continue
            indices = []
            for loop_index in face.loop_indices:
                index = mesh.loops[loop_index].vertex_index
                if index not in mapping:
                    mapping[index] = len(vertices)
                    vertices.append(matrix @ mesh.vertices[index].co)
                    originals.append(matrix @ (old.vertices[index].co if old else mesh.vertices[index].co))
                    part_vertices[source_name].append(mapping[index])
                indices.append(mapping[index])
                uv.append(tuple(mesh.uv_layers['ValisPaintUV'].data[loop_index].uv))
                normals.append((normal_matrix @ mesh.corner_normals[loop_index].vector).normalized())
            faces.append(indices)
            material = mesh.materials[face.material_index]
            if material not in materials:
                materials.append(material)
            mat_ids.append(materials.index(material))
            smooth.append(face.use_smooth)
    pivot = Vector((0, 0, 0))
    if sign:
        if name.startswith('Wing upper'):
            housing = part_vertices['70 shoulder pivot housing']
            pivot = sum((vertices[i] for i in housing), Vector()) / len(housing)
        else:
            # The innermost blade edge is its physical attachment.
            points = [vertices[i] for i in part_vertices['20 lower swept blades']]
            min_x = min(p.x * sign for p in points)
            attachment = [p for p in points if p.x * sign < min_x + .12]
            pivot = sum(attachment, Vector()) / len(attachment)
    mesh = bpy.data.meshes.new(name)
    mesh.from_pydata([p - pivot for p in vertices], [], faces)
    for material in materials:
        mesh.materials.append(material)
    for face, mat_id, shading in zip(mesh.polygons, mat_ids, smooth):
        face.material_index = mat_id
        face.use_smooth = shading
    layer = mesh.uv_layers.new(name='ValisPaintUV')
    for item, value in zip(layer.data, uv):
        item.uv = value
    mesh.normals_split_custom_set(normals)
    obj = bpy.data.objects.new(name, mesh)
    collection.objects.link(obj)
    obj.parent = root
    obj.matrix_world = Matrix.Translation(pivot)
    for part_name, indices in part_vertices.items():
        group = obj.vertex_groups.new(name=part_name)
        group.add(indices, 1, 'REPLACE')
    shaped_parts = [n for n in names if cache[n][2] is not None]
    if shaped_parts:
        obj.shape_key_add(name='Sculpted profile')
        key = obj.shape_key_add(name='Original plate profile')
        for item, p in zip(key.data, originals):
            item.co = p - pivot
        key.value = 0
    obj['assembly_parts'] = json.dumps(names)
    obj['flight_pose'] = 'Swept'
    new_objects.append(obj)
    bpy.context.view_layer.update()
    error = max((obj.matrix_world @ v.co - p).length for v, p in zip(mesh.vertices, vertices))
    assert error < 2e-6, (name, error)
    assert len(mesh.loops) == len(uv)
    report.append({'name':name, 'parts':names, 'vertices':len(mesh.vertices), 'faces':len(mesh.polygons), 'materials':[m.name for m in materials], 'pivot':list(pivot), 'max_position_error':error})

reference = bpy.data.collections.new('Reference - pre-assembly parts')
bpy.context.scene.collection.children.link(reference)
for obj in sources:
    reference.objects.link(obj)
    for old_collection in list(obj.users_collection):
        if old_collection != reference:
            old_collection.objects.unlink(obj)
    obj.hide_render = True
layer = bpy.context.view_layer.layer_collection.children[reference.name]
layer.exclude = True
collection.name = 'Valis - assemblies'
bpy.ops.object.select_all(action='DESELECT')
for obj in new_objects:
    obj.select_set(True)
bpy.context.view_layer.objects.active = new_objects[1]
for obj, shaped, old in cache.values():
    bpy.data.meshes.remove(shaped)
    if old:
        bpy.data.meshes.remove(old)
bpy.context.view_layer.update()
OUT.joinpath('assembly-proof.json').write_text(json.dumps(report, indent=2))
bpy.ops.wm.save_as_mainfile(filepath=str(OUT / 'Valis-profile-assemblies.blend'), copy=True)
result = {'assemblies':report, 'visible_meshes':len(new_objects), 'reference_parts':len(sources), 'saved':str(OUT / 'Valis-profile-assemblies.blend')}
