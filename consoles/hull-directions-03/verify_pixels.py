import hashlib
import json
from pathlib import Path
from PIL import Image, ImageDraw
import numpy as np

folder = Path(__file__).resolve().parent
source = folder / 'wide-display-base.png'
base = Image.open(source).convert('RGB')
mask = Image.new('L', base.size)
ImageDraw.Draw(mask).polygon([(153, 499), (450, 499), (488, 530), (510, 803), (501, 815), (160, 815), (151, 805)], fill=255)
outside = np.array(mask) == 0
original = np.array(base)
checks = []
for option in 'abcd':
    full = Image.open(folder / f'{option}-full-scene.png').convert('RGB')
    native = Image.open(folder / f'{option}-native.png').convert('RGB')
    closeup = Image.open(folder / f'{option}-closeup.png')
    changed = np.any(np.array(full) != original, axis=2)
    check = {
        'option': option.upper(),
        'fullSceneSize': list(full.size),
        'nativeSize': list(native.size),
        'closeupSize': list(closeup.size),
        'changedPixels': int(changed.sum()),
        'changedOutsideGlass': int((changed & outside).sum()),
        'nativeMatchesFullSceneCrop': bool(np.array_equal(np.array(native), np.array(full.crop((150, 497, 520, 816)))))
    }
    checks.append(check)
report = {
    'source': source.name,
    'sourceSha256': hashlib.sha256(source.read_bytes()).hexdigest(),
    'comparison': 'Native overlay against the widened base; no comparison to the earlier imagegen source.',
    'directions': checks,
    'status': 'passed' if all(c['changedOutsideGlass'] == 0 and c['nativeMatchesFullSceneCrop'] and c['fullSceneSize'] == [1672, 941] and c['nativeSize'] == [370, 319] and c['closeupSize'] == [1110, 957] for c in checks) else 'failed'
}
(folder / 'pixel-checks.json').write_text(json.dumps(report, indent=2) + '\n', encoding='utf-8')
print(json.dumps(report))
raise SystemExit(report['status'] != 'passed')
