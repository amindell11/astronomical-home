"""Embed palette inputs for file:// use. Run with Python 3 from any directory.

Reads neutral-materials.png, palette-regions.png and palette-regions.json here.
Writes palette-assets.js here; the source images remain unchanged.
Raises an error before writing when an input is missing or invalid.
"""
from pathlib import Path
import base64
import hashlib
import json

root=Path(__file__).resolve().parent
image=(root/'neutral-materials.png').read_bytes()
mask=(root/'palette-regions.png').read_bytes()
metadata=json.loads((root/'palette-regions.json').read_text(encoding='utf-8-sig'))
if not isinstance(metadata.get('regions'),list) or not metadata['regions']:
    raise ValueError('palette-regions.json must declare the material regions.')
payload={'image':'data:image/png;base64,'+base64.b64encode(image).decode('ascii'),
         'mask':'data:image/png;base64,'+base64.b64encode(mask).decode('ascii'),
         'metadata':metadata,'sourceSHA256':hashlib.sha256(image).hexdigest()}
(root/'palette-assets.js').write_text('window.CONSOLE_PALETTE_ASSETS='+json.dumps(payload,separators=(',',':'),ensure_ascii=False)+';\n',encoding='utf-8')
print(json.dumps({'output':str(root/'palette-assets.js'),'sourceSHA256':payload['sourceSHA256'],'regions':len(metadata['regions'])}))
