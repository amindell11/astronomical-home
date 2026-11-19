"""Assemble camera comparisons, aspect measurements and an exporter-parts sheet from local renders."""
from pathlib import Path
import json
import numpy as np
from PIL import Image, ImageDraw, ImageFont
from scipy.optimize import minimize
from scipy.ndimage import binary_erosion, distance_transform_edt

ROOT = Path(__file__).resolve().parent
OUT = ROOT / 'previews'
FONT = 'C:/Windows/Fonts/segoeui.ttf'
font = ImageFont.truetype(FONT, 27)
small = ImageFont.truetype(FONT, 21)
massing = Image.open(ROOT/'massing/D13-camera.png').convert('RGB')
deck = Image.open(OUT/'deck-camera.png').convert('RGB')
comparison = Image.new('RGB',(3840,1140),'#121c2a')
comparison.paste(massing,(0,60))
comparison.paste(deck,(1920,60))
draw = ImageDraw.Draw(comparison)
draw.text((24,12),'D13 approved massing',font=font,fill='white')
draw.text((1944,12),'Deck: finish 1, surface detail and lamps',font=font,fill='white')
comparison.save(OUT/'deck-vs-massing.png')
Image.blend(massing,deck,.5).save(OUT/'deck-camera-overlay.png')

audit = json.loads((OUT/'mesh-audit.json').read_text())
colors = json.loads((OUT/'part-colors.json').read_text())
raw = Image.open(OUT/'deck-parts-raw.png').convert('RGBA')
sheet = Image.new('RGB',(2500,1140),'#101a29')
sheet.paste(raw,(0,60),raw)
draw = ImageDraw.Draw(sheet)
draw.text((24,12),'Exporter parts. The eight switches belong to ConsoleBody.',font=font,fill='white')
draw.text((1940,16),'Part name and index',font=font,fill='white')
role_index = {role:i+1 for i,role in enumerate(colors)}
for role,color in colors.items():
    number = role_index[role]
    y = 67+(number-1)*48
    draw.rounded_rectangle((1940,y,1972,y+30),radius=4,fill='#'+color)
    draw.text((1984,y-2),f'{number:02d}  {role}',font=small,fill='white')
locations = {
    'ConsoleBody':(950,170),'MainScreenGlass':(235,380),'DetailScreenGlass':(270,480),
    'PrimaryScreenGlass':(1665,365),'OrdnanceScreenGlass':(1650,475),
    'SelectorKnob':(1240,655),'PreviousButton':(540,680),'PreviousLegend':(590,745),
    'NextButton':(675,680),'NextLegend':(710,745),'LaunchButton':(850,760),
    'LaunchLegend':(1010,760),'ProjectorLens':(1005,630),
    'PrimaryPreviousButton':(1320,250),'PrimaryPreviousLegend':(1370,320),
    'PrimaryNextButton':(1270,370),'PrimaryNextLegend':(1320,440),
    'OrdnancePreviousButton':(1260,490),'OrdnancePreviousLegend':(1310,560),
    'OrdnanceNextButton':(1395,615),'OrdnanceNextLegend':(1485,650)}
for role,loc in locations.items():
    entries = [p for p in audit['parts'] if p['role'] == role]
    if role == 'ConsoleBody':
        target = (960,90)
    else:
        target = entries[0]['pixel']
    x,y = loc
    tx,ty = target[0],target[1]+60
    color = '#'+colors[role]
    draw.line([(x,y),(tx,ty)],fill=color,width=2)
    draw.ellipse((tx-3,ty-3,tx+3,ty+3),fill=color)
    draw.rounded_rectangle((x-20,y-17,x+21,y+18),radius=5,fill='#101a29',outline=color,width=2)
    draw.text((x-14,y-15),f'{role_index[role]:02d}',font=small,fill='white')
sheet.save(OUT/'deck-parts.png')

rectangles = []
for screen in audit['screens']:
    p = np.array(screen['vertices'])[:,:2]
    a,b=p[1]-p[0],p[2]-p[1]
    if a[0]*b[1]-a[1]*b[0] < 0:
        p = p[::-1]
    low,high = p.min(axis=0),p.max(axis=0)
    def constraints(z):
        result=[]
        for a,b in zip(p,np.roll(p,-1,axis=0)):
            dx,dy=b-a
            for x in z[:2]:
                for y in z[2:]:
                    result.append(dx*(y-a[1])-dy*(x-a[0]))
        return np.array(result)
    centre=(low+high)/2
    quarter=(high-low)/4
    initial=[centre[0]-quarter[0],centre[0]+quarter[0],centre[1]-quarter[1],centre[1]+quarter[1]]
    result=minimize(lambda z:-np.log(z[1]-z[0])-np.log(z[3]-z[2]),initial,
        method='SLSQP',constraints=[{'type':'ineq','fun':constraints}],
        bounds=[(low[0],high[0])]*2+[(low[1],high[1])]*2,options={'ftol':1e-12,'maxiter':500})
    assert result.success and min(constraints(result.x)) > -1e-6
    l,r,b,t=result.x
    rectangles.append({'role':screen['role'],'bounds_l_r_b_t':list(result.x),
        'width':r-l,'height':t-b,'glass_size':list(high-low),
        'normalized_bounds_l_r_b_t':[(l-low[0])/(high[0]-low[0]),(r-low[0])/(high[0]-low[0]),
                                    (b-low[1])/(high[1]-low[1]),(t-low[1])/(high[1]-low[1])]})
(OUT/'screen-rectangles.json').write_text(json.dumps(rectangles,indent=2))

masks=[np.array(Image.open(OUT/name))[:,:,3]>127 for name in ['massing-mask.png','deck-mask.png']]
a,b=masks
ea=a^binary_erosion(a)
eb=b^binary_erosion(b)
distances=np.concatenate([distance_transform_edt(~ea)[eb],distance_transform_edt(~eb)[ea]])
metrics={'hardware_intersection_over_union':float((a&b).sum()/(a|b).sum()),
    'changed_pixels':int((a^b).sum()),'image_pixels':int(a.size),
    'edge_distance_95_percentile_px':float(np.percentile(distances,95)),
    'edge_distance_max_px':float(distances.max())}
(OUT/'silhouette-comparison.json').write_text(json.dumps(metrics,indent=2))
overlay=np.array(deck)
overlay[a&~b]=[255,70,70]
overlay[b&~a]=[40,255,220]
Image.fromarray(overlay).save(OUT/'silhouette-difference.png')
print(json.dumps(metrics))

reference = np.array(Image.open(OUT/'reference/deck-mask.png'))[:, :, 3] > 127
current = masks[1]
old_edge = reference ^ binary_erosion(reference)
new_edge = current ^ binary_erosion(current)
distances = np.concatenate([distance_transform_edt(~old_edge)[new_edge],
                            distance_transform_edt(~new_edge)[old_edge]])
coverage = {'hardware_intersection_over_union': float((reference & current).sum() / (reference | current).sum()),
            'changed_pixels': int((reference ^ current).sum()), 'image_pixels': int(reference.size),
            'edge_distance_95_percentile_px': float(np.percentile(distances, 95)),
            'edge_distance_max_px': float(distances.max()), 'camera_views': {}}
projections = json.loads((OUT / 'coverage-projections.json').read_text())
for suffix in ('', '-21x9', '-32x9', '-16x10', '-3x2', '-4x3'):
    mask = np.array(Image.open(OUT/('deck-mask' + suffix + '.png')))[:, :, 3] > 127
    height, width = mask.shape
    projection = projections[suffix.lstrip('-') or '16x9']
    coverage['camera_views'][suffix.lstrip('-') or '16x9'] = {
        'size': [width, height],
        'horizontal_fov_degrees': projection['horizontal_fov_degrees'],
        'vertical_fov_degrees': projection['effective_vertical_fov_degrees'],
        'shift_y': projection['shift_y'],
        'top_edge_sky_pixels': int((~mask[0]).sum()),
        'bottom_edge_sky_pixels': int((~mask[-1]).sum())}
baseline_audit = json.loads((OUT/'reference/mesh-audit.json').read_text())
coverage['moving_controls_unchanged'] = audit['controls'] == baseline_audit['controls']
coverage['screen_glasses_unchanged'] = audit['screens'] == baseline_audit['screens']
preservation = json.loads((OUT/'preservation-check.json').read_text())
coverage['screen_shapes_frames_unchanged'] = preservation['glass_changed_only_in_depth']
(OUT/'aspect-comparison.json').write_text(json.dumps(coverage, indent=2))
overlay = np.array(deck)
overlay[reference & ~current] = [255, 70, 70]
overlay[current & ~reference] = [40, 255, 220]
Image.fromarray(overlay).save(OUT/'deck-extension-difference.png')
print(json.dumps(coverage))

previous = Image.open(OUT/'deck-recess-original.png').convert('RGB')
comparison = Image.new('RGB', (3840, 1790), '#121c2a')
comparison.paste(previous, (0, 60))
comparison.paste(deck, (1920, 60))
draw = ImageDraw.Draw(comparison)
draw.text((24, 12), 'Previous: raised screen lips', font=font, fill='white')
draw.text((1944, 12), 'Current: wider pod corners, shallow recesses and finish 1', font=font, fill='white')
for column, (source, box, label) in enumerate([
        (previous, (300, 480, 580, 660), 'Left top inner corner, previous, 3x'),
        (deck, (300, 480, 580, 660), 'Left top inner corner, updated, 3x'),
        (previous, (1340, 480, 1620, 660), 'Right top inner corner, previous, 3x'),
        (deck, (1340, 480, 1620, 660), 'Right top inner corner, updated, 3x')]):
    x = column * 960 + 60
    draw.text((x, 1170), label, font=font, fill='white')
    comparison.paste(source.crop(box).resize((840, 540), Image.Resampling.NEAREST), (x, 1220))
comparison.save(OUT/'deck-recess-compare.png')
