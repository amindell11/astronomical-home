"""Apply the requested local mask edit using the canonical authored-region selection."""
import hashlib
import json
import shutil
import sys
from pathlib import Path

import numpy as np

root=Path('D:/amind/git/agent-4')
sys.path.insert(0,str(root/'art/tools/ship'))
import ship_contract as contract
import ship_paint
import ship_regions
import ship_source

folder=root/'art/ships/vanguard'
out=root/'results/vanguard-redo/clean-canopy-rim'
out.mkdir(parents=True,exist_ok=True)
sha=lambda p:hashlib.sha256(p.read_bytes()).hexdigest()
source=folder/'Vanguard.blend'
assert sha(source)=='e4c3dbf9552873ddf0a461f361d59245da13edf76208f27b0f0e56b3920fa031'
before={p.relative_to(folder).as_posix():sha(p) for p in folder.rglob('*') if p.is_file()}
shutil.copytree(folder/'layers',out/'before-layers')
scene=ship_source.open_source(source)
evaluation=ship_source.Evaluation(scene)
paint=contract.load_settings(folder/'paint.json',contract.parse_paint)
palette=contract.load_settings(folder/'palettes.json',contract.parse_palettes)
authored=ship_regions.load(folder/'regions.json')
rim_marks=[m for m in authored['marks'] if m['region']=='HullAccent' and 'MVP Cockpit.001' in m['parts']]
assert len(rim_marks)==1
names=sorted(palette['palettes'][palette['default']])
parts={name:ship_paint.Part(evaluation,name,paint) for name in rim_marks[0]['parts']}
selected=ship_regions.rasterize(parts,authored['marks'],names,4096,12,contract.PAINT_UV)['Region_HullAccent']>0
selected &= ship_paint.read_gray(folder/'layers/Region_HullAccent.png')>0
assert selected.any()
ship_paint.write_png(out/'rim-selection.png',selected.astype(np.uint8)*255)
counts={}
for mask in ['Shadow','Light']:
    path=folder/'layers'/f'{mask}.png'
    pixels=ship_paint.read_gray(path)
    previous=pixels.copy()
    pixels[selected]=0
    counts[mask]=int(np.count_nonzero(pixels!=previous))
    assert np.array_equal(pixels[~selected],previous[~selected])
    ship_paint.write_png(path,pixels)
allowed={'layers/Shadow.png','layers/Light.png'}
for relative,digest in before.items():
    if relative not in allowed:
        assert sha(folder/relative)==digest,relative
result={'verdict':'pass','selected_texels':int(selected.sum()),'changed_texels':counts,
        'outside_rim_unchanged':True,'baseline_unchanged':True,'source_sha256':sha(source),
        'before':before,'after_masks':{name:sha(folder/name) for name in sorted(allowed)}}
(out/'rim-cleanup.json').write_text(json.dumps(result,indent=2))
print('RIM_CLEANUP_PASS',json.dumps({'selected':int(selected.sum()),'changed':counts}))
