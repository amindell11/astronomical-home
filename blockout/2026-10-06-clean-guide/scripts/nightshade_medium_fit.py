from pathlib import Path
import bpy
from mathutils import Vector
from mathutils.bvhtree import BVHTree

root=Path('D:/amind/git/agent-1')
bpy.context.preferences.filepaths.save_version=0
deps=bpy.context.evaluated_depsgraph_get()
errors=[]

def surface(name):
    bpy.context.view_layer.update()
    return BVHTree.FromObject(bpy.data.objects[name],deps)

def project(patch_name,base_name,offset,ring=False,lower=False):
    patch=bpy.data.objects[patch_name]
    bvh=surface(base_name)
    old_z=[v.co.z for v in patch.data.vertices]
    ring_z=[old_z[j] for j in range(0,len(old_z),8)] if ring else []
    misses=[]
    for i,v in enumerate(patch.data.vertices):
        ray=Vector((v.co.x,v.co.y,-10 if lower else 10))
        hit=bvh.ray_cast(ray,Vector((0,0,1 if lower else -1)))[0]
        if hit is None:
            misses.append(i)
            continue
        shape=old_z[i]-ring_z[i//8] if ring else 0
        v.co.z=hit.z+(-offset if lower else offset)+shape
    if misses:errors.append(f'{patch_name}: overlay leaves {base_name} at vertices {misses}')
    patch.data.update()

project('Upper glazing','Upper canopy frame',.023)
project('Lower glazing','Lower canopy frame',.023,lower=True)
root_armor=bpy.data.objects['Wing root armor cap']
for v in list(root_armor.data.vertices)[8:16]:v.co.x=.522+(v.co.x-.522)*.91
profile=[0,.10,.32,.76,1,.91,.40,.09]
for name,j,inner,outer in [('Outer magenta tips',3,(1251-765)/200,(1274-765)/200),
                         ('Outer magenta tips',5,(1276.4-765)/200,(1278.5-765)/200),
                         ('Inner magenta tips',1,(918-765)/200,(965-765)/200)]:
    for i,u in enumerate(profile):bpy.data.objects[name].data.vertices[j*8+i].co.x=inner+(outer-inner)*u
for patch,base in [
    ('Wing leading armor plates','Forward wing shells'),
    ('Wing middle armor plates','Forward wing shells'),
    ('Wing root armor cap','Wing root saddles'),
    ('Outer blade bevel plates','Outer swept blades'),
    ('Outer magenta tips','Outer swept blades'),
    ('Inner fin armor','Inner swept fins'),
    ('Inner magenta tips','Inner swept fins'),
    ('Tail outer sculpted ridge','Sculpted tail blades')]:
    project(patch,base,.014,True)

root_cap=bpy.data.objects['Tail root armor']
for i,u in enumerate(profile):
    root_cap.data.vertices[i].co.y=.21
    root_cap.data.vertices[i].co.x=.40+.15*u
    root_cap.data.vertices[8+i].co.x=.458+(.62-.458)*u
project('Tail root armor','Sculpted tail blades',.014,True)

for patch,base,offset in [
    ('Spine front inset','Dorsal spine armor',.009),
    ('Spine inner crest','Spine front inset',.009),
    ('Dorsal recessed shield','Dorsal spine armor',.008),
    ('Dorsal inset center','Dorsal recessed shield',.008)]:
    project(patch,base,offset)

for name in ('Sculpted tail blades','Tail outer sculpted ridge','Tail root armor'):
    for v in bpy.data.objects[name].data.vertices:
        if v.co.y<-.35:v.co.y=-.35+(v.co.y+.35)*1.15
bpy.data.materials['Violet glass'].diffuse_color=(.42,.32,.90,1)
bpy.data.materials['Magenta core'].diffuse_color=(.88,.07,.94,1)
if errors:raise RuntimeError('\n'.join(errors))
bpy.ops.wm.save_as_mainfile(filepath=str(root/'art/ships/nightshade/Nightshade.blend'))
