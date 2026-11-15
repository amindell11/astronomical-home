import bpy
from mathutils import Vector
from pathlib import Path
s=bpy.context.scene
ob=bpy.data.objects
proof=Path('C:/Users/amind/.codex/visualizations/2026/11/15/01a1dc89-c32a-7c82-ba76-5ae7751e1d5f/hangar')

def move(name,d):
    m=ob[name].matrix_world.copy(); m.translation+=Vector(d); ob[name].matrix_world=m

move('07 | Angled cooling bank',(-2.35,0,0))
for name in ['08 | Complete hose loom','10 | Complete tube assembly','14 | Recessed front hose socket',
       '14 | Front entry hose 0','14 | Front entry hose 1','15 | Seated tube gland 0',
       '15 | Seated tube gland 1','28 | Tube entry assembly']:
    move(name,(-1.5,.55,.48))
for name in ['14 | Projector formed armor','14 | Projector recessed folded sidewalls']:
    o=ob[name]; inv=o.matrix_world.inverted()
    for v in o.data.vertices:
        p=o.matrix_world@v.co
        if p.y < -.7: p.y=-.7+(p.y+.7)*.35
        v.co=inv@p
for name in ['14 | Forearm fold into Launch and controls','14 | Launch casing folded returns',
             '14 | Projector to control folded shoulder','14 | Right shoulder into Launch edge',
             '18 | Projector control and Launch seat','18 | Lower tray mounting return',
             '14 | Dark support beneath apron fold','15 | Projector to control rear end cap',
             '29 | Right lower service cover']:
    ob[name].hide_render=True; ob[name].hide_set(True)

c=bpy.data.collections['29 | Weapon display housings']
def box(name,loc,scale,material):
    bpy.ops.mesh.primitive_cube_add(size=1,location=loc)
    o=bpy.context.object; o.name=name
    o.dimensions=scale
    bpy.ops.object.transform_apply(location=False,rotation=False,scale=True)
    for col in list(o.users_collection): col.objects.unlink(o)
    c.objects.link(o)
    o.data.materials.append(bpy.data.materials[material])
    m=o.modifiers.new('Folded edge','BEVEL'); m.width=.09; m.segments=2
    o.modifiers.new('Panel normals','WEIGHTED_NORMAL')
    return o
box('29 | Continuous lower desk', (3.1,-1.35,.25),(12.0,5.0,.65),'21 | Graphite chassis')
box('29 | Launch mounting plinth', (0,-2.25,.55),(3.25,2.1,.48),'21 | Warm grey casing')
box('29 | Right service ledge',(5.7,-2.5,.50),(7.6,1.9,.12),'21 | Warm grey casing')
s.render.filepath='//console-camera.png'
bpy.context.preferences.filepaths.save_version=0
bpy.context.view_layer.update()
bpy.ops.wm.save_as_mainfile(filepath=bpy.data.filepath)
s.render.resolution_percentage=75;s.cycles.samples=12
s.render.filepath=str(proof/'layout-v2.png')
bpy.ops.render.render(write_still=True)
print('LAYOUT=pass')
