from pathlib import Path
from PIL import Image
import json
pool=Path('D:/amind/git/agent-2')
ship=pool/'art/ships/nightshade'
config=ship/'ship.json'
if config.exists():
    raise FileExistsError(config)
config.write_text(json.dumps(dict(schema_version=1,name='Nightshade',source='Nightshade.blend',texture_size=2048,uv_density_max_ratio=2.0,contour_roles=['hull'],exemptions=[]),indent=2),encoding='utf-8')
out=pool/'results/nightshade-blockout/blockout/2026-10-05-r01'
refs=out/'references'
refs.mkdir(parents=True,exist_ok=True)
with Image.open(ship/'concepts/nightshade-top.png') as im:
    im.crop((225,90,1306,985)).save(refs/'top-reference.png')
with Image.open(ship/'concepts/nightshade-turnaround.png') as im:
    im.crop((90,100,1460,395)).transpose(Image.Transpose.FLIP_LEFT_RIGHT).save(refs/'side-reference.png')
print('Created ship.json and cropped reference images; side nose points right for Blender +Y.')
