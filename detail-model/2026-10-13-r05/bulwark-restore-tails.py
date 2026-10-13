import bpy,json,sys,hashlib
from pathlib import Path
sys.path.insert(0,'D:/amind/git/agent-3/art/tools/ship')
import ship_source
root=Path('D:/amind/git/agent-3/results/bulwark/tail-revert');root.mkdir(parents=True,exist_ok=True)
source=Path('D:/amind/git/agent-3/results/bulwark/interfaces/owner/Bulwark.blend')
assert hashlib.sha256(source.read_bytes()).hexdigest()=='e218ca1f4c0e13ce4ef7b3e22de0c24d45c4f89a519d725363ea40a41f7fd505'
assert bpy.context.mode=='OBJECT'
before=ship_source.fingerprint(bpy.context.scene)
expected=json.load(open('D:/amind/git/agent-3/results/bulwark/straight-tail/refined/check/ship_check.json'))['fingerprint']
assert before==expected,'Scene changed since inspection; do not discard edits'
base=json.load(open('D:/amind/git/agent-3/results/bulwark/interfaces/owner/check/ship_check.json'))['fingerprint']
names=['04 Tail booms','05 Tail tips']
old={n:bpy.data.objects[n].data for n in names}
with bpy.data.libraries.load(str(source),link=False) as (data_from,data_to):
 data_to.meshes=list(names)
for name,mesh in zip(names,data_to.meshes):
 mats=list(old[name].materials)
 mesh.materials.clear()
 for material in mats:mesh.materials.append(material)
 bpy.data.objects[name].data=mesh
bpy.context.view_layer.update()
after=ship_source.fingerprint(bpy.context.scene)
changed=[n for n in before['parts'] if before['parts'][n]!=after['parts'].get(n)]
assert set(changed)==set(names),changed
assert all(after['parts'][n]==base['parts'][n] for n in names),'Tail restoration differs from pre-interface checkpoint'
assert after['parts']['06 Cockpit dome']==before['parts']['06 Cockpit dome']
assert after['parts']['06 Cockpit surround']==before['parts']['06 Cockpit surround']
assert after['parts']['07 Side insets']==before['parts']['07 Side insets']
for name in names:
 if old[name].users==0:bpy.data.meshes.remove(old[name])
 bpy.data.objects[name].data.name=name
for screen in bpy.data.screens:
 for area in screen.areas:
  if area.type=='VIEW_3D':area.spaces.active.overlay.show_overlays=True
bpy.context.scene['review_stage']='Original tails restored; circular insets and cockpit seating retained'
target=Path('C:/Users/amind/.codex/visualizations/2026/10/07/01a11457-037a-7c22-8788-f0f5a5d26229/ship-concepts/bulwark/detail-model/2026-10-13-r05/Bulwark.blend');target.parent.mkdir(parents=True,exist_ok=True)
assert not target.exists()
bpy.ops.wm.save_as_mainfile(filepath=str(target),check_existing=False)
ship=json.load(open('D:/amind/git/agent-3/results/bulwark/straight-tail/refined/ship.json'));ship['source']='Bulwark.blend'
(target.parent/'ship.json').write_text(json.dumps(ship,indent=2))
audit={'source_checkpoint':'9d17e34f53414637ac43c3f96d69cf3fac5d9458','restored_parts':names,'restored_parts_match_checkpoint':True,'other_parts_unchanged':True,'retained_fixes':['07 Side insets','06 Cockpit dome','06 Cockpit surround'],'output_sha256':hashlib.sha256(target.read_bytes()).hexdigest(),'fingerprint':after}
(root/'restore-audit.json').write_text(json.dumps(audit,indent=2))
result={'saved':str(target),'restored':names,'other_parts_unchanged':True,'sha256':audit['output_sha256']}
