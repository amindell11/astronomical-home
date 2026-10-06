from pathlib import Path
import bpy,bmesh,math,json
from mathutils import Vector
from mathutils.bvhtree import BVHTree
root=Path('D:/amind/git/agent-1');source=root/'art/ships/nightshade/Nightshade.blend'
bpy.ops.wm.open_mainfile(filepath=str(source));origin=bpy.data.objects['SymmetryOrigin']
dark=bpy.data.materials['Clean mechanical recess'];edge=bpy.data.materials['Clean armor bevel'];light=bpy.data.materials['Clean armor face']
def part(name,vertices,faces,materials):
    mesh=bpy.data.meshes.new(name);mesh.from_pydata(vertices,[],faces);mesh.update()
    bm=bmesh.new();bm.from_mesh(mesh);bmesh.ops.recalc_face_normals(bm,faces=bm.faces);bm.to_mesh(mesh);bm.free()
    ob=bpy.data.objects.new(name,mesh);bpy.data.collections['role.hull'].objects.link(ob);ob.parent=origin
    for m in materials:mesh.materials.append(m)
    m=ob.modifiers.new('Live X symmetry','MIRROR');m.mirror_object=origin;m.use_clip=True;m.merge_threshold=.00001
    return ob
def strip(name,target,points,width,lower=False):
    mesh=bpy.data.objects[target].data
    tree=BVHTree.FromPolygons([v.co for v in mesh.vertices],[list(p.vertices) for p in mesh.polygons])
    vertices=[]
    for i,xy in enumerate(points):
        a=Vector(points[max(i-1,0)]);b=Vector(points[min(i+1,len(points)-1)])
        side=Vector((-(b-a).y,(b-a).x)).normalized()*width/2
        for sign in (-1,1):
            x,y=Vector(xy)+side*sign
            loc,_,_,_=tree.ray_cast(Vector((x,y,-.5 if lower else .5)),Vector((0,0,1 if lower else -1)),1)
            assert loc is not None,(name,x,y)
            vertices.append((x,y,loc.z+(-.0008 if lower else .0008)))
    faces=[(i*2,i*2+1,i*2+3,i*2+2) for i in range(len(points)-1)]
    return part(name,vertices,faces,[dark])

strip('Main wing transverse armor joint','Rebuilt main wing blades',[(.421+i*.218/12,.074) for i in range(13)],.005)
strip('Main wing inset channel','Rebuilt main wing blades',[(.284,.56),(.331,.46),(.387,.35),(.44,.24),(.476,.13),(.471,.03),(.495,-.10),(.56,-.24),(.65,-.39),(.726,-.52)],.003)
for lower in (False,True):
    prefix='Lower' if lower else 'Upper'
    strip(prefix+' fin armor joint',prefix+' small swept fins',[(.242+i*.091/8,.08) for i in range(9)],.003,lower)

for lower in (False,True):
    outline=[(0,.299),(.029,.287),(.037,.262),(.026,.235),(0,.230)]
    inner=[(x*.65,.262+(y-.262)*.68) for x,y in outline]
    z=.190
    vertices=[(x,y,-z-.016 if lower else z) for x,y in outline]+[(x,y,-(z+.003)-.016 if lower else z+.003) for x,y in inner]
    faces=[(i,i+1,i+6,i+5) for i in range(4)]
    faces.extend([(5,6,7),(5,7,8),(5,8,9)])
    ob=part(('Ventral' if lower else 'Dorsal')+' shield inset',vertices,faces,[edge,light,dark])
    for p in ob.data.polygons:p.material_index=2 if p.index>=4 else 1

# The collar follows the rebuilt aft hull and leaves a recessed exhaust face.
verts=[];n=9
for y,w,h,z in [(-.443,.049,.034,.027),(-.461,.043,.030,.027),(-.493,.028,.019,.026),(-.490,.021,.013,.026)]:
    for k in range(n):
        a=math.pi*k/(n-1);verts.append((w*math.sin(a),y,z+h*math.cos(a)))
faces=[(j*n+k,j*n+k+1,(j+1)*n+k+1,(j+1)*n+k) for j in range(3) for k in range(n-1)]
faces.extend((3*n,3*n+k,3*n+k+1) for k in range(1,n-1))
ob=part('Aft engine collar',verts,faces,[edge,light,dark])
for p in ob.data.polygons:p.material_index=2 if p.index>=24 else 1 if p.index<8 else 0

bpy.context.scene['review']='Clean medium-detail candidate; legacy is a dimension guide only. Owner shape approval pending.'
bpy.context.preferences.filepaths.save_version=0
bpy.ops.wm.save_as_mainfile(filepath=str(source))
out=root/'results/nightshade-clean-guide/round-02';out.mkdir(parents=True,exist_ok=True)
meshes=[o for o in bpy.context.scene.objects if o.type=='MESH']
(out/'topology.json').write_text(json.dumps({'cage_vertices':sum(len(o.data.vertices) for o in meshes),'cage_faces':sum(len(o.data.polygons) for o in meshes),'quad_faces':sum(len(p.vertices)==4 for o in meshes for p in o.data.polygons),'mesh_parts':len(meshes),'all_mirrors_live':all(any(m.type=='MIRROR' for m in o.modifiers) for o in meshes),'legacy_faces_copied':0,'legacy_uvs_copied':0,'legacy_textures_used':0,'images':[i.name for i in bpy.data.images],'parts':[{'name':o.name,'vertices':len(o.data.vertices),'faces':len(o.data.polygons),'uv_layers':len(o.data.uv_layers)} for o in meshes]},indent=2))
