from pathlib import Path
from PIL import Image, ImageDraw, ImageFont
from datetime import datetime, timezone
import hashlib
import json
import shutil

SCRATCH = Path(__file__).parent
OUT = SCRATCH / 'paint-round-01'
ROOT = Path('D:/amind/git/agent-1')
GENERATED = Path('C:/Users/amind/.codex/generated_images/01a112c7-f840-7683-a496-b2b5e5eabf06/exec-be4e1bf4-7d7f-4b13-9960-e0a016885f98.png')
SOURCE = ROOT / 'art/ships/nightshade/Nightshade.blend'
sha = lambda path: hashlib.sha256(path.read_bytes()).hexdigest()
audit = json.loads((OUT / 'render-audit.json').read_text())
assert sha(SOURCE) == audit['source_sha256_after']
shutil.copy2(GENERATED, OUT / 'concept.png')

im = Image.open(GENERATED).convert('RGB')
# Review thumbnails change presentation only; generated paint pixels remain unretouched.
top_box = (681, 27, 1203, 600)
top = im.crop(top_box)
top.save(OUT / 'concept-top.png')
strip = Image.new('RGB', (1056, 438), (28, 33, 44))
draw = ImageDraw.Draw(strip)
font = ImageFont.truetype('C:/Windows/Fonts/arial.ttf', 21)
small_font = ImageFont.truetype('C:/Windows/Fonts/arial.ttf', 17)
draw.text((20, 14), 'NIGHTSHADE  /  PAINT CONCEPT 01  /  TOP-VIEW READABILITY', font=font, fill='white')
for i, height in enumerate((32, 48, 64, 96)):
    x = i * 264
    tiny = top.resize((round(top.width * height / top.height), height), Image.Resampling.LANCZOS)
    tiny.save(OUT / ('concept-top-' + str(height) + 'px.png'))
    draw.text((x + 25, 52), str(height) + ' px ship length', font=small_font, fill=(210, 216, 231))
    strip.paste(tiny, (x + (264 - tiny.width) // 2, 83 + (100 - height) // 2))
    enlarged = tiny.resize((round(tiny.width * 192 / height), 192), Image.Resampling.NEAREST)
    strip.paste(enlarged, (x + (264 - enlarged.width) // 2, 201))
draw.text((20, 411), 'Actual pixels above; enlarged below. Concept-only thumbnails, not an in-game capture.', font=small_font, fill=(188, 195, 212))
strip.save(OUT / 'game-scale.png')

references = [
    {'role': 'exact current-model edit target', 'path': 'flat-contact-sheet.png', 'sha256': sha(OUT / 'flat-contact-sheet.png')},
    {'role': 'palette and surface-style reference only', 'path': 'art/ships/nightshade/concepts/nightshade-turnaround.png', 'sha256': sha(ROOT / 'art/ships/nightshade/concepts/nightshade-turnaround.png')},
    {'role': 'palette and surface-style reference only', 'path': 'art/ships/nightshade/concepts/nightshade-top.png', 'sha256': sha(ROOT / 'art/ships/nightshade/concepts/nightshade-top.png')}]
provenance = {'provider': 'openai-built-in-imagegen', 'tool': 'image_gen.imagegen',
    'recorded_at_utc': datetime.now(timezone.utc).isoformat(), 'status': 'exploration',
    'approval': 'Pending owner paint-concept approval.', 'prompt': (OUT / 'prompt.txt').read_text(),
    'transparent_background': False, 'references': references,
    'output': {'path': 'concept-01.png', 'sha256': sha(GENERATED), 'dimensions': list(im.size)},
    'source_commit': audit['source_commit'], 'source_sha256': audit['source_sha256_after'],
    'prerequisites': audit['prerequisites'],
    'review_thumbnails': {'crop_xyxy': list(top_box), 'ship_lengths_px': [32, 48, 64, 96],
                         'reduction': 'Lanczos', 'enlargement': 'nearest', 'not_in_game': True},
    'source_geometry_or_materials_changed': False, 'final_masks_generated': False}
(OUT / 'concept.json').write_text(json.dumps(provenance, indent=2), encoding='utf-8')
print(json.dumps({'concept': str(OUT / 'concept.png'), 'scale': str(OUT / 'game-scale.png'), 'source_unchanged': True}))
