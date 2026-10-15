import bpy,json,sys,hashlib
from pathlib import Path
import numpy as np
sys.path.insert(0,'D:/amind/git/agent-2/art/tools/ship')
import ship_source

def snapshot(path):
    bpy.ops.wm.open_mainfile(filepath=path)
    result={}
    for o in bpy.data.objects:
        if o.type!='MESH':continue
        mesh=o.data
        keys=('vertices','corner_indices','corner_normals','triangle_indices')
        record={k:hashlib.sha256(a.tobytes()).hexdigest() for k,a in zip(keys,ship_source.mesh_arrays(mesh))}
        record['uv']={u.name:hashlib.sha256(ship_source.read_array(u.uv,'vector',np.float32,2).tobytes()).hexdigest() for u in mesh.uv_layers}
        record['world']=list(sum((list(row) for row in o.matrix_world),[]))
        record['modifiers']=[ship_source._modifier_record(m,ship_source.GEOMETRY_PARAMETERS) for m in o.modifiers]
        result[o.name]=record
    return result
before=snapshot('D:/amind/git/astronomical-home/art/ships/vanguard/drawn-study/VanguardPainted.blend')
after=snapshot('D:/amind/git/agent-2/art/ships/vanguard/Vanguard.blend')
changes={name:[key for key in old if old[key]!=after[name][key]] for name,old in before.items() if name not in after or old!=after[name]}
assert not changes, changes
result={'original_meshes_checked':len(before),'changed_original_meshes':changes,'new_meshes':sorted(set(after)-set(before)),'comparison':'Exact positions, corner/triangle indices, corner normals, UVs, world transforms and modifier parameters'}
Path(__file__).with_name('preservation-audit.json').write_text(json.dumps(result,indent=2)+'\n')
print(json.dumps(result))
