from pathlib import Path
from PIL import Image,ImageDraw,ImageFont,ImageOps
import json
repo=Path('D:/amind/git/agent-1');root=repo/'results/nightshade-hard-surface';out=root/'round-01'
font=ImageFont.truetype('C:/Windows/Fonts/arial.ttf',26);small=ImageFont.truetype('C:/Windows/Fonts/arial.ttf',19)
bg=(28,33,44)
def card(name,source,box,title,subtitle):
    picture=Image.open(source).convert('RGB').crop(box)
    im=Image.new('RGB',(picture.width,picture.height+84),bg);im.paste(picture,(0,84));d=ImageDraw.Draw(im)
    d.text((24,13),title,font=font,fill='white');d.text((24,49),subtitle,font=small,fill=(181,188,207))
    im.save(out/name,quality=95)
card('review-iso.jpg',out/'material/iso_front.png',(155,240,1260,1110),
     'NIGHTSHADE — HARD-SURFACE REVISION','Planar armor • Angular hull sides • Curved silhouette retained')
card('review-side.jpg',out/'material/side_reference_angle.png',(25,470,1375,930),
     'NIGHTSHADE — SIDE / 9° ABOVE LEVEL','Existing parts edited in place • Live Mirrors • Same overall profile')
card('review-clay.jpg',out/'clay/iso_front.png',(155,240,1260,1110),
     'NIGHTSHADE — CLAY SURFACE REVIEW','Broad flat faces and narrow geometric chamfers')
def fit(im,box,canvas):
    x,y,w,h=box;im=ImageOps.contain(im,(w,h));canvas.paste(im,(x+(w-im.width)//2,y+(h-im.height)//2))
im=Image.new('RGB',(1440,705),bg);d=ImageDraw.Draw(im)
d.text((22,15),'BEFORE — rounded surface treatment',font=font,fill='white')
d.text((742,15),'NOW — planar armor construction',font=font,fill='white')
old=Image.open(repo/'results/nightshade-clean-guide/round-02/clay/iso_front.png').convert('RGB').crop((155,240,1260,1110))
new=Image.open(out/'clay/iso_front.png').convert('RGB').crop((155,240,1260,1110))
fit(old,(0,65,720,630),im);fit(new,(720,65,720,630),im)
im.save(out/'surface-before-after.jpg',quality=95)
reference=Image.open(repo/'art/ships/nightshade/concepts/nightshade-turnaround.png').convert('RGB')
im=Image.new('RGB',(1440,910),bg);d=ImageDraw.Draw(im)
d.text((22,15),'APPROVED NIGHTSHADE CONCEPT',font=font,fill='white')
d.text((742,15),'NIGHTSHADE SURFACE REVISION',font=font,fill='white')
fit(reference.crop((78,100,1475,405)),(10,70,700,250),im)
fit(Image.open(out/'material/side_reference_angle.png').convert('RGB').crop((25,500,1375,890)),(730,70,700,250),im)
fit(reference.crop((810,455,1536,1020)),(10,340,700,550),im)
fit(Image.open(out/'material/iso_front.png').convert('RGB').crop((155,240,1260,1110)),(730,340,700,550),im)
im.save(out/'nightshade-concept-comparison.jpg',quality=95)
old=json.loads((root/'precheck/ship_check.json').read_text())['fingerprint']['parts']
new=json.loads((out/'check/ship_check.json').read_text())['fingerprint']['parts']
claimed=json.loads((out/'surface-edit-audit.json').read_text())['edits']
changed=[name for name in old if old[name]!=new.get(name)]
assert not set(changed)-set(claimed)
assert set(old)==set(new)
(out/'fingerprint-delta.json').write_text(json.dumps({'changed_parts':changed,'parts_added':[],'parts_removed':[],
    'unclaimed_changes':[],'tail_geometry_unchanged':old['Sculpted compact tail pair']==new['Sculpted compact tail pair'],
    'canopy_geometry_unchanged':all(old[n]==new[n] for n in old if 'canopy' in n)},indent=2))
print('REVIEW_READY',changed)
