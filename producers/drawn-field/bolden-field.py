import bpy
from pathlib import Path
from mathutils.bvhtree import BVHTree
root=Path('D:/amind/git/agent-4')
for i in range(1,11):
 bpy.ops.wm.open_mainfile(filepath=str(root/f'art/asteroid-field/Asteroid{i}.blend'))
 rock=bpy.data.objects[f'Asteroid{i}'];drawing=bpy.data.objects[f'Asteroid{i}Drawing']
 rock.data.calc_loop_triangles()
 bvh=BVHTree.FromPolygons([v.co for v in rock.data.vertices],[list(t.vertices) for t in rock.data.loop_triangles],all_triangles=True)
 radius=max(v.co.length for v in rock.data.vertices)
 verts=drawing.data.vertices
 for j in range(0,len(verts),2):
  a,b=verts[j:j+2];center=(a.co+b.co)*.5;half=(b.co-a.co)*.5
  for vert,sign in ((a,-1),(b,1)):
   p,n,index,d=bvh.find_nearest(center+half*(2.2*sign))
   vert.co=p+n*(radius*.007)
 drawing.data.update()
 for face in drawing.data.polygons:
  p,n,index,d=bvh.find_nearest(face.center)
  if face.normal.dot(n)<0:face.flip()
 drawing.data.materials[0].diffuse_color=(.002,.003,.005,1)
 bpy.ops.object.select_all(action='DESELECT');drawing.select_set(True);bpy.context.view_layer.objects.active=drawing
 out=root/f'src/Asteroids3D/Assets/Visuals/Environment/Asteroids/DrawnField/Shape{i:02}/Asteroid{i}Drawing.fbx'
 bpy.ops.export_scene.fbx(filepath=str(out),use_selection=True,axis_forward='-Z',axis_up='Y',apply_unit_scale=True,bake_space_transform=True,object_types={'MESH'},add_leaf_bones=False,bake_anim=False)
 bpy.ops.wm.save_as_mainfile(filepath=str(root/f'art/asteroid-field/Asteroid{i}.blend'))
 print('BOLD_FIELD',i,len(verts),flush=True)
