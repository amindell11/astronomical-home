"""Verify finish 2 against the frozen round 1 geometry and lamp materials.

Run headless Blender after building. --capture freezes round 1 before rebuilding.
Writes finish2-baseline.json or finish2-audit.json; exits nonzero on a failed check.
"""
from pathlib import Path
from collections import Counter
import json

root = Path(__file__).resolve().parent
source = (root / 'verify_finish1.py').read_text(encoding='utf-8')
source = source.replace('finish1-baseline.json', 'finish2-baseline.json')
source = source.replace('finish1-audit.json', 'finish2-audit.json')
exec(compile(source, str(root / 'verify_finish1.py'), 'exec'))


def face_inventory(record):
    return Counter((tuple(sorted(tuple(round(c, 5) for c in record['vertices'][i]) for i in face)), material)
                   for face, material in zip(record['faces'], record['face_materials']))


original = face_inventory(baseline['objects'][body_name])
finished = face_inventory(current['objects'][body_name])
report['checks']['round1_body_faces_retained'] = not bool(original - finished)
old_audit = json.loads((OUT / 'finish1-audit.json').read_text(encoding='utf-8'))
old_lamps = old_audit['lamps']
report['checks']['lamp_anchors_and_colours_retained'] = all(
    all(a[key] == b[key] for key in ('object', 'position', 'color', 'outward', 'active_material'))
    for a, b in zip(old_lamps, report['lamps']))
current_materials = {m['name']: m for m in report['materials']}
report['checks']['round1_materials_retained'] = all(
    current_materials.get(m['name']) == m for m in old_audit['materials'])
(OUT / 'finish2-audit.json').write_text(json.dumps(report, indent=2), encoding='utf-8')
print(json.dumps(report['checks']))
assert all(report['checks'].values()), report['checks']
