import bpy,json,sys
from pathlib import Path
from mathutils import Vector
from mathutils.bvhtree import BVHTree
sys.path.insert(0,'D:/amind/git/agent-2/art/tools/ship')
import ship_source
scene=bpy.context.scene
before={o.name:ship_source.part_fingerprint(o) for o in scene.objects if o.type in {'MESH','EMPTY'}}
originals={o.name:ship_source.part_fingerprint(o) for s in bpy.data.scenes if s!=scene for o in s.objects if o.type in {'MESH','EMPTY'}}
root=scene.objects['SymmetryOrigin']
role=bpy.data.collections['role.hull']
palette={}
for name,color in {'HullPrimary':(.72,.69,.61,1),'Structure':(.035,.046,.063,1),'EdgeHighlight':(.9,.86,.76,1)}.items():
    material=bpy.data.materials.new(name)
    material.diffuse_color=color
    palette[name]=material
records=[]
def assembly(name,host,quad,slats):
    mesh=scene.objects[host].data
    matrix=scene.objects[host].matrix_world
    vertices=[matrix@v.co for v in mesh.vertices]
    tree=BVHTree.FromPolygons(vertices,[list(p.vertices) for p in mesh.polygons],all_triangles=True)
    points=[Vector(p) for p in quad]
    coords=[];faces=[];mats=[]
    def point(u,v,h):
        xy=(points[0]*(1-u)+points[1]*u)*(1-v)+(points[3]*(1-u)+points[2]*u)*v
        hit,normal,index,distance=tree.ray_cast(Vector((xy.x,xy.y,10)),Vector((0,0,-1)),20)
        if hit is None:raise ValueError(f'{name}: ({u},{v}) is outside {host}')
        return (xy.x,xy.y,hit.z+h)
    def ring(u0,v0,u1,v1,h):
        ids=[]
        for u,v in ((u0,v0),(u1,v0),(u1,v1),(u0,v1)):
            ids.append(len(coords));coords.append(point(u,v,h))
        return ids
    def bridge(a,b,material):
        for i in range(4):faces.append((a[i],a[(i+1)%4],b[(i+1)%4],b[i]));mats.append(material)
    base=ring(0,0,1,1,-.001)
    outer=ring(0,0,1,1,.004)
    bevel=ring(.035,.035,.965,.965,.006)
    inner=ring(.11,.13,.89,.87,.006)
    floor=ring(.11,.13,.89,.87,.0015)
    faces.append(tuple(reversed(base)));mats.append(0)
    bridge(base,outer,0);bridge(outer,bevel,0);bridge(bevel,inner,0);bridge(inner,floor,1)
    faces.append(tuple(floor));mats.append(1)
    if slats:
        for i in range(slats):
            v=.23+i*(.54/max(1,slats-1))
            a=ring(.17,v-.028,.83,v+.028,.001)
            b=ring(.17,v-.028,.83,v+.028,.0045)
            bridge(a,b,0);faces.append(tuple(b));mats.append(0);faces.append(tuple(reversed(a)));mats.append(1)
    else:
        a=ring(.69,.38,.81,.62,.001)
        b=ring(.69,.38,.81,.62,.004)
        bridge(a,b,2);faces.append(tuple(b));mats.append(2);faces.append(tuple(reversed(a)));mats.append(1)
    data=bpy.data.meshes.new(name)
    area=sum(points[i].x*points[(i+1)%4].y-points[(i+1)%4].x*points[i].y for i in range(4))
    if area<0:
        faces=[tuple(reversed(face)) for face in faces]
    data.from_pydata(coords,[],faces)
    data.update()
    obj=bpy.data.objects.new(name,data)
    role.objects.link(obj)
    obj.parent=root
    for material in palette.values():data.materials.append(material)
    for polygon,index in zip(data.polygons,mats):polygon.material_index=index
    modifier=obj.modifiers.new('Bilateral detail','MIRROR')
    modifier.mirror_object=root
    modifier.use_axis[0]=True
    records.append({'part':name,'host':host,'vertices':len(coords),'faces':len(faces),'depth':.007,'slats':slats})
assembly('Pod cooling hatch','MVP wing_armor',[(.326,-.32),(.402,-.303),(.413,-.426),(.331,-.482)],5)
assembly('Cockpit cooling vent','MVP Cockpit.001',[(.08,.274),(.116,.278),(.122,.326),(.083,.322)],3)
assembly('Wing service hatch','MVP Wing',[(.448,-.307),(.538,-.296),(.53,-.258),(.442,-.27)],0)
bpy.context.view_layer.update()
assert all(ship_source.part_fingerprint(scene.objects[name])==record for name,record in before.items()), 'Existing production part changed'
assert originals=={o.name:ship_source.part_fingerprint(o) for s in bpy.data.scenes if s!=scene for o in s.objects if o.type in {'MESH','EMPTY'}}, 'Editable original scene changed'
bpy.context.preferences.filepaths.save_version=0
bpy.ops.wm.save_as_mainfile(filepath=bpy.data.filepath)
Path(__file__).with_name('detail-audit.json').write_text(json.dumps({'existing_parts_unchanged':len(before),'inactive_parts_unchanged':len(originals),'new_parts':records},indent=2)+'\n')

