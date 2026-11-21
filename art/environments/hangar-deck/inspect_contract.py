"""Read the source console without saving it; write its authoring contract locally."""
import json
import math
from pathlib import Path
import bpy

ROOT = Path(__file__).resolve().parent
bpy.ops.wm.open_mainfile(filepath=str(ROOT.parent / 'hangar-console/console.blend'))
scene = bpy.context.scene
camera = scene.camera
data = {'camera': {'name': camera.name, 'position': list(camera.location),
                   'matrix': [list(row) for row in camera.matrix_world],
                   'lens': camera.data.lens, 'sensor_width': camera.data.sensor_width,
                   'sensor_height': camera.data.sensor_height, 'sensor_fit': camera.data.sensor_fit,
                   'vertical_fov': math.degrees(2*math.atan(camera.data.sensor_width/(2*camera.data.lens*16/9)))},
        'render': {'line_thickness': scene.render.line_thickness,
                   'line_thickness_mode': scene.render.line_thickness_mode,
                   'use_freestyle': scene.render.use_freestyle}, 'linesets': [], 'parts': []}
for line in bpy.context.view_layer.freestyle_settings.linesets:
    item = {'name': line.name}
    for p in line.bl_rna.properties:
        if p.identifier.startswith(('select_', 'exclude_')) or p.identifier in ('edge_type_combination', 'edge_type_negation', 'visibility', 'qi_start', 'qi_end'):
            item[p.identifier] = getattr(line, p.identifier)
    item['collection'] = line.collection.name if line.collection else None
    item['members'] = [o.name for o in line.collection.objects] if line.collection else []
    item['style'] = {key: list(getattr(line.linestyle,key)) if key == 'color' else getattr(line.linestyle,key)
                     for key in ('color','thickness','thickness_position','alpha','caps')}
    data['linesets'].append(item)
for obj in scene.objects:
    if obj.name.startswith(('07 |', '08 |', '09 |', '19 | Launch', '30 |')):
        data['parts'].append({'name': obj.name, 'type': obj.type, 'position': list(obj.location),
                              'matrix': [list(row) for row in obj.matrix_world],
                              'dimensions': list(obj.dimensions),
                              'materials': [m.name if m else None for m in obj.data.materials] if hasattr(obj.data,'materials') else []})
data['triangles'] = sum(len(p.vertices)-2 for o in scene.objects if o.type == 'MESH' for p in o.data.polygons)
(ROOT / 'contract-inspection.json').write_text(json.dumps(data,indent=2),encoding='utf-8')
print(json.dumps({'camera':data['camera'],'render':data['render'],'linesets':data['linesets'],'triangles':data['triangles']}))
