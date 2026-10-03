import bpy,json
from pathlib import Path
from mathutils import Vector
from mathutils.kdtree import KDTree
from mathutils.bvhtree import BVHTree
out=Path('D:/amind/git/agent-7/results/crimson-shoulder-20260928-003934')
bpy.ops.wm.open_mainfile(filepath=str(out/'Before-shoulder-fix.blend'))
s=bpy.context.scene
s.render.engine='BLENDER_WORKBENCH';s.render.resolution_x=1100;s.render.resolution_y=1000;s.render.resolution_percentage=100
sh=s.display.shading;sh.light='STUDIO';sh.color_type='SINGLE';sh.single_color=(.63,.66,.70);sh.show_cavity=True;sh.cavity_type='BOTH';sh.background_type='WORLD';s.world.color=(.07,.08,.1)
s.view_settings.view_transform='Standard'
d=bpy.data.cameras.new('QA');cam=bpy.data.objects.new('QA',d);s.collection.objects.link(cam);s.camera=cam;d.type='ORTHO';d.ortho_scale=1.75
cam.location=(0,-.08,-5);cam.rotation_euler=(Vector((0,-.08,0))-cam.location).to_track_quat('-Z','Y').to_euler()
for o in s.objects:
 if o.type=='MESH':o.hide_render=o.hide_get()
def render(label):
 s.render.filepath=str(out/(label+'.png'));bpy.ops.render.render(write_still=True)
render('before')
o=bpy.data.objects['Lower wing foundation'];dg=bpy.context.evaluated_depsgraph_get();ev=o.evaluated_get(dg);me=ev.to_mesh();me.calc_loop_triangles()
k=KDTree(len(o.data.vertices))
for v in o.data.vertices:k.insert(v.co,v.index)
k.balance()
lookup={}
for v in me.vertices:
 co=v.co.copy();co.x=-abs(co.x)
 nearest,i,dist=k.find(co);assert dist<1e-6
 lookup[v.index]=i
replacements={}
for p in me.polygons:
 if len(p.vertices)<=4 or p.center.x<=0:continue
 ids={lookup[i] for i in p.vertices}
 source=next(p2 for p2 in o.data.polygons if set(p2.vertices)==ids)
 replacements[source.index]=[[lookup[i] for i in reversed(t.vertices)] for t in me.loop_triangles if t.polygon_index==p.index]
faces=[];normals=[]
for p in o.data.polygons:
 fs=replacements.get(p.index,[list(p.vertices)]);faces.extend(fs)
 normals.extend([list(p.normal) for f in fs for i in f])
verts=[list(v.co) for v in o.data.vertices];ev.to_mesh_clear()
assert len(replacements)==2
new=bpy.data.meshes.new('Lower wing foundation - explicit cap topology');new.from_pydata(verts,[],faces);new.update()
for p in new.polygons:p.use_smooth=True
new.normals_split_custom_set(normals)
o.data=new
bpy.context.view_layer.update()
(out/'candidate.json').write_text(json.dumps({'vertices':verts,'faces':faces,'normals':normals}))
# Confirm complete evaluated triangle symmetry and clearance at the visible patch.
ev=o.evaluated_get(bpy.context.evaluated_depsgraph_get());me=ev.to_mesh();me.calc_loop_triangles()
def key(coords):return tuple(sorted(tuple(round(c,5) for c in v) for v in coords))
ts={key([me.vertices[i].co for i in t.vertices]) for t in me.loop_triangles}
mis=[t for t in ts if key([(-v[0],v[1],v[2]) for v in t]) not in ts]
tree=BVHTree.FromPolygons([v.co for v in me.vertices],[list(t.vertices) for t in me.loop_triangles],all_triangles=True)
z=[tree.ray_cast(Vector((x,0,-3)),Vector((0,0,1)))[0].z for x in [-.35,.35]]
checks={'replaced_caps':len(replacements),'asymmetric_triangles':len(mis),'patch_foundation_z':z,'vertices_unchanged':verts==json.loads((out/'before.json').read_text())['objects'][o.name]['mesh']['vertices']}
ev.to_mesh_clear()
(out/'checks.json').write_text(json.dumps(checks,indent=2));assert not mis
render('after')
print(json.dumps(checks))

