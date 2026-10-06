import bpy,json
from pathlib import Path
from mathutils import Vector,Matrix
root=Path('D:/amind/git/agent-1')
scratch=Path('C:/Users/amind/.codex/visualizations/2026/10/06/01a11013-205d-7b23-b294-06d76bc3b8ba')
base=json.loads((scratch/'canopy-round-before.json').read_text())['Rebuilt pitched hull']
ob=bpy.data.objects['Rebuilt pitched hull'];iv=ob.matrix_world.inverted();mat=Matrix(base['matrix'])
assert bpy.context.mode=='OBJECT' and not bpy.data.is_dirty
before=[ob.matrix_world@v.co for v in ob.data.vertices];old=[mat@Vector(v) for v in base['vertices']]
delta={}
for boundary,ids in [(38,[39,40,41]),(46,[45,44,43]),(51,[52,53,54]),(59,[58,57,56])]:
    d=before[boundary]-old[boundary]
    for i,w in zip(ids,[.72,.33,.04]):delta[i]=d*w
neighbors={i:[] for i in range(len(before))}
for e in ob.data.edges:
    a,b=e.vertices;neighbors[a].append(b);neighbors[b].append(a)
for idx in range(388,422):
    ends=[i for i in neighbors[idx] if i<388]
    if len(ends)==2 and any(i in delta for i in ends):delta[idx]=sum((delta.get(i,Vector()) for i in ends),Vector())*.5
for i,d in delta.items():ob.data.vertices[i].co=iv@(before[i]+d)
ob.data.update()
out=root/'results/nightshade-rounded-canopy/round-03';out.mkdir(parents=True,exist_ok=True)
(out/'edit-audit.json').write_text(json.dumps({'changed_part':ob.name,'changed_vertices':sorted(delta),
    'reason':'Blend the aperture displacement across its existing adjacent rail faces.'},indent=2))
bpy.context.preferences.filepaths.save_version=0;bpy.ops.wm.save_as_mainfile(filepath=bpy.data.filepath)
result={'saved':bpy.data.filepath,'rail_vertices':len(delta)}
