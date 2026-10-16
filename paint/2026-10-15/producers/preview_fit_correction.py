import bpy,sys,json
from pathlib import Path
import numpy as np
from mathutils import Vector
from mathutils.bvhtree import BVHTree
sys.path.insert(0,'D:/amind/git/agent-2/art/tools/ship')
import ship_source,ship_render,ship_contract
source=Path('D:/amind/git/agent-2/art/ships/vanguard/Vanguard.blend')
source_hash=ship_contract.sha256_file(source)
evaluation,framing=ship_render.prepare(source)
source_scene=bpy.data.scenes['Vanguard - Texture MVP']
source_geometry=ship_source.fingerprint(source_scene)['components']['geometry']
items={'Pod cooling hatch':('MVP wing_armor',[(.326,-.32),(.402,-.303),(.413,-.426),(.331,-.482)],.004),'Cockpit cooling vent':('MVP Cockpit.001',[(.08,.274),(.116,.278),(.122,.326),(.083,.322)],.0004)}
out=Path('D:/amind/git/agent-2/results/vanguard-redo/paint/fit-review');out.mkdir(parents=True,exist_ok=True)
report={};tiles=[]
def tree(obj):
    mesh=obj.data;mesh.calc_loop_triangles()
    return BVHTree.FromPolygons([obj.matrix_world@v.co for v in mesh.vertices],[t.vertices for t in mesh.loop_triangles],all_triangles=True)
def audit(obj,host,points):
    a,b=tree(obj),tree(host);points=[Vector(p) for p in points];differences=[]
    for i in range(1,40):
        for j in range(1,40):
            u=.11+.78*i/40;v=.13+.74*j/40
            xy=(points[0]*(1-u)+points[1]*u)*(1-v)+(points[3]*(1-u)+points[2]*u)*v
            origin=Vector((xy.x,xy.y,10));direction=Vector((0,0,-1))
            detail=a.ray_cast(origin,direction)[0];hull=b.ray_cast(origin,direction)[0]
            if detail is None or hull is None:raise ValueError('Missing fitted surface')
            differences.append(detail.z-hull.z)
    return {'samples':len(differences),'below_hull':sum(d<-.00001 for d in differences),'minimum_clearance':min(differences)}
for name,(host,quad,lift) in items.items():
    obj=evaluation.copies[name];hull=evaluation.copies[host]
    points=np.array([list(obj.matrix_world@v.co) for v in obj.data.vertices])
    box=(points.min(axis=0)-.012,points.max(axis=0)+.012)
    slug='pod' if name.startswith('Pod') else 'cockpit'
    before=audit(obj,hull,quad)
    image=ship_render.render(evaluation,box,{'top':ship_render.ORTHO['top']},768,out,slug+'_before_')['top']
    tiles.append(ship_render.load_pixels(image))
    obj.data=obj.data.copy()
    for v in obj.data.vertices:
        if v.index>=16:v.co.z+=lift
    bpy.context.view_layer.update()
    after=audit(obj,hull,quad)
    assert after['below_hull']==0,(name,after)
    image=ship_render.render(evaluation,box,{'top':ship_render.ORTHO['top']},768,out,slug+'_after_')['top']
    tiles.append(ship_render.load_pixels(image))
    report[name]={'lift':lift,'before':before,'after':after}
ship_render.save_pixels(out/'fit-comparison.png',ship_render.grid(tiles,2))
assert ship_contract.sha256_file(source)==source_hash,'Preview changed saved source'
assert ship_source.fingerprint(source_scene)['components']['geometry']==source_geometry,'Preview changed source geometry'
(out/'fit-review.json').write_text(json.dumps({'saved_source_unchanged':True,'geometry_lock_untouched':True,'parts':report},indent=2)+'\n')
print(json.dumps(report))
