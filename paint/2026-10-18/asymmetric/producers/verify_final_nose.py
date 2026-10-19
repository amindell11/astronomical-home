import hashlib
import json
from pathlib import Path

root=Path('D:/amind/git/agent-4')
out=root/'results/vanguard-redo/asymmetric'
sha=lambda path:hashlib.sha256(path.read_bytes()).hexdigest()
expected=json.loads((out/'nose-correction.json').read_text())
report=json.loads((out/'final-paint/ship_paint.json').read_text())
assert report['verdict']=='pass'
assert report['edited_texels']['Shadow']==expected['changed_texels']==2725
assert all(value==0 for name,value in report['edited_texels'].items() if name!='Shadow')
assert sha(root/'art/ships/vanguard/layers/Shadow.png')==expected['corrected_shadow_sha256']
assert sha(root/'art/ships/vanguard/layers/baseline/Shadow.png')==expected['baseline_shadow_sha256']
assert sha(root/'art/ships/vanguard/Vanguard.blend')==expected['source_sha256']
assert sha(out/'final-paint/preview/Composite.png')==sha(out/'nose-cleanup/paint/preview/Composite.png')
for path in (out/'final-paint/unity/Vanguard/Paint').glob('*.png'):
    assert sha(path)==sha(out/'nose-cleanup/paint/unity/Vanguard/Paint'/path.name),path.name
result={**expected,'normal_rebake':'pass','corrected_mask_reproduced_byte_for_byte':True,
        'baseline_unchanged':True,'preview_matches_final_bake':True}
(out/'final-verification.json').write_text(json.dumps(result,indent=2))
print(json.dumps(result,indent=2))
