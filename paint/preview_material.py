"""Evidence-only: make every material used by the active scene's mesh parts show COMPOSITE as its
active image texture, so `ship_render --textured` renders the paint composite. Saves the file.

blender -b --factory-startup --python-exit-code 1 -P preview_material.py -- BLEND COMPOSITE.png
"""
import sys
from pathlib import Path

import bpy

blend, composite = (Path(p).resolve() for p in sys.argv[sys.argv.index("--") + 1:][:2])
bpy.ops.wm.open_mainfile(filepath=str(blend), load_ui=False)
image = bpy.data.images.get("Composite") or bpy.data.images.load(str(composite))
image.name = "Composite"
image.filepath = str(composite)
image.reload()
touched = set()
for obj in bpy.context.scene.objects:
    if obj.type != "MESH":
        continue
    for slot in obj.material_slots:
        material = slot.material
        if material is None or material.name in touched:
            continue
        touched.add(material.name)
        material.use_nodes = True
        node = material.node_tree.nodes.get("Composite preview") or material.node_tree.nodes.new("ShaderNodeTexImage")
        node.name = "Composite preview"
        node.image = image
        material.node_tree.nodes.active = node
bpy.context.preferences.filepaths.save_version = 0
bpy.ops.wm.save_as_mainfile(filepath=str(blend), check_existing=False)
print("PREVIEW_MATERIAL_DONE", sorted(touched), file=sys.stderr)
