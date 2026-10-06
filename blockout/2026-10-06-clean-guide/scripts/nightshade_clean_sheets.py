from pathlib import Path
from PIL import Image,ImageDraw,ImageFont
import json
root=Path('D:/amind/git/agent-1/results/nightshade-clean-guide')
out=root/'round-02'
font=ImageFont.truetype('C:/Windows/Fonts/arial.ttf',26)
small=ImageFont.truetype('C:/Windows/Fonts/arial.ttf',19)
def card(filename,source,box,title,subtitle):
    picture=Image.open(source).convert('RGB').crop(box)
    im=Image.new('RGB',(picture.width,picture.height+84),(28,33,44));im.paste(picture,(0,84))
    d=ImageDraw.Draw(im);d.text((24,13),title,font=font,fill='white');d.text((24,49),subtitle,font=small,fill=(181,188,207))
    im.save(out/filename,quality=95)
card('review-side.jpg',out/'material/side_reference_angle.png',(25,470,1375,930),
     'NIGHTSHADE — SIDE / 9° ABOVE LEVEL','Clean rebuild from native guide measurements • New forks • Provisional review colors')
card('review-iso.jpg',out/'material/iso_front.png',(155,240,1260,1110),
     'NIGHTSHADE — CLEAN MEDIUM-DETAIL CANDIDATE','New topology throughout • 21 editable parts • Live X symmetry')
card('review-clay.jpg',out/'clay/iso_front.png',(155,240,1260,1110),
     'NIGHTSHADE — CLAY SHAPE REVIEW','Original mesh used as a dimensional guide only')
im=Image.new('RGB',(1400,795),(28,33,44));d=ImageDraw.Draw(im)
for i,(name,label) in enumerate([('top','TOP'),('bottom','BOTTOM')]):
    picture=Image.open(out/'material'/f'{name}.png').convert('RGB');picture.thumbnail((700,700))
    im.paste(picture,(i*700,95));d.text((24+i*700,30),label,font=font,fill='white')
im.save(out/'review-top-bottom.jpg',quality=94)
scale=Image.open(out/'scale/scale.png').convert('RGB')
im=Image.new('RGB',(scale.width,scale.height+84),(28,33,44));im.paste(scale,(0,84));d=ImageDraw.Draw(im)
d.text((18,12),'GAME SCALE — 32 / 48 / 64 / 96 PX',font=font,fill='white')
d.text((18,47),'Nearest-neighbor enlargement from the sanctioned ship_render output',font=small,fill=(181,188,207))
im.save(out/'review-game-scale.jpg',quality=96)
old=json.loads((root/'round-01/check/ship_check.json').read_text())['fingerprint']['parts']
new=json.loads((out/'check/ship_check.json').read_text())['fingerprint']['parts']
report={'existing_parts_changed':[name for name in old if name in new and old[name]!=new[name]],
        'parts_added':sorted(set(new)-set(old)), 'parts_removed':sorted(set(old)-set(new))}
assert not report['existing_parts_changed'] and not report['parts_removed'],report
(out/'detail-fingerprint-delta.json').write_text(json.dumps(report,indent=2))
print(json.dumps(report))
