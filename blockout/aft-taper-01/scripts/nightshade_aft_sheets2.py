from pathlib import Path
from PIL import Image,ImageDraw,ImageFont,ImageFilter
import json
repo=Path('D:/amind/git/agent-1');root=repo/'results/nightshade-aft-taper';out=root/'round-02'
font=ImageFont.truetype('C:/Windows/Fonts/arial.ttf',26)
small=ImageFont.truetype('C:/Windows/Fonts/arial.ttf',19)
bg=(28,33,44);panel=(188,191,195)

def card(name,src,box,title,subtitle):
    pic=Image.open(src).convert('RGB').crop(box)
    im=Image.new('RGB',(pic.width,pic.height+84),bg);im.paste(pic,(0,84));d=ImageDraw.Draw(im)
    d.text((24,13),title,font=font,fill='white');d.text((24,49),subtitle,font=small,fill=(181,188,207))
    im.save(out/name,quality=95)

im=Image.new('RGB',(1030,690),bg);d=ImageDraw.Draw(im)
for label,y in [('before',0),('after',345)]:
    d.text((24,y+14),'BEFORE — central fuselage' if label=='before' else 'AFTER — continuous taper with a gradual rise',font=font,fill='white')
    img=Image.open(out/'focus'/f'{label}.png').convert('RGBA').crop((0,0,1030,274))
    im.paste(Image.new('RGB',img.size,panel),(0,y+58));im.paste(img,(0,y+58),img)
im.save(out/'aft-before-after.jpg',quality=95)
comparison=Image.new('RGB',(1030,690),bg);draw=ImageDraw.Draw(comparison)
for path,y,label in [(root/'round-01/focus/after.png',0,'REJECTED — pinched transition'),
                     (out/'focus/after.png',345,'CURRENT — continuous, gradual taper')]:
    draw.text((24,y+14),label,font=font,fill='white')
    pic=Image.open(path).convert('RGBA').crop((0,0,1030,274))
    comparison.paste(Image.new('RGB',pic.size,panel),(0,y+58));comparison.paste(pic,(0,y+58),pic)
comparison.save(out/'rejected-vs-current.jpg',quality=95)
ref=Image.open(Path(__file__).with_name('current-packed-side.png')).convert('RGBA')
after=Image.open(out/'focus/after.png').convert('RGBA')
alpha=after.getchannel('A')
overlay=Image.new('RGBA',ref.size,(5,155,184,0));overlay.putalpha(alpha.point(lambda a:int(a*.22)))
ref=Image.alpha_composite(ref,overlay)
outer=alpha.filter(ImageFilter.MaxFilter(5));inner=alpha.filter(ImageFilter.MinFilter(5))
from PIL import ImageChops
edge=ImageChops.subtract(outer,inner)
line=Image.new('RGBA',ref.size,(0,150,183,0));line.putalpha(edge)
ref=Image.alpha_composite(ref,line)
canvas=Image.new('RGB',(1327,358),bg);canvas.paste(ref.convert('RGB'),(0,84));d=ImageDraw.Draw(canvas)
d.text((24,13),'CURRENT HULL OUTLINE OVER YOUR PACKED SIDE REFERENCE',font=font,fill='white')
d.text((24,49),'Same placement and scale as the Blender reference plane',font=small,fill=(181,188,207))
canvas.save(out/'aft-reference-overlay.jpg',quality=95)
card('review-side.jpg',out/'material/side_reference_angle.png',(35,510,1370,875),
     'NIGHTSHADE — CENTRAL AFT TAPER','Continuous taper • More volume through the middle • Gradual upward curve')
card('review-iso.jpg',out/'material/iso_front.png',(210,260,1270,1070),
     'NIGHTSHADE — CENTRAL FUSELAGE CORRECTION','Original topology and live Mirrors retained')
old=json.loads((root/'precheck/ship_check.json').read_text());new=json.loads((out/'check/ship_check.json').read_text())
a,b=old['fingerprint']['parts'],new['fingerprint']['parts']
changed=[n for n in a if a[n]!=b.get(n)]
assert set(changed)=={'Rebuilt pitched hull','Aft spine armor','Aft engine collar'}
assert set(a)==set(b)
assert old['findings']==new['findings']
(out/'fingerprint-delta.json').write_text(json.dumps({'changed_parts':changed,'parts_added':[],
    'parts_removed':[],'unclaimed_changes':[],'new_findings':[],
    'existing_findings':new['findings'],'owner_canopy_and_wings_unchanged':True},indent=2))
print('REVIEW_READY',changed,'one unchanged baseline scale finding')

