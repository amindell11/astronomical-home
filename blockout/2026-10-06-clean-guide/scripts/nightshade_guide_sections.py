from pathlib import Path
import bpy,json,math,numpy as np
from mathutils import Matrix
root=Path('D:/amind/git/agent-1')
out=root/'results/nightshade-clean-guide/guide'
out.mkdir(parents=True,exist_ok=True)
bpy.ops.wm.open_mainfile(filepath=str(root/'results/nightshade-legacy-base/inspection/LegacyNative.blend'))
ob=next(o for o in bpy.context.scene.objects if o.type=='MESH')
ob.data.calc_loop_triangles()
co=np.array([Matrix.Rotation(math.pi,4,'Z')@ob.matrix_world@v.co for v in ob.data.vertices])
tri=np.array([t.vertices[:] for t in ob.data.loop_triangles])
polys=co[tri]
report={}
for axis,planes in [(1,[-.65,-.5,-.4,-.3,-.2,-.1,0,.1,.2,.3,.4,.5,.6,.7,.8]),(2,[-.18,-.14,-.1,-.06,-.02,.02,.06,.1,.14,.18])]:
    for cut in planes:
        chosen=polys[(polys[:,:,axis].min(axis=1)<cut)&(polys[:,:,axis].max(axis=1)>cut)]
        segments=[]
        for t in chosen:
            ps=[]
            for a,b in [(t[0],t[1]),(t[1],t[2]),(t[2],t[0])]:
                if (a[axis]-cut)*(b[axis]-cut)<0:
                    p=a+(b-a)*(cut-a[axis])/(b[axis]-a[axis])
                    ps.append([float(p[k]) for k in range(3) if k!=axis])
            if len(ps)==2:segments.append(ps)
        report[f'{axis}:{cut}']=segments
(out/'sections.json').write_text(json.dumps(report))
print('SECTIONS_READY')
