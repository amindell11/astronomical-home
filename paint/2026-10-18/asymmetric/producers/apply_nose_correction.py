import hashlib
import json
import shutil
from pathlib import Path

import numpy as np
from PIL import Image

root=Path('D:/amind/git/agent-4')
layers=root/'art/ships/vanguard/layers'
out=root/'results/vanguard-redo/asymmetric'
candidate=out/'nose-cleanup/layers'
sha=lambda path:hashlib.sha256(path.read_bytes()).hexdigest()
unchanged=[]
for path in layers.glob('*.png'):
    if path.name=='Shadow.png':
        continue
    assert sha(path)==sha(candidate/path.name),path.name
    unchanged.append(path.name)
previous=np.array(Image.open(layers/'Shadow.png'))
corrected=np.array(Image.open(candidate/'Shadow.png'))
assert previous.shape==corrected.shape==(4096,4096)
assert np.any(previous!=corrected)
assert np.all(corrected<=previous)
baseline_sha=sha(layers/'baseline/Shadow.png')
report={'verdict':'pass','changed_mask':'Shadow', 'changed_texels':int(np.count_nonzero(previous!=corrected)),
        'unchanged_masks':unchanged,'baseline_shadow_sha256':baseline_sha,
        'source_sha256':sha(root/'art/ships/vanguard/Vanguard.blend'),
        'before_shadow_sha256':sha(layers/'Shadow.png'), 'corrected_shadow_sha256':sha(candidate/'Shadow.png'),
        'correction':'Remove only the local nose-cap shadow sweep; preserve brush, facing shading and light.'}
shutil.copy2(candidate/'Shadow.png',layers/'Shadow.png')
assert sha(layers/'baseline/Shadow.png')==baseline_sha
(out/'nose-correction.json').write_text(json.dumps(report,indent=2))
print(json.dumps(report,indent=2))
