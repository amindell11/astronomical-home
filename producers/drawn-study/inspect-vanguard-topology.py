import bpy,json
from pathlib import Path
bpy.ops.wm.open_mainfile(filepath='D:/amind/git/agent-4/art/ships/vanguard/drawn-study/VanguardStudy.blend')
s=bpy.data.scenes['Vanguard - Texture MVP']
report={}
for name in ['MVP Fuselage','MVP Sparrow Tail','MVP Cockpit.001']:
 o=s.objects[name]; verts=[o.matrix_world@v.co for v in o.data.vertices]
 report[name]={'vertices':[[round(c,5) for c in v] for v in verts], 'faces':[list(f.vertices) for f in o.data.polygons]}
Path('D:/amind/git/agent-4/results/vanguard-hull-topology.json').write_text(json.dumps(report))
