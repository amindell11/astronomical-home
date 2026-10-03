import bpy,bmesh,math
from pathlib import Path
from mathutils import Vector
from mathutils.bvhtree import BVHTree
from mathutils.kdtree import KDTree
root=Path('D:/amind/git/agent-4')
for number in range(1,11):
 path=root/f'art/asteroid-field/Asteroid{number}.blend'
 bpy.ops.wm.open_mainfile(filepath=str(path))
 rock=bpy.data.objects[f'Asteroid{number}'];drawing=bpy.data.objects[f'Asteroid{number}Drawing'];mesh=rock.data
 radius=max(v.co.length for v in mesh.vertices)
 mesh.calc_loop_triangles();bvh=BVHTree.FromPolygons([v.co for v in mesh.vertices],[list(t.vertices) for t in mesh.loop_triangles],all_triangles=True)
 tree=KDTree(len(drawing.data.vertices))
 for v in drawing.data.vertices:tree.insert(v.co,v.index)
 tree.balance()
 bm=bmesh.new();bm.from_mesh(mesh);bm.verts.ensure_lookup_table();bm.edges.ensure_lookup_table();bm.normal_update()
 scores={e.index:e.calc_face_angle(0) for e in bm.edges};ranked=sorted(bm.edges,key=lambda e:scores[e.index],reverse=True)
 paths=[];used=set();centers=[]
 for edge in ranked:
  if len(paths)>=16:break
  center=(edge.verts[0].co+edge.verts[1].co)*.5
  if scores[edge.index]<.10 or tree.find(center)[2]<radius*.065 or any((center-c).length<radius*.18 for c in centers):continue
  chain=[edge.verts[0],edge.verts[1]];local={v.index for v in chain}
  for reverse in (False,True):
   if reverse:chain.reverse()
   for step in range(4):
    prev,current=chain[-2:];direction=(current.co-prev.co).normalized();options=[]
    for e in current.link_edges:
     other=e.other_vert(current)
     if other.index in local or other.index in used or tree.find(other.co)[2]<radius*.045:continue
     align=direction.dot((other.co-current.co).normalized())
     if align<.35:continue
     options.append((scores[e.index]+align*.32,other))
    if not options:break
    other=max(options,key=lambda x:x[0])[1];chain.append(other);local.add(other.index)
  length=sum((a.co-b.co).length for a,b in zip(chain,chain[1:]))
  if length<radius*.20:continue
  points=[v.co.copy() for v in chain]
  if length>radius*.65:points=points[:max(3,int(len(points)*radius*.65/length))]
  paths.append(points);centers.append(center.copy());used.update(v.index for v in chain)
 bm.free()
 verts=[v.co.copy() for v in drawing.data.vertices];faces=[list(f.vertices) for f in drawing.data.polygons]
 def project(p):
  q,n,idx,d=bvh.find_nearest(p);return q+n*(radius*.007)
 for pathPoints in paths:
  samples=[]
  for a,b in zip(pathPoints,pathPoints[1:]):
   count=max(2,math.ceil((b-a).length/(radius*.007)))
   samples.extend(a.lerp(b,i/count) for i in range(count))
  samples.append(pathPoints[-1]);start=len(verts)
  for i,p in enumerate(samples):
   q,n,idx,d=bvh.find_nearest(p)
   tangent=(samples[min(i+1,len(samples)-1)]-samples[max(0,i-1)]).normalized();side=n.cross(tangent).normalized()
   t=i/(len(samples)-1);pressure=math.sin(math.pi*t)**.55*(.84+.16*math.sin(t*5+number))
   for sign in (-1,1):verts.append(project(q+side*(.012*radius*pressure*sign)))
  for i in range(len(samples)-1):
   a=start+2*i;faces.append((a,a+1,a+3,a+2))
 old=drawing.data;new=bpy.data.meshes.new('Dense fitted graphite strokes');new.from_pydata(verts,[],faces);new.update()
 for material in old.materials:new.materials.append(material)
 for f in new.polygons:
  q,n,idx,d=bvh.find_nearest(f.center)
  if f.normal.dot(n)<0:f.flip()
  f.use_smooth=True
 drawing.data=new;bpy.data.meshes.remove(old)
 bpy.ops.object.select_all(action='DESELECT');drawing.select_set(True);bpy.context.view_layer.objects.active=drawing
 out=root/f'src/Asteroids3D/Assets/Visuals/Environment/Asteroids/DrawnField/Shape{number:02}/Asteroid{number}Drawing.fbx'
 bpy.ops.export_scene.fbx(filepath=str(out),use_selection=True,axis_forward='-Z',axis_up='Y',apply_unit_scale=True,bake_space_transform=True,object_types={'MESH'},add_leaf_bones=False,bake_anim=False)
 bpy.ops.wm.save_as_mainfile(filepath=str(root/f'art/asteroid-field/Asteroid{number}.blend'))
 print('DENSER_FIELD',number,'additional_ridge_paths',len(paths),flush=True)
