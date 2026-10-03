import bpy
from pathlib import Path
root=Path('D:/amind/git/agent-4')
bpy.ops.wm.open_mainfile(filepath=str(root/'art/asteroid-study/AsteroidFractureStudy.blend'))
for material in bpy.data.materials:
    if not material.use_nodes:
        continue
    for node in material.node_tree.nodes:
        if node.type=='TEX_IMAGE':
            node.image=bpy.data.images.load(str(root/'src/Asteroids3D/Assets/Visuals/Environment/Asteroids/DrawnStudy/AsteroidStoneAlbedo.png'))
            node.image.pack()
    material.name='Unlit slate and olive stone'
for image in list(bpy.data.images):
    if image.users==0:
        bpy.data.images.remove(image)
bpy.ops.wm.save_as_mainfile(filepath=str(root/'art/asteroid-study/AsteroidFractureStudy.blend'))
