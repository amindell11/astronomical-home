from pathlib import Path
import math
import bpy

root=Path('D:/amind/git/agent-1')
bpy.context.preferences.filepaths.save_version=0
wing=bpy.data.objects['Outer wing blades']
inner=[1085,1090,1092,1130,1170,1208,1230,1254,1275,1278]
outer=[1174,1177,1188,1204,1227,1251,1281,1284,1280,1279]
profile=[0,.12,.44,.84,1,.84,.44,.12]
for j in range(len(inner)):
    for i,u in enumerate(profile):
        wing.data.vertices[j*8+i].co.x=(inner[j]+u*(outer[j]-inner[j])-765)/200

tail=bpy.data.objects['Rising tail blades']
old_z=[.095,.068,.045,.044,.066,.131,.235,.355,.438,.482,.509,.538,.560,.565]
new_z=[.095,.068,.05,.05,.052,.060,.085,.155,.280,.410,.472,.507,.530,.535]
for j,(old,new) in enumerate(zip(old_z,new_z)):
    for v in list(tail.data.vertices)[j*8:j*8+8]:v.co.z+=new-old

for name in ('Rising tail blades','Outer wing shoulders','Outer wing blades','Inner swept fins'):
    ob=bpy.data.objects[name]
    crease=ob.data.attributes.new('crease_edge','FLOAT','EDGE')
    for edge in ob.data.edges:
        a,b=edge.vertices
        if abs(a-b)==8 and a%8 in (0,2,4,6):
            crease.data[edge.index].value=.52 if a%8 in (2,6) else .68

full=bpy.data.objects['reference.side']
full.name='reference.turnaround'
side=full.copy()
side.data=bpy.data.images.load(str(root/'results/nightshade-blockout/references/side-profile.png'))
bpy.data.collections['ignore'].objects.link(side)
side.name='reference.side'
side.empty_display_size=1327/324
side.rotation_euler=(math.pi/2,0,-math.pi/2)
side.location=(3.2,-.048,(295-253)/324)
side.show_empty_image_only_axis_aligned=True
side.hide_set(False)
side.data.pack()
side.data.filepath='//packed-side-profile.png'
full.hide_set(True)
for name in ('camera.top','camera.side','camera.front'):
    bpy.data.objects[name].hide_set(True)
for screen in bpy.data.screens:
    for area in screen.areas:
        if area.type=='VIEW_3D':
            area.spaces.active.shading.color_type='MATERIAL'
            area.spaces.active.overlay.show_floor=False
bpy.ops.wm.save_as_mainfile(filepath=str(root/'art/ships/nightshade/Nightshade.blend'))
