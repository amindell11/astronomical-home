import bpy,json
from mathutils import Vector
from mathutils.bvhtree import BVHTree
from pathlib import Path
items={'Pod cooling hatch':('MVP wing_armor',[(.326,-.32),(.402,-.303),(.413,-.426),(.331,-.482)]),'Cockpit cooling vent':('MVP Cockpit.001',[(.08,.274),(.116,.278),(.122,.326),(.083,.322)]),'Wing service hatch':('MVP Wing',[(.448,-.307),(.538,-.296),(.53,-.258),(.442,-.27)])}
def tree(obj):
    m=obj.data;m.calc_loop_triangles()
    return BVHTree.FromPolygons([obj.matrix_world@v.co for v in m.vertices],[t.vertices for t in m.loop_triangles],all_triangles=True)
out={}
for name,(host,points) in items.items():
    a,b=tree(bpy.data.objects[name]),tree(bpy.data.objects[host])
    points=[Vector(p) for p in points]
    differences=[]
    for i in range(1,40):
        for j in range(1,40):
            u=.11+.78*i/40;v=.13+.74*j/40
            xy=(points[0]*(1-u)+points[1]*u)*(1-v)+(points[3]*(1-u)+points[2]*u)*v
            ray=Vector((xy.x,xy.y,10))
            detail=a.ray_cast(ray,Vector((0,0,-1)))[0];hull=b.ray_cast(ray,Vector((0,0,-1)))[0]
            if detail is not None and hull is not None:differences.append(detail.z-hull.z)
    out[name]={'samples':len(differences),'below_hull':sum(d<-.00001 for d in differences),'minimum_clearance':min(differences)}
Path(__file__).with_name('detail-fit-audit.json').write_text(json.dumps(out,indent=2)+'\n')
print(json.dumps(out))

assert all(row['below_hull']==0 for row in out.values()),out
