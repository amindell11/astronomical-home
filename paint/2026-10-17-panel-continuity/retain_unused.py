import bpy,json,hashlib,numpy as np
from pathlib import Path
s=Path(__file__).parent;bpy.ops.wm.open_mainfile(filepath='D:/amind/git/agent-3/art/ships/bulwark/Bulwark.blend')
d=json.loads((s/'reload-images.json').read_text());missing=[k for k in d['expected'] if k not in d['actual']]
with bpy.data.libraries.load(str(s/'checkpoint.blend'),link=False) as (src,dst):
 assert all(k in src.images for k in missing);dst.images=list(missing)
for im in dst.images:
 a=np.empty(len(im.pixels),np.float32);im.pixels.foreach_get(a);assert hashlib.sha256(a.tobytes()).hexdigest()==d['expected'][im.name]['pixels'];im.use_fake_user=True
bpy.context.preferences.filepaths.save_version=0;bpy.ops.wm.save_as_mainfile(filepath=bpy.data.filepath,check_existing=False)
(s/'retained-unused-images.json').write_text(json.dumps(missing,indent=2))
