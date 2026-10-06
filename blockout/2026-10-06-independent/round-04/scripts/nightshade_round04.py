from pathlib import Path
import math
import bpy
from mathutils import Matrix

root=Path('D:/amind/git/agent-1')
bpy.context.preferences.filepaths.save_version=0
parts=[o for o in bpy.context.scene.objects if o.type=='MESH']
for ob in parts:
    for mod in list(ob.modifiers):
        if mod.type=='SUBSURF':ob.modifiers.remove(mod)
    for face in ob.data.polygons:face.use_smooth=False

body=bpy.data.objects['Fuselage']
widths=[.05,.20,.35,.48,.55,.57,.52,.46,.37,.26,.15,.035]
tops=[.045,.065,.10,.145,.165,.215,.205,.185,.14,.10,.065,.018]
bottoms=[.035,.05,.075,.095,.105,.105,.09,.075,.065,.05,.027,.008]
xs=[0,.45,.78,.98,1,.94,.70,.38,0]
zs=[1,1,.65,.22,0,-.40,-.87,-1,-1]
for j,(w,top,bottom) in enumerate(zip(widths,tops,bottoms)):
    for i in range(9):
        v=body.data.vertices[j*9+i]
        v.co.x=w*xs[i]
        v.co.z=(top if i<5 else bottom)*zs[i]

glass=bpy.data.objects['Upper canopy']
stations=[
    (1.89,.06,.065,.095),(1.83,.17,.080,.150),
    (1.70,.29,.102,.215),(1.51,.355,.120,.252),
    (1.32,.373,.127,.270),(1.18,.361,.131,.272),
    (1.10,.342,.125,.224)]
for j,(y,w,edge,peak) in enumerate(stations):
    for i,(x,z) in enumerate(zip([0,.50,.83,.97,1],[1,1,.72,.24,0])):
        glass.data.vertices[j*5+i].co=(x*w,y,edge+(peak-edge)*z)
lower=bpy.data.objects['Lower canopy']
for v in lower.data.vertices:
    v.co.x*=.79
    v.co.y=1.89+(v.co.y-1.89)*.80
    v.co.z*=.59

tail=bpy.data.objects['Rising tail blades']
old=[.095,.068,.05,.05,.052,.060,.085,.155,.280,.410,.472,.507,.530,.535]
new=[.078,.060,.045,.025,.008,.000,.018,.052,.086,.108,.110,.095,.072,.062]
for j,(before,after) in enumerate(zip(old,new)):
    for v in list(tail.data.vertices)[j*8:j*8+8]:
        v.co.z=after+(v.co.z-before)*.80

for name in ('Inner swept fins','Wing root joints'):
    ob=bpy.data.objects[name]
    for j in range(len(ob.data.vertices)//8):
        ring=list(ob.data.vertices)[j*8:j*8+8]
        center=ring[0].co.z
        for i,v in enumerate(ring):
            if i in (1,2,3):v.co.z=center+(.030 if name=='Inner swept fins' else .052)
            elif i in (5,6,7):v.co.z=center-(.024 if name=='Inner swept fins' else .042)

pitch=Matrix.Rotation(math.radians(-4),4,'X')
for ob in parts:
    for v in ob.data.vertices:v.co=pitch@v.co
    bevel=ob.modifiers.new('Small mechanical edge chamfer','BEVEL')
    bevel.width=.006 if ob.name not in ('Upper canopy','Lower canopy') else .003
    bevel.segments=1
    bevel.limit_method='ANGLE'
    bevel.angle_limit=math.radians(24)
    ob.data.update()
bpy.data.materials['Blockout canopy'].diffuse_color=(.17,.16,.42,1)
bpy.ops.wm.save_as_mainfile(filepath=str(root/'art/ships/nightshade/Nightshade.blend'))
