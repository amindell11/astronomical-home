from pathlib import Path
from PIL import Image, ImageDraw, ImageFont, ImageOps
import json

repo=Path('D:/amind/git/agent-1');root=repo/'results/nightshade-form-refinement';out=root/'round-02'
font=ImageFont.truetype('C:/Windows/Fonts/arial.ttf',26)
small=ImageFont.truetype('C:/Windows/Fonts/arial.ttf',19)
bg=(28,33,44)

def card(name,src,box,title,subtitle):
    pic=Image.open(src).convert('RGB').crop(box)
    im=Image.new('RGB',(pic.width,pic.height+84),bg);im.paste(pic,(0,84))
    d=ImageDraw.Draw(im);d.text((24,13),title,font=font,fill='white')
    d.text((24,49),subtitle,font=small,fill=(181,188,207))
    im.save(out/name,quality=95)

card('review-iso.jpg',out/'material/iso_front.png',(210,265,1260,1065),
     'NIGHTSHADE — BLOCKOUT REFINEMENT','Flared tips • Fuller tail roots • Defined armor • Framed canopy')
card('review-side.jpg',out/'material/side_reference_angle.png',(35,520,1370,865),
     'NIGHTSHADE — SIDE / 9° ABOVE LEVEL','Restrained wingtip dip • Chunkier tail with the low sweep retained')
card('review-clay.jpg',out/'clay/iso_front.png',(210,265,1260,1065),
     'NIGHTSHADE — CLAY REVIEW','Angular mechanical surfaces with deliberate silhouette curves')
card('review-top.jpg',out/'material/top.png',(200,155,1200,1235),
     'NIGHTSHADE — TOP','Slender fuselage footprint retained')

def fit(src,box,canvas):
    x,y,w,h=box;im=ImageOps.contain(src,(w,h))
    canvas.paste(im,(x+(w-im.width)//2,y+(h-im.height)//2))

ref=Image.open(repo/'art/ships/nightshade/concepts/nightshade-turnaround.png').convert('RGB')
im=Image.new('RGB',(1440,900),bg);d=ImageDraw.Draw(im)
d.text((22,15),'APPROVED NIGHTSHADE CONCEPT',font=font,fill='white')
d.text((742,15),'REFINED NIGHTSHADE BLOCKOUT',font=font,fill='white')
fit(ref.crop((78,100,1475,405)),(10,65,700,250),im)
fit(Image.open(out/'material/side_reference_angle.png').convert('RGB').crop((35,520,1370,865)),(730,65,700,250),im)
fit(ref.crop((810,455,1536,1020)),(10,330,700,550),im)
fit(Image.open(out/'material/iso_front.png').convert('RGB').crop((210,265,1260,1065)),(730,330,700,550),im)
im.save(out/'nightshade-concept-comparison.jpg',quality=95)

old=json.loads((root/'precheck/ship_check.json').read_text())['fingerprint']['parts']
new=json.loads((out/'check/ship_check.json').read_text())['fingerprint']['parts']
audit=json.loads((root/'round-01/form-edit-audit.json').read_text())
changed=[n for n in old if old[n]!=new.get(n)]
added=sorted(set(new)-set(old));removed=sorted(set(old)-set(new))
assert not set(changed)-set(audit['edits'])
assert added==sorted(audit['new_parts']) and not removed
(out/'fingerprint-delta.json').write_text(json.dumps({'changed_parts':changed,'parts_added':added,
    'parts_removed':removed,'unclaimed_changes':[],'original_topology_retained':True},indent=2))
print('REVIEW_READY',changed,added)
