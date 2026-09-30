# Export a station .blend to the FBX Unity imports from Assets/Visuals/Environment/<station>/source/.
#   blender -b <station>.blend --python art/stations/export_unity_fbx.py -- <out>.fbx
# The kwargs are the ones Unity's own .blend importer passes (Editor/Data/Tools/Unity-BlenderToFBX.py,
# Blender >= 2.80 branch), so the FBX imports with the same hierarchy and fileIDs as the .blend did.
import sys
import bpy

outfile = " ".join(sys.argv[sys.argv.index("--") + 1:])
bpy.ops.export_scene.fbx(
    filepath=outfile,
    check_existing=False,
    use_selection=False,
    use_active_collection=False,
    object_types={"ARMATURE", "CAMERA", "LIGHT", "MESH", "OTHER", "EMPTY"},
    use_mesh_modifiers=True,
    mesh_smooth_type="OFF",
    use_custom_props=True,
    bake_anim_use_nla_strips=False,
    bake_anim_use_all_actions=False,
    apply_scale_options="FBX_SCALE_ALL",
)
print("exported", outfile)
