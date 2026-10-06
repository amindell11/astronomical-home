from pathlib import Path
import bpy
import bmesh
from mathutils import Vector
from mathutils.bvhtree import BVHTree

root=Path('D:/amind/git/agent-1')
origin=bpy.data.objects['SymmetryOrigin']
bpy.context.preferences.filepaths.save_version=0

def armor(name,rows):
    verts,faces=[],[]
    for y,inner,outer,z,h in rows:
        for u,v in [(0,0),(.10,.55),(.32,1),(.76,.88),(1,0),(.91,-.68),(.40,-.80),(.09,-.48)]:
            verts.append((inner+(outer-inner)*u,y,z+h*v-.025*y))
    for j in range(len(rows)-1):
        for i in range(8):
            a=j*8+i
            faces.append((a,j*8+(i+1)%8,(j+1)*8+(i+1)%8,a+8))
    for j in (0,len(rows)-1):
        for i in range(1,7):faces.append((j*8,j*8+i,j*8+i+1))
    mesh=bpy.data.meshes.new(name)
    mesh.from_pydata(verts,[],faces)
    mesh.update()
    bm=bmesh.new()
    bm.from_mesh(mesh)
    bmesh.ops.recalc_face_normals(bm,faces=list(bm.faces))
    bm.to_mesh(mesh)
    bm.free()
    mesh.uv_layers.new(name='PaintUV')
    for key in ('Hull slate','Armor bevel','Armor flank'):mesh.materials.append(bpy.data.materials[key])
    for p in mesh.polygons:p.material_index=1 if p.index%8==1 else 2 if p.index%8 in (3,4,5,6) else 0
    ob=bpy.data.objects.new(name,mesh)
    bpy.data.collections['role.hull'].objects.link(ob)
    ob.parent=origin
    mirror=ob.modifiers.new('Live bilateral symmetry','MIRROR')
    mirror.mirror_object=origin
    mirror.use_clip=True
    crease=mesh.attributes.new('crease_edge','FLOAT','EDGE')
    for e in mesh.edges:
        a,b=e.vertices
        if abs(a-b)==8 and a%8 in (0,2,3,4,6):crease.data[e.index].value=.86
    sub=ob.modifiers.new('Shaped armor surface','SUBSURF')
    sub.levels=sub.render_levels=1

armor('Dorsal shoulder mantle',[
    (1.045,.255,.322,.253,.014),(.945,.240,.404,.275,.026),
    (.79,.226,.543,.255,.058),(.60,.259,.653,.224,.071),
    (.41,.261,.622,.208,.077),(.22,.223,.523,.199,.065),
    (.05,.192,.426,.184,.058),(-.14,.160,.359,.157,.045),
    (-.32,.135,.262,.139,.030),(-.47,.113,.160,.126,.009)])
armor('Shoulder trailing flanges',[
    (.67,.49,.552,.208,.012),(.53,.479,.684,.185,.040),
    (.36,.431,.653,.164,.044),(.16,.382,.555,.155,.035),
    (-.05,.301,.415,.141,.022),(-.26,.229,.269,.129,.006)])

cores=bpy.data.objects['Outer magenta tips']
profile=[0,.10,.32,.76,1,.91,.40,.09]
inner=[1194,1210,1230,1248,1266,1276.4]
outer=[1221,1230,1255,1274,1281,1278.5]
bpy.context.view_layer.update()
tree=BVHTree.FromObject(bpy.data.objects['Outer swept blades'],bpy.context.evaluated_depsgraph_get())
for j in range(6):
    for i,u in enumerate(profile):
        v=cores.data.vertices[j*8+i]
        v.co.x=(inner[j]+(outer[j]-inner[j])*u-765)/200
        hit=tree.ray_cast(Vector((v.co.x,v.co.y,10)),Vector((0,0,-1)))[0]
        if hit is None:raise RuntimeError('Outer core exceeds blade at '+str((j,i)))
        v.co.z=hit.z+.012+[0,.004,.008,.007,0,-.005,-.006,-.004][i]

wings=['Forward wing shells','Wing leading armor plates','Wing middle armor plates',
       'Outer swept blades','Outer blade bevel plates','Outer magenta tips',
       'Wing root saddles','Wing root armor cap',
       'Wing outer inset 1','Wing outer inset 2','Wing outer inset 3']
for name in wings:
    for v in bpy.data.objects[name].data.vertices:
        v.co.x=.44+(v.co.x-.44)*.81
for name in ('Inner swept fins','Inner fin armor','Inner magenta tips'):
    for v in bpy.data.objects[name].data.vertices:v.co.x=.40+(v.co.x-.40)*.86
bpy.ops.wm.save_as_mainfile(filepath=str(root/'art/ships/nightshade/Nightshade.blend'))
