"""Load rejected console.blend; report whether its display has parallel opposite edges. No save."""
import bpy,json,math
from pathlib import Path
o=bpy.data.objects['Main display | approved B graphics']
v=[o.matrix_world@p.co for p in o.data.vertices]
edges=[(v[b]-v[a]).normalized() for a,b in [(0,1),(2,3),(4,5),(6,7)]]
angles=[math.degrees(math.acos(max(-1,min(1,abs(edges[a].dot(edges[b]))))))for a,b in [(0,2),(1,3)]]
report={'oppositeEdgeDivergenceDegrees':angles,'rectangularFace':max(angles)<.01,'method':'World-space directions of the four long sides of the chamfered display face.'}
(Path(__file__).parent/'rejected-face-diagnosis.json').write_text(json.dumps(report,indent=2))
print(json.dumps(report))
