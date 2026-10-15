import bpy
import json
import runpy
from pathlib import Path

stage = Path(__file__).parent
api = runpy.run_path(str(stage / 'organize.py'))
bpy.ops.wm.open_mainfile(filepath=str(api['SOURCE']))
names = json.loads((stage / 'name-map.json').read_text())
before = json.loads((stage / 'before-data.json').read_text())
objects = {old: bpy.data.objects[names.get(old, old)] for old in before['objects']}
after = api['capture'](objects)
after = json.loads(json.dumps(after))
changed = {old: [key for key in record if record[key] != after['objects'][old][key]]
           for old, record in before['objects'].items() if record != after['objects'][old]}
paint_changes = [name for name in before['images'] if before['images'][name]['pixels'] != after['images'].get(name, {}).get('pixels')]
packed_changes = [name for name in before['images'] if before['images'][name]['packed'] != after['images'].get(name, {}).get('packed')]
result = {'objects_checked': len(objects), 'changed': changed, 'paint_changes': paint_changes,
          'packed_changes': packed_changes,
          'passed': not changed and not paint_changes and packed_changes == ['Bulwark BaseRegion']}
(stage / 'saved-verification.json').write_text(json.dumps(result, indent=2))
assert result['passed'], result
print(json.dumps(result))
