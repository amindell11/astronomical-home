from pathlib import Path
from PIL import Image, ImageDraw, ImageFont
import json
import shutil

out = Path(__file__).parent
font = ImageFont.truetype('C:/Windows/Fonts/segoeui.ttf', 26)
names = ('Crimson', 'Nightshade', 'Valis', 'Vanguard')
labels = out / 'motion-labelled'
labels.mkdir(exist_ok=True)
for i, path in enumerate(sorted((out / 'motion').glob('*.png'))):
    frame = Image.open(path).convert('RGB')
    sheet = Image.new('RGB', (2048, 696), '#181b20')
    sheet.paste(frame, (0, 56))
    draw = ImageDraw.Draw(sheet)
    for column, name in enumerate(names):
        draw.text((column * 512 + 20, 10), name, font=font, fill='white')
    sheet.save(labels / f'f_{i:05d}.png')
(labels / 'manifest.json').write_text(json.dumps({'suggestedFps': 20, 'width': 2048, 'height': 696, 'frameCount': 120}))
for suffix in ('', '-bank'):
    before = Image.open(out / f'before{suffix}.png').convert('RGB')
    after = Image.open(out / f'after{suffix}.png').convert('RGB')
    sheet = Image.new('RGB', (2400, 1648), '#181b20')
    draw = ImageDraw.Draw(sheet)
    for row, (title, frame) in enumerate((('Original static paint', before), ('Surface-aware shared canopy motion', after))):
        y = row * 824
        draw.text((24, y + 10), title, font=font, fill='white')
        for column, name in enumerate(names):
            draw.text((column * 600 + 400, y + 10), name, font=font, fill='#bac5d0')
        sheet.paste(frame, (0, y + 56))
    sheet.save(out / f'comparison{suffix}.png')
publish = out / 'publish'
for name in ('comparison.png', 'comparison-bank.png', 'CanopyCapture.cs', 'prepare_evidence.py'):
    shutil.copy2(out / name, publish / name)
shutil.copy2(out / 'after-underside.png', publish / 'underside.png')
comparison = out / 'curvature-motion'
comparison.mkdir(exist_ok=True)
for i, path in enumerate(sorted((out / 'motion').glob('*.png'))):
    sheet = Image.new('RGB', (2048, 1472), '#181b20')
    draw = ImageDraw.Draw(sheet)
    for row, (title, folder) in enumerate((('Previous planar mapping', out / 'planar-374b0ef'), ('Reflection follows the glass surface', out))):
        y = row * 736
        draw.text((20, y + 10), title, font=font, fill='white')
        for col, name in enumerate(names):
            draw.text((col * 512 + 20, y + 50), name, font=font, fill='#bac5d0')
        sheet.paste(Image.open(folder / 'motion' / path.name), (0, y + 96))
    sheet.save(comparison / f'f_{i:05d}.png')
(comparison / 'manifest.json').write_text(json.dumps({'suggestedFps': 20, 'width': 2048, 'height': 1472, 'frameCount': 120}))
for frame in (15, 30, 75, 105):
    shutil.copy2(out / f'curvature-{frame:03}.png', publish / f'curvature-{frame:03}.png')
