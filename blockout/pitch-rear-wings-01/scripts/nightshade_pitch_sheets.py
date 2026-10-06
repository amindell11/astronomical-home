from pathlib import Path
from PIL import Image,ImageDraw,ImageFont,ImageOps
import json
repo=Path('D:/amind/git/agent-1');root=repo/'results/nightshade-pitch-rear-wings';out=root/'round-01'
font=ImageFont.truetype('C:/Windows/Fonts/arial.ttf',26)
small=ImageFont.truetype('C:/Windows/Fonts/arial.ttf',19)
bg=(28,33,44)

def card(name,source,box,title,subtitle):
    pic=Image.open(source).convert('RGB')
    if box:pic=pic.crop(box)
    im=Image.new('RGB',(pic.width,pic.height+84),bg);im.paste(pic,(0,84))
    d=ImageDraw.Draw(im);d.text((24,13),title,font=font,fill='white')
    d.text((24,49),subtitle,font=small,fill=(181,188,207));im.save(out/name,quality=95)

card('review-iso.jpg',out/'material/iso_front.png',(210,260,1270,1070),
     'NIGHTSHADE — REAR WING AND PITCH REVISION','Broad angular rear wings • Stronger swept-fin roots • Forward body pitch')
card('review-side.jpg',out/'material/side_reference_angle.png',(35,510,1370,875),
     'NIGHTSHADE — SIDE / 9° ABOVE LEVEL','Sharper nose taper • Five-degree upper-body pitch • Low rear-wing sweep')
card('review-clay.jpg',out/'clay/iso_front.png',(210,260,1270,1070),
     'NIGHTSHADE — CLAY REVIEW','Editable armor surfaces and new mirrored rear wing pair')
card('review-top.jpg',out/'material/top.png',None,
     'NIGHTSHADE — TOP','Owner-edited outer wings retained')

def fit(src,box,canvas):
    x,y,w,h=box;im=ImageOps.contain(src,(w,h));canvas.paste(im,(x+(w-im.width)//2,y+(h-im.height)//2))

ref=Image.open(repo/'art/ships/nightshade/concepts/nightshade-turnaround.png').convert('RGB')
im=Image.new('RGB',(1440,900),bg);d=ImageDraw.Draw(im)
d.text((22,15),'APPROVED NIGHTSHADE CONCEPT',font=font,fill='white')
d.text((742,15),'PITCH AND REAR-WING REVISION',font=font,fill='white')
fit(ref.crop((78,100,1475,405)),(10,65,700,250),im)
fit(Image.open(out/'material/side_reference_angle.png').convert('RGB').crop((35,510,1370,875)),(730,65,700,250),im)
fit(ref.crop((810,455,1536,1020)),(10,330,700,550),im)
fit(Image.open(out/'material/iso_front.png').convert('RGB').crop((210,260,1270,1070)),(730,330,700,550),im)
im.save(out/'nightshade-concept-comparison.jpg',quality=95)

old=json.loads((root/'precheck/ship_check.json').read_text())['fingerprint']['parts']
new=json.loads((out/'check/ship_check.json').read_text())['fingerprint']['parts']
audit=json.loads((out/'edit-audit.json').read_text())
changed=[n for n in old if old[n]!=new.get(n)]
added=sorted(set(new)-set(old));removed=sorted(set(old)-set(new))
assert not set(changed)-set(audit['claimed_edits'])
assert added==['Rear swept wing pair'] and not removed
assert old['Rebuilt main wing blades']==new['Rebuilt main wing blades']
assert 'Sculpted compact tail pair' not in new
(out/'fingerprint-delta.json').write_text(json.dumps({'changed_parts':changed,'parts_added':added,
    'parts_removed':removed,'unclaimed_changes':[],'owner_main_wing_edits_retained':True,
    'owner_deleted_tail_remains_absent':True,'original_part_topology_retained':True},indent=2))
print('REVIEW_READY',len(changed),'changed parts;',added)
