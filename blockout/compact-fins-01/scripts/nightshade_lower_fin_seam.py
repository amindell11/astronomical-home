from pathlib import Path
import bpy,json,sys,numpy as np
from mathutils import Vector
root=Path('D:/amind/git/agent-1');out=root/'results/nightshade-sharp-fins/round-02'
sys.path.insert(0,str(root/'art/tools/ship'));import ship_source
before=ship_source.fingerprint(bpy.context.scene)
assert before==json.loads((out/'check/ship_check.json').read_text())['fingerprint']
fin=bpy.data.objects['Lower small swept fins'];seam=bpy.data.objects['Lower fin armor joint']
rows=np.array([v.co[:] for v in fin.data.vertices]).reshape(16,14,3)
assert len(seam.data.vertices)==18
transform=seam.matrix_world.inverted()@fin.matrix_world
def point(row_index,u):
    j=int(row_index);t=row_index-j
    points=[]
    for ring in rows[j:j+2]:
        section=ring[:7];fraction=(section[:,0]-section[0,0])/(section[-1,0]-section[0,0])
        points.append(np.array([np.interp(u,fraction,section[:,axis]) for axis in range(3)]))
    p=points[0]*(1-t)+points[1]*t;p[2]-=.0008
    return Vector(p)
for i,v in enumerate(seam.data.vertices):
    u=.38+(i//2)/8*.50
    v.co=transform@point(9.34+(-.022 if i%2==0 else .022),u)
seam.data.update()
after=ship_source.fingerprint(bpy.context.scene)
assert [n for n in before['parts'] if before['parts'][n]!=after['parts'][n]]==['Lower fin armor joint']
bpy.context.preferences.filepaths.save_version=0
bpy.ops.wm.save_as_mainfile(filepath=str(root/'art/ships/nightshade/Nightshade.blend'))
audit=json.loads((out/'edit-audit.json').read_text())
audit['lower_fin_seam']='Existing vertices reseated on the owner-tilted fin; topology and object transform retained'
(out/'edit-audit.json').write_text(json.dumps(audit,indent=2))
result={'saved':True,'changed':'Lower fin armor joint'}
