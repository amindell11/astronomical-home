from pathlib import Path
import math
import bpy
import bmesh

root=Path('D:/amind/git/agent-1')
scene=bpy.context.scene
origin=bpy.data.objects['SymmetryOrigin']
bpy.context.preferences.filepaths.save_version=0

def loft(name, stations, smooth=True):
    verts,faces=[],[]
    for py,inner,outer,z,h in stations:
        y=(540-py)/200
        x0=(inner-765)/200
        w=(outer-inner)/200
        for u,v in [(0,0),(.12,.66),(.44,1),(.84,.60),(1,0),(.84,-.6),(.44,-.8),(.12,-.55)]:
            verts.append((x0+w*u,y,z+h*v))
    for j in range(len(stations)-1):
        for i in range(8):
            a=j*8+i
            faces.append((a,j*8+(i+1)%8,(j+1)*8+(i+1)%8,a+8))
    for j in (0,len(stations)-1):
        for i in range(1,7):
            faces.append((j*8,j*8+i,j*8+i+1))
    mesh=bpy.data.meshes.new(name+' control cage')
    mesh.from_pydata(verts,[],faces)
    mesh.update()
    bm=bmesh.new()
    bm.from_mesh(mesh)
    bmesh.ops.recalc_face_normals(bm,faces=list(bm.faces))
    bm.to_mesh(mesh)
    bm.free()
    mesh.uv_layers.new(name='PaintUV')
    mesh.materials.append(bpy.data.materials['Blockout slate'])
    if name in bpy.data.objects:
        ob=bpy.data.objects[name]
        old=ob.data
        ob.data=mesh
        bpy.data.meshes.remove(old)
        for mod in list(ob.modifiers):
            if mod.type!='MIRROR': ob.modifiers.remove(mod)
    else:
        ob=bpy.data.objects.new(name,mesh)
        bpy.data.collections['role.hull'].objects.link(ob)
        ob.parent=origin
        mod=ob.modifiers.new('Live bilateral symmetry','MIRROR')
        mod.mirror_object=origin
        mod.use_clip=True
    for p in mesh.polygons:p.use_smooth=smooth
    sub=ob.modifiers.new('Soft sculpted volume','SUBSURF')
    sub.levels=1
    sub.render_levels=1
    return ob

loft('Outer wing shoulders',[
    (119,918,921,.065,.006),(126,918,928,.064,.016),
    (146,918,948,.06,.035),(172,921,982,.054,.055),
    (196,907,1007,.05,.074),(220,915,1031,.043,.086),
    (242,921,1049,.034,.096),(267,897,1068,.025,.103),
    (298,891,1088,.010,.105),(317,906,1100,.0,.098),
    (350,979,1116,-.012,.088),(384,1018,1126,-.020,.070),
    (431,1041,1143,-.020,.058),(473,1050,1156,-.017,.049),
    (505,1070,1169,-.012,.044),(519,1083,1174,-.010,.041),
    (524,1089,1176,-.010,.038)])
loft('Outer wing blades',[
    (518,1085,1174,-.010,.041),(526,1090,1177,-.010,.041),
    (560,1116,1188,-.008,.040),(610,1155,1204,-.004,.038),
    (664,1198,1227,.004,.035),(715,1237,1251,.014,.030),
    (748,1258,1281,.031,.027),(785,1270,1284,.047,.020),
    (824,1277,1280,.064,.005),(830,1278,1279,.067,.002)])
loft('Wing root joints',[
    (245,850,924,.020,.040),(255,849,939,.018,.088),
    (285,848,946,.016,.103),(312,851,950,.010,.088),
    (321,860,950,.005,.035)])
loft('Inner swept fins',[
    (321,849,907,.13,.040),(334,847,913,.145,.055),
    (373,852,929,.177,.063),(414,860,940,.200,.067),
    (450,871,950,.224,.063),(486,888,960,.239,.050),
    (520,901,972,.252,.035),(540,925,959,.265,.020),
    (548,939,941,.273,.006)])
loft('Rising tail blades',[
    (452,841,893,.095,.054),(472,849,903,.068,.064),
    (502,860,915,.045,.070),(542,883,931,.044,.084),
    (585,894,945,.066,.091),(627,881,946,.131,.088),
    (666,857,934,.235,.080),(706,833,910,.355,.070),
    (750,818,884,.438,.062),(801,789,859,.482,.053),
    (848,785,833,.509,.040),(902,785,811,.538,.025),
    (946,790,798,.560,.008),(960,792,795,.565,.002)])

for name in ('Fuselage','Upper canopy','Lower canopy'):
    ob=bpy.data.objects[name]
    for p in ob.data.polygons:p.use_smooth=True
upper=bpy.data.objects['Upper canopy']
for v in upper.data.vertices:
    if v.co.y<1.0:
        t=(1.0-v.co.y)/.06
        v.co.y-=.10*t
        v.co.z-=.07*t*(1-(v.co.x/.46)**2)

for name in ('Outer wing blades','Inner swept fins'):
    ob=bpy.data.objects[name]
    ob.data.materials.append(bpy.data.materials['Blockout tip region'])
    threshold=-.85 if name=='Outer wing blades' else .12
    for p in ob.data.polygons:
        if all(ob.data.vertices[v].co.y<threshold for v in p.vertices):
            p.material_index=1

for mat in bpy.data.materials:
    if mat.name=='Blockout slate':mat.diffuse_color=(.19,.22,.30,1)
bpy.data.objects['reference.top'].hide_set(False)
bpy.ops.wm.save_as_mainfile(filepath=str(root/'art/ships/nightshade/Nightshade.blend'))
