import bpy, sys, json, hashlib
import numpy as np
from pathlib import Path
from types import SimpleNamespace

stage = Path(__file__).parent
root = Path('D:/amind/git/agent-3/art/ships/bulwark')
sys.path.insert(0, str(stage.parent/'2026-10-18-asymmetric/tool/art/tools/ship'))
import ship_source, ship_regions, ship_paint
bpy.ops.wm.open_mainfile(filepath=str(root/'Bulwark.blend'))
size = 2048
before = ship_source.fingerprint(bpy.context.scene)
base = {p.name:hashlib.sha256(p.read_bytes()).hexdigest() for p in (root/'layers').glob('BaseRegion*.png')}
original = {n:ship_paint.read_gray(root/'layers'/f'{n}.png') for n in ('Ink','Light','Shadow')}
out = {n:np.zeros((size,size),np.float32) for n in ('Ink','Light')}
faces = {o['name']:{f['i']:f for f in o['faces']} for o in json.loads((stage/'faces.json').read_text())}
marks = []

def add(name, face, kind, **kwargs):
    marks.append(dict(part=name,face=face,kind=kind,**kwargs))

add('Hull - Dorsal spine',8,'path',points=[[.10,-.72],[.47,-.72],[.59,-.84],[.88,-.84]])
add('Engine - Shoulder booms',20,'path',points=[[.66,-1.65],[.91,-1.65],[1.05,-1.82],[1.44,-1.82]])
for face in (0,9):
    add('Upper flaps - Fins',face,'path',points=[[.75,-2.65],[1.28,-2.65],[1.42,-2.80],[1.87,-2.80]])
(stage/'authored-details.json').write_text(json.dumps(marks,indent=2))

def stroke(distance, halfwidth=.012):
    return np.clip((halfwidth + .007 - distance)/.014,0,1)

def rect_sdf(q, bounds, scale):
    a,b,c,d = bounds
    center=np.array([(a+b)/2,(c+d)/2]); half=np.array([(b-a)/2,(d-c)/2])*scale
    delta=np.abs((q-center)*scale)-half
    return np.linalg.norm(np.maximum(delta,0),axis=1)+np.minimum(np.maximum(delta[:,0],delta[:,1]),0)

def evaluate(mark,p):
    face=faces[mark['part']][mark['face']]
    verts=np.array(face['verts']); normal=np.array(face['normal']);normal/=np.linalg.norm(normal)
    eligible=np.abs((p-verts[0])@normal)<.018
    ink=np.zeros(len(p));light=ink.copy();em=ink.copy()
    if not eligible.any():return ink,light,em
    origin=verts[0]; axes=np.stack([verts[1]-origin,verts[-1]-origin],axis=1)
    q=(p-origin)@np.linalg.pinv(axes).T; scale=np.linalg.norm(axes,axis=0)
    if mark.get('vertical'):
        q=q[:,::-1];scale=scale[::-1]
    kind=mark['kind']
    if kind in ('vent','hatch','lamps'):
        bounds=mark['bounds']; a,b,c,d=bounds
        dist=rect_sdf(q,bounds,scale)
        ink=np.maximum(ink,stroke(abs(dist),.012)*.8)
        lip=rect_sdf(q+np.array([.012,.008])/scale,bounds,scale)
        light=np.maximum(light,stroke(abs(lip),.006)*.40)
        if kind=='vent':
            ink=np.maximum(ink,(dist<0)*.19)
            for i in range(mark['slats']):
                u=a+(b-a)*(i+.5)/mark['slats']; w=(b-a)/mark['slats']*.23
                slot=rect_sdf(q,[u-w,u+w,c+.055,d-.055],scale)
                ink=np.maximum(ink,np.clip((.007-slot)/.014,0,1)*.90)
                rim=rect_sdf(q+np.array([.014,0])/scale,[u-w,u+w,c+.055,d-.055],scale)
                light=np.maximum(light,stroke(abs(rim),.005)*.34)
        if kind=='lamps':
            ink=np.maximum(ink,(dist<0)*.85)
            for i in range(mark['segments']):
                lo=a+(b-a)*i/mark['segments']+.02;hi=a+(b-a)*(i+1)/mark['segments']-.02
                lamp=rect_sdf(q,[lo,hi,c+.025,d-.025],scale)
                em=np.maximum(em,np.clip((.006-lamp)/.012,0,1))
    elif kind=='path':
        distance=np.full(len(p),np.inf);lip_distance=distance.copy()
        path=np.array(mark['points'])
        for a,b in zip(path[:-1],path[1:]):
            delta=b-a
            for points,acc in [(p[:,:2],distance),(p[:,:2]-[0,.025],lip_distance)]:
                t=np.clip(((points-a)@delta)/(delta@delta),0,1)
                np.minimum(acc,np.linalg.norm(points-a-t[:,None]*delta,axis=1),out=acc)
        ink=stroke(distance,.015)*.78
        light=stroke(lip_distance,.007)*.39
    elif kind=='cross_seam':
        ink=stroke(np.abs(p[:,1]-mark['y']),.011)*.66
        light=stroke(np.abs(p[:,1]-mark['y']-.022),.006)*.32
    elif kind=='fastener':
        r=np.linalg.norm(p[:,:2]-mark['center'],axis=1)
        ink=stroke(np.abs(r-mark['radius']),.008)*.62
        light=stroke(np.abs(np.linalg.norm(p[:,:2]-np.array(mark['center'])-[0,.016],axis=1)-mark['radius']),.005)*.30
    elif kind=='canister':
        center=verts.mean(axis=0)
        v=np.array([0.,0.,1.]);v-=normal*np.dot(v,normal);v/=np.linalg.norm(v)
        u=np.cross(v,normal)
        coords=np.column_stack([(p-center)@u,(p-center)@v]);r=np.linalg.norm(coords,axis=1)
        angle=np.arctan2(coords[:,1],coords[:,0]);gap=np.abs(np.sin(angle*3))
        ring=np.clip((r-.403)/.012,0,1)*np.clip((.520-r)/.012,0,1)
        em=ring*np.clip((gap-.075)/.07,0,1)
        ink=np.maximum(stroke(abs(r-.394),.009),stroke(abs(r-.529),.009))*.78
        light=np.maximum(stroke(abs(r-.379),.006),stroke(abs(r-.545),.006))*.38
        for i in range(6):
            a=i*np.pi/3;center2=np.array([np.cos(a),np.sin(a)])*.548
            rr=np.linalg.norm(coords-center2,axis=1)
            ink=np.maximum(ink,stroke(abs(rr-.015),.006)*.70)
    return ink*eligible,light*eligible,em*eligible

deps=bpy.context.evaluated_depsgraph_get()
for name in sorted(set(m['part'] for m in marks)):
    obj=bpy.data.objects[name];ev=obj.evaluated_get(deps);mesh=ev.to_mesh(preserve_all_data_layers=True,depsgraph=deps)
    co=np.array([list(ev.matrix_world@v.co) for v in mesh.vertices]);part=SimpleNamespace(mesh=mesh,co=co)
    selected=[m for m in marks if m['part']==name]
    for tri,xs,ys,p in ship_regions.surface_samples(part,size,'PaintUV'):
        p=p.copy();p[:,0]=np.abs(p[:,0])
        for mark in selected:
            values=evaluate(mark,p)
            for key,val in zip(out,values):
                np.maximum.at(out[key],(ys,xs),val)
    ev.to_mesh_clear()
    print('AUTHORED',name,flush=True)

(stage/'masks').mkdir(exist_ok=True)
for name,a in out.items():
    delta=np.rint(a*255).astype(np.uint8)
    ship_paint.write_png(stage/'masks'/f'Detail{name}.png',delta)
    final=np.maximum(original[name],delta) if name in original else delta
    ship_paint.write_png(stage/'masks'/f'{name}.png',final)
    image=bpy.data.images.get('Bulwark Drawn '+name) if name in original else None
    tmp=bpy.data.images.load(str(stage/'masks'/f'{name}.png'),check_existing=False);tmp.colorspace_settings.name='Non-Color'
    if image is not None:
        old_name=image.name;image.user_remap(tmp);bpy.data.images.remove(image);tmp.name=old_name
    else:
        tmp.name='Bulwark Detail Emission'
    image=tmp
    image.pack();image.use_fake_user=True
    image.filepath=str(stage/'masks'/f'{name}.png')

assert before==ship_source.fingerprint(bpy.context.scene)
assert base=={p.name:hashlib.sha256(p.read_bytes()).hexdigest() for p in (root/'layers').glob('BaseRegion*.png')}
(stage/'authoring-report.json').write_text(json.dumps({'source_fingerprint_unchanged':True,'base_coat_files_unchanged':True,'marks':len(marks),'counts':{k:int(np.count_nonzero(v)) for k,v in out.items()},'fingerprint':before},indent=2))
bpy.context.preferences.filepaths.save_version=0
bpy.ops.wm.save_as_mainfile(filepath=str(stage/'detail-review.blend'),check_existing=False)
