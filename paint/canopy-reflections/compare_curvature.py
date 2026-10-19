from pathlib import Path
from PIL import Image, ImageDraw, ImageFont

out = Path(__file__).parent
font = ImageFont.truetype('C:/Windows/Fonts/segoeui.ttf', 22)
names = ('Crimson', 'Nightshade', 'Valis', 'Vanguard')
for frame in (0, 15, 30, 45, 75, 105):
    sheet = Image.new('RGB', (2048, 1472), '#181b20')
    draw = ImageDraw.Draw(sheet)
    for row, (title, folder) in enumerate((('Previous planar motion', out / 'planar-374b0ef'), ('Surface-aware reflection motion', out))):
        image = Image.open(folder / 'motion' / f'{frame:03}.png')
        y = row * 736
        draw.text((20, y + 10), title, font=font, fill='white')
        for col, name in enumerate(names):
            draw.text((col * 512 + 20, y + 50), name, font=font, fill='#bac5d0')
        sheet.paste(image, (0, y + 96))
    sheet.resize((1536, 1104)).save(out / f'curvature-{frame:03}.png')
