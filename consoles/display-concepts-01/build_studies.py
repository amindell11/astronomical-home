"""Embed verified screen data for file:// review; source assets remain unchanged."""
from pathlib import Path
import json

root=Path(__file__).resolve().parent
data=json.loads((root/'assets/screen-data.json').read_text(encoding='utf-8-sig'))
(root/'screen-data.js').write_text('window.SCREEN_STUDY_DATA='+json.dumps(data,separators=(',',':'),ensure_ascii=False)+';\n',encoding='utf-8')
print(json.dumps({'ship':data['ship'],'keystone':data['keystone']['icon'],'stats':len(data['stats'])}))
