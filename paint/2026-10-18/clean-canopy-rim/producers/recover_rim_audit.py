import hashlib
import json
import subprocess
from pathlib import Path

import numpy as np
from PIL import Image

root=Path('D:/amind/git/agent-4')
folder=root/'art/ships/vanguard'
out=root/'results/vanguard-redo/clean-canopy-rim'
sha=lambda p:hashlib.sha256(p.read_bytes()).hexdigest()
allowed={'layers/Shadow.png','layers/Light.png'}
changed=set(subprocess.check_output(['git','diff','--name-only'],cwd=root,text=True).splitlines())
assert changed=={'art/ships/vanguard/'+p for p in allowed},changed
before={p.relative_to(folder).as_posix():sha(p) for p in folder.rglob('*') if p.is_file()}
for p in (out/'before-layers').rglob('*'):
    if p.is_file():
        relative='layers/'+p.relative_to(out/'before-layers').as_posix()
        original=subprocess.check_output(['git','cat-file','--filters','HEAD:art/ships/vanguard/'+relative],cwd=root)
        assert hashlib.sha256(original).hexdigest()==sha(p),relative
        before[relative]=sha(p)
        if relative not in allowed:
            assert sha(folder/relative)==sha(p),relative
selected=np.array(Image.open(out/'rim-selection.png'))>0
assert selected.any()
counts={}
for mask in ['Shadow','Light']:
    current=np.array(Image.open(folder/'layers'/f'{mask}.png'))
    original=np.array(Image.open(out/'before-layers'/f'{mask}.png'))
    assert np.count_nonzero(current[selected])==0
    assert np.array_equal(current[~selected],original[~selected])
    counts[mask]=int(np.count_nonzero(current!=original))
result={'verdict':'pass','selected_texels':int(selected.sum()),'changed_texels':counts,
        'outside_rim_unchanged':True,'baseline_unchanged':True,'source_sha256':sha(folder/'Vanguard.blend'),
        'before':before,'after_masks':{name:sha(folder/name) for name in sorted(allowed)},
        'audit_recovered_after_windows_path_separator_error':True}
(out/'rim-cleanup.json').write_text(json.dumps(result,indent=2))
print('RIM_CLEANUP_PASS',json.dumps({'selected':int(selected.sum()),'changed':counts}))
