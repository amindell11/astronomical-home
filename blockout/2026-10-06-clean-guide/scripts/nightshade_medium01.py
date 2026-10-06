from pathlib import Path
import math
import bpy
import bmesh
from mathutils import Vector
from mathutils.geometry import tessellate_polygon

root=Path('D:/amind/git/agent-1')
origin=bpy.data.objects['SymmetryOrigin']
bpy.context.preferences.filepaths.save_version=0
for ob in list(bpy.context.scene.objects):
    if ob.type=='MESH':
        mesh=ob.data
        bpy.data.objects.remove(ob,do_unlink=True)
        if mesh.users==0:bpy.data.meshes.remove(mesh)

def mat(name,rgb):
    m=bpy.data.materials.get(name) or bpy.data.materials.new(name)
    m.diffuse_color=(*rgb,1)
    return m

slate=mat('Hull slate',(.245,.267,.355))
light=mat('Armor bevel',(.315,.34,.435))
dark=mat('Recess shadow',(.080,.092,.140))
edge=mat('Armor flank',(.155,.176,.242))
glass=mat('Violet glass',(.235,.195,.65))
glass_edge=mat('Canopy surround',(.10,.10,.19))
pink=mat('Magenta core',(.76,.065,.83))
pink_side=mat('Core bevel',(.37,.035,.45))

def mesh_part(name,verts,faces,role='hull',materials=None,bevel=.006,smooth=False):
    mesh=bpy.data.meshes.new(name)
    mesh.from_pydata(verts,[],faces)
    mesh.update()
    bm=bmesh.new()
    bm.from_mesh(mesh)
    bmesh.ops.recalc_face_normals(bm,faces=list(bm.faces))
    bm.to_mesh(mesh)
    bm.free()
    mesh.uv_layers.new(name='PaintUV')
    ob=bpy.data.objects.new(name,mesh)
    bpy.data.collections['role.'+role].objects.link(ob)
    ob.parent=origin
    for m in materials or [slate,light,edge,dark]:mesh.materials.append(m)
    for p in mesh.polygons:p.use_smooth=smooth
    mirror=ob.modifiers.new('Live bilateral symmetry','MIRROR')
    mirror.mirror_object=origin
    mirror.use_clip=True
    mirror.merge_threshold=.0001
    if bevel:
        mod=ob.modifiers.new('Small softened edges','BEVEL')
        mod.width=bevel
        mod.segments=2
        mod.angle_limit=.35
    return ob

def ribbon(name,stations,role='hull',materials=None,soft=False,bevel=.007):
    verts,faces=[],[]
    profile=[(0,0),(.10,.55),(.32,1),(.76,.88),(1,0),(.91,-.68),(.40,-.80),(.09,-.48)]
    for y,x0,x1,z,h in stations:
        for u,v in profile:verts.append((x0+(x1-x0)*u,y,z+h*v))
    for j in range(len(stations)-1):
        for i in range(8):
            a=j*8+i
            faces.append((a,j*8+(i+1)%8,(j+1)*8+(i+1)%8,a+8))
    for j in (0,len(stations)-1):
        for i in range(1,7):faces.append((j*8,j*8+i,j*8+i+1))
    ob=mesh_part(name,verts,faces,role,materials,0 if soft else bevel)
    for p in ob.data.polygons:
        if len(ob.data.materials)>1:
            i=p.index%8
            p.material_index=1 if i==1 else 2 if i in (3,4,5,6) and len(ob.data.materials)>2 else 0
    if soft:
        crease=ob.data.attributes.new('crease_edge','FLOAT','EDGE')
        for e in ob.data.edges:
            a,b=e.vertices
            if abs(a-b)==8 and a%8 in (0,2,3,4,6):crease.data[e.index].value=.78
        sub=ob.modifiers.new('Shaped blade surface','SUBSURF')
        sub.levels=1
        sub.render_levels=1
    return ob

def half_hull(name,stations,materials=None):
    verts,faces=[],[]
    profile=[(0,1),(.42,1),(.77,.70),(.96,.32),(1,0),(.96,-.35),(.72,-.83),(.38,-1),(0,-1)]
    for y,w,z,top,bottom in stations:
        for u,v in profile:verts.append((w*u,y,z+(top if v>=0 else bottom)*v))
    for j in range(len(stations)-1):
        for i in range(8):
            a=j*9+i
            faces.append((a,a+1,a+10,a+9))
    for j in (0,len(stations)-1):
        c=len(verts)
        verts.append((0,stations[j][0],stations[j][2]))
        for i in range(8):faces.append((c,j*9+i,j*9+i+1))
    ob=mesh_part(name,verts,faces,materials=materials)
    for p in ob.data.polygons:
        if len(ob.data.materials)>2:p.material_index=2 if p.index%8>=4 else 1 if p.index%8==2 else 0
    return ob

def polygon(name,xy,z,depth=.02,materials=None,role='hull'):
    points=[Vector((x,y,z(x,y) if callable(z) else z)) for x,y in xy]
    verts=[tuple(p) for p in points]+[(p.x,p.y,p.z-depth) for p in points]
    n=len(points)
    faces=[]
    for tri in tessellate_polygon([points]):
        ids=tuple(tri)
        faces.extend([ids,tuple(i+n for i in reversed(ids))])
    for i in range(n):faces.append((i,(i+1)%n,(i+1)%n+n,i+n))
    return mesh_part(name,verts,faces,role,materials)

def from_pixels(rows):
    return [((540-py)/200,(inner-765)/200,(outer-765)/200,z,h) for py,inner,outer,z,h in rows]

half_hull('Central keel',[
    (1.98,.045,-.075,.035,.030),(1.94,.18,-.062,.064,.064),
    (1.84,.32,-.048,.102,.100),(1.66,.43,-.025,.136,.130),
    (1.42,.50,0,.154,.148),(1.15,.53,.018,.178,.158),
    (.86,.52,.030,.192,.155),(.53,.46,.034,.191,.135),
    (.18,.37,.045,.160,.111),(-.19,.25,.055,.123,.078),
    (-.55,.14,.062,.080,.045),(-.79,.07,.068,.040,.023),
    (-.83,.025,.068,.019,.012)])

window=[
    (1.91,.04,.028,.042),(1.86,.18,.043,.092),(1.72,.30,.070,.154),
    (1.53,.39,.090,.212),(1.30,.423,.104,.260),(1.07,.411,.107,.281),
    (.87,.370,.111,.262),(.75,.312,.128,.216)]

def canopy(name,rows,role,materials,shrink=1,lower=False):
    verts,faces=[],[]
    for y,w,edge_z,peak in rows:
        y=1.34+(y-1.34)*shrink
        w*=shrink
        for u,h in [(0,1),(.35,.99),(.69,.86),(.90,.49),(1,0)]:
            z=edge_z+(peak-edge_z)*h
            z=z+.012 if shrink<1 else z
            if lower:z=-.068-z*.47
            verts.append((w*u,y,z))
    for j in range(len(rows)-1):
        for i in range(4):
            a=j*5+i
            faces.append((a,a+1,a+6,a+5))
    ob=mesh_part(name,verts,faces,role,materials,0)
    solid=ob.modifiers.new('Canopy shell thickness','SOLIDIFY')
    solid.thickness=.018
    bevel=ob.modifiers.new('Canopy edge chamfer','BEVEL')
    bevel.width=.004
    bevel.segments=2
    return ob

canopy('Upper canopy frame',window,'hull',[glass_edge])
canopy('Upper glazing',window,'canopy',[glass],.91)
canopy('Lower canopy frame',window,'hull',[glass_edge],1,True)
canopy('Lower glazing',window,'canopy',[glass],.88,True)

ribbon('Nose equator armor',[
    (1.97,.010,.115,-.044,.032),(1.92,.155,.266,-.030,.046),
    (1.82,.276,.394,-.010,.047),(1.65,.369,.476,.009,.046),
    (1.42,.422,.535,.024,.042),(1.14,.437,.557,.044,.035),
    (.90,.431,.549,.057,.030),(.77,.402,.520,.076,.028)],soft=True)

half_hull('Dorsal spine armor',[
    (1.045,.035,.280,.033,.030),(.95,.136,.285,.090,.047),
    (.83,.245,.263,.126,.058),(.63,.303,.225,.132,.052),
    (.34,.280,.205,.115,.046),(.02,.214,.174,.108,.038),
    (-.30,.146,.132,.093,.030),(-.63,.092,.103,.055,.020),
    (-.77,.043,.088,.026,.012)])
ribbon('Rear cheek armor',[
    (.92,.345,.487,.194,.025),(.73,.312,.528,.199,.046),
    (.50,.270,.496,.179,.062),(.21,.215,.425,.155,.057),
    (-.08,.165,.320,.142,.044),(-.36,.113,.224,.131,.030),
    (-.54,.090,.146,.112,.020)],soft=True)
ribbon('Wing root saddles',[
    (1.45,.405,.80,.002,.046),(1.36,.416,.917,.004,.097),
    (1.14,.443,.955,.018,.119),(.98,.455,.976,.036,.109),
    (.92,.460,.934,.043,.057)])
ribbon('Wing root armor cap',[
    (1.41,.537,.79,.060,.023),(1.30,.522,.951,.090,.031),
    (1.08,.540,.955,.113,.030),(.985,.570,.930,.110,.015)])

wing_rows=[
    (119,918,921,-.075,.007),(135,915,938,-.066,.028),
    (172,921,982,-.041,.052),(196,908,1007,-.020,.063),
    (225,918,1034,.012,.074),(250,925,1055,.048,.079),
    (270,913,1071,.061,.082),(297,915,1087,.067,.083),
    (319,931,1100,.070,.081),(350,979,1116,.069,.071),
    (384,1018,1130,.071,.062),(427,1039,1145,.077,.054),
    (469,1047,1158,.085,.047),(505,1067,1169,.094,.041),
    (523,1088,1178,.098,.036)]
ribbon('Forward wing shells',from_pixels(wing_rows),soft=True)
ribbon('Wing leading armor plates',from_pixels([
    (159,931,951,-.007,.010),(195,928,994,.018,.022),
    (229,938,1024,.066,.024),(257,940,1040,.104,.023),
    (283,939,1051,.126,.020),(310,950,1065,.138,.018),
    (335,973,1074,.139,.014)]),soft=True)
ribbon('Wing middle armor plates',from_pixels([
    (327,985,1097,.144,.014),(352,1006,1106,.144,.019),
    (387,1033,1118,.141,.021),(424,1050,1130,.141,.021),
    (458,1060,1140,.146,.018),(487,1071,1146,.145,.012),
    (506,1087,1147,.145,.006)]),soft=True)

blade_rows=[
    (518,1086,1176,.098,.037),(530,1090,1180,.101,.036),
    (567,1114,1191,.108,.035),(612,1145,1205,.119,.034),
    (662,1180,1224,.133,.032),(708,1215,1248,.148,.029),
    (747,1240,1280,.163,.025),(785,1261,1285,.173,.019),
    (829,1277,1280,.185,.004)]
ribbon('Outer swept blades',from_pixels(blade_rows),soft=True)
ribbon('Outer blade bevel plates',from_pixels([
    (534,1107,1167,.145,.010),(573,1130,1180,.148,.013),
    (615,1160,1193,.157,.012),(651,1184,1207,.165,.009),
    (688,1210,1229,.177,.004)]),soft=True)
ribbon('Outer magenta tips',from_pixels([
    (665,1213,1221,.169,.003),(692,1222,1230,.179,.006),
    (722,1242,1255,.190,.011),(748,1251,1277,.198,.013),
    (785,1271,1281,.204,.010),(819,1278,1281,.202,.003)]),
    'cores',[pink,pink,pink_side],soft=True)

ribbon('Inner swept fins',from_pixels([
    (316,851,897,.155,.031),(348,850,915,.190,.050),
    (390,856,932,.229,.046),(435,867,947,.263,.040),
    (476,889,959,.295,.036),(514,907,972,.320,.025),
    (541,932,959,.329,.014),(548,941,946,.330,.003)]),soft=True)
ribbon('Inner fin armor',from_pixels([
    (351,868,905,.247,.009),(393,873,923,.281,.014),
    (438,882,937,.307,.014),(472,896,943,.329,.007)]),soft=True)
ribbon('Inner magenta tips',from_pixels([
    (490,908,962,.339,.007),(514,918,969,.347,.012),
    (539,937,955,.348,.010),(546,941,946,.339,.002)]),
    'cores',[pink,pink,pink_side],soft=True)

tail_rows=[
    (.265,.355,.585,.149,.052),(.06,.435,.696,.120,.062),
    (-.18,.543,.812,.086,.074),(-.39,.617,.898,.052,.084),
    (-.60,.585,.890,.074,.090),(-.79,.516,.842,.119,.090),
    (-.97,.445,.758,.195,.083),(-1.14,.372,.664,.257,.068),
    (-1.34,.317,.567,.280,.056),(-1.52,.300,.472,.271,.040),
    (-1.69,.305,.393,.252,.026),(-1.83,.318,.338,.235,.004)]
ribbon('Sculpted tail blades',tail_rows,soft=True)
ribbon('Tail outer sculpted ridge',[
    (-.17,.704,.785,.164,.012),(-.37,.761,.872,.141,.022),
    (-.57,.728,.874,.167,.023),(-.76,.664,.815,.213,.024),
    (-.95,.593,.727,.278,.022),(-1.12,.529,.641,.323,.018),
    (-1.31,.463,.544,.335,.014),(-1.51,.412,.455,.312,.008),
    (-1.68,.361,.386,.274,.003)],soft=True)
ribbon('Tail root armor',[
    (.285,.400,.581,.212,.011),(.145,.458,.654,.206,.024),
    (-.06,.543,.737,.189,.026),(-.22,.632,.790,.171,.014)],soft=True)

polygon('Spine front inset',[(0,.965),(.070,.982),(.111,.899),(.098,.780),(.039,.721),(0,.713)],
    lambda x,y:.354+.13*(y-.80),.012,[dark])
polygon('Spine inner crest',[(0,.937),(.048,.947),(.077,.886),(.064,.813),(.019,.766),(0,.760)],
    lambda x,y:.369+.13*(y-.80),.011,[light])
polygon('Dorsal recessed shield',[(0,.666),(.065,.621),(.075,.537),(.029,.470),(0,.457)],
    lambda x,y:.324+.09*(y-.50),.010,[dark])
polygon('Dorsal inset center',[(0,.632),(.040,.601),(.048,.540),(.015,.498),(0,.489)],
    lambda x,y:.334+.09*(y-.50),.010,[edge])
ribbon('Spine flank ridge',[
    (.58,.235,.289,.317,.010),(.34,.205,.264,.288,.014),
    (.04,.160,.207,.257,.013),(-.25,.108,.147,.208,.009),
    (-.52,.074,.093,.151,.004)])

for index,(py,a,b) in enumerate([(353,1092,1113),(385,1104,1123),(417,1112,1133)]):
    y=(540-py)/200
    x0=(a-765)/200
    x1=(b-765)/200
    polygon('Wing outer inset '+str(index+1),[(x0,y+.08),(x1,y+.035),(x1+.02,y-.025),(x0+.018,y+.016)],
        .147 if index==0 else .143,.009,[dark])

for ob in [o for o in bpy.context.scene.objects if o.type=='MESH']:
    for v in ob.data.vertices:
        v.co.z-=.025*v.co.y
    ob.data.update()
for mat_name in ('Blockout slate','Blockout canopy','Blockout tip region'):
    m=bpy.data.materials.get(mat_name)
    if m is not None and m.users==0:bpy.data.materials.remove(m)
bpy.ops.object.select_all(action='DESELECT')
bpy.data.objects['Sculpted tail blades'].select_set(True)
bpy.context.view_layer.objects.active=bpy.data.objects['Sculpted tail blades']
bpy.ops.wm.save_as_mainfile(filepath=str(root/'art/ships/nightshade/Nightshade.blend'))
