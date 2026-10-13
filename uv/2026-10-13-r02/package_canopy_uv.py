import json,hashlib,shutil
from pathlib import Path
root=Path('D:/amind/git/agent-3');out=root/'results/bulwark/uv/2026-10-13-r02'
before=json.loads((root/'results/bulwark/uv/2026-10-13-r01/check-after/ship_check.json').read_text())
after=json.loads((out/'check-after/ship_check.json').read_text())
assert before['source_sha256']=='44ddcb64c81d22cdea4c875c9c66c56b50281d53929b3a5573f4f3d6c9cdf9a7'
assert before['fingerprint']['components']['geometry']==after['fingerprint']['components']['geometry']
assert before['fingerprint']['components']['materials']==after['fingerprint']['components']['materials']
assert before['fingerprint']['parts'].keys()==after['fingerprint']['parts'].keys()
changed_uv=[n for n,p in before['fingerprint']['parts'].items() if p['uv']!=after['fingerprint']['parts'][n]['uv']]
assert changed_uv==['06 Cockpit dome'],changed_uv
assert all(p['geometry']==after['fingerprint']['parts'][n]['geometry'] for n,p in before['fingerprint']['parts'].items())
old={json.dumps(f,sort_keys=True) for f in before['findings']};new={json.dumps(f,sort_keys=True) for f in after['findings']};assert old==new
sha=hashlib.sha256((root/'art/ships/bulwark/Bulwark.blend').read_bytes()).hexdigest()
for p in ('canopy-audit.json','canopy-seam-check.json','lock-after/ship_lock.json','uv-after/ship_uv.json','check-after/ship_check.json','uv-overlap.json','checker-report.json'):
 assert json.loads((out/p).read_text())['source_sha256']==sha,p
delta={'source_sha256':sha,'baseline_source_sha256':before['source_sha256'],'before':len(old),'after':len(new),'added':[],'resolved':[],
 'geometry_unchanged':True,'materials_unchanged':True,'changed_geometry_parts':[],'changed_uv_parts':changed_uv,'other_uvs_unchanged':True,'part_count':len(after['fingerprint']['parts'])}
(out/'check-delta.json').write_text(json.dumps(delta,indent=2))
for file in ('Bulwark.blend','ship.json','lock.json'):shutil.copyfile(root/'art/ships/bulwark'/file,out/file)
for file in ('canopy_seam.py','canopy_seam_validate.py','pack_canopy.py','bulwark_uv_review_r02.py','create_uv_guides_r02.py','package_canopy_uv.py'):
 shutil.copyfile(Path(__file__).parent/file,out/file)
print(json.dumps(delta))
