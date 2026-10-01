import bpy,json
bpy.ops.wm.open_mainfile(filepath='D:/amind/git/agent-2/art/ships/valis/Valis.blend')
for n in ['20 lower swept blades','21 lower blade lavender tips','10 upper swept wings','12 upper wing lavender tips']:
 o=bpy.data.objects[n]
 print(n, [(round(p.normal.z,2),round(p.center.z,2)) for p in o.data.polygons],[(v.co.x,v.co.z) for v in o.data.vertices][:2])
print('LIGHTS',[(s.name,s.type) for s in bpy.context.preferences.studio_lights])
