import bpy, math
from mathutils import Vector
from pathlib import Path

root=Path('D:/amind/git/agent-4')
out=root/'src/Asteroids3D/Assets/Visuals/Environment/Asteroids/DrawnStudy'
bpy.ops.wm.open_mainfile(filepath=str(root/'art/asteroid-study/AsteroidFractureStudy.blend'))
rock=next(o for o in bpy.context.scene.objects if o.type=='MESH')
rock.name='AsteroidFractureStudy'
mesh=rock.data
uv=mesh.uv_layers.active
vertex_uv={}
for p in mesh.polygons:
 for li in p.loop_indices:
  vertex_uv[mesh.loops[li].vertex_index]=Vector(uv.data[li].uv)

# Selected small impact bowls stay separate from the large original clefts.
craters=[(.34,.56,.028,.036,.055),(.19,.49,.021,.027,.04),(.37,.37,.018,.027,.038),
         (.56,.69,.027,.034,.05),(.82,.48,.025,.033,.045),(.05,.39,.018,.024,.035)]
for vert in mesh.vertices:
 u,v=vertex_uv[vert.index]
 normal=vert.normal.copy()
 depth=0
 for cu,cv,ru,rv,d in craters:
  du=min(abs(u-cu),abs(u-cu-1),abs(u-cu+1))
  t=math.sqrt((du/ru)**2+((v-cv)/rv)**2)
  depth-=d*max(0,1-t*t)
  depth+=.009*math.exp(-((t-1)/.16)**2)
 vert.co+=normal*depth

# Flatten selected ridge shoulders instead of faceting the whole object.
for cu,cv,ru,rv,amount in [(.12,.61,.065,.15,.07),(.38,.73,.07,.15,.075),(.29,.27,.10,.075,.06),(.69,.53,.07,.12,.065)]:
 chosen=[]
 for vert in mesh.vertices:
  u,v=vertex_uv[vert.index]
  du=min(abs(u-cu),abs(u-cu-1),abs(u-cu+1))
  t=math.sqrt((du/ru)**2+((v-cv)/rv)**2)
  if t<1: chosen.append((vert,t))
 n=sum((v.normal for v,t in chosen),Vector()).normalized()
 center=sum((v.co for v,t in chosen),Vector())/len(chosen)
 for vert,t in chosen:
  distance=(vert.co-center).dot(n)
  vert.co-=n*max(0,distance+amount)*(1-t*t)
mesh.update()
mesh.calc_loop_triangles()
triangles=[]
for tri in mesh.loop_triangles:
 coords=[Vector(uv.data[li].uv) for li in tri.loops]
 points=[mesh.vertices[i].co.copy() for i in tri.vertices]
 normals=[mesh.vertices[i].normal.copy() for i in tri.vertices]
 triangles.append((coords,points,normals))
grid={}
for tri in triangles:
 coords=tri[0]
 for x in range(math.floor(min(p.x for p in coords)*64),math.floor(max(p.x for p in coords)*64)+1):
  for y in range(math.floor(min(p.y for p in coords)*64),math.floor(max(p.y for p in coords)*64)+1):
   grid.setdefault((x,y),[]).append(tri)
def surface(u,v):
 for shift in (0,1,-1):
  p=Vector((u+shift,v))
  for coords,points,normals in grid.get((math.floor(p.x*64),math.floor(p.y*64)),[]):
   a,b,c=coords; ab=b-a; ac=c-a; q=p-a
   det=ab.x*ac.y-ab.y*ac.x
   if abs(det)<1e-10: continue
   s=(q.x*ac.y-q.y*ac.x)/det; t=(ab.x*q.y-ab.y*q.x)/det
   if s>=-1e-5 and t>=-1e-5 and s+t<=1.00001:
    n=(normals[0]*(1-s-t)+normals[1]*s+normals[2]*t).normalized()
    return points[0]*(1-s-t)+points[1]*s+points[2]*t+n*.006
 raise ValueError(('UV outside mesh',u,v))
verts=[];faces=[]
def stroke(path,width=.0015):
 samples=[]
 for a,b in zip(path,path[1:]):
  a,b=Vector(a),Vector(b)
  steps=max(2,math.ceil((b-a).length/.0015))
  for i in range(steps): samples.append(a.lerp(b,i/steps))
 samples.append(Vector(path[-1]))
 offset=len(verts)
 for i,p in enumerate(samples):
  tangent=(samples[min(i+1,len(samples)-1)]-samples[max(0,i-1)]).normalized()
  side=Vector((-tangent.y,tangent.x))
  t=i/(len(samples)-1)
  pressure=math.sin(math.pi*t)**.55*(.82+.18*math.sin(t*5+1))
  for sign in (-1,1):
   q=p+side*width*3.1*pressure*sign
   verts.append(surface(q.x,q.y))
 for i in range(len(samples)-1):
  a=offset+2*i;faces.append((a,a+1,a+3,a+2))

# Rim-following strokes: open, angular and tapered, never filled shadow pockets.
paths=[[(.09,.83),(.092,.875),(.125,.924),(.169,.939),(.194,.906)],
[(.216,.745),(.23,.801),(.278,.816),(.314,.773)],
[(.291,.636),(.326,.67),(.344,.722),(.329,.751)],
[(.171,.285),(.176,.342),(.199,.397),(.226,.444)],
[(.236,.221),(.274,.236),(.286,.28)],
[(.64,.868),(.659,.939),(.709,.959),(.744,.923)],
[(.578,.527),(.582,.582),(.613,.668),(.656,.68)],
[(.85,.639),(.873,.72),(.919,.739),(.951,.705)],
[(.687,.203),(.704,.279),(.756,.311),(.79,.276)],
[(.852,.129),(.867,.203),(.914,.24),(.944,.208)]]
for path in paths: stroke(path,.00165)
# A few connecting seams emphasize changes in plane.
for path in [[(.14,.71),(.122,.653),(.131,.604),(.105,.566)],[(.30,.50),(.31,.465),(.294,.422)],
[(.391,.60),(.413,.55),(.398,.505)],[(.64,.42),(.665,.381),(.65,.345)],[(.84,.83),(.824,.778),(.836,.747)]]:
 stroke(path,.0013)
for path in [
[(.322,.665),(.300,.636),(.268,.625),(.244,.643)],
[(.185,.803),(.158,.776),(.127,.776)],
[(.211,.411),(.246,.433),(.264,.408)],
[(.261,.362),(.277,.329),(.284,.306)],
[(.112,.559),(.130,.530),(.153,.513)],
[(.343,.605),(.366,.624),(.388,.613)],
[(.429,.468),(.409,.433),(.414,.397)],
[(.590,.507),(.621,.461),(.658,.455)],
[(.721,.752),(.753,.736),(.772,.700)],
[(.879,.573),(.910,.548),(.940,.567)],
[(.720,.171),(.758,.131),(.795,.151)],
[(.048,.567),(.074,.545),(.080,.505)]]:
 stroke(path,.0013)
for cu,cv,ru,rv,d in craters:
 angles=[-30,10,65,118,164,214,252]
 path=[(cu+ru*math.cos(math.radians(a)),cv+rv*math.sin(math.radians(a))) for a in angles]
 stroke(path,.00125)
 stroke([(cu+ru*.72,cv-rv*.45),(cu+ru*.22,cv-rv*.67),(cu-ru*.26,cv-rv*.53)],.0008)
# Paired tapered chisels have clear direction and open stone between their strokes.
for u,v,scale in [(.26,.55,1),(.35,.81,.7),(.16,.60,.8),(.33,.29,.8),(.48,.49,.8),(.74,.64,1),(.87,.33,.85),(.06,.68,.8)]:
 stroke([(u-.004*scale,v+.013*scale),(u+.003*scale,v),(u+.002*scale,v-.024*scale)],.0012*scale)
 stroke([(u+.003*scale,v),(u+.013*scale,v-.015*scale)],.0008*scale)
drawingmesh=bpy.data.meshes.new('Authored tapered drawing')
drawingmesh.from_pydata(verts,[],faces);drawingmesh.update()
drawing=bpy.data.objects.new('AsteroidSurfaceDrawing',drawingmesh)
bpy.context.collection.objects.link(drawing)
# Orient each ribbon outward for the imported one-sided material.
for face in drawingmesh.polygons:
 if face.normal.dot(face.center)<0:
  face.flip()
for face in drawingmesh.polygons: face.use_smooth=True
ink=bpy.data.materials.new('Cool graphite linework');ink.diffuse_color=(.018,.024,.034,1)
drawing.data.materials.append(ink)
mat=rock.data.materials[0]
mat.name='Hand brushed slate and olive'
for node in mat.node_tree.nodes:
 if node.type=='TEX_IMAGE':
  node.image=bpy.data.images.load(str(out/'AsteroidBrushAlbedo.png'));node.image.pack()
bpy.ops.object.select_all(action='DESELECT')
rock.select_set(True);bpy.context.view_layer.objects.active=rock
bpy.ops.wm.save_as_mainfile(filepath=str(root/'art/asteroid-study/AsteroidDrawnStudy.blend'))
for obj,filename in [(drawing,'AsteroidSurfaceDrawing.fbx')]:
 bpy.ops.object.select_all(action='DESELECT');obj.select_set(True);bpy.context.view_layer.objects.active=obj
 bpy.ops.export_scene.fbx(filepath=str(out/filename),use_selection=True,axis_forward='-Z',axis_up='Y',global_scale=1,apply_unit_scale=True,bake_space_transform=True,object_types={'MESH'},use_mesh_modifiers=True,add_leaf_bones=False,bake_anim=False,path_mode='AUTO')
print('DRAWING',len(verts),'vertices',len(faces),'ribbon quads')
