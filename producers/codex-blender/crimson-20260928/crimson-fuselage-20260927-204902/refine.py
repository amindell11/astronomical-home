import bpy,json
import numpy as np
from pathlib import Path
from mathutils import Vector
out=Path('D:/amind/git/agent-7/results/crimson-fuselage-20260927-204902')
bpy.ops.wm.open_mainfile(filepath=str(out/'Before-fuselage-refinement.blend'))
h=bpy.data.objects['Central hull'];c=bpy.data.objects['Cockpit surround']
assert len(h.data.vertices)==104 and len(c.data.vertices)==120
cw=np.array([list(c.matrix_world@v.co) for v in c.data.vertices])
hw=np.array([list(h.matrix_world@v.co) for v in h.data.vertices])
# Keep the user's inset roof footprint while refining the surrounding wall.
for ring in range(5):
    ids=slice(ring*24,(ring+1)*24)
    xyz=cw[ids].copy()
    nose=np.clip((xyz[:,1]-.28)/.48,0,1)
    if ring<3:
        xyz[:,0]*=(.92-.08*nose)
    elif ring==3:
        xyz[:,0]*=(.97-.035*nose)
    fit=np.column_stack([np.ones(24),xyz[:,1],abs(xyz[:,0])])
    z=fit@np.linalg.lstsq(fit,xyz[:,2],rcond=None)[0]
    xyz[:,2]=.25*xyz[:,2]+.75*z
    cw[ids]=xyz
# Lay out the two wall loops at uniform intervals between the base and shoulder.
for ring,t in [(1,.32),(2,.68)]:
    cw[ring*24:(ring+1)*24]=cw[:24]*(1-t)+cw[72:96]*t
for ring,offset in [(1,.016),(2,.032)]:
    ids=slice(ring*24,(ring+1)*24)
    cw[ids,1]-=offset*np.clip((.18-cw[ids,1])/.22,0,1)
cw[:,2]=-.08+(cw[:,2]+.08)*.92
# Sample the lower surround edge to bury the hull's former protruding rim.
edge=cw[1:13]
order=np.argsort(edge[:,1]);edge=edge[order]
for i,p in enumerate(hw):
    ring=i//26
    w=np.clip((p[1]+.08)/.24,0,1)
    p[0]*=1-.08*w
    if ring in (1,2,3):
        p[1]-=.028*np.clip((p[1]-.35)/.40,0,1)
        max_width=np.interp(p[1],edge[:,1],abs(edge[:,0]))
        if abs(p[0])>0:
            limit=max_width*(.985 if ring==2 else .93 if ring==3 else .87)
            p[0]=np.sign(p[0])*((1-w)*abs(p[0])+w*min(abs(p[0]),limit))
        seam_z=np.interp(p[1],edge[:,1],edge[:,2])
        desired=seam_z+(.003 if ring==2 else .011 if ring==3 else -.025)
        p[2]=(1-w)*p[2]+w*desired
    else:
        p[2]+=.025*np.clip((p[1]+.25)/.45,0,1)
    hw[i]=p
for o,world in [(h,hw),(c,cw)]:
    inv=o.matrix_world.inverted()
    for v,p in zip(o.data.vertices,world): v.co=inv@Vector(p)
    o.data.update()
(out/'candidate-coordinates.json').write_text(json.dumps({o.name:[list(v.co) for v in o.data.vertices] for o in (h,c)}))
# Render the same inspection views without writing to the live editor.
code=(out/'inspect.py').read_text()
code=code[code.index('s=bpy.context.scene'):].replace("'before-'","'after-'")
exec(code)


