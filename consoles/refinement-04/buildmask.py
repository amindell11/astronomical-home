from pathlib import Path
import json, hashlib
import numpy as np
from PIL import Image, ImageDraw

ROOT = Path(__file__).resolve().parent
DIAGNOSTICS = Path('D:/amind/scratch/console-palette-diagnostics')
DIAGNOSTICS.mkdir(parents=True, exist_ok=True)
source_path = ROOT / 'neutral-materials.png'
source = Image.open(source_path).convert('RGB')
width, height = source.size
rgb = np.asarray(source).astype(np.float32)
lum = rgb @ np.array([0.2126, 0.7152, 0.0722], dtype=np.float32)
SUPERSAMPLE = 4

def shape_layer(polygons=(), ellipses=(), holes=()):
    canvas = Image.new('L', (width*SUPERSAMPLE,height*SUPERSAMPLE))
    draw = ImageDraw.Draw(canvas)
    for points in polygons:
        draw.polygon([(round(x*SUPERSAMPLE),round(y*SUPERSAMPLE)) for x,y in points],fill=255)
    for box in ellipses:
        draw.ellipse(tuple(round(v*SUPERSAMPLE) for v in box),fill=255)
    for points in holes:
        draw.polygon([(round(x*SUPERSAMPLE),round(y*SUPERSAMPLE)) for x,y in points],fill=0)
    return np.asarray(canvas.resize((width,height),Image.Resampling.LANCZOS)).astype(np.float32)/255

housing = shape_layer(polygons=[
    [(8,884),(16,360),(59,320),(193,320),(201,317),(638,317),(664,339),(681,339),(708,360),(752,881),(730,919),(39,921)],
    [(800,711),(841,669),(1019,668),(1041,686),(1027,708),(829,714)],
    [(962,817),(1032,712),(1046,713),(1059,748),(1205,749),(1246,779),(1193,783),(1114,845),(976,845)],
    [(1228,705),(1261,672),(1505,672),(1523,682),(1570,754),(1555,778),(1257,779)],
],holes=[
    [(165,394),(178,381),(654,382),(681,405),(701,696),(714,838),(698,864),(180,866),(161,851)],
    [(170,337),(195,318),(639,318),(661,339),(648,355),(184,356)],
    [(166,905),(184,884),(591,884),(612,904),(608,922),(168,923)],
    [(46,714),(51,710),(136,710),(143,717),(142,786),(135,792),(49,792),(44,786)],
])
housing *= 1-shape_layer(polygons=[
    [(793,793),(814,738),(871,687),(889,685),(910,688),(910,700),(836,796),(817,798),(792,798)],
    [(848,796),(867,740),(924,687),(942,685),(965,688),(965,700),(891,798)],
    [(905,796),(924,741),(979,688),(997,685),(1018,688),(1018,702),(949,798)],
])

trim = shape_layer(polygons=[
    [(171,338),(196,319),(637,319),(660,340),(648,355),(184,355)],
    [(167,905),(183,885),(590,885),(612,904),(608,922),(168,922)],
    [(46,714),(51,710),(135,710),(142,717),(141,786),(134,791),(49,791),(45,786)],
    [(946,877),(968,877),(974,884),(972,919),(965,925),(947,923),(943,914)],
    [(1284,718),(1297,721),(1293,744),(1282,741)],
    [(1339,719),(1351,721),(1347,744),(1336,742)],
    [(68,519),(82,519),(82,540),(67,540)],
],ellipses=[(1277,732,1300,753),(1331,732,1355,754),(62,525,88,545)],holes=[
    [(531,326),(614,325),(621,334),(619,347),(612,352),(532,352),(528,345),(528,333)],
])

taupe = shape_layer(polygons=[
    [(1116,845),(1193,784),(1365,784),(1322,837),(1286,914),(1248,914),(1249,901),(1241,881),(1230,874),(1140,871)],
    [(1577,711),(1626,728),(1653,761),(1596,757)],
])

dark = shape_layer(polygons=[
    [(735,679),(821,678),(801,710),(738,711)],
    [(738,714),(831,713),(873,689),(1035,689),(1029,713),(963,816),(746,816)],
    [(793,793),(814,738),(871,687),(889,685),(910,688),(910,700),(836,796),(817,798),(792,798)],
    [(848,796),(867,740),(924,687),(942,685),(965,688),(965,700),(891,798)],
    [(905,796),(924,741),(979,688),(997,685),(1018,688),(1018,702),(949,798)],
    [(747,817),(964,817),(976,846),(1116,846),(1139,872),(1232,874),(1249,890),(1248,916),(1337,940),(733,940),(733,910)],
    [(1212,690),(1250,660),(1519,660),(1541,684),(1583,750),(1574,781),(1245,785),(1212,750)],
    [(1580,759),(1655,765),(1669,787),(1669,940),(1649,940),(1657,904)],
    [(1367,783),(1573,783),(1659,904),(1651,938),(1335,938),(1289,915),(1323,840)],
    [(0,905),(7,902),(37,924),(166,925),(169,929),(608,929),(612,924),(731,922),(754,906),(812,932),(1669,940),(0,940)],
],holes=[
    [(1227,704),(1261,672),(1506,672),(1523,681),(1571,753),(1555,778),(1258,780)],
    [(1365,810),(1387,797),(1575,797),(1589,808),(1606,855),(1602,883),(1586,901),(1390,901),(1373,885),(1357,833)],
    [(1044,689),(1205,689),(1226,745),(1216,753),(1056,753),(1045,739)],
])

buttons = shape_layer(polygons=[
    [(603,517),(635,517),(642,523),(642,555),(634,561),(603,558),(597,551),(597,529)],
    [(651,517),(681,518),(689,525),(689,552),(682,561),(650,560),(647,551),(648,524)],
    [(609,703),(640,702),(650,707),(650,739),(643,746),(612,743),(605,736),(605,712)],
    [(658,702),(688,703),(700,710),(701,737),(692,746),(661,745),(656,738),(656,711)],
    [(614,783),(647,782),(655,788),(655,818),(648,825),(616,822),(608,815),(608,791)],
    [(665,782),(695,783),(707,790),(708,816),(701,825),(668,825),(661,818),(661,790)],
],ellipses=[(64,493,85,523)])

knobs = shape_layer(polygons=[
    [(53,510),(63,508),(65,522),(84,522),(85,508),(94,510),(97,550),(92,557),(53,556),(49,550)],
    [(1263,728),(1302,726),(1312,756),(1308,764),(1268,764),(1261,753)],
    [(1319,729),(1356,728),(1369,757),(1364,765),(1323,765),(1317,754)],
    [(806,852),(825,852),(834,873),(848,882),(942,884),(951,879),(969,878),(979,885),(1168,885),(1178,881),(1215,882),(1238,887),(1247,898),(1241,913),(1215,920),(1177,918),(1169,914),(978,914),(967,925),(945,925),(940,916),(859,922),(834,919),(820,909),(808,890)],
],ellipses=[(58,590,135,666),(1277,700,1304,728),(1333,701,1360,730),(1393,700,1464,767)])

lights = shape_layer(polygons=[
    [(1479,704),(1526,703),(1540,748),(1488,751)],
    [(1172,881),(1226,881),(1228,916),(1170,917)],
],ellipses=[(1377,691,1480,779)])

protected = shape_layer(polygons=[
    [(177,394),(190,386),(566,388),(580,404),(594,836),(584,851),(192,853),(178,837)],
    [(1037,687),(1207,686),(1228,746),(1215,754),(1054,752),(1040,735)],
    [(1365,807),(1387,795),(1576,796),(1591,808),(1608,854),(1604,885),(1587,903),(1388,903),(1371,885),(1356,833)],
    [(531,325),(615,325),(623,333),(622,349),(614,354),(532,354),(527,345),(528,332)],
    [(105,509),(141,507),(146,514),(145,540),(138,546),(105,544),(101,537),(102,516)],
])

gates = {
    1:np.clip((lum-72)/45,0,1),
    2:np.clip((lum-42)/42,0,1),
    3:np.clip((lum-34)/34,0,1),
    4:np.clip((lum-8)/16,0,1),
    5:np.clip((lum-82)/42,0,1),
    6:np.clip((lum-9)/23,0,1),
}
amber = np.clip((rgb[:,:,0]-rgb[:,:,1]-35)/30,0,1)*np.clip((rgb[:,:,1]-rgb[:,:,2]-42)/35,0,1)*np.clip((rgb[:,:,0]-135)/70,0,1)
cyan = np.clip((np.minimum(rgb[:,:,1],rgb[:,:,2])-rgb[:,:,0]-12)/24,0,1)*np.clip((np.minimum(rgb[:,:,1],rgb[:,:,2])-58)/55,0,1)
light_membership = np.clip((np.minimum(rgb[:,:,1],rgb[:,:,2])-rgb[:,:,0]-5)/9,0,1)*np.clip((np.minimum(rgb[:,:,1],rgb[:,:,2])-12)/23,0,1)

layers = {1:housing,2:trim,3:taupe,4:dark,5:buttons,6:knobs,7:lights}
coverage = {}
for region_id,shape in layers.items():
    if region_id==7:
        coverage[region_id]=shape*light_membership
    else:
        coverage[region_id]=shape*gates[region_id]*(1-protected)*(1-amber)*(1-cyan)

ids=np.zeros((height,width),dtype=np.uint8)
alpha=np.zeros((height,width),dtype=np.uint8)
for region_id in (4,1,3,6,2,5,7):
    layer_alpha=np.round(coverage[region_id]*255).astype(np.uint8)
    use=layer_alpha>2
    ids[use]=region_id
    alpha[use]=layer_alpha[use]
rgba=np.zeros((height,width,4),dtype=np.uint8)
rgba[:,:,0]=ids
rgba[:,:,1]=alpha
rgba[:,:,3]=255
Image.fromarray(rgba).save(ROOT/'palette-regions.png')

specs=[
    (1,'mainHousing','Main housing','#ead0ad','#bba78e',[(90,365),(152,690),(691,620),(1278,686)]),
    (2,'silverTrim','Metal trim','#bbb6b9','#999187',[(300,336),(355,902),(64,726)]),
    (3,'taupeCovers','Taupe covers','#997766','#8e7565',[(1221,825),(1603,735)]),
    (4,'darkStructure','Dark structure','#343137','#343238',[(780,830),(1020,865),(1557,791)]),
    (5,'buttonCaps','Selector caps','#ddc4a8','#c8b195',[(617,530),(679,716),(641,807)]),
    (6,'knobsCables','Knobs and cables','#413b3e','#353234',[(95,627),(1433,735),(1080,894)]),
    (7,'projectionLights','Projection accents','#8af6fb','#d9c6a2',[(1504,725),(1197,899),(1457,727)]),
]
regions=[]
for region_id,key,label,reference,default,samples in specs:
    pixels=int(np.count_nonzero(ids==region_id))
    approximate=np.array([int(reference[i:i+2],16) for i in (1,3,5)],dtype=np.float32)
    distance=((rgb-approximate)**2).sum(axis=2)
    distance[(ids!=region_id)|(alpha<245)]=np.inf
    sample_y,sample_x=np.unravel_index(np.argmin(distance),distance.shape)
    reference='#'+''.join(f'{int(v):02x}' for v in rgb[sample_y,sample_x])
    regions.append({'id':region_id,'key':key,'label':label,'originalColor':reference,'referenceColor':reference,'defaultColor':default,'pixelCount':pixels,'weightedPixelCount':round(float(alpha[ids==region_id].sum()/255),2),'referenceSample':{'x':int(sample_x),'y':int(sample_y),'color':reference},'recolorMode':'luminanceTint' if region_id==7 else 'linearChannelGain'})
manifest={'schemaVersion':1,'sourceImage':{'file':'neutral-materials.png','width':width,'height':height,'sha256':hashlib.sha256(source_path.read_bytes()).hexdigest()},'maskImage':'palette-regions.png','encoding':{'format':'RGBA8','regionChannel':'R','regionValues':'integer IDs 1..7; 0 is protected','coverageChannel':'G','coverageRange':[0,255],'blueChannel':0,'alphaChannel':255,'protectedPixel':{'r':0,'g':0,'b':0,'a':255}},'regions':regions,'protected':['Holographic ship','Sky','Display face and text','Projector aperture and beam','Amber status lights','Launch glowing cap'],'qualification':'Hand-authored supersampled part polygons, luminance preservation for ink and shadow, amber emission protection, and local cyan hue membership for projection accents.'}
(ROOT/'palette-regions.json').write_text(json.dumps(manifest,indent=2)+'\n')

debug_colors=np.array([(0,0,0),(255,76,76),(70,222,255),(247,159,48),(151,99,255),(68,242,118),(245,87,206),(255,242,65)],dtype=np.float32)
amount=(alpha.astype(np.float32)/255*0.68)[:,:,None]
overlay=rgb*(1-amount)+debug_colors[ids]*amount
Image.fromarray(np.uint8(np.clip(overlay,0,255))).save(DIAGNOSTICS/'palette-mask-overlay.png')
Image.fromarray(debug_colors[ids].astype(np.uint8)).save(DIAGNOSTICS/'palette-mask-regions.png')

def to_linear(value):
    value=np.asarray(value,dtype=np.float32)/255
    return np.where(value<=0.04045,value/12.92,((value+0.055)/1.055)**2.4)
def to_srgb(value):
    return np.where(value<=0.0031308,value*12.92,1.055*np.maximum(value,0)**(1/2.4)-0.055)*255
def color(text):
    return np.array([int(text[i:i+2],16) for i in (1,3,5)],dtype=np.float32)
linear=to_linear(rgb)
result=linear.copy()
for region in regions:
    choice=ids==region['id']
    target=to_linear(color(region['defaultColor']))
    reference=to_linear(color(region['referenceColor']))
    weight=alpha[choice,None]/255
    if region['id']==7:
        luminance_weights=np.array([0.2126,0.7152,0.0722])
        source_luminance=(linear[choice] @ luminance_weights)[:,None]
        recolored=target*source_luminance/max(float(reference @ luminance_weights),0.0001)
    else:
        recolored=linear[choice]*target/np.maximum(reference,0.0001)
    result[choice]=linear[choice]*(1-weight)+recolored*weight
preview=np.uint8(np.clip(np.rint(to_srgb(result)),0,255))
preview[ids==0]=np.asarray(source)[ids==0]
Image.fromarray(preview).save(DIAGNOSTICS/'palette-default-preview.png')
print(json.dumps({'dimensions':[width,height],'maskedPixels':int(np.count_nonzero(ids)),'protectedPixels':int(np.count_nonzero(ids==0)),'regionCounts':{r['key']:r['pixelCount'] for r in regions}},indent=2))
