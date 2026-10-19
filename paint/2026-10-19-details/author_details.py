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
out = {n:np.zeros((size,size),np.float32) for n in ('Ink','Light','DetailEmission')}
faces = {o['name']:{f['i']:f for f in o['faces']} for o in json.loads((stage/'faces.json').read_text())}
marks = []

def add(name, face, kind, **kwargs):
    marks.append(dict(part=name,face=face,kind=kind,**kwargs))

# The approved plate edges are already present in the generated Ink mask.
for name in ('Armor - Upper shoulder guards','Armor - Lower skids'):
    add(name,9,'lamps',bounds=[.17,.83,.72,.88],segments=3)
    add(name,9,'vent',bounds=[.21,.79,.23,.54],slats=3)
add('Armor - Lower skids',20,'lamps',bounds=[.20,.78,.69,.84],segments=3)
add('Armor - Side plates',2,'vent',bounds=[.24,.73,.25,.75],slats=3,vertical=True)
add('Armor - Side plates',16,'vent',bounds=[.20,.79,.23,.77],slats=3,vertical=True)
add('Engine - Shrouds',9,'vent',bounds=[.22,.77,.17,.82],slats=4,vertical=True)
add('Engine - Shrouds',2,'hatch',bounds=[.24,.74,.22,.78])
add('Upper flaps - Fins',0,'cross_seam',y=-3.35)
add('Upper flaps - Fins',9,'cross_seam',y=-3.35)
add('Lower flaps - Fins',0,'cross_seam',y=-3.12)
add('Lower flaps - Fins',9,'cross_seam',y=-3.12)
add('Hull - Ram nose',10,'fastener',center=[.80,4.63],radius=.035)
add('Canisters - Metal side modules',60,'canister')
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

def linear(c):return ((c+.055)/1.055)**2.4 if c>.04045 else c/12.92
color=tuple(linear(x/255) for x in (226,185,250))+(1.,)
for mat in bpy.data.materials:
    if not mat.use_nodes or 'Drawn preview' not in mat.node_tree.nodes:continue
    tree=mat.node_tree;n=tree.nodes;preview=n['Drawn preview']
    tex=n.new('ShaderNodeTexImage');tex.name='Detail Emission mask';tex.label='Editable lilac light coverage';tex.image=bpy.data.images['Bulwark Detail Emission']
    tree.links.new(n['Drawn UV'].outputs['UV'],tex.inputs['Vector'])
    mix=n.new('ShaderNodeMixRGB');mix.name='Detail emissive color';mix.inputs[2].default_value=color
    tree.links.new(preview.inputs['Color'].links[0].from_socket,mix.inputs[1]);tree.links.new(tex.outputs['Color'],mix.inputs[0]);tree.links.new(mix.outputs[0],preview.inputs['Color'])
    strength=n.new('ShaderNodeMath');strength.name='Detail emission energy';strength.operation='MULTIPLY_ADD';strength.inputs[1].default_value=.15;strength.inputs[2].default_value=1
    tree.links.new(tex.outputs['Color'],strength.inputs[0]);tree.links.new(strength.outputs[0],preview.inputs['Strength'])

assert before==ship_source.fingerprint(bpy.context.scene)
assert base=={p.name:hashlib.sha256(p.read_bytes()).hexdigest() for p in (root/'layers').glob('BaseRegion*.png')}
(stage/'authoring-report.json').write_text(json.dumps({'source_fingerprint_unchanged':True,'base_coat_files_unchanged':True,'marks':len(marks),'counts':{k:int(np.count_nonzero(v)) for k,v in out.items()},'fingerprint':before},indent=2))
bpy.context.preferences.filepaths.save_version=0
bpy.ops.wm.save_as_mainfile(filepath=str(stage/'detail-review.blend'),check_existing=False)
