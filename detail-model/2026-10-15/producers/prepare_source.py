import bpy,json,sys
from pathlib import Path
from mathutils import Matrix
sys.path.insert(0,str(Path('D:/amind/git/agent-2/art/tools/ship')))
import ship_source
scene=bpy.context.scene
before={o.name:ship_source.part_fingerprint(o) for s in bpy.data.scenes for o in s.objects if s != scene}
world={o.name:[list(row) for row in o.matrix_world] for o in scene.objects if o.type=='MESH'}
origin=bpy.data.objects.new('SymmetryOrigin',None)
scene.collection.objects.link(origin)
collections={}
for name in ('role.hull','role.canopy','role.cores','role.ink','ignore'):
    collection=bpy.data.collections.new(name)
    scene.collection.children.link(collection)
    collections[name]=collection
for o in list(scene.objects):
    if o.type!='MESH': continue
    role='ignore' if o.hide_render else 'role.canopy' if o.name=='MVP Canopy' else 'role.cores' if o.name=='MVP Power Nacelle Core' else 'role.ink' if o.name=='Vanguard structural ink' else 'role.hull'
    collections[role].objects.link(o)
    matrix=o.matrix_world.copy()
    o.parent=origin
    o.matrix_parent_inverse=Matrix.Identity(4)
    o.matrix_world=matrix
bpy.context.view_layer.update()
assert all([list(row) for row in scene.objects[name].matrix_world]==matrix for name,matrix in world.items())
after={o.name:ship_source.part_fingerprint(o) for s in bpy.data.scenes for o in s.objects if s != scene}
assert before==after, 'Inactive editable source changed'
root=Path(bpy.data.filepath).parent
(root/'ship.json').write_text(json.dumps({'schema_version':1,'name':'Vanguard','source':'Vanguard.blend','texture_size':4096,'uv_density_max_ratio':2.0,'contour_roles':['hull'],'exemptions':[]},indent=2)+'\n')
bpy.context.preferences.filepaths.save_version=0
bpy.ops.wm.save_as_mainfile(filepath=bpy.data.filepath)
(root/'baseline-audit.json').write_text(json.dumps({'inactive_parts_unchanged':len(before),'production_world_transforms_preserved':len(world)},indent=2)+'\n')
