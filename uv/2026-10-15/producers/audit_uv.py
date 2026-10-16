import bpy,sys,json
from pathlib import Path
import numpy as np
sys.path.insert(0,'D:/amind/git/agent-2/art/tools/ship')
import ship_source,ship_contract
scene=bpy.context.scene
roles=ship_source.Roles(scene)
parts=sorted({n for r in ship_contract.VISUAL_ROLES for n in roles.members.get(r,()) if scene.objects[n].type=='MESH'})
evaluation=ship_source.Evaluation(scene)
size=2048
owners=np.zeros((size,size),np.uint8)
overlaps={};bounds={};coverage={}
for index,name in enumerate(parts,1):
    mesh=evaluation.mesh(name)
    _,_,_,triangles=ship_source.mesh_arrays(mesh)
    uv=ship_source.read_array(mesh.uv_layers['PaintUV'].uv,'vector',np.float64,2).reshape(-1,2)
    bounds[name]=[uv.min(axis=0).tolist(),uv.max(axis=0).tolist()]
    assert np.all(uv>=0) and np.all(uv<=1),name
    for triangle in uv[triangles]*size:
        low=np.maximum(np.floor(triangle.min(axis=0)).astype(int),0)
        high=np.minimum(np.ceil(triangle.max(axis=0)).astype(int),size-1)
        if np.any(high<low):continue
        yy,xx=np.mgrid[low[1]:high[1]+1,low[0]:high[0]+1]
        a,b,c=triangle
        e0=b-a;e1=c-a
        denominator=e0[0]*e1[1]-e0[1]*e1[0]
        if abs(denominator)<1e-10:continue
        dx=xx+.5-a[0];dy=yy+.5-a[1]
        u=(dx*e1[1]-dy*e1[0])/denominator
        v=(e0[0]*dy-e0[1]*dx)/denominator
        inside=(u>1e-5)&(v>1e-5)&(u+v<1-1e-5)
        old=owners[yy[inside],xx[inside]]
        for other in np.unique(old[(old!=0)&(old!=index)]):
            pair=' / '.join(sorted((name,parts[other-1])))
            overlaps[pair]=overlaps.get(pair,0)+int(np.count_nonzero(old==other))
        owners[yy[inside],xx[inside]]=index
    bpy.data.meshes.remove(mesh)
    coverage[name]=int(np.count_nonzero(owners==index))
assert not overlaps,overlaps
result={'resolution':size,'inter_part_interior_overlap_pixels':overlaps,'uv_bounds':bounds,'covered_pixels':coverage,'verdict':'pass'}
Path('D:/amind/git/agent-2/results/vanguard-redo/uv/uv-overlap-audit.json').write_text(json.dumps(result,indent=2)+'\n')
print(json.dumps({'verdict':'pass','parts':len(parts),'inter_part_overlap_pixels':0}))
