import bpy,json
from pathlib import Path
root=Path('D:/amind/git/agent-1')
bpy.ops.wm.open_mainfile(filepath=str(root/'art/ships/nightshade/Nightshade.blend'))
names=json.loads(Path('C:/Users/amind/.codex/visualizations/2026/10/06/01a11013-205d-7b23-b294-06d76bc3b8ba/pod-owner-geometry.json').read_text())
record={n:{'faces':[sorted(p.vertices) for p in bpy.data.objects[n].data.polygons],
    'matrix':[list(r) for r in bpy.data.objects[n].matrix_basis],
    'mods':[(m.name,m.type) for m in bpy.data.objects[n].modifiers]} for n in names}
(root/'results/nightshade-glass-pod/pre-taper/connectivity.json').write_text(json.dumps(record))
print('PRE_TAPER_CONNECTIVITY_CAPTURED')
