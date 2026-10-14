from pathlib import Path
import json
import cv2
import numpy as np
from PIL import Image, ImageDraw

stage = Path(__file__).parent
owner = np.load(stage / 'owner-BaseRegion.npy')[::-1, :, 0]
prior = np.asarray(Image.open(stage.parent / '2026-10-13-r07/layers/BaseRegion.png').convert('L'), dtype=float) / 255
clean_prior = np.asarray(Image.open(stage.parent / '2026-10-13-owner-setup/layers/BaseRegion.png').convert('L'), dtype=float) / 255
edited = np.abs(owner - prior) > 2 / 255
solid = np.where(edited, owner, clean_prior)
binary = (solid > .45).astype(np.uint8) * 255
contours, hierarchy = cv2.findContours(binary, cv2.RETR_TREE, cv2.CHAIN_APPROX_SIMPLE)
scale = 4
canvas = np.zeros((2048 * scale, 2048 * scale), np.uint8)
polygons = []
straightened = {
    8: [[1694,1653],[1705,1657],[1668,1787],[1657,1784]],
    13: [[631,1635],[682,1658],[641,1781],[630,1777],[668,1664],[628,1646]],
    26: [[1367,1378],[1428,1378],[1294,1465],[1273,1465]],
    28: [[343,1377],[406,1377],[280,1462],[253,1462]],
    52: [[1050,981],[1196,981],[1196,994],[1050,994]],
    53: [[27,981],[173,981],[173,994],[27,994]],
    57: [[1050,954],[1196,954],[1196,967],[1050,967]],
    58: [[27,954],[173,954],[173,967],[27,967]],
    67: [[1673,727],[1683,727],[1683,856],[1673,856]],
    75: [[552,657],[649,673],[658,854],[602,833],[599,800],[628,704],[551,689]],
    93: [[1549,436],[1564,439],[1564,640],[1551,640]],
    94: [[526,434],[540,437],[540,634],[526,634]],
    102: [[1576,268],[1589,271],[1589,405],[1576,403]],
    105: [[551,266],[612,282],[614,408],[603,406],[603,291],[565,281],[562,403],[551,401]],
}
for index, contour in enumerate(contours):
    area = abs(cv2.contourArea(contour))
    depth = 0
    parent = hierarchy[0, index, 3]
    while parent != -1:
        depth += 1
        parent = hierarchy[0, parent, 3]
    if area < 12:
        continue
    approx = cv2.approxPolyDP(contour, 3.0, True)
    if len(approx) < 3:
        approx = cv2.approxPolyDP(contour, 1.0, True)
    if index in straightened:
        approx = np.array(straightened[index],dtype=np.int32).reshape(-1,1,2)
    cv2.fillPoly(canvas, [approx * scale], 255 if depth % 2 == 0 else 0)
    polygons.append({'index':index, 'depth':depth, 'area':area, 'points':approx[:,0].tolist()})
mesh=json.loads((stage/'owner-mesh.json').read_text(encoding='utf-8'))
for part_index, face_index in [(2,111),(2,132),(2,121),(2,142),(2,107)]:
    face=mesh['parts'][part_index]['faces'][face_index]
    points=np.array([(u*2048*scale,(1-v)*2048*scale) for u,v in face['uv']],dtype=np.int32)
    cv2.fillPoly(canvas,[points],255)
    cv2.polylines(canvas,[points],True,255,3*scale)
out = Image.fromarray(canvas).resize((2048,2048), Image.Resampling.LANCZOS)
out.save(stage / 'clean-BaseRegion.png')
(stage / 'livery-polygons.json').write_text(json.dumps(polygons, indent=2), encoding='utf-8')
overlay = np.repeat(np.asarray(out)[:,:,None],3,axis=2)
mask = np.asarray(out)>127
old = owner>.45
overlay[old & ~mask] = [255,80,70]
overlay[~old & mask] = [60,220,100]
Image.fromarray(overlay).save(stage/'cleanup-delta.png')

atlas=Image.fromarray(np.repeat(np.rint(owner*160).astype(np.uint8)[:,:,None],3,axis=2))
draw=ImageDraw.Draw(atlas)
mesh=json.loads((stage/'owner-mesh.json').read_text(encoding='utf-8'))
colors=['#fc7070','#70e0fc','#d0fc70','#fc70ef','#ffc070','#70fcbd']
for k,part in enumerate(mesh['parts']):
    if not any(x in part['materials'] for x in ['Bulwark Bone','Bulwark Plum','Bulwark Flap','Bulwark EdgeCoat']):continue
    for i,face in enumerate(part['faces']):
        if part['materials'][face['material']] not in ['Bulwark Bone','Bulwark Plum','Bulwark Flap','Bulwark EdgeCoat']:continue
        pts=[(u*2048,(1-v)*2048) for u,v in face['uv']]
        draw.line(pts+[pts[0]],fill=colors[k%len(colors)],width=1)
        if abs(cv2.contourArea(np.array(pts,dtype=np.float32)))>1100:
            c=np.mean(pts,axis=0)
            draw.text(tuple(c),f'{k}:{i}',fill='yellow')
atlas.save(stage/'owner-uv-labelled.png')
print(json.dumps({'polygons':len(polygons),'changed_pixels':int(np.sum(old != mask)), 'parts':{k:x['name'] for k,x in enumerate(mesh['parts'])}}))
