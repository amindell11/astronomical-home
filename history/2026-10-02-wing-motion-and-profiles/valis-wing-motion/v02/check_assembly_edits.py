import bpy
import hashlib
import json
from pathlib import Path

out = Path(__file__).parent
live = json.loads(out.joinpath('live-assembly-hashes.json').read_text())
def digest(obj):
    points = [tuple(v.co) for v in obj.data.vertices]
    faces = [tuple(p.vertices) for p in obj.data.polygons]
    uv = [tuple(v.uv) for v in obj.data.uv_layers['ValisPaintUV'].data]
    keys = [(k.name, [tuple(v.co) for v in k.data]) for k in obj.data.shape_keys.key_blocks] if obj.data.shape_keys else []
    return hashlib.sha256(json.dumps([points, faces, uv, keys]).encode()).hexdigest()
report = {name:digest(bpy.data.objects[name]) == value for name, value in live.items()}
out.joinpath('assembly-edits-check.json').write_text(json.dumps(report, indent=2))
print(json.dumps(report))
