import json,hashlib,shutil
from pathlib import Path
root=Path('D:/amind/git/agent-3')
out=root/'results/bulwark/uv/2026-10-13-r01'
before_path=root/'results/bulwark/hinged-flaps/payload/detail-model/2026-10-13-r08/ship_check.json'
before=json.loads(before_path.read_text())
after=json.loads((out/'check-after/ship_check.json').read_text())
assert before['source_sha256']=='0fe0ee9690d0f3e16791d6398fc8bcbe168641847f2c501881a9d10326eaf3a3'
assert before['fingerprint']['components']['geometry']==after['fingerprint']['components']['geometry']
assert before['fingerprint']['components']['materials']==after['fingerprint']['components']['materials']
assert before['fingerprint']['parts'].keys()==after['fingerprint']['parts'].keys()
changed=[n for n,p in before['fingerprint']['parts'].items() if p['geometry']!=after['fingerprint']['parts'][n]['geometry']]
assert not changed,changed
encode=lambda f:json.dumps(f,sort_keys=True)
old={encode(f) for f in before['findings']}
new={encode(f) for f in after['findings']}
assert old==new
sha=hashlib.sha256((root/'art/ships/bulwark/Bulwark.blend').read_bytes()).hexdigest()
for p in ('uv-audit.json','lock-after/ship_lock.json','uv-after/ship_uv.json','check-after/ship_check.json','uv-overlap.json','checker-report.json'):
 assert json.loads((out/p).read_text())['source_sha256']==sha,p
delta={'source_sha256':sha,'baseline_source_sha256':before['source_sha256'],'before':len(old),'after':len(new),'added':[],'resolved':[],
       'geometry_unchanged':True,'materials_unchanged':True,'changed_geometry_parts':changed,'part_count':len(after['fingerprint']['parts'])}
(out/'check-delta.json').write_text(json.dumps(delta,indent=2))
(out/'check-before').mkdir(exist_ok=True)
shutil.copyfile(before_path,out/'check-before/ship_check.json')
for file in ('Bulwark.blend','ship.json','lock.json'):
 shutil.copyfile(root/'art/ships/bulwark'/file,out/file)
for file in ('bulwark_uv.py','bulwark_uv_review.py','create_uv_guides.py','package_uv.py'):
 shutil.copyfile(Path(__file__).parent/file,out/file)
print(json.dumps(delta))
