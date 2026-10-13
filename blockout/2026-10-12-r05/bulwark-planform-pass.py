import bpy,json,sys,hashlib
from pathlib import Path
from mathutils import Vector
sys.path.insert(0,'D:/amind/git/agent-3/art/tools/ship')
import ship_source
scene=bpy.context.scene
assert bpy.context.mode=='OBJECT'
previous=json.load(open('D:/amind/git/agent-3/results/bulwark/tail-blockout-2026-10-12/check/ship_check.json'))['fingerprint']
assert ship_source.fingerprint(scene)==previous, 'Source changed since the checkpoint; preserve owner edits first.'
out=Path('D:/amind/git/agent-3/results/bulwark/planform-2026-10-12')
out.mkdir(parents=True,exist_ok=True)
snapshot={o.name:{'world':[list(r) for r in o.matrix_world],'verts':[list(v.co) for v in o.data.vertices]} for o in scene.objects if o.type=='MESH'}
(out/'before.json').write_text(json.dumps(snapshot,indent=2))
def set_world(o,v,p):
    v.co=o.matrix_world.inverted()@Vector(p)
for name in ['01 Ram nose','02 Upper hull','03 Lower hull','07 Side insets']:
    o=bpy.data.objects[name]
    for v in o.data.vertices:
        old=v.co.copy();p=o.matrix_world@old
        if name=='01 Ram nose':
            t=max(0,min(1,old.y/2.22))
            p.x*=.975*(1-t)+.84*t
        elif name=='03 Lower hull' and abs(old.y+2)<.001:
            p.x*=.55/(2.0352*1.0460197925567627)
            p.y=-1.23
        else:
            p.x*=.93
            if name=='02 Upper hull' and abs(old.y+2)<.001:
                p.x*=2.35/(2.0352*1.0460197925567627*.93)
                p.y=0.
        set_world(o,v,p)
    o.data.update()
boom=bpy.data.objects['04 Tail booms']
specs=[([7,6,5,4,3,2,1],-.676,1.0094,.55,2.35,.14),
       ([0,8,9,10,11,12,13],-.676,.3204,.55,1.846,-1.20),
       ([14,15,16,17,18,19,20],-.5912,.4264,.55,1.44,-2.28)]
for indices,old_min,old_max,new_min,new_max,y in specs:
    for i in indices:
        v=boom.data.vertices[i];old=v.co.copy();p=boom.matrix_world@old
        t=(old.x-old_min)/(old_max-old_min)
        p.x=new_min+t*(new_max-new_min);p.y=y
        if y==.14:
            p.z+=.24*max(0,min(1,(p.z-1.34)/(2.795-1.34)))
        set_world(boom,v,p)
boom.data.update()
tip=bpy.data.objects['05 Tail tips']
for v in tip.data.vertices:
    old=v.co.copy();p=tip.matrix_world@old;i=v.index
    if i<7:
        q=boom.matrix_world@boom.data.vertices[14+i].co
        p=q
    elif i<14:
        t=(old.x+.1968)/1.1448
        p.x=.55+t*1.39;p.y=-3.53
    elif i<28:
        t=(old.x+.26)/1.34
        p.x=.62+t*1.22;p.y=-3.68-t*1.32
    else:
        t=(old.x+.045+.26)/1.34
        p.x=.65+t*1.16;p.y=-2.85
        p.z=2.46+(old.z+.402)/.924*.8
    set_world(tip,v,p)
tip.data.update()
engine=bpy.data.objects['08 Nested engines']
for v in engine.data.vertices:
    old=v.co.copy()
    p=Vector((1.23+(old.x-.41)*.914,-3.48+(old.y+1.35)/.56*.66,2.91+(old.z-.06)*.914))
    set_world(engine,v,p)
engine.data.update()
bpy.context.view_layer.update()
assert ship_source.part_fingerprint(bpy.data.objects['06 Cockpit dome'])==previous['parts']['06 Cockpit dome']
bpy.ops.ed.undo_push(message='Bulwark concept top silhouette correction')
result={'changed_parts':['01 Ram nose','02 Upper hull','03 Lower hull','04 Tail booms','05 Tail tips','07 Side insets','08 Nested engines'],'cockpit_unchanged':True,'topology_preserved':all(len(bpy.data.objects[n].data.vertices)==len(p['verts']) for n,p in snapshot.items()),'source_saved':False}
(out/'edit-audit.json').write_text(json.dumps(result,indent=2))

