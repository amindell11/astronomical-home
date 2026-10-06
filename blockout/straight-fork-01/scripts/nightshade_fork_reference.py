from pathlib import Path
import bpy, json, numpy as np
from mathutils import Vector
from mathutils.bvhtree import BVHTree

scratch=Path(__file__).parent
root=Path('D:/amind/git/agent-1')
out=root/'results/nightshade-straight-fork'
out.mkdir(parents=True,exist_ok=True)
folder=root/'results/nightshade-legacy-base/inspection'
bpy.ops.wm.open_mainfile(filepath=str(folder/'LegacyNative.blend'))
obj=bpy.data.objects['temp'];mesh=obj.data
indices=json.loads((folder/'component-indices.json').read_text())[1]
lookup={v:i for i,v in enumerate(indices)}
coords=np.array([obj.matrix_world@mesh.vertices[i].co for i in indices])
coords[:,1]*=-1
faces=[[lookup[i] for i in p.vertices] for p in mesh.polygons if all(i in lookup for i in p.vertices)]
tree=BVHTree.FromPolygons([Vector(v) for v in coords],faces)
edges=set()
for face in faces:
    for a,b in zip(face,face[1:]+face[:1]):edges.add(tuple(sorted((a,b))))
edges=np.array(sorted(edges))
a,b=coords[edges[:,0]],coords[edges[:,1]]

def section(y):
    mask=(np.minimum(a[:,1],b[:,1])<=y)&(np.maximum(a[:,1],b[:,1])>=y)&(np.abs(a[:,1]-b[:,1])>1e-8)
    u=(y-a[mask,1])/(b[mask,1]-a[mask,1])
    return a[mask]+u[:,None]*(b[mask]-a[mask])

ys=np.linspace(.16,coords[:,1].min()+0.0001,801)
sections=[]
for y in ys:
    p=section(y)
    sections.append([float(y),float(p[:,0].min()),float(p[:,0].max()),float(p[:,2].min()),float(p[:,2].max())])
sections=np.array(sections)
(out/'reference-sections.json').write_text(json.dumps(sections.tolist()))

# Retain silhouette bends while discarding the legacy mesh's dense tessellation.
def simplify(values,epsilon):
    keep={0,len(values)-1}
    def step(lo,hi):
        if hi-lo<2:return
        t=(values[lo+1:hi,0]-values[lo,0])/(values[hi,0]-values[lo,0])
        pred=values[lo,1:]+t[:,None]*(values[hi,1:]-values[lo,1:])
        error=np.max(np.abs(values[lo+1:hi,1:]-pred),axis=1)
        i=int(np.argmax(error))+lo+1
        if error.max()>epsilon:keep.add(i);step(lo,i);step(i,hi)
    step(0,len(values)-1)
    return sorted(keep)

keep=simplify(sections,.0012)
while len(keep)<61:
    lo,hi=max(zip(keep,keep[1:]),key=lambda pair:pair[1]-pair[0])
    keep.append((lo+hi)//2);keep.sort()
assert len(keep)==61
fractions=[0,.07,.28,.66,.93,1]
rows=[]
for index in keep:
    y,inside,outside,zmin,zmax=sections[index]
    points=section(y)
    top=[];bottom=[]
    for fraction in fractions:
        x=inside+(outside-inside)*fraction
        if fraction in (0,1):
            near=points[np.abs(points[:,0]-x)<.000025]
            low,high=float(near[:,2].min()),float(near[:,2].max())
            if high-low<.00015:
                mid=(low+high)/2;low=mid-.000075;high=mid+.000075
        else:
            high_hit=tree.ray_cast(Vector((x,y,.6)),Vector((0,0,-1)),1.2)[0]
            low_hit=tree.ray_cast(Vector((x,y,-.6)),Vector((0,0,1)),1.2)[0]
            assert high_hit is not None and low_hit is not None,(index,fraction)
            low,high=low_hit.z,high_hit.z
        top.append([float(x),float(y),high]);bottom.append([float(x),float(y),low])
    rows.append(top+list(reversed(bottom)))

data={'rows':rows,'sections':sections.tolist(),'selected_stations':keep,
      'bounds':[coords.min(axis=0).tolist(),coords.max(axis=0).tolist()],
      'silhouette_tolerance':.0012,'source_component':1,
      'source':'Original legacy native mesh; positive-X wing, Y sign changed for nose +Y; no scaling',
      'legacy_topology_imported':False}
(out/'fork-guide.json').write_text(json.dumps(data,indent=2))
print('FORK_GUIDE',len(rows),'sections;',len(rows)*12,'new cage vertices; bounds',data['bounds'],flush=True)
