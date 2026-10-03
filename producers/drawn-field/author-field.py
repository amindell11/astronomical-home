import bpy, bmesh, math, json, sys
from pathlib import Path
from mathutils import Vector, Matrix
from mathutils.bvhtree import BVHTree
from mathutils.geometry import barycentric_transform

root=Path('D:/amind/git/agent-4')
base=root/'src/Asteroids3D/Assets/Visuals/Environment/Asteroids/DrawnField'
source=root/'scratch/capture/field-sources'
art=root/'art/asteroid-field'
base.mkdir(exist_ok=True);art.mkdir(exist_ok=True)
# Each shape gets its own impact directions and chisel orientation.
layouts={
1:[(25,22,.14),(135,-18,.11),(225,38,.13),(310,-35,.10)],
2:[(55,-12,.18),(165,42,.12),(250,-30,.14),(345,25,.11)],
3:[(5,35,.12),(90,-22,.14),(200,10,.16),(290,-38,.11)],
4:[(35,8,.13),(120,-36,.10),(210,32,.15),(330,-12,.14)],
5:[(15,-25,.17),(110,30,.13),(215,-10,.18),(305,40,.12)],
6:[(50,32,.12),(150,-20,.16),(235,20,.11),(320,-35,.14)],
7:[(10,10,.15),(100,-30,.12),(195,40,.13),(280,-10,.16)],
8:[(40,-20,.13),(145,25,.16),(230,-40,.10),(325,15,.12)],
9:[(65,35,.14),(155,-15,.17),(240,15,.12),(335,-35,.11)],
10:[(20,25,.11),(115,-35,.15),(205,20,.13),(300,-10,.16)]}
selected=[int(a) for a in sys.argv[sys.argv.index('--')+1:]] if '--' in sys.argv else range(1,11)
for number in selected:
 out=base/f'Shape{number:02}';out.mkdir(exist_ok=True)
 bpy.ops.wm.read_factory_settings(use_empty=True)
 data=json.loads((source/f'Asteroid{number}_LOD0.json').read_text())
 # FBX's Unity conversion maps Blender (-x,-z,y) back to the original game coordinates.
 verts=[(-v['x'],-v['z'],v['y']) for v in data['vertices']]
 faces=[data['triangles'][i:i+3] for i in range(0,len(data['triangles']),3)]
 mesh=bpy.data.meshes.new(f'Asteroid{number}');mesh.from_pydata(verts,[],faces);mesh.update()
 low=bpy.data.objects.new(f'Asteroid{number}',mesh);bpy.context.collection.objects.link(low)
 bpy.context.view_layer.objects.active=low;low.select_set(True)
 bm=bmesh.new();bm.from_mesh(mesh);bmesh.ops.remove_doubles(bm,verts=list(bm.verts),dist=.00001)
 bmesh.ops.recalc_face_normals(bm,faces=list(bm.faces));bm.to_mesh(mesh);bm.free()
 for f in mesh.polygons: f.use_smooth=True
 mesh.update()
 radius=max(v.co.length for v in mesh.vertices)
 def tree():
  mesh.calc_loop_triangles()
  return BVHTree.FromPolygons([v.co for v in mesh.vertices],[list(t.vertices) for t in mesh.loop_triangles],all_triangles=True)
 bvh=tree()
 impacts=[]
 for az,el,size in layouts[number]:
  az=math.radians(az);el=math.radians(el)
  d=Vector((math.cos(az)*math.cos(el),math.sin(az)*math.cos(el),math.sin(el)))
  p,n,idx,dist=bvh.ray_cast(d*radius*3,-d,radius*6)
  assert p is not None
  tangent=n.cross(Vector((0,0,1)))
  if tangent.length<.1: tangent=n.cross(Vector((0,1,0)))
  tangent.normalize();side=n.cross(tangent).normalized()
  impacts.append((p.copy(),n.copy(),tangent,side,size*radius))
 # Shallow impact bowls retain the source silhouette and dominant clefts.
 for v in mesh.vertices:
  shift=0
  for p,n,t,s,r in impacts:
   q=v.co-p
   if abs(q.dot(n))>r*.7: continue
   d=math.sqrt((q.dot(t)/r)**2+(q.dot(s)/(r*.78))**2)
   if d<1: shift-=r*.18*(1-d*d)**2
  v.co+=v.normal*shift
 mesh.update();bvh=tree()
 def project(p,offset=.005):
  q,n,idx,d=bvh.find_nearest(p)
  return q+n*(offset*radius)
 # Rank actual ridge breaks and trace continuous, separated sections.
 bm=bmesh.new();bm.from_mesh(mesh);bm.verts.ensure_lookup_table();bm.edges.ensure_lookup_table();bm.normal_update()
 scores={e.index:e.calc_face_angle(0) for e in bm.edges}
 ranked=sorted(bm.edges,key=lambda e:scores[e.index],reverse=True)
 paths=[];used=set();centers=[]
 for edge in ranked:
  if len(paths)>=22: break
  center=(edge.verts[0].co+edge.verts[1].co)*.5
  if scores[edge.index]<.12 or any((center-c).length<radius*.27 for c in centers):continue
  chain=[edge.verts[0],edge.verts[1]];local={v.index for v in chain}
  for reverse in (False,True):
   if reverse: chain.reverse()
   for step in range(5):
    prev,current=chain[-2:];direction=(current.co-prev.co).normalized()
    options=[]
    for e in current.link_edges:
     other=e.other_vert(current)
     if other.index in local or other.index in used:continue
     align=direction.dot((other.co-current.co).normalized())
     if align<.25:continue
     options.append((scores[e.index]+align*.32,other))
    if not options:break
    other=max(options,key=lambda x:x[0])[1];chain.append(other);local.add(other.index)
  length=sum((a.co-b.co).length for a,b in zip(chain,chain[1:]))
  if length<radius*.22:continue
  points=[v.co.copy() for v in chain]
  if length>radius*.9:points=points[:max(3,int(len(points)*radius*.9/length))]
  paths.append(points);centers.append(center.copy());used.update(v.index for v in chain)
 bm.free()
 ribbonverts=[];ribbonfaces=[]
 def stroke(points,width):
  samples=[]
  for a,b in zip(points,points[1:]):
   count=max(2,math.ceil((b-a).length/(radius*.007)))
   samples.extend(a.lerp(b,i/count) for i in range(count))
  samples.append(points[-1]);start=len(ribbonverts)
  for i,p in enumerate(samples):
   q,n,idx,d=bvh.find_nearest(p)
   tangent=(samples[min(i+1,len(samples)-1)]-samples[max(0,i-1)]).normalized()
   side=n.cross(tangent).normalized();u=i/(len(samples)-1)
   pressure=math.sin(math.pi*u)**.55*(.84+.16*math.sin(u*5+number))
   for sign in (-1,1):ribbonverts.append(project(q+side*width*1.5*radius*pressure*sign))
  for i in range(len(samples)-1):
   a=start+2*i;ribbonfaces.append((a,a+1,a+3,a+2))
 for k,path in enumerate(paths):stroke(path,.006 if k<8 else .0042)
 for p,n,t,s,r in impacts:
  stroke([project(p+t*(r*math.cos(math.radians(a)))+s*(r*.78*math.sin(math.radians(a))),0) for a in (-35,5,58,112,165,210,240)],.0048)
  stroke([p+t*r*.50-s*r*.30,p-t*r*.05-s*r*.45,p-t*r*.32-s*r*.27],.0032)
 # Paired nicks and scuff fans share the direction of nearby ridge breaks.
 cuts=[]
 for k,path in enumerate(paths[::2]):
  p=project(path[len(path)//2],0);q,n,idx,d=bvh.find_nearest(p)
  t=(path[-1]-path[0]).normalized();t=(t-n*t.dot(n)).normalized();s=n.cross(t).normalized()
  p=q+s*radius*.075
  stroke([p+t*radius*.04,p,p-t*radius*.055+s*radius*.018],.0036)
  for j in range(3):
   cuts.append((p+s*radius*(.045+j*.035)+t*radius*j*.012,n,t,s,radius*(.045+.01*(j%2)),radius*.013,radius*.008))
 # UVs are spherical, with per-triangle seam unwrap for the shared quiet paint.
 uv=mesh.uv_layers.active or mesh.uv_layers.new(name='PaintUV')
 for face in mesh.polygons:
  coords=[]
  for vi in face.vertices:
   p=mesh.vertices[vi].co;coords.append([math.atan2(p.y,p.x)/(2*math.pi)+.5,math.asin(max(-1,min(1,p.z/p.length)))/math.pi+.5])
  if max(c[0] for c in coords)-min(c[0] for c in coords)>.5:
   for c in coords:
    if c[0]<.5:c[0]+=1
  for li,c in zip(face.loop_indices,coords):uv.data[li].uv=c
 drawingmesh=bpy.data.meshes.new('Fitted graphite strokes');drawingmesh.from_pydata(ribbonverts,[],ribbonfaces);drawingmesh.update()
 for face in drawingmesh.polygons:
  q,n,idx,d=bvh.find_nearest(face.center)
  if face.normal.dot(n)<0:face.flip()
  face.use_smooth=True
 drawing=bpy.data.objects.new(f'Asteroid{number}Drawing',drawingmesh);bpy.context.collection.objects.link(drawing)
 mat=bpy.data.materials.new('Painted slate');mat.use_nodes=True;low.data.materials.append(mat)
 paint=bpy.data.images.load(str(base.parent/'DrawnStudy/AsteroidBrushAlbedo.png'));paint.pack()
 paintnode=mat.node_tree.nodes.new('ShaderNodeTexImage');paintnode.image=paint
 mat.node_tree.links.new(paintnode.outputs['Color'],mat.node_tree.nodes.get('Principled BSDF').inputs['Base Color'])
 high=low.copy();high.data=mesh.copy();bpy.context.collection.objects.link(high);high.name='Editable scuff sculpt'
 bpy.ops.object.select_all(action='DESELECT');high.select_set(True);bpy.context.view_layer.objects.active=high
 sub=high.modifiers.new('Scuff sculpt density','SUBSURF');sub.subdivision_type='SIMPLE';sub.levels=2
 bpy.ops.object.modifier_apply(modifier=sub.name)
 baseline=[v.normal.copy() for v in high.data.vertices]
 mesh.calc_loop_triangles();tris=list(mesh.loop_triangles)
 smooth=[]
 for vi,v in enumerate(high.data.vertices):
  q,n,idx,dist=bvh.find_nearest(v.co);ids=tris[idx].vertices
  smooth.append(barycentric_transform(q,*[mesh.vertices[j].co for j in ids],*[mesh.vertices[j].normal for j in ids]).normalized())
  depth=0
  for p,n,t,s,rl,rw,amount in cuts:
   delta=v.co-p
   if abs(delta.dot(n))>radius*.08:continue
   x=delta.dot(t);y=delta.dot(s);tip=max(0,1-(abs(x)/rl)**3)
   if tip<=0:continue
   d=abs(y)/(rw*tip)
   if d<1:depth-=amount*min(1,(1-d)/.4)*tip
   elif d<1.5:depth+=amount*.12*(1-abs(d-1.25)/.25)*tip
  for p,n,t,s,r in impacts:
   delta=v.co-(p+t*r*1.18)
   if abs(delta.dot(n))>r*.45:continue
   d=max(abs(delta.dot(t)/(r*.35)),abs(delta.dot(s)/(r*.5)),abs(delta.dot(t)+delta.dot(s))/(r*.56))
   if d<1:depth-=radius*.007*min(1,(1-d)/.3)
  v.co+=baseline[vi]*depth
 print('SCULPTED',number,flush=True)
 high.data.update()
 high.data.normals_split_custom_set_from_vertices([(smooth[i]+v.normal-baseline[i]).normalized() for i,v in enumerate(high.data.vertices)])
 image=bpy.data.images.new(f'Asteroid{number}Normal',width=1024,height=1024,alpha=False);image.colorspace_settings.name='Non-Color'
 node=mat.node_tree.nodes.new('ShaderNodeTexImage');node.image=image;mat.node_tree.nodes.active=node
 scene=bpy.context.scene;scene.render.engine='CYCLES';scene.cycles.samples=1;scene.cycles.device='CPU'
 scene.render.bake.use_selected_to_active=True;scene.render.bake.cage_extrusion=radius*.025;scene.render.bake.max_ray_distance=radius*.06
 scene.render.bake.margin=10;scene.render.bake.normal_space='TANGENT';drawing.hide_render=True
 low.select_set(True);bpy.context.view_layer.objects.active=low
 bpy.ops.object.bake(type='NORMAL');image.filepath_raw=str(out/f'Asteroid{number}Normal.png');image.file_format='PNG';image.save();image.pack()
 normal=mat.node_tree.nodes.new('ShaderNodeNormalMap');mat.node_tree.links.new(node.outputs['Color'],normal.inputs['Color']);mat.node_tree.links.new(normal.outputs['Normal'],mat.node_tree.nodes.get('Principled BSDF').inputs['Normal'])
 high.hide_render=True;high.hide_set(True);drawing.hide_render=False
 ink=bpy.data.materials.new('Graphite drawing');ink.diffuse_color=(.018,.024,.034,1);drawing.data.materials.append(ink)
 for obj,suffix in [(low,''),(drawing,'Drawing')]:
  bpy.ops.object.select_all(action='DESELECT');obj.select_set(True);bpy.context.view_layer.objects.active=obj
  bpy.ops.export_scene.fbx(filepath=str(out/f'Asteroid{number}{suffix}.fbx'),use_selection=True,axis_forward='-Z',axis_up='Y',apply_unit_scale=True,bake_space_transform=True,object_types={'MESH'},add_leaf_bones=False,bake_anim=False)
 bpy.ops.wm.save_as_mainfile(filepath=str(art/f'Asteroid{number}.blend'))
 print('FIELD_SHAPE',number,'ridges',len(paths),'cuts',len(cuts),'mesh',len(mesh.vertices),'drawing',len(ribbonverts),'radius',radius,flush=True)
