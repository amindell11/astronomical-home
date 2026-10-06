from pathlib import Path
import bpy,bmesh,math,json
from mathutils import Vector,Matrix
from mathutils.bvhtree import BVHTree

root=Path('D:/amind/git/agent-1')
source=root/'art/ships/nightshade/Nightshade.blend'
out=root/'results/nightshade-clean-guide/round-01';out.mkdir(parents=True,exist_ok=True)
bpy.ops.wm.open_mainfile(filepath=str(source))
origin=bpy.data.objects['SymmetryOrigin']
tail=bpy.data.objects['Sculpted compact tail pair']
for ob in list(bpy.context.scene.objects):
    if ob.type=='MESH' and ob!=tail:bpy.data.objects.remove(ob,do_unlink=True)
with bpy.data.libraries.load(str(root/'results/nightshade-legacy-base/inspection/LegacyNative.blend'),link=False) as (_,loaded):loaded.objects=['temp']
guide=loaded.objects[0]
coords=[Matrix.Rotation(math.pi,4,'Z')@guide.matrix_world@v.co for v in guide.data.vertices]
guide_bvh=BVHTree.FromPolygons(coords,[list(p.vertices) for p in guide.data.polygons],all_triangles=False)

def mat(name,rgb):
    m=bpy.data.materials.get(name) or bpy.data.materials.new(name);m.diffuse_color=(*rgb,1);return m
slate=mat('Clean armor slate',(.090,.110,.160));highlight=mat('Clean armor face',(.120,.145,.210))
edge=mat('Clean armor bevel',(.045,.058,.090));dark=mat('Clean mechanical recess',(.018,.024,.043))
glass=mat('Clean violet canopy',(.170,.095,.700));pink=mat('Clean magenta emitter',(.700,.015,.800))
palette=[slate,highlight,edge,dark,pink]

def part(name,verts,faces,role='hull',materials=None,bevel=0,smooth=True):
    mesh=bpy.data.meshes.new(name+' quad cage');mesh.from_pydata(verts,[],faces);mesh.update()
    bm=bmesh.new();bm.from_mesh(mesh);bmesh.ops.recalc_face_normals(bm,faces=bm.faces);bm.to_mesh(mesh);bm.free()
    ob=bpy.data.objects.new(name,mesh);bpy.data.collections['role.'+role].objects.link(ob);ob.parent=origin
    for m in materials or palette:mesh.materials.append(m)
    for p in mesh.polygons:p.use_smooth=smooth
    m=ob.modifiers.new('Live X symmetry','MIRROR');m.mirror_object=origin;m.use_clip=True;m.merge_threshold=.00001
    if bevel:
        m=ob.modifiers.new('Machined edge softness','BEVEL');m.width=bevel;m.segments=2;m.angle_limit=.6
    ob['topology']='New authored surface; no legacy faces or UVs transferred'
    return ob

def loft(name,rows,materials=None):
    verts=[];n=17
    for y,w,hi,lo in rows:
        center=(hi+lo)/2;h=(hi-lo)/2
        for k in range(n):
            a=math.pi*k/(n-1)
            # Superellipse sections follow the original's broad, flattened hull.
            x=w*(math.sin(a)**.72);z=center+h*math.copysign(abs(math.cos(a))**.72,math.cos(a))
            verts.append((0 if k in (0,n-1) else x,y,z))
    faces=[(j*n+k,j*n+k+1,(j+1)*n+k+1,(j+1)*n+k) for j in range(len(rows)-1) for k in range(n-1)]
    for j in (0,len(rows)-1):
        c=len(verts);y,w,hi,lo=rows[j];verts.append((0,y,(hi+lo)/2))
        faces.extend((c,j*n+k,j*n+k+1) for k in range(n-1))
    ob=part(name,verts,faces,materials=materials)
    for p in ob.data.polygons:
        k=p.index%(n-1);p.material_index=2 if k in (7,8) else 0
    return ob

body_rows=[(.851,.012,-.017,-.068),(.833,.055,.000,-.080),(.800,.101,.020,-.091),
 (.75,.139,.036,-.102),(.70,.165,.049,-.110),(.65,.195,.059,-.116),(.60,.214,.068,-.119),
 (.55,.226,.076,-.123),(.50,.234,.084,-.127),(.45,.235,.103,-.142),(.40,.232,.125,-.156),
 (.35,.222,.137,-.157),(.30,.211,.141,-.157),(.25,.204,.143,-.157),(.20,.195,.141,-.157),
 (.15,.183,.146,-.154),(.10,.170,.146,-.149),(.05,.156,.113,-.120),(0,.143,.092,-.092),
 (-.10,.113,.085,-.077),(-.20,.081,.086,-.054),(-.30,.059,.085,-.030),
 (-.40,.058,.083,-.020),(-.45,.047,.070,-.013),(-.485,.030,.052,.003),(-.498,.022,.043,.014)]
body=loft('Rebuilt pitched hull',body_rows)
body['guide']='Native legacy cross-sections; same dimensional envelope and forward pitch'

def hits(x,y):
    result=[];start=Vector((x,y,.5))
    for _ in range(18):
        loc,n,index,dist=guide_bvh.ray_cast(start,Vector((0,0,-1)),1.0)
        if loc is None:break
        result.append(loc.z);start=loc-Vector((0,0,.00005))
    return result

def blade(name,rows,kind='wing',lower=False):
    # Rows: longitudinal position, inside/outside X, expected upper surface.
    us=[0,.05,.20,.45,.72,.95,1];verts=[]
    for y,x0,x1,z in rows:
        samples=[]
        for u in us:
            x=x0+(x1-x0)*u
            sample_x=x0+(x1-x0)*max(.045,min(.955,u))
            values=hits(sample_x,y)
            if kind=='wing':values=[v for v in values if -.11<v<.09]
            else:values=[v for v in values if .075<v<.196]
            if values:
                top=max(values);bottom=min(values)
                if top-bottom>.085:bottom=top-(.020 if kind=='fin' else .027)
            else:top=z;bottom=z-(.015 if kind=='fin' else .022)
            # Narrow edge bevels keep the thin original blades readable.
            if u in (0,1):
                mid=(top+bottom)/2;top=mid+.0025;bottom=mid-.0025
            samples.append((x,y,top,bottom))
        ring=[(x,y,z1) for x,y,z1,z2 in samples]+[(x,y,z2) for x,y,z1,z2 in reversed(samples)]
        if lower:ring=[(x,y,-z-.016) for x,y,z in ring]
        verts.extend(ring)
    n=14;faces=[]
    for j in range(len(rows)-1):
        for k in range(n):faces.append((j*n+k,j*n+(k+1)%n,(j+1)*n+(k+1)%n,(j+1)*n+k))
    for j in (0,len(rows)-1):
        for k in range(1,n-1):faces.append((j*n,j*n+k,j*n+k+1))
    ob=part(name,verts,faces)
    for p in ob.data.polygons:
        k=p.index%14;p.material_index=1 if k in (0,1) else 2 if k in (5,6,12,13) else 0
        row=p.index//14
        if kind=='wing' and row>=len(rows)-4 and k in (0,1,2,3,4,5):p.material_index=4
        if kind=='fin' and row>=len(rows)-3 and k in (0,1,2,3,4,5):p.material_index=4
    for e in ob.data.edges:
        a,b=e.vertices
        if a%14==b%14 and a%14 in (0,2,6,7,11,13):e.use_edge_sharp=True
    ob['guide']='New quad strips sampled against native legacy surfaces; no mesh or UV import'
    return ob

wing_rows=[(.881,.262,.268,-.036),(.855,.253,.285,-.038),(.80,.253,.338,-.031),
 (.75,.251,.400,-.030),(.70,.257,.436,-.026),(.65,.239,.475,-.021),(.60,.216,.496,-.019),
 (.55,.210,.526,-.012),(.50,.225,.544,-.010),(.45,.260,.570,-.007),(.40,.297,.589,-.012),
 (.35,.331,.603,-.012),(.30,.360,.612,-.012),(.25,.386,.621,-.013),(.20,.407,.629,-.012),
 (.15,.420,.638,-.010),(.10,.419,.647,-.008),(.05,.407,.652,.000),(0,.406,.657,.008),
 (-.10,.441,.683,.013),(-.20,.494,.705,.014),(-.30,.557,.729,.016),(-.40,.621,.750,.012),
 (-.50,.685,.788,.014),(-.60,.750,.811,.018),(-.65,.768,.813,.020),(-.70,.797,.817,.012),(-.714,.813,.821,.008)]
wing=blade('Rebuilt main wing blades',wing_rows)
fin_rows=[(.435,.200,.217,.098),(.40,.196,.275,.106),(.36,.136,.297,.129),
 (.32,.044,.312,.144),(.28,.009,.318,.153),(.24,0,.323,.162),(.20,0,.327,.163),
 (.16,.085,.334,.150),(.12,.200,.341,.130),(.10,.225,.344,.125),(.05,.247,.355,.132),
 (0,.252,.364,.141),(-.05,.257,.373,.146),(-.10,.259,.372,.150),(-.15,.297,.360,.157),(-.175,.326,.347,.157)]
for lower in (False,True):blade(('Lower' if lower else 'Upper')+' small swept fins',fin_rows,'fin',lower)

def plate(name,rows,lower=False,bevel=0):
    verts=[];n=8
    for y,w,z,h in rows:
        for x,k in [(0,1),(.45,1),(.90,.6),(1,0),(1,-.5),(.85,-1),(.4,-1),(0,-1)]:
            zz=z+h*k;verts.append((w*x,y,-zz-.016 if lower else zz))
    faces=[(j*n+k,j*n+k+1,(j+1)*n+k+1,(j+1)*n+k) for j in range(len(rows)-1) for k in range(n-1)]
    for j in (0,len(rows)-1):
        faces.extend((j*n,j*n+k,j*n+k+1) for k in range(1,n-1))
    ob=part(name,verts,faces,bevel=bevel)
    for p in ob.data.polygons:p.material_index=1 if p.index%7 in (1,2) else 0
    for e in ob.data.edges:
        a,b=e.vertices
        if a%n==b%n and a%n in (1,3,5):e.use_edge_sharp=True
    return ob

spine_rows=[(.417,.082,.149,.005),(.39,.090,.157,.010),(.33,.059,.167,.010),
 (.28,.045,.177,.008),(.21,.071,.175,.010),(.14,.119,.161,.014),(.10,.136,.159,.015),
 (.04,.118,.164,.012),(0,.099,.168,.010),(-.07,.062,.163,.009),(-.115,.032,.156,.007)]
cap_rows=[(.338,.008,.188,.002),(.31,.048,.184,.004),(.27,.067,.182,.005),(.22,.063,.183,.006),
 (.18,.052,.185,.005),(.15,.024,.188,.004)]
for lower in (False,True):
    prefix='Ventral' if lower else 'Dorsal'
    plate(prefix+' central blade',spine_rows,lower)
    plate(prefix+' shield cap',cap_rows,lower,bevel=.0015)

body_bvh=BVHTree.FromPolygons([v.co for v in body.data.vertices],[list(p.vertices) for p in body.data.polygons])
def body_z(x,y,lower):
    loc,_,_,_=body_bvh.ray_cast(Vector((x,y,-.5 if lower else .5)),Vector((0,0,1 if lower else -1)),1)
    return loc.z

glass_rows=[(.817,.010),(.794,.055),(.758,.092),(.71,.118),(.65,.142),(.58,.157),(.52,.159),(.47,.153),(.425,.143),(.413,.130)]
for lower in (False,True):
    for surround in (True,False):
        verts=[];n=7
        for y,w in glass_rows:
            for k in range(n):
                x=(w+(.009 if surround else 0))*k/(n-1)
                z=body_z(x,y,lower)+(-1 if lower else 1)*(.002 if surround else .004)
                verts.append((x,y,z))
        faces=[(j*n+k,j*n+k+1,(j+1)*n+k+1,(j+1)*n+k) for j in range(len(glass_rows)-1) for k in range(n-1)]
        ob=part(('Lower' if lower else 'Upper')+(' canopy frame' if surround else ' canopy glazing'),verts,faces,
            'hull' if surround else 'canopy',[dark] if surround else [glass])
        mod=ob.modifiers.new('Thin glass or frame shell','SOLIDIFY');mod.thickness=.001

# Thin wing root fairings and narrow armor seams add structure without changing the guide silhouette.
plate('Aft spine armor',[(.08,.112,.10,.006),(0,.11,.104,.008),(-.10,.09,.10,.009),
    (-.20,.061,.098,.009),(-.30,.038,.096,.006),(-.40,.027,.093,.004),(-.445,.020,.076,.003)])
for name in ('Fork upper armor','Fork ridge','Fork bevel','Fork underside'):
    bpy.data.materials[name].diffuse_color=slate.diffuse_color if name=='Fork upper armor' else highlight.diffuse_color if name=='Fork ridge' else edge.diffuse_color
tail['topology']='New quad cage authored independently; not derived from legacy faces'

bpy.data.objects.remove(guide,do_unlink=True)
bpy.data.orphans_purge(do_recursive=True)
for ob in bpy.context.scene.objects:ob.select_set(False)
body.select_set(True);bpy.context.view_layer.objects.active=body
bpy.context.scene['review']='Clean remodel using original native dimensions as a hard guide; no imported hull topology or UVs'
bpy.context.scene['legacy_reuse']='Reference measurements only; no legacy geometry or texture in deliverable'
bpy.context.preferences.filepaths.save_version=0
bpy.ops.wm.save_as_mainfile(filepath=str(source))
meshes=[o for o in bpy.context.scene.objects if o.type=='MESH']
(out/'topology.json').write_text(json.dumps({'meshes':[{'name':o.name,'vertices':len(o.data.vertices),'faces':len(o.data.polygons),'quads':sum(len(p.vertices)==4 for p in o.data.polygons),'uv_layers':len(o.data.uv_layers)} for o in meshes],'legacy_faces_copied':0,'legacy_uvs_copied':0,'legacy_textures_used':0,'guide_body_rows':body_rows,'guide_wing_rows':wing_rows},indent=2))
print('CLEAN_REBUILD_READY',sum(len(o.data.vertices) for o in meshes),sum(len(o.data.polygons) for o in meshes))
