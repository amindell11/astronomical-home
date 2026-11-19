from pathlib import Path
import json
import bpy
import bmesh

root = Path(__file__).resolve().parents[1]

def snapshot(path):
    bpy.ops.wm.open_mainfile(filepath=str(path))
    scene = bpy.context.scene
    hardware = [o for o in scene.objects if o.type == 'MESH' and not o.get('preview_only')]
    objects = {}
    for obj in hardware:
        if obj.name == 'Deck | joined closed structure':
            continue
        objects[obj.name] = {
            'role': obj.get('export_role'),
            'matrix': [list(row) for row in obj.matrix_world],
            'vertices': [list(v.co) for v in obj.data.vertices],
            'faces': [list(p.vertices) for p in obj.data.polygons],
            'materials': [m.name for m in obj.data.materials],
            'face_materials': [p.material_index for p in obj.data.polygons]}
    palette = {}
    for material in bpy.data.materials:
        if not material.name.startswith('Deck |'):
            continue
        shader = material.node_tree.nodes.get('Principled BSDF')
        palette[material.name] = {
            name: list(shader.inputs[name].default_value) if name.endswith('Color') else shader.inputs[name].default_value
            for name in ('Base Color', 'Emission Color', 'Emission Strength', 'Roughness', 'Specular IOR Level')}
    lines = {}
    for line in bpy.context.view_layer.freestyle_settings.linesets:
        lines[line.name] = {
            'collection': line.collection.name,
            'silhouette': line.select_silhouette, 'crease': line.select_crease,
            'thickness': line.linestyle.thickness, 'position': line.linestyle.thickness_position,
            'color': list(line.linestyle.color), 'alpha': line.linestyle.alpha}
    cam = scene.camera
    return {'objects': objects, 'palette': palette, 'linesets': lines,
            'camera': {'matrix': [list(row) for row in cam.matrix_world],
                       'lens': cam.data.lens, 'sensor': [cam.data.sensor_width, cam.data.sensor_height, cam.data.sensor_fit]},
            'names': sorted(o.name for o in hardware),
            'modifiers': [(o.name, m.type) for o in hardware for m in o.modifiers]}

original = snapshot(root / '.temp/reference.blend')
current = snapshot(root / 'deck.blend')
result = {key + '_unchanged': original[key] == current[key] for key in original}
(root / 'previews/preservation-check.json').write_text(json.dumps(result, indent=2))
print(json.dumps(result))
assert all(result.values()), result
