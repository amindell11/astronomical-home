import bpy, json, sys
argv = sys.argv[sys.argv.index("--") + 1:]
fbx, out = argv
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.fbx(filepath=fbx)
deps = bpy.context.evaluated_depsgraph_get()
meshes = []
for ob in bpy.context.scene.objects:
    if ob.type != 'MESH':
        continue
    ev = ob.evaluated_get(deps)
    me = ev.to_mesh()
    me.calc_loop_triangles()
    mw = ob.matrix_world
    vs = [list(mw @ v.co) for v in me.vertices]
    tris = [list(t.vertices) for t in me.loop_triangles]
    meshes.append({"name": ob.name, "verts": vs, "tris": tris})
    ev.to_mesh_clear()
json.dump(meshes, open(out, "w"))
print("MESHES", [(m["name"], len(m["tris"])) for m in meshes])
