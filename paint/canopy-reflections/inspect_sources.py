import json
from pathlib import Path
import sys
import bpy
import numpy as np

root = Path(__file__).resolve().parents[2]
sys.path.insert(0, str(root / 'art/tools/ship'))
import ship_source

out = Path(__file__).parent
report = {}
for ship in ('Crimson', 'Nightshade', 'Valis', 'Vanguard'):
    source = root / 'art/ships' / ship.lower() / (ship + '.blend')
    scene = ship_source.open_source(source)
    fingerprint = ship_source.fingerprint(scene)
    lock_path = source.parent / 'lock.json'
    if lock_path.exists():
        locked = json.loads(lock_path.read_text())
        assert locked['geometry'] == fingerprint['components']['geometry'], ship + ' geometry changed'
    evaluation = ship_source.Evaluation(scene)
    parts = []
    for name, obj in evaluation.copies.items():
        if obj.type != 'MESH':
            continue
        materials = [m.name if m else None for m in evaluation.materials(name)]
        if not any(s in (name + ' ' + str(materials)).lower() for s in ('canopy', 'glass', 'cockpit')):
            continue
        mesh = evaluation.mesh(name)
        co, corners, normals, triangles = ship_source.mesh_arrays(mesh)
        matrix = np.array(evaluation.to_space(name), np.float64)
        points = co @ matrix[:3, :3].T + matrix[:3, 3]
        uv = ship_source.read_array(mesh.uv_layers['PaintUV'].uv, 'vector', np.float32, 2).reshape(-1, 2) if 'PaintUV' in mesh.uv_layers else np.zeros((len(corners), 2))
        normals = normals @ np.linalg.inv(matrix[:3, :3])
        normals /= np.maximum(np.linalg.norm(normals, axis=1, keepdims=True), 1e-20)
        safe = name.replace('/', '_')
        np.savez(out / (ship + '-' + safe + '.npz'), co=points, corners=corners, normals=normals, triangles=triangles, uv=uv,
                 material_index=ship_source.read_array(mesh.loop_triangles, 'material_index', np.int32))
        parts.append(dict(name=name, materials=materials, vertices=len(co), triangles=len(triangles),
                          bounds=[points.min(0).tolist(), points.max(0).tolist()], uv_bounds=[uv.min(0).tolist(), uv.max(0).tolist()],
                          normal_z=[float(normals[:,2].min()), float(normals[:,2].max())]))
        bpy.data.meshes.remove(mesh)
    report[ship] = dict(fingerprint=fingerprint['components'], parts=parts)
    print(ship + ': ' + json.dumps(parts))
(out / 'source-inspection.json').write_text(json.dumps(report, indent=2))
