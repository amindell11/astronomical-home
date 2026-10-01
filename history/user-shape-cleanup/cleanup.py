import bpy,bmesh,json,math,hashlib
from pathlib import Path
from mathutils import Vector
from mathutils.bvhtree import BVHTree
P=Path('D:/amind/git/agent-2/results/valis-cleanup')

def signature():
    return {o.name:{'vertices':[[round(c,6) for c in v.co] for v in o.data.vertices], 'matrix':[[round(c,6) for c in row] for row in o.matrix_world], 'faces':[list(f.vertices) for f in o.data.polygons]} for o in bpy.context.scene.objects if o.type=='MESH'}

def world(o):return [o.matrix_world@v.co for v in o.data.vertices]
def set_world(o,points):
    inv=o.matrix_world.inverted()
    for v,p in zip(o.data.vertices,points):v.co=inv@p
    o.data.update()

def tree(o,evaluated=True):
    bpy.context.view_layer.update()
    if evaluated:
        evaluated_obj=o.evaluated_get(bpy.context.evaluated_depsgraph_get());m=evaluated_obj.to_mesh()
    else:m=o.data
    bvh=BVHTree.FromPolygons([o.matrix_world@v.co for v in m.vertices],[list(f.vertices) for f in m.polygons],all_triangles=False)
    if evaluated:evaluated_obj.to_mesh_clear()
    return bvh

def replace_mesh(o,points,faces):
    inv=o.matrix_world.inverted();m=bpy.data.meshes.new(o.name+' fitted mesh')
    m.from_pydata([inv@p for p in points],[],faces);m.update()
    for mat in o.data.materials:m.materials.append(mat)
    o.data=m

def hit_z(bvh,x,y,above):
    hit,n,face,d=bvh.ray_cast(Vector((x,y,10 if above else -10)),Vector((0,0,-1 if above else 1)))
    if hit is None:raise ValueError('Requested fitting point lies outside supporting surface')
    return hit.z

before=signature()
if bpy.app.background:
    (P/'baseline-signature.json').write_text(json.dumps(before,sort_keys=True))
else:
    expected=json.loads((P/'baseline-signature.json').read_text())
    if before!=expected:raise RuntimeError('The live model changed since its backup; preserve and inspect the new edits before fitting')

objects=bpy.data.objects
armor=objects['04 dorsal armor'];shoulder=objects['05 dorsal shoulder bevel']
a=world(armor);s=world(shoulder)
seam_map={0:1,3:4,4:3,5:2}
before_seams=[(s[i]-a[j]).length for i,j in seam_map.items()]
for i,j in seam_map.items():s[i]=a[j].copy()
set_world(shoulder,s)

hull=objects['01 central fuselage'];h=world(hull)
cheeks=objects['03 canopy side cheeks'];points=[]
for ring in range(1,5):
    top=h[ring*5+1];side=h[ring*5+2]
    for t in [.08,.92]:
        p=top.lerp(side,t);p.z+=.016;points.append(p)
set_world(cheeks,points)

belly=objects['08 ventral armor'];points=[]
for ring in range(9):
    center=h[ring*5+4];edge=h[ring*5+3]
    for t in [0,.94]:
        p=center.lerp(edge,t);p.z-=.008;points.append(p)
set_world(belly,points)

crest=objects['07 rear crest'];crest_points=world(crest)
armor_surface=tree(armor,False)
for p in crest_points:
    near,n,i,d=armor_surface.find_nearest(p)
    p.z=near.z+.032
set_world(crest,crest_points)
crest.modifiers['Editable plate thickness'].thickness=.05

rails=objects['06 dorsal lavender rails'];r=world(rails)
r[0]=s[1]+Vector((.004,0,-.025))
r[1]=Vector((h[22].x+.016,h[22].y,h[22].z+.32))
r[2]=s[2]+Vector((-.025,-.05,-.02))
set_world(rails,r)

for accent_name,base_name in [('11 upper wing gray planes','10 upper swept wings'),('12 upper wing lavender tips','10 upper swept wings'),('21 lower blade lavender tips','20 lower swept blades'),('31 forward fin lavender edges','30 forward lower fins')]:
    accent=objects[accent_name];base=objects[base_name];surface=tree(base,False)
    fitted=[]
    for p in world(accent):
        near,n,i,d=surface.find_nearest(p)
        if n.z<0:n=-n
        fitted.append(near+n*(.025 if 'lavender' in accent_name else .009))
    set_world(accent,fitted)
    accent.modifiers['Editable plate thickness'].thickness=.025
    underside=objects.get(accent_name+' underside')
    if underside:
        surface=tree(base)
        fitted=[]
        for p in world(underside):
            near,n,i,d=surface.find_nearest(p)
            fitted.append(near-n*.007)
        set_world(underside,fitted)
        underside.modifiers['Editable plate thickness'].thickness=.014

stripe=objects['63 ventral engine stripe'];v=world(stripe)
belly_surface=tree(belly)
for p in v:
    p.z=hit_z(belly_surface,p.x,p.y,False)+.003
set_world(stripe,v)
stripe.modifiers['Editable plate thickness'].thickness=.015

ribs=objects['62 engine raised ribs'];old=world(ribs);engine=objects['60 engine spine']
engine_surface=tree(engine)
xs=[min(v.x for v in old),max(v.x for v in old)]
y0=min(v.y for v in old);y1=min(max(v.y for v in old),3.19)
ys=sorted(set([y0,y1]+[float(v.y) for v in world(engine) if y0<v.y<y1]))
points=[]
for y in ys:
    for x in xs:points.append(Vector((x,y,hit_z(engine_surface,x,y,True)+.018)))
replace_mesh(ribs,points,[(i,i+2,i+3,i+1) for i in range(0,len(points)-2,2)])
ribs.modifiers['Editable plate thickness'].thickness=.04

for o in [shoulder,cheeks,belly,crest,rails,ribs]:
    if o==belly:continue
    bm=bmesh.new();bm.from_mesh(o.data)
    bmesh.ops.recalc_face_normals(bm,faces=list(bm.faces))
    for f in bm.faces:
        if f.normal.z<0:f.normal_flip()
    bm.to_mesh(o.data);bm.free();o.data.update()

support=objects['53 auxiliary vane roots']
with bpy.context.temp_override(object=support,active_object=support):
    bpy.ops.object.modifier_move_down(modifier='Live bilateral symmetry')
for f in shoulder.data.polygons:f.use_smooth=True
shoulder.modifiers['Editable plate thickness'].use_quality_normals=True
bpy.context.view_layer.update()
after=signature()
protected=['01 central fuselage','02 black canopy','10 upper swept wings','13 charcoal trailing spars','20 lower swept blades','30 forward lower fins','40 forward outriggers','41 outrigger lavender toes','42 outrigger inner sockets','50 aft split prongs','51 midship auxiliary vanes','60 engine spine','64 exhaust rim']
for n in protected:assert before[n]==after[n],n
seam_distances=[(world(shoulder)[i]-world(armor)[j]).length for i,j in seam_map.items()]
assert max(seam_distances)<.00001
report={'seam_gaps_before':before_seams,'seam_gaps_after':seam_distances,'protected_shape_parts_unchanged':protected,'changed_parts':[n for n in before if before[n]!=after[n]],'source_snapshot':'Valis-user-before-cleanup.blend'}
(P/'cleanup-report.json').write_text(json.dumps(report,indent=2))
if bpy.app.background:bpy.ops.wm.save_as_mainfile(filepath=str(P/'Valis-cleanup-candidate.blend'))
else:bpy.ops.wm.save_as_mainfile(filepath='D:/amind/git/agent-2/art/ships/valis/Valis.blend')
print(json.dumps(report))


