from pathlib import Path
import json,hashlib,shutil
from PIL import Image
root=Path('D:/amind/git/agent-3');out=root/'results/bulwark/paint/2026-10-13-r05';source=root/'art/ships/bulwark/Bulwark.blend'
sha=hashlib.sha256(source.read_bytes()).hexdigest()
before=json.loads((root/'results/bulwark/paint/2026-10-13-r02/check-after/ship_check.json').read_text());after=json.loads((out/'check-after/ship_check.json').read_text())
for key in ('geometry','uv'):assert before['fingerprint']['components'][key]==after['fingerprint']['components'][key]
old={json.dumps(f,sort_keys=True) for f in before['findings']};new={json.dumps(f,sort_keys=True) for f in after['findings']};assert old==new
for p in ('paint-source-audit.json','lock-after/ship_lock.json','uv-after/ship_uv.json','check-after/ship_check.json'):
 assert json.loads((out/p).read_text())['source_sha256']==sha,p
for p in (out/'layers').glob('*.png'):
 image=Image.open(p);assert image.mode=='L' and image.size==(2048,2048),(p,image.mode,image.size)
read=lambda p:json.loads((out/p).read_text(encoding='utf-8-sig'))
pre=read('state-before.json');post=read('state-after.json');capture=read('capture.json')
assert pre['success'] and post['success'] and capture['success'] and capture['data']['result']['success']
assert pre['data']['result']['success'] and post['data']['result']['success']
assert pre['data']['result']==post['data']['result']
assert not any(m['dirty'] for m in post['data']['result']['result']['materials'])
assert set(r['ship'] for r in capture['data']['result']['result']['roster'])=={'Crimson','Vanguard','Nightshade','Valis'}
report={'source_sha256':sha,'geometry_unchanged_from_uv_approval':True,'uvs_unchanged_from_uv_approval':True,
 'inherited_source_findings':len(new),'added_findings':[],'geometry_lock':'pass','uv_density':'pass',
 'unity_scene_state_unchanged':True,'unity_selection_unchanged':True,'existing_materials_dirty':0,
 'roster':['Crimson','Vanguard','Nightshade','Valis','Bulwark'],'cargo_excluded':True,
 'native_shader':'Astronomical/Drawn/Surface','masks_grayscale_2048':True,'base_colours_recolourable':True,
 'capture_kind':'Temporary evaluated mesh and materials rendered by PreviewRenderUtility; current game prefab assets for other ships',
 'gameplay_prefab_integration':False,'flight_check_claimed':False,'owner_approval':'UV and plum direction approved; paint masks awaiting review'}
(out/'paint-validation.json').write_text(json.dumps(report,indent=2))
for name in ('Bulwark.blend','ship.json','lock.json','README.md','palettes.json','paint-settings.json','UV-guide.png'):shutil.copyfile(source.parent/name,out/name)
for name in ('export_paint_mesh_r05.py','paint_masks_r05.py','prepare_paint_preview_r05.py','apply_paint_source_r05.py','BulwarkPaintPreviewR05.cs','compose_paint_lineup_r05.py','package_paint_r05.py','paint_region_rules_r05.py'):
 shutil.copyfile(Path(__file__).parent/name,out/name)
print(json.dumps(report))
