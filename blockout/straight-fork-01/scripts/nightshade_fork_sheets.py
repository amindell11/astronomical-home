from pathlib import Path
from PIL import Image,ImageDraw,ImageFont
import json, numpy as np

repo=Path('D:/amind/git/agent-1');base=repo/'results/nightshade-straight-fork';out=base/'round-01'
font=ImageFont.truetype('C:/Windows/Fonts/arial.ttf',26)
small=ImageFont.truetype('C:/Windows/Fonts/arial.ttf',19)
bg=(28,33,44)
def card(name,src,box,title,subtitle):
    pic=Image.open(src).convert('RGB').crop(box)
    im=Image.new('RGB',(pic.width,pic.height+84),bg);im.paste(pic,(0,84));d=ImageDraw.Draw(im)
    d.text((24,13),title,font=font,fill='white');d.text((24,49),subtitle,font=small,fill=(181,188,207))
    im.save(out/name,quality=95)
card('review-side.jpg',out/'material/side_reference_angle.png',(35,500,1370,875),
     'NIGHTSHADE — RESTORED TAPER + STRAIGHT FORK','Gradual fuselage taper • Original fork dimensions and restrained rise')
card('review-iso.jpg',out/'material/iso_front.png',(205,265,1285,1080),
     'NIGHTSHADE — STRAIGHT FORK CANDIDATE','Existing editable cage fitted to the original mesh • Live Mirror retained')
card('review-top.jpg',out/'material/top.png',(35,35,1365,1370),
     'NIGHTSHADE — TOP VIEW','Straight parallel fork ends • Original native proportions')

guide=json.loads((base/'fork-guide.json').read_text());ref=np.array(guide['sections']);rows=np.array(guide['rows'])
fit=np.array([[r[0,1],r[:,0].min(),r[:,0].max(),r[:,2].min(),r[:,2].max()] for r in rows])
im=Image.new('RGB',(1100,1190),bg);d=ImageDraw.Draw(im)
d.text((24,16),'FORK OUTLINES AT THE SAME NATIVE SCALE',font=font,fill='white')
d.text((24,55),'White: original mesh   •   Cyan: fitted cage   •   No stretching',font=small,fill=(181,188,207))
def top_point(x,y):return (int(400+x*680),int(130+(.16-y)*680))
def side_point(y,z):return (int(160+(.16-y)*680),int(1120-z*680))
for data,color,width in [(ref,'white',5),(fit,(30,202,225),2)]:
    for sign in (-1,1):
        points=[top_point(sign*r[1],r[0]) for r in data]+[top_point(sign*r[2],r[0]) for r in data[::-1]]
        d.line(points+[points[0]],fill=color,width=width)
    points=[side_point(r[0],r[3]) for r in data]+[side_point(r[0],r[4]) for r in data[::-1]]
    d.line(points+[points[0]],fill=color,width=width)
d.text((370,925),'TOP',font=small,fill='white');d.text((510,1150),'SIDE',font=small,fill='white')
im.save(out/'fork-reference-overlay.jpg',quality=95)
old=json.loads((base/'precheck/ship_check.json').read_text());new=json.loads((out/'check/ship_check.json').read_text())
a,b=old['fingerprint']['parts'],new['fingerprint']['parts']
changed=[n for n in a if a[n]!=b.get(n)]
assert set(a)==set(b)
assert set(changed)=={'Rebuilt pitched hull','Aft spine armor','Aft engine collar','Rear swept wing pair'}
assert not new['findings']
(out/'fingerprint-delta.json').write_text(json.dumps({'changed_parts':changed,'parts_added':[],
    'parts_removed':[],'unclaimed_changes':[],'findings':[],'all_other_owner_parts_unchanged':True},indent=2))
print('REVIEW_READY',changed)
