"""Scratch: copy #731's CrimsonPaintUV into main's Crimson.blend as PaintUV; dump old/new evaluated UV triangles."""
import bpy, sys, numpy as np
argv = sys.argv[sys.argv.index("--") + 1:]
src731, dump = argv[0], argv[1]
scene = bpy.context.scene
parts = sorted(o.name for o in scene.objects if o.type == 'MESH')

def eval_uv_tris():
    dg = bpy.context.evaluated_depsgraph_get()
    out = {}
    for name in parts:
        ev = bpy.data.objects[name].evaluated_get(dg)
        me = ev.to_mesh()
        me.calc_loop_triangles()
        uv = np.empty(len(me.loops) * 2, np.float32); me.uv_layers[0].data.foreach_get("uv", uv)
        tri = np.empty(len(me.loop_triangles) * 3, np.int32); me.loop_triangles.foreach_get("loops", tri)
        out[name] = uv.reshape(-1, 2)[tri.reshape(-1, 3)]
        ev.to_mesh_clear()
    return out

old = eval_uv_tris()
with bpy.data.libraries.load(src731) as (src, dst):
    dst.objects = [n for n in src.objects if n in parts]
loaded = {o.name.rsplit(".001", 1)[0]: o for o in dst.objects}
for name in parts:
    m, m2 = bpy.data.objects[name].data, loaded[name].data
    assert len(m.loops) == len(m2.loops) and len(m.uv_layers) == 1 == len(m2.uv_layers)
    uv = np.empty(len(m2.loops) * 2, np.float32); m2.uv_layers[0].data.foreach_get("uv", uv)
    m.uv_layers[0].data.foreach_set("uv", uv)
    m.uv_layers[0].name = "PaintUV"
for o in dst.objects:
    me = o.data; bpy.data.objects.remove(o); bpy.data.meshes.remove(me)
old_img, new_img = bpy.data.images["Crimson painted base color"], bpy.data.images["Crimson painted base color.001"]
old_img.user_remap(new_img); bpy.data.images.remove(old_img); new_img.name = "Crimson painted base color"
for mat in list(bpy.data.materials):
    if mat.users == 0: bpy.data.materials.remove(mat)
for img in bpy.data.images:
    print("IMG", img.name, img.users, img.packed_file is not None, tuple(img.size))
for me in list(bpy.data.meshes):
    if me.users == 0: print("ORPHAN", me.name)
new = eval_uv_tris()
for name in parts:
    assert old[name].shape == new[name].shape, name
np.savez(dump, **{f"old|{n}": old[n] for n in parts}, **{f"new|{n}": new[n] for n in parts})
bpy.ops.wm.save_mainfile()
print("TRANSFER_OK", len(parts))
