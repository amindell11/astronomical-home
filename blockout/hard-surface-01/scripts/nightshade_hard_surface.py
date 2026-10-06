from pathlib import Path
import bpy,numpy as np,json,hashlib
from mathutils import Vector
from mathutils.bvhtree import BVHTree

root=Path('D:/amind/git/agent-1');source=root/'art/ships/nightshade/Nightshade.blend'
out=root/'results/nightshade-hard-surface/round-01';out.mkdir(parents=True,exist_ok=True)
bpy.ops.wm.open_mainfile(filepath=str(source))
before={o.name:{'xyz':np.array([v.co[:] for v in o.data.vertices]),'faces':[tuple(p.vertices) for p in o.data.polygons]} for o in bpy.context.scene.objects if o.type=='MESH'}
edits={}

def hard_edges(ob,columns,n,rows):
    for p in ob.data.polygons:p.use_smooth=False
    for e in ob.data.edges:
        a,b=e.vertices
        e.use_edge_sharp=(a%n==b%n and a%n in columns) or (a//n==b//n and a//n in rows)
    ob.data.update()

def plate_blade(name,n,knots,slope,lower=False):
    ob=bpy.data.objects[name];mesh=ob.data
    assert len(mesh.vertices)%n==0,name
    rows=len(mesh.vertices)//n
    assert knots[0]==0 and knots[-1]==rows-1,(name,rows)
    co=np.array([v.co[:] for v in mesh.vertices]).reshape(rows,n,3)
    native=co.copy()
    if lower:co[:,:,2]=-co[:,:,2]-.016
    ys=co[:,0,1]
    intercept=np.mean(co[:,1:6,2]-slope*co[:,1:6,0],axis=1)
    thickness=np.median(co[:,1:6,2]-co[:,8:13,2][:,::-1],axis=1)
    for a,b in zip(knots,knots[1:]):
        for j in range(a,b+1):
            t=(ys[j]-ys[a])/(ys[b]-ys[a])
            h=intercept[a]*(1-t)+intercept[b]*t
            depth=max(.009,min(.052,thickness[a]*(1-t)+thickness[b]*t))
            for k in range(n):
                z=slope*co[j,k,0]+h
                if k in (0,6):z-=min(.004,depth*.2)
                elif k in (7,13):z-=depth-min(.004,depth*.2)
                elif k>=8:z-=depth
                co[j,k,2]=z
    if lower:co[:,:,2]=-co[:,:,2]-.016
    for v,c in zip(mesh.vertices,co.reshape(-1,3)):v.co.z=float(c[2])
    hard_edges(ob,{0,1,5,6,7,8,12,13},n,set(knots))
    # The broad armor faces are exact planes; the narrow perimeter strips are chamfers.
    residuals=[]
    for a,b in zip(knots,knots[1:]):
        p=co[a:b+1,1:6].reshape(-1,3)
        fit=np.linalg.lstsq(np.c_[p[:,:2],np.ones(len(p))],p[:,2],rcond=None)[0]
        residuals.append(float(np.abs(np.c_[p[:,:2],np.ones(len(p))]@fit-p[:,2]).max()))
    edits[name]={'construction':'Planar armor sections and narrow geometric chamfers','section_rows':knots,
                 'max_planar_residual':max(residuals),'max_vertex_displacement':float(np.linalg.norm(co-native,axis=2).max())}

plate_blade('Rebuilt main wing blades',14,[0,4,9,16,22,27],-.145)
plate_blade('Upper small swept fins',14,[0,3,7,11,15],-.15)
plate_blade('Lower small swept fins',14,[0,3,7,11,15],-.15,True)

ob=bpy.data.objects['Rebuilt pitched hull'];mesh=ob.data;n=17;rows=26
assert len(mesh.vertices)==rows*n+2
anchors=[0,4,7,9,12,16]
for row in range(rows):
    original=[mesh.vertices[row*n+k].co.copy() for k in range(n)]
    if original[0].y>.4:continue
    for a,b in zip(anchors,anchors[1:]):
        for k in range(a+1,b):mesh.vertices[row*n+k].co=original[a].lerp(original[b],(k-a)/(b-a))
for p in mesh.polygons:
    center_y=sum(mesh.vertices[i].co.y for i in p.vertices)/len(p.vertices)
    p.use_smooth=center_y>.4
for e in mesh.edges:
    a,b=e.vertices
    if a<rows*n and b<rows*n:e.use_edge_sharp=(a%n==b%n and a%n in (4,7,9,12))
mesh.update()
edits[ob.name]={'construction':'Six angular cross-section faces aft of the curved nose; width and longitudinal pitch retained'}

def central_plate(name,knots,lower=False):
    ob=bpy.data.objects[name];n=8;rows=len(ob.data.vertices)//n
    assert rows*n==len(ob.data.vertices) and knots[-1]==rows-1
    co=np.array([v.co[:] for v in ob.data.vertices]).reshape(rows,n,3)
    if lower:co[:,:,2]=-co[:,:,2]-.016
    top=co[:,0,2].copy();bottom=co[:,7,2].copy();ys=co[:,0,1]
    for a,b in zip(knots,knots[1:]):
        for j in range(a,b+1):
            t=(ys[j]-ys[a])/(ys[b]-ys[a])
            hi=top[a]*(1-t)+top[b]*t;lo=bottom[a]*(1-t)+bottom[b]*t
            depth=hi-lo
            for k,ratio in enumerate([0,0,.16,.42,.69,1,1,1]):co[j,k,2]=hi-depth*ratio
    if lower:co[:,:,2]=-co[:,:,2]-.016
    for v,c in zip(ob.data.vertices,co.reshape(-1,3)):v.co.z=float(c[2])
    hard_edges(ob,{1,2,3,4,5},n,set(knots))
    edits[name]={'construction':'Planar deck panels with discrete angular bevel bands','section_rows':knots}

for prefix,lower in [('Dorsal',False),('Ventral',True)]:
    central_plate(prefix+' central blade',[0,4,7,10],lower)
    central_plate(prefix+' shield cap',[0,2,5],lower)
central_plate('Aft spine armor',[0,2,4,6])

def reproject(name,target,lower=False,shift_y=0):
    mesh=bpy.data.objects[target].data
    tree=BVHTree.FromPolygons([v.co for v in mesh.vertices],[list(p.vertices) for p in mesh.polygons])
    ob=bpy.data.objects[name]
    for v in ob.data.vertices:
        v.co.y+=shift_y
        loc,_,_,_=tree.ray_cast(Vector((v.co.x,v.co.y,-.5 if lower else .5)),Vector((0,0,1 if lower else -1)),1)
        assert loc is not None,(name,v.index)
        v.co.z=loc.z+(-.0008 if lower else .0008)
    for p in ob.data.polygons:p.use_smooth=False
    ob.data.update()
    edits[name]={'construction':'Existing seam repositioned onto its revised armor plate'}

reproject('Main wing transverse armor joint','Rebuilt main wing blades',shift_y=.026)
reproject('Main wing inset channel','Rebuilt main wing blades')
reproject('Upper fin armor joint','Upper small swept fins')
reproject('Lower fin armor joint','Lower small swept fins',True)

for name in ('Dorsal shield inset','Ventral shield inset','Aft engine collar'):
    for p in bpy.data.objects[name].data.polygons:p.use_smooth=False

audit={}
for name,old in before.items():
    ob=bpy.data.objects[name];new=np.array([v.co[:] for v in ob.data.vertices])
    assert old['faces']==[tuple(p.vertices) for p in ob.data.polygons],name
    changed=not np.array_equal(old['xyz'],new)
    if changed:assert name in edits,name
    audit[name]={'positions_changed':changed,'topology_unchanged':True,'max_vertex_displacement':float(np.linalg.norm(new-old['xyz'],axis=1).max())}
assert len(before)==sum(o.type=='MESH' for o in bpy.context.scene.objects)
bpy.context.scene['review']='Hard-surface revision: in-place planar armor, angular cross-sections and geometric chamfers; key outline curves retained'
bpy.context.preferences.filepaths.save_version=0
bpy.ops.wm.save_as_mainfile(filepath=str(source))
(out/'surface-edit-audit.json').write_text(json.dumps({'edits':edits,'parts':audit,'all_original_parts_retained':True,'topology_regenerated':False,'modifiers_applied':False},indent=2))
print(json.dumps(edits))
