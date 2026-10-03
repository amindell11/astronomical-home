import bpy,bmesh,json,math
import numpy as np
from pathlib import Path
from mathutils import Vector
from mathutils.bvhtree import BVHTree
out=Path('D:/amind/git/agent-7/results/crimson-canopy-20260928-001334')
bpy.ops.wm.open_mainfile(filepath=str(out/'Before-canopy-and-web.blend'))
changed=[];added=[]
def assign(obj,verts,faces,smooth=False):
    data=bpy.data.meshes.new(obj.name+' refined')
    data.from_pydata(verts,[],faces);data.update()
    bm=bmesh.new();bm.from_mesh(data);bmesh.ops.recalc_face_normals(bm,faces=list(bm.faces));bm.to_mesh(data);bm.free()
    obj.data=data
    for f in data.polygons:f.use_smooth=smooth
    return obj

def add(name,verts,faces,smooth=False):
    obj=bpy.data.objects.new(name,bpy.data.meshes.new(name))
    bpy.data.collections['01 Hull and cockpit'].objects.link(obj)
    assign(obj,verts,faces,smooth);added.append(obj)
    return obj

def sidefaces(n,rows):
    return [(r*n+i,r*n+(i+1)%n,(r+1)*n+(i+1)%n,(r+1)*n+i) for r in range(rows-1) for i in range(n)]

cockpit=bpy.data.objects['Cockpit surround']
world=np.array([list(cockpit.matrix_world@v.co) for v in cockpit.data.vertices])
edge=world[96:120].copy();center=edge.mean(0)
# The seating edge follows the user's inset roof, below a raised frame.
plane=np.linalg.lstsq(np.c_[np.ones(24),edge[:,1],abs(edge[:,0])],edge[:,2],rcond=None)[0]
def roof(x,y):return float(plane[0]+plane[1]*y+plane[2]*abs(x))
edge[:,2]=[roof(x,y) for x,y,z in edge]
world[96:120]=edge
floor=edge.copy();floor[:,2]-=.020
faces=[tuple(p.vertices) for p in cockpit.data.polygons if set(p.vertices)!=set(range(96,120))]
faces += [(96+i,96+(i+1)%24,120+(i+1)%24,120+i) for i in range(24)]
faces += [tuple(range(120,144))]
inv=cockpit.matrix_world.inverted()
assign(cockpit,[inv@Vector(p) for p in np.vstack([world,floor])],faces)
weights=cockpit.data.attributes.new('bevel_weight_edge','FLOAT','EDGE')
for e in cockpit.data.edges:
    if all(72<=v<96 for v in e.vertices):weights.data[e.index].value=1
bevel=cockpit.modifiers.new('Rounded cockpit shoulder','BEVEL');bevel.limit_method='WEIGHT';bevel.width=.008;bevel.segments=3
changed.append(cockpit)

# Closed perimeter frame, with its inner edge standing above the glazing seat.
profiles=[(1.055,-.004),(1.055,.012),(.95,.010),(.95,-.004)]
verts=[]
for scale,z in profiles:
    for p in edge:
        x,y=center[:2]+(p[:2]-center[:2])*scale
        verts.append((x,y,roof(x,y)+z))
faces=sidefaces(24,4)+[(72+i,72+(i+1)%24,(i+1)%24,i) for i in range(24)]
frame=add('Canopy perimeter rim',verts,faces)
bevel=frame.modifiers.new('Soft rim edges','BEVEL');bevel.width=.0015;bevel.segments=2

# Low crown glazing inside the perimeter; side edges sit below the rim.
glassedge=edge.copy();glassedge[:,:2]=center[:2]+(edge[:,:2]-center[:2])*.955
verts=[]
for scale,lift in [(1,-.010),(1,.001),(.83,.019),(.52,.029),(.20,.033)]:
    for p in glassedge:
        x,y=center[:2]+(p[:2]-center[:2])*scale
        verts.append((x,y,roof(x,y)+lift))
faces=sidefaces(24,5)+[tuple(reversed(range(24))),tuple(range(96,120))]
canopy=bpy.data.objects['Canopy'];inv=canopy.matrix_world.inverted()
assign(canopy,[inv@Vector(p) for p in verts],faces,True);changed.append(canopy)

# The crossbar follows the actual glass crown and terminates inside the rim.
bm=bmesh.new();bm.from_mesh(canopy.data);bm.transform(canopy.matrix_world);bvh=BVHTree.FromBMesh(bm)
ybar=float(edge[:,1].min()+.61*(edge[:,1].max()-edge[:,1].min()))
left=glassedge[1:13];order=np.argsort(left[:,1]);left=left[order]
width=float(np.interp(ybar,left[:,1],abs(left[:,0])))
rings=[]
for x in np.linspace(-width-.003,width+.003,17):
    hit=bvh.ray_cast(Vector((x,ybar,1)),Vector((0,0,-1)))[0]
    z=hit.z if hit is not None else roof(x,ybar)+.006
    rings.append([(x,ybar-.0045,z-.002),(x,ybar+.0045,z-.002),
                  (x,ybar+.0045,z+.008),(x,ybar-.0045,z+.008)])
bm.free()
faces=sidefaces(4,len(rings))+[tuple(reversed(range(4))),tuple(range((len(rings)-1)*4,len(rings)*4))]
bar=add('Canopy transverse frame',[p for r in rings for p in r],faces)
bevel=bar.modifiers.new('Soft frame edges','BEVEL');bevel.width=.001;bevel.segments=2

web=bpy.data.objects['Structural center web']
w=np.array([list(web.matrix_world@v.co) for v in web.data.vertices]);oldmax=float(w[:,1].max())
assert len(w)==228
ends=[23,24,25,26,31,32,33,34]
for r in range(4):
    row=w[r*57:(r+1)*57]
    keep=np.ones(57,dtype=bool);keep[ends]=False
    fit=np.linalg.lstsq(np.c_[np.ones(keep.sum()),row[keep,0],row[keep,1]],row[keep,2],rcond=None)[0]
    for j in ends:
        x=w[57+j,0]
        if r in (0,3):x+=math.copysign(.008,.443-abs(x))*np.sign(x)
        y=oldmax-(.008 if r in (0,3) else 0)
        w[r*57+j]=(x,y,float(fit@[1,x,y]))
inv=web.matrix_world.inverted()
keep=[i for i in range(57) if i not in (24,25,32,33)]
verts=[inv@Vector(w[r*57+i]) for r in range(4) for i in keep]
n=len(keep)
faces=sidefaces(n,4)+[tuple(reversed(range(n))),tuple(range(3*n,4*n))]
assign(web,verts,faces);changed.append(web)
bm=bmesh.new();bm.from_mesh(web.data);bm.transform(web.matrix_world)
web_bvh=BVHTree.FromBMesh(bm)
assert web_bvh.ray_cast(Vector((0,.70,1)),Vector((0,0,-1)))[0] is None,'Central notch must remain open'
bm.free()
assert abs(max((web.matrix_world@v.co).y for v in web.data.vertices)-oldmax)<1e-6

# Keep the new frames editable around the ship's shared symmetry plane.
for obj in added:
    bm=bmesh.new();bm.from_mesh(obj.data)
    bmesh.ops.bisect_plane(bm,geom=list(bm.verts)+list(bm.edges)+list(bm.faces),dist=1e-7,plane_co=(0,0,0),plane_no=(1,0,0),clear_inner=True,clear_outer=False)
    for v in bm.verts:
        if abs(v.co.x)<1e-6:v.co.x=0
    bm.to_mesh(obj.data);bm.free()
    mirror=obj.modifiers.new('Ship center symmetry','MIRROR');mirror.mirror_object=bpy.data.objects['SHIP CENTER - mirror plane'];mirror.use_clip=True
    bpy.context.view_layer.objects.active=obj
    bpy.ops.object.modifier_move_up(modifier=mirror.name)

bpy.context.view_layer.update();report=[]
for obj in changed+added:
    evaluated=obj.evaluated_get(bpy.context.evaluated_depsgraph_get());data=evaluated.to_mesh()
    bm=bmesh.new();bm.from_mesh(data)
    bad=sum(not e.is_manifold for e in bm.edges);zero=sum(f.calc_area()<1e-10 for f in bm.faces)
    report.append({'name':obj.name,'vertices':len(obj.data.vertices),'nonmanifold':bad,'zero_faces':zero})
    bm.free();evaluated.to_mesh_clear()
    assert bad==0 and zero==0,report[-1]
payload={}
for obj in changed+added:
    payload[obj.name]={'vertices':[list(v.co) for v in obj.data.vertices],'faces':[list(f.vertices) for f in obj.data.polygons],
        'smooth':[f.use_smooth for f in obj.data.polygons], 'new':obj in added,
        'bevel': {'width': next(m.width for m in obj.modifiers if m.type=='BEVEL'),'segments':next(m.segments for m in obj.modifiers if m.type=='BEVEL'),'weighted':obj==cockpit} if any(m.type=='BEVEL' for m in obj.modifiers) else None,
        'mirror':obj in added}
(out/'candidate.json').write_text(json.dumps(payload));(out/'checks.json').write_text(json.dumps(report,indent=2))
print(json.dumps(report))
code=(out/'inspect.py').read_text();code=code[code.index('s=bpy.context.scene'):].replace("'before-'","'after-'").replace("['Canopy','Cockpit surround','Central hull']","['Canopy','Cockpit surround','Central hull','Canopy perimeter rim','Canopy transverse frame']")
exec(code)

