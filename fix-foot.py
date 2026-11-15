import bpy
ob=bpy.data.objects['29 | Display desk foot']
x,z=1.05,.125
c=.05
ring=[(-x+c,-z),(x-c,-z),(x,-z+c),(x,z-c),(x-c,z),(-x+c,z),(-x,z-c),(-x,-z+c)]
for v,(px,py,pz) in zip(ob.data.vertices,[(a,y,b) for y in (-.6,.6) for a,b in ring]): v.co=(px,py,pz)
bpy.context.preferences.filepaths.save_version=0
bpy.ops.wm.save_as_mainfile(filepath=bpy.data.filepath)
print('FOOT_CHAMFER=0.05 HEIGHT=0.25')
