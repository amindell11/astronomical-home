import bpy, math, json
from pathlib import Path
from mathutils import Vector, Matrix

out=Path('D:/amind/git/agent-5/art/environments/hangar-console/console.blend')
proof=Path('C:/Users/amind/.codex/visualizations/2026/11/15/01a1dc89-c32a-7c82-ba76-5ae7751e1d5f/hangar')
scene=bpy.context.scene
objects=bpy.data.objects
bpy.context.view_layer.update()
left_names=['07 | Monitor assembly','07 | Main rectangular display','07 | Detail rectangular display']
left={n:objects[n].matrix_world.copy() for n in left_names}

def shift(names, delta):
    for name in names:
        obj=objects[name]
        matrix=obj.matrix_world.copy()
        matrix.translation += Vector(delta)
        obj.matrix_world=matrix
    bpy.context.view_layer.update()

shift(['07 | Projector assembly','08 | Projector housing','09 | Projector optical assembly',
       '14 | Projector formed armor','14 | Projector recessed folded sidewalls',
       '15 | Projector rear casing closure','19 | Projection spill'],(-2.35,0,0))
shift(['07 | Launch assembly','14 | Launch casing folded returns','19 | Launch legend',
       '19 | Launch glow'],(-6.474295616,0,0))
shift(['08 | Complete hose loom','10 | Complete tube assembly','14 | Recessed front hose socket',
       '14 | Front entry hose 0','14 | Front entry hose 1','15 | Seated tube gland 0',
       '15 | Seated tube gland 1','28 | Tube entry assembly'],(5.5,0,-.08))
shift(['07 | Hologram assembly','11 | Neutral projector frustum']+
      [o.name for o in scene.objects if o.name.startswith('19 | Vanguard')],(-2.35,0,0))

collection=bpy.data.collections.new('29 | Weapon display housings')
scene.collection.children.link(collection)
materials={n:bpy.data.materials[n] for n in ['21 | Warm limestone paint','21 | Paint edge',
    '21 | Warm grey casing','21 | Graphite chassis','21 | Black elastomer']}

def mesh(name, verts, faces, mat, bevel=.04):
    data=bpy.data.meshes.new(name)
    data.from_pydata(verts,[],faces)
    data.materials.append(materials[mat])
    obj=bpy.data.objects.new(name,data)
    collection.objects.link(obj)
    if bevel:
        mod=obj.modifiers.new('Machined edge','BEVEL')
        mod.width=bevel
        mod.segments=2
        obj.modifiers.new('Panel normals','WEIGHTED_NORMAL')
    return obj

def corners(width,height,cut):
    x,z=width/2,height/2
    return [(-x+cut,-z),(x-cut,-z),(x,-z+cut),(x,z-cut),
            (x-cut,z),(-x+cut,z),(-x,z-cut),(-x,-z+cut)]

def panel(name,width,height,depth,mat):
    ring=corners(width,height,.14)
    verts=[(x,y,z) for y in (-depth/2,depth/2) for x,z in ring]
    faces=[tuple(range(8)),tuple(reversed(range(8,16)))]
    faces += [(i,(i+1)%8,(i+1)%8+8,i+8) for i in range(8)]
    return mesh(name,verts,faces,mat)

def frame(name,width,height):
    outer=corners(width,height,.16)
    inner=corners(width-.42,height-.38,.10)
    rings=[outer,inner,outer,inner]
    ys=[-.22,-.22,.16,.16]
    verts=[(x,y,z) for ring,y in zip(rings,ys) for x,z in ring]
    faces=[]
    for i in range(8):
        j=(i+1)%8
        faces.extend([(i,j,8+j,8+i),(i,16+i,16+j,j),
                      (8+i,8+j,24+j,24+i),(16+i,24+i,24+j,16+j)])
    return mesh(name,verts,faces,'21 | Warm limestone paint',.045)

for index,height in enumerate((3.0,5.05),1):
    root=bpy.data.objects.new(f'29 | Weapon display {index} assembly',None)
    collection.objects.link(root)
    root.location=(6.05,.65,height)
    root.rotation_euler=(math.radians(-7),0,math.radians(-8))
    for obj in [panel(f'29 | Weapon display {index} rear enclosure',4.25,1.87,.52,'21 | Warm grey casing'),
                frame(f'29 | Weapon display {index} armor frame',4.35,1.97),
                panel(f'29 | Weapon display {index} gasket',3.96,1.62,.06,'21 | Black elastomer'),
                panel(f'29 | Weapon display {index} blank glass',3.77,1.43,.035,'21 | Graphite chassis')]:
        obj.parent=root
        if obj.name.endswith('enclosure'): obj.location.y=.17
        if obj.name.endswith('gasket'): obj.location.y=-.185
        if obj.name.endswith('glass'): obj.location.y=-.224

mast=panel('29 | Desk mounted display spine',.58,4.30,.40,'21 | Graphite chassis')
mast.location=(6.05,1.07,3.35)
base=panel('29 | Display desk foot',2.1,.25,1.2,'21 | Warm grey casing')
base.location=(6.05,.95,1.25)

# Close the vacated Launch recess beneath the relocated hose bundle.
cover=panel('29 | Right lower service cover',5.25,.30,2.8,'21 | Graphite chassis')
cover.location=(6.45,-2.55,.50)

bpy.context.view_layer.update()
assert abs(objects['09 | Projector lens'].matrix_world.translation.x)<1e-5
assert abs(objects['08 | Launch broad beveled button'].matrix_world.translation.x)<1e-5
for name in left_names:
    assert all(abs(left[name][r][c]-objects[name].matrix_world[r][c])<1e-6 for r in range(4) for c in range(4))
scene.render.filepath='//console-camera.png'
bpy.context.preferences.filepaths.save_version=0
bpy.ops.wm.save_as_mainfile(filepath=str(out))
scene.render.resolution_percentage=75
scene.cycles.samples=16
scene.render.filepath=str(proof/'layout-v1.png')
bpy.ops.render.render(write_still=True)
print('LAYOUT=pass')
