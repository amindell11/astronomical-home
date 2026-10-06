from pathlib import Path
import bpy,json,sys,math
from mathutils import Vector
root=Path('D:/amind/git/agent-1');base=root/'results/nightshade-frame-fit';out=base/'round-02'
out.mkdir(parents=True,exist_ok=True)
sys.path.insert(0,str(root/'art/tools/ship'));import ship_source
before=ship_source.fingerprint(bpy.context.scene)
assert before==json.loads((base/'round-01/check/ship_check.json').read_text())['fingerprint']
for name,stride,outward,z,xyfactor,zfactor in [
    ('Upper canopy inner seal',5,.004,.001,1,1),
    ('Lower canopy metal bezel',8,.006,0,.55,.65)]:
    ob=bpy.data.objects[name];inv=ob.matrix_world.inverted()
    old=[ob.matrix_world@v.co for v in ob.data.vertices]
    centers=[sum(old[s*stride:(s+1)*stride],Vector())/stride for s in range(22)]
    for station in range(22):
        tangent=centers[min(station+1,21)]-centers[max(station-1,0)]
        tangent.z=0;tangent.normalize();normal=Vector((-tangent.y,tangent.x,0))
        for k in range(stride):
            d=old[station*stride+k]-centers[station]
            p=centers[station]+normal*outward+Vector((d.x*xyfactor,d.y*xyfactor,d.z*zfactor+z))
            if station in (0,21):p.x=0
            ob.data.vertices[station*stride+k].co=inv@p
    ob.data.update()
after=ship_source.fingerprint(bpy.context.scene)
changed=[n for n in before['parts'] if before['parts'][n]!=after['parts'].get(n)]
assert set(changed)=={'Upper canopy inner seal','Lower canopy metal bezel'}
bpy.context.preferences.filepaths.save_version=0;bpy.ops.wm.save_as_mainfile(filepath=bpy.data.filepath)
(out/'edit-audit.json').write_text(json.dumps({'changed_parts':changed,'all_unclaimed_parts_unchanged':True,
    'topology_and_visibility_retained':True,'upper_seal_offset_outward':.004,
    'lower_rim_cross_section_xy_factor':.55,'lower_rim_cross_section_z_factor':.65},indent=2))
result={'saved':bpy.data.filepath,'changed':changed}
