from pathlib import Path
import bpy
from mathutils import Vector
from mathutils.bvhtree import BVHTree

root=Path('D:/amind/git/agent-1')
bpy.context.preferences.filepaths.save_version=0
patches=[('Spine front inset','Dorsal spine armor',.010,.012),
         ('Spine inner crest','Spine front inset',.012,.011),
         ('Dorsal recessed shield','Dorsal spine armor',.009,.010),
         ('Dorsal inset center','Dorsal recessed shield',.012,.010)]
patches.extend((f'Wing outer inset {i}','Forward wing shells',.006,.009) for i in range(1,4))
for i in range(1,4):
    for v in bpy.data.objects[f'Wing outer inset {i}'].data.vertices:v.co.x-=.06
for name,base,offset,depth in patches:
    bpy.context.view_layer.update()
    bvh=BVHTree.FromObject(bpy.data.objects[base],bpy.context.evaluated_depsgraph_get())
    mesh=bpy.data.objects[name].data
    n=len(mesh.vertices)//2
    for i in range(n):
        v=mesh.vertices[i]
        hit=bvh.ray_cast(Vector((v.co.x,v.co.y,10)),Vector((0,0,-1)))[0]
        if hit is None:raise RuntimeError(name+' has unsupported inset vertex '+str(i))
        v.co.z=hit.z+offset
        mesh.vertices[i+n].co.z=v.co.z-depth
    mesh.update()
bpy.ops.wm.save_as_mainfile(filepath=str(root/'art/ships/nightshade/Nightshade.blend'))
