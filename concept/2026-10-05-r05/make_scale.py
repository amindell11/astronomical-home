from PIL import Image, ImageDraw
from pathlib import Path
root = Path('D:/amind/git/agent-2/results/nightshade-concept/concept/2026-10-05-r05')
sheet = Image.new('RGB', (240, 390), '#282d3b')
draw = ImageDraw.Draw(sheet)
for col, label in enumerate('j'):
    source = Image.open(root / f'nightshade-{label}-top.jpg').convert('RGB')
    draw.text((col*240+20, 15), f'{label.upper()} - round 05', fill='white')
    for size, y in [(100, 48), (150, 192)]:
        preview = source.copy()
        preview.thumbnail((size,size), Image.Resampling.LANCZOS)
        sheet.paste(preview, (col*240+(240-preview.width)//2,y))
        draw.text((col*240+20,y+size+8), f'{size}px image', fill='white')
sheet.save(root/'game-scale.png')



