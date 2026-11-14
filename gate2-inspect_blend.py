"""Check a blockout file the way the owner would meet it: what is in it, and does moving a part work."""
import sys

import bpy

path = sys.argv[sys.argv.index("--") + 1]
bpy.ops.wm.open_mainfile(filepath=path)
print("FILE", path)
for col in bpy.data.collections:
    print("COLLECTION", col.name, "objects", len(col.objects), "hidden_in_viewport", col.hide_viewport,
          "selectable", not col.hide_select)
empties = [o for o in bpy.data.objects if o.type == "EMPTY"]
root = next(o for o in empties if o.parent is None and o.children)
print("ROOT", root.name, "scale", tuple(round(v, 3) for v in root.scale))


def walk(ob, depth):
    kids = [c for c in ob.children if c.type == "EMPTY"]
    meshes = [c for c in ob.children if c.type == "MESH" and not c.name.endswith("/ ink")]
    print("  " * depth + f"{ob.name}  loc={tuple(round(v, 1) for v in ob.location)} scale={tuple(round(v, 2) for v in ob.scale)} meshes={len(meshes)}")
    for k in kids:
        walk(k, depth + 1)


walk(root, 0)

# Moving a part must carry its meshes and its ink shell with it.
part = next(o for o in empties if o.name in ("Sail 1 panels", "Inner ring", "Magazine 1"))
mesh = next(c for c in part.children if c.type == "MESH" and not c.name.endswith("/ ink"))
ink = next(c for c in part.children if c.name == mesh.name + " / ink")
before = mesh.matrix_world.translation.copy()
part.location.x += 3.0
bpy.context.view_layer.update()
moved = (mesh.matrix_world.translation - before).length
print(f"MOVE {part.name}: mesh moved {moved:.2f} u, ink follows {(ink.matrix_world.translation - mesh.matrix_world.translation).length < 1e-5}, "
      f"ink shares mesh data {ink.data is mesh.data}")
unapplied = [o.name for o in bpy.data.objects if any(abs(s - 1) > 1e-4 for s in o.scale)]
print("OBJECTS WITH SCALE NOT 1:", unapplied or "none")
