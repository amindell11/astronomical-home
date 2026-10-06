from pathlib import Path
import json,math,sys
import bpy,bmesh
from mathutils import Vector

root=Path('D:/amind/git/agent-1')
out=root/'results/nightshade-legacy-base/round-01'
out.mkdir(parents=True,exist_ok=True)
source=root/'art/ships/nightshade/Nightshade.blend'
bpy.ops.wm.open_mainfile(filepath=str(source))
origin=bpy.data.objects['SymmetryOrigin']
for ob in list(bpy.context.scene.objects):
    if ob.type=='MESH': bpy.data.objects.remove(ob,do_unlink=True)
with bpy.data.libraries.load(str(root/'results/nightshade-legacy-base/inspection/LegacyReview.blend'),link=False) as (data,loaded):
    loaded.objects=['Original Nightshade']
ob=loaded.objects[0]
ob.name='Legacy body wings and upper lower fins'
bpy.data.collections['role.hull'].objects.link(ob)
ob.parent=origin
ob.rotation_euler.z=math.pi
components=json.loads((root/'results/nightshade-legacy-base/inspection/component-indices.json').read_text())
remove=set(components[1]+components[2])
bm=bmesh.new();bm.from_mesh(ob.data);bm.verts.ensure_lookup_table()
bmesh.ops.delete(bm,geom=[bm.verts[i] for i in remove],context='VERTS')
bm.to_mesh(ob.data);bm.free()
ob.data.update()
mirror=ob.modifiers.new('Live X symmetry — original proportions','MIRROR')
mirror.use_axis=(True,False,False)
mirror.use_bisect_axis=(True,False,False)
mirror.use_clip=True
mirror.merge_threshold=.00001
mirror.mirror_object=origin
ob['source']='Original cruiserUpdate1.fbx; native dimensions; rigid orientation only'
ob['preserved']='Original body, main wings, canopy, dorsal and ventral small fins, exhaust'
ob['changed']='Disconnected original tail forks removed; all retained vertex coordinates unchanged'
mat=ob.data.materials[0]
mat.name='Original Nightshade paint — reference preview'
mat.diffuse_color=(.22,.25,.34,1)
mat.use_nodes=True
tex=next((i for i in bpy.data.images if i.name.startswith('Nightshade_Albedo')),None)
if tex is None:
    tex=bpy.data.images.load(str(root/'src/Asteroids3D/Assets/Visuals/Ships/Nightshade/Nightshade_Albedo.png'))
tex.pack()
nodes=mat.node_tree.nodes
image=next((n for n in nodes if n.type=='TEX_IMAGE'),None) or nodes.new('ShaderNodeTexImage')
image.image=tex;nodes.active=image
shader=next(n for n in nodes if n.type=='BSDF_PRINCIPLED')
mat.node_tree.links.new(image.outputs['Color'],shader.inputs['Base Color'])
shader.inputs['Roughness'].default_value=.65

def material(name,color):
    m=bpy.data.materials.get(name) or bpy.data.materials.new(name)
    m.diffuse_color=(*color,1)
    return m
tail_mats=[material('Fork upper armor',(.205,.231,.315)),
           material('Fork ridge',(.27,.30,.40)),
           material('Fork bevel',(.17,.19,.27)),
           material('Fork underside',(.095,.115,.17))]

# x, y, z, half-width, half-thickness; only the new forks are authored.
stations=[(.198,.145,.040,.027,.015),(.198,.090,.040,.030,.018),
          (.198,-.160,.047,.031,.017),(.215,-.300,.052,.036,.019),
          (.258,-.435,.070,.058,.024),(.286,-.555,.097,.070,.027),
          (.287,-.675,.129,.062,.025),(.264,-.805,.155,.045,.021),
          (.231,-.930,.178,.026,.014),(.210,-1.015,.190,.002,.002)]
def catmull(a,b,c,d,t):
    return [0.5*((2*b[i])+(-a[i]+c[i])*t+(2*a[i]-5*b[i]+4*c[i]-d[i])*t*t+(-a[i]+3*b[i]-3*c[i]+d[i])*t*t*t) for i in range(5)]
rows=[]
for j in range(len(stations)-1):
    for k in range(3): rows.append(catmull(stations[max(j-1,0)],stations[j],stations[j+1],stations[min(j+2,len(stations)-1)],k/3))
rows.append(stations[-1])
profile=[(-1,-.3),(-.9,.4),(-.64,.83),(0,1),(.66,.75),(.94,.2),(1,-.4),(.67,-.8),(0,-1),(-.7,-.8)]
vertices=[]
for i,(x,y,z,w,h) in enumerate(rows):
    prev=rows[max(i-1,0)];nxt=rows[min(i+1,len(rows)-1)]
    side=Vector((prev[1]-nxt[1],nxt[0]-prev[0],0)).normalized()
    for a,b in profile:
        v=Vector((x,y,z))+side*(a*w)+Vector((0,0,b*h))
        vertices.append(v)
faces=[]
for i in range(len(rows)-1):
    for j in range(len(profile)):
        faces.append((i*10+j,i*10+(j+1)%10,(i+1)*10+(j+1)%10,(i+1)*10+j))
faces.extend([tuple(reversed(range(10))),tuple((len(rows)-1)*10+j for j in range(10))])
mesh=bpy.data.meshes.new('Sculpted fork editable quad cage')
mesh.from_pydata(vertices,[],faces);mesh.update()
fork=bpy.data.objects.new('Sculpted compact tail pair',mesh)
bpy.data.collections['role.hull'].objects.link(fork);fork.parent=origin
for m in tail_mats:mesh.materials.append(m)
for p in mesh.polygons:
    j=p.index%10
    p.material_index=1 if j in (2,3) else 2 if j in (0,1,4,5) else 3
    p.use_smooth=True
bm=bmesh.new();bm.from_mesh(mesh)
bmesh.ops.recalc_face_normals(bm,faces=bm.faces)
bm.to_mesh(mesh);bm.free()
m=fork.modifiers.new('Live X symmetry — tail pair','MIRROR')
m.mirror_object=origin;m.use_clip=True;m.merge_threshold=.00001
fork['design']='Side/iso priority: compact curved forks, modest upsweep no higher than the original upper fins'
for image in list(bpy.data.images):
    if image.source=='FILE' and image.users==0:bpy.data.images.remove(image)
for ob in bpy.context.scene.objects:ob.select_set(False)
fork.select_set(True);bpy.context.view_layer.objects.active=fork
bpy.context.scene['review']='Legacy-based medium-detail shape candidate; owner approval pending'
bpy.context.scene['legacy_reuse']='Native mesh body, wings and both upper/lower small fins; only tail forks replaced'
bpy.context.preferences.filepaths.save_version=0
bpy.ops.wm.save_as_mainfile(filepath=str(source))
(out/'revision.json').write_text(json.dumps({'legacy_fbx':'src/Asteroids3D/Assets/Visuals/Ships/Nightshade/cruiserUpdate1.fbx','legacy_scale':[1,1,1],'legacy_rotation_z_degrees':180,'removed_components':[1,2],'retained_coordinates':'unchanged','tail_stations':stations},indent=2))
print('LEGACY_BASE_READY')
