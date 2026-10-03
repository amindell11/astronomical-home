import bpy, math
from pathlib import Path
from mathutils import Vector
root=Path('D:/amind/git/agent-4')
out=root/'src/Asteroids3D/Assets/Visuals/Environment/Asteroids/DrawnStudy'
bpy.ops.wm.open_mainfile(filepath=str(root/'art/asteroid-study/AsteroidDrawnStudy.blend'))
low=bpy.data.objects['AsteroidFractureStudy']
for o in bpy.context.scene.objects: o.select_set(False)
high=low.copy();high.data=low.data.copy();bpy.context.collection.objects.link(high);high.name='Sculpted scuff relief source'
bpy.context.view_layer.objects.active=high;high.select_set(True)
sub=high.modifiers.new('Relief sculpt resolution','SUBSURF');sub.subdivision_type='SIMPLE';sub.levels=3
bpy.ops.object.modifier_apply(modifier=sub.name)
uv=high.data.uv_layers.active
uvs={}
for loop in high.data.loops: uvs[loop.vertex_index]=uv.data[loop.index].uv.copy()
# Short scrape fans sit beside known rims and existing chisel marks.
clusters=[(.335,.615,-35),(.22,.515,25),(.376,.407,-25),(.255,.79,45),(.15,.665,-15),
          (.609,.626,35),(.805,.521,-30),(.73,.288,30),(.908,.602,-40),(.077,.428,20)]
cuts=[]
for u,v,a in clusters:
 for du,dv,length,width,depth in [(-.011,0,.021,.0045,.018),(0,.012,.028,.004,.022),(.012,.021,.016,.0035,.014)]:
  cuts.append((u+du,v+dv,math.radians(a),length,width,depth))
# Broader shallow flakes add bevels at a few crease junctions.
flakes=[(.302,.66,.018,.026,.018),(.184,.414,.015,.021,.015),(.364,.535,.017,.024,.019),
        (.585,.551,.017,.029,.018),(.88,.684,.018,.022,.017),(.767,.247,.019,.018,.016)]
low.data.calc_loop_triangles()
lowuv=low.data.uv_layers.active
uvgrid={}
for tri in low.data.loop_triangles:
 coords=[Vector(lowuv.data[li].uv) for li in tri.loops]
 normals=[low.data.vertices[i].normal.copy() for i in tri.vertices]
 for x in range(math.floor(min(p.x for p in coords)*64),math.floor(max(p.x for p in coords)*64)+1):
  for y in range(math.floor(min(p.y for p in coords)*64),math.floor(max(p.y for p in coords)*64)+1):
   uvgrid.setdefault((x,y),[]).append((coords,normals))
def source_normal(u,v):
 for shift in (0,1,-1):
  p=Vector((u+shift,v))
  for coords,normals in uvgrid.get((math.floor(p.x*64),math.floor(p.y*64)),[]):
   a,b,c=coords;ab=b-a;ac=c-a;q=p-a
   det=ab.x*ac.y-ab.y*ac.x
   if abs(det)<1e-10: continue
   s=(q.x*ac.y-q.y*ac.x)/det;t=(ab.x*q.y-ab.y*q.x)/det
   if s>=-1e-4 and t>=-1e-4 and s+t<=1.0001:
    return (normals[0]*(1-s-t)+normals[1]*s+normals[2]*t).normalized()
 raise ValueError(('UV normal',u,v))
base_normals=[vert.normal.copy() for vert in high.data.vertices]
for vert in high.data.vertices:
 u,v=uvs[vert.index];u%=1
 depth=0
 for cu,cv,a,rl,rw,d in cuts:
  du=(u-cu+.5)%1-.5;dv=v-cv
  if abs(du)>.065 or abs(dv)>.065: continue
  x=du*math.cos(a)+dv*math.sin(a);y=-du*math.sin(a)+dv*math.cos(a)
  tip=max(0,1-(abs(x)/rl)**3)
  if tip<=0: continue
  t=abs(y)/(rw*tip)
  if t<1:
   depth-=d*min(1,(1-t)/.4)*tip
  elif t<1.6:
   depth+=d*.16*(1-abs(t-1.25)/.35)*tip
 for cu,cv,ru,rv,d in flakes:
  x=((u-cu+.5)%1-.5)/ru;y=(v-cv)/rv
  t=max(abs(x),abs(y),abs(x+y)*.68)
  if t<1: depth-=d*min(1,(1-t)/.24)*(1+.2*x)
 if depth: vert.co+=vert.normal*depth
high.data.update()
relief_normals=[]
for vert in high.data.vertices:
 u,v=uvs[vert.index]
 relief_normals.append((source_normal(u,v)+vert.normal-base_normals[vert.index]).normalized())
high.data.normals_split_custom_set_from_vertices(relief_normals)
for o in bpy.context.scene.objects:
 if o not in (high,low): o.hide_render=True
image=bpy.data.images.new('AsteroidScuffNormal',width=1024,height=1024,alpha=False)
image.colorspace_settings.name='Non-Color'
mat=low.data.materials[0].copy();low.data.materials.clear();low.data.materials.append(mat)
node=mat.node_tree.nodes.new('ShaderNodeTexImage');node.image=image;mat.node_tree.nodes.active=node;node.select=True
scene=bpy.context.scene;scene.render.engine='CYCLES';scene.cycles.samples=1;scene.cycles.device='CPU'
scene.render.bake.use_selected_to_active=True;scene.render.bake.cage_extrusion=.035;scene.render.bake.max_ray_distance=.075
scene.render.bake.margin=12;scene.render.bake.normal_space='TANGENT'
bpy.ops.object.select_all(action='DESELECT');high.select_set(True);low.select_set(True);bpy.context.view_layer.objects.active=low
bpy.ops.object.bake(type='NORMAL')
image.filepath_raw=str(out/'AsteroidScuffNormal.png');image.file_format='PNG';image.save();image.pack()
normal=mat.node_tree.nodes.new('ShaderNodeNormalMap');mat.node_tree.links.new(node.outputs['Color'],normal.inputs['Color']);mat.node_tree.links.new(normal.outputs['Normal'],mat.node_tree.nodes.get('Principled BSDF').inputs['Normal'])
high.hide_render=True;high.hide_set(True)
for o in bpy.context.scene.objects:
 if o!=high: o.hide_render=False
bpy.ops.wm.save_as_mainfile(filepath=str(root/'art/asteroid-study/AsteroidReliefStudy.blend'))
print('RELIEF_BAKE',len(high.data.vertices),'sculpt vertices',image.filepath_raw)
