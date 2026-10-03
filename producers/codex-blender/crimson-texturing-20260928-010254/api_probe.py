import bpy,inspect
print(bpy.ops.uv.smart_project.get_rna_type().properties.keys())
print(bpy.ops.uv.pack_islands.get_rna_type().properties.keys())
print(bpy.ops.object.bake.get_rna_type().properties.keys())
