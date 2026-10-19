import hashlib
import json
from pathlib import Path

import numpy as np
from PIL import Image

root=Path('D:/amind/git/agent-4')
folder=root/'art/ships/vanguard'
out=root/'results/vanguard-redo/clean-canopy-rim'
sha=lambda p:hashlib.sha256(p.read_bytes()).hexdigest()
edit=json.loads((out/'rim-cleanup.json').read_text())
report=json.loads((out/'paint/ship_paint.json').read_text())
selection=np.array(Image.open(out/'rim-selection.png'))>0
assert report['verdict']=='pass'
expected_edits={}
for mask in ['Shadow','Light']:
    path=folder/'layers'/f'{mask}.png'
    pixels=np.array(Image.open(path))
    before=np.array(Image.open(out/'before-layers'/f'{mask}.png'))
    baseline=np.array(Image.open(folder/'layers/baseline'/f'{mask}.png'))
    assert sha(path)==edit['after_masks'][f'layers/{mask}.png']
    assert np.count_nonzero(pixels[selection])==0
    assert np.array_equal(pixels[~selection],before[~selection])
    expected_edits[mask]=int(np.count_nonzero(pixels!=baseline))
    assert report['edited_texels'][mask]==expected_edits[mask]
for relative,digest in edit['before'].items():
    if relative not in edit['after_masks']:
        assert sha(folder/relative)==digest,relative
assert all(count==0 for name,count in report['edited_texels'].items() if name not in expected_edits)
result={'verdict':'pass','normal_rebake_preserves_edit':True,'rim_light_and_shadow_zero':True,
        'outside_rim_unchanged':True,'baseline_unchanged':True,'source_sha256':sha(folder/'Vanguard.blend'),
        'edited_texels':expected_edits,'changed_texels_this_pass':edit['changed_texels']}
(out/'verification.json').write_text(json.dumps(result,indent=2))
print(json.dumps(result,indent=2))
