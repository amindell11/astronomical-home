import bpy,json
bpy.ops.wm.open_mainfile(filepath='D:/amind/git/agent-7/art/ships/crimson/Crimson.blend')
print(json.dumps({'images':[{'name':i.name,'path':i.filepath,'packed':[{'path':p.filepath,'readonly':p.bl_rna.properties['filepath'].is_readonly} for p in i.packed_files],'users':i.users,'size':list(i.size)} for i in bpy.data.images]}))
