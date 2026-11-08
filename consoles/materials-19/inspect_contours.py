import bpy
print([(o.name,o.parent.name if o.parent else '',[m.name for m in o.data.materials]) for o in bpy.data.objects if o.type=='CURVE' and any(m and 'HOLO' in m.name for m in o.data.materials)][:35])
