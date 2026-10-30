from pathlib import Path
import json
import math
import bpy

ROOT = Path(__file__).resolve().parent
OUT = ROOT / 'hangar-blockout.blend'
if OUT.exists():
    raise RuntimeError('Preserve the editable source. Build into a new folder instead.')
DATA = json.loads((ROOT / 'inputs/spec-data.json').read_text())
bpy.ops.wm.read_factory_settings(use_empty=True)
s = bpy.context.scene
s.name = 'Camera-first dispatch layers'
s.render.engine = 'BLENDER_EEVEE'
s.eevee.taa_render_samples = 16
s.render.resolution_x = 1920
s.render.resolution_y = 1080
s.render.resolution_percentage = 100
s.render.image_settings.file_format = 'PNG'
s.render.fps = 30
s.frame_end = 390
s.view_settings.view_transform = 'Standard'
s.view_settings.look = 'None'
s.world = bpy.data.worlds.new('Unlit layered blockout')
s.world.color = (0, 0, 0)

def collection(name):
    c = bpy.data.collections.new(name)
    s.collection.children.link(c)
    return c

cols = [collection(n) for n in ['00 Final camera', '01 Space', '02 Dock edge',
    '03 Arrival lane', '04 Hologram and projector', '05 Cropped console frames',
    '06 Main display - editable spec', '07 Deck display - editable hardpoints',
    '08 Physical Call', '09 Transition and canopy', '10 Reference and guide']]

def empty(name, col):
    o = bpy.data.objects.new(name, None)
    col.objects.link(o)
    o.empty_display_size = 60
    return o

def mat(name, grey):
    m = bpy.data.materials.new(name)
    m.diffuse_color = (grey, grey, grey, 1)
    n = m.node_tree.nodes
    n.clear()
    e = n.new('ShaderNodeEmission')
    e.inputs['Color'].default_value = (grey, grey, grey, 1)
    out = n.new('ShaderNodeOutputMaterial')
    m.node_tree.links.new(e.outputs[0], out.inputs['Surface'])
    return m

ink = mat('Screen ink', .78)
dim = mat('Secondary screen ink', .35)
dark = mat('Structural seams', .013)
screen_mat = mat('Display face', .016)
grey = mat('Grey frame face', .22)
lit = mat('Grey exposed edge', .37)
shade = mat('Grey side face', .09)
deck_mat = mat('Grey deck', .14)
track = mat('Empty quantity segment', .055)
lamp = mat('Neutral bay lamps', .85)

def poly(name, points, z, material, col, parent=None):
    mesh = bpy.data.meshes.new(name)
    mesh.from_pydata([(x, -y, 0) for x, y in points], [], [list(range(len(points)))])
    mesh.update()
    o = bpy.data.objects.new(name, mesh)
    col.objects.link(o)
    o.location.z = z
    o.data.materials.append(material)
    if parent:
        o.parent = parent
    return o

def rect(name, x, y, w, h, z, material, col, parent=None):
    return poly(name, [(x,y),(x+w,y),(x+w,y+h),(x,y+h)], z, material, col, parent)

ANGLE = math.tan(math.radians(40))
def chamfer(name, x, y, w, h, cut, z, material, col, parent=None):
    dy = cut * ANGLE
    return poly(name, [(x+cut,y),(x+w-cut,y),(x+w,y+dy),(x+w,y+h-dy),
        (x+w-cut,y+h),(x+cut,y+h),(x,y+h-dy),(x,y+dy)],z,material,col,parent)

def wire(name, pts, z, width, material, col, parent=None, smooth=False):
    c = bpy.data.curves.new(name, 'CURVE')
    c.dimensions = '3D'
    c.resolution_u = 16
    c.bevel_depth = width / 2
    c.bevel_resolution = 2
    p = c.splines.new('BEZIER' if smooth else 'POLY')
    if smooth:
        p.bezier_points.add(len(pts)-1)
        for bp,(x,y) in zip(p.bezier_points,pts):
            bp.co = (x,-y,0)
            bp.handle_left_type = bp.handle_right_type = 'AUTO'
    else:
        p.points.add(len(pts)-1)
        for bp,(x,y) in zip(p.points,pts): bp.co=(x,-y,0,1)
    o = bpy.data.objects.new(name,c)
    col.objects.link(o)
    o.location.z=z
    o.data.materials.append(material)
    if parent:o.parent=parent
    return o

def bolt(name,x,y,z,col,parent=None):
    pts=[(x+8*math.cos(i*math.tau/12), y+8*math.sin(i*math.tau/12)) for i in range(12)]
    poly(name+' seat',[(x-12,y-12),(x+12,y-12),(x+12,y+12),(x-12,y+12)],z,dark,col,parent)
    poly(name,pts,z+1,lit,col,parent)
    wire(name+' slot',[(x-4,y),(x+4,y)],z+2,2,dark,col,parent)

fontdir = ROOT.parents[1] / 'combat-hud/shared/fonts'
sans = bpy.data.fonts.load(str(fontdir/'plex-sans.ttf'))
mono = bpy.data.fonts.load(str(fontdir/'plex-mono.ttf'))
sans.pack();mono.pack()
def text(name,body,x,y,size,col,parent=None,font=None,material=None,align='LEFT',z=110):
    c=bpy.data.curves.new(name,'FONT');c.body=body;c.size=size
    c.font=font or sans;c.align_y='CENTER';c.align_x=align;c.resolution_u=4
    o=bpy.data.objects.new(name,c);col.objects.link(o);o.location=(x,-y,z)
    o.data.materials.append(material or ink)
    if parent:o.parent=parent
    return o

camera_data=bpy.data.cameras.new('Fixed final 1920 by 1080 crop')
camera=bpy.data.objects.new('CAMERA - fixed composition',camera_data)
cols[0].objects.link(camera);camera.location=(960,-540,4000)
camera_data.type='ORTHO';camera_data.ortho_scale=1920;camera_data.clip_end=8000
s.camera=camera
camera['Composition']='Image C layout plus dock edge. No legs, pedestal or room.'

def image_plane(name,path,x,y,w,h,z,col,key=False,sequence=False,opacity=1,strength=1,uv=None):
    o=rect(name,x,y,w,h,z,dark,col)
    uv_layer=o.data.uv_layers.new(name='UVMap')
    coords=uv or [(0,1),(1,1),(1,0),(0,0)]
    for loop,coord in zip(uv_layer.data,coords):loop.uv=coord
    image=bpy.data.images.load(str(path),check_existing=False)
    m=bpy.data.materials.new(name+' material');m.surface_render_method='BLENDED'
    nodes=m.node_tree.nodes;nodes.clear();links=m.node_tree.links
    tex=nodes.new('ShaderNodeTexImage');tex.image=image
    if sequence:
        image.source='SEQUENCE';tex.image_user.frame_duration=180
        tex.image_user.frame_start=1;tex.image_user.use_cyclic=True;tex.image_user.use_auto_refresh=True
    else:image.pack()
    emit=nodes.new('ShaderNodeEmission');emit.inputs['Strength'].default_value=strength
    links.new(tex.outputs['Color'],emit.inputs['Color'])
    out=nodes.new('ShaderNodeOutputMaterial')
    alpha=nodes.new('ShaderNodeValue');alpha.name='Opacity';alpha.outputs[0].default_value=opacity
    transparent=nodes.new('ShaderNodeBsdfTransparent')
    mix=nodes.new('ShaderNodeMixShader')
    if key:
        dist=nodes.new('ShaderNodeVectorMath');dist.operation='DISTANCE'
        srgb=lambda v:(v/255/12.92) if v/255<=.04045 else ((v/255+.055)/1.055)**2.4
        dist.inputs[1].default_value=tuple(srgb(v) for v in (9,13,23))
        links.new(tex.outputs['Color'],dist.inputs[0])
        rang=nodes.new('ShaderNodeMapRange');rang.inputs['From Min'].default_value=.0003
        rang.inputs['From Max'].default_value=.005;links.new(dist.outputs['Value'],rang.inputs['Value'])
        mul=nodes.new('ShaderNodeMath');mul.operation='MULTIPLY'
        links.new(rang.outputs['Result'],mul.inputs[0]);links.new(alpha.outputs[0],mul.inputs[1])
        links.new(mul.outputs[0],mix.inputs[0])
    else:links.new(alpha.outputs[0],mix.inputs[0])
    links.new(transparent.outputs[0],mix.inputs[1]);links.new(emit.outputs[0],mix.inputs[2])
    links.new(mix.outputs[0],out.inputs['Surface']);o.data.materials.clear();o.data.materials.append(m)
    return o,alpha

sky,_=image_plane('SPACE - real capture crop',ROOT/'inputs/real-hangar-reference.png',0,0,1920,1080,0,cols[1],
    strength=.65,uv=[(.375,1),(.6525,1),(.6525,.722222),(.375,.722222)])

dock=empty('DOCK EDGE - anchors and bay lights',cols[2])
poly('Dock edge top',[(760,690),(850,690),(905,736),(1925,736),(1980,782),(760,782)],20,lit,cols[2],dock)
poly('Dock edge fascia',[(760,782),(1980,782),(1980,835),(760,835)],21,shade,cols[2],dock)
wire('Dock edge seam',[(800,755),(1920,755)],23,5,dark,cols[2],dock)
for x in [880,1040,1555,1815]:
    chamfer('Bay light socket',x,757,58,25,9,25,dark,cols[2],dock)
    rect('Bay light',x+11,764,34,7,26,lamp,cols[2],dock)
for x in [843,1128,1670,1870]:bolt('Dock bolt',x,808,27,cols[2],dock)

arrival=empty('ARRIVAL LANE - deep space toward camera',cols[3])
arrival['Route']='Point at 1540,175 grows toward 1470,500, then banks past deck lip at y736. Fixed camera.'
arrival.empty_display_type='ARROWS';arrival.location=(1540,-175,30)
arrival['Occluder']='Deck top edge and main console frame are in front of the arriving hull.'

console=empty('CONSOLE FRAME - cropped left and bottom',cols[5])
poly('Deck foundation',[(0,865),(760,865),(870,773),(1920,773),(2050,882),(2050,1200),(-140,1200)],60,deck_mat,cols[5],console)
wire('Deck upper seam',[(810,817),(1920,817)],62,6,dark,cols[5],console)
chamfer('Main frame deep edge',-96,53,913,940,65,65,dark,cols[5],console)
chamfer('Main frame body',-102,38,906,920,65,67,grey,cols[5],console)
chamfer('Main frame raised rim',48,100,708,804,40,69,lit,cols[5],console)
chamfer('Main frame recess',62,114,680,778,36,71,dark,cols[5],console)
chamfer('Main screen gasket',77,129,650,748,25,73,shade,cols[5],console)
chamfer('Main display face',85,137,634,731,21,75,screen_mat,cols[6])
poly('Frame right facet',[(756,100),(804,78),(804,904),(756,944)],70,shade,cols[5],console)
for y in [72,932]:
    for x in [26,768]:bolt('Main frame mounting bolt',x,y,80,cols[5],console)
for y in [240,515,760]:
    chamfer('Cropped side mount',-65,y,88,150,22,80,shade,cols[5],console)
    chamfer('Side mount recess',-50,y+13,56,118,13,81,dark,cols[5],console)
for x in [95,665]:
    chamfer('Console dock anchor',x,943,90,180,20,81,shade,cols[5],console)
    bolt('Anchor bolt upper',x+45,975,84,cols[5],console)
    bolt('Anchor bolt lower',x+45,1045,84,cols[5],console)
rect('Base access panel',220,971,345,89,82,shade,cols[5],console)
for y in [990,1004,1018,1032]:wire('Access ventilation',[(265,y),(515,y)],84,5,dark,cols[5],console)

for i in range(3):
    pts=[(810,895+i*18),(839,1067+i*12),(1250,1110+i*12),(1513+i*18,1010),(1495+i*18,799),(1400,763+i*12)]
    wire('Cable jacket '+str(i),pts,83,16,dark,cols[5],console,True)
    wire('Cable core '+str(i),pts,84,9,shade,cols[5],console,True)
    rect('Cable clip '+str(i),1502+i*18,887,14,28,85,grey,cols[5],console)

projector=empty('PROJECTOR - housing and aperture',cols[4])
chamfer('Projector bolted plinth',1110,699,335,112,38,85,dark,cols[4],projector)
chamfer('Projector side housing',1122,690,312,98,34,87,shade,cols[4],projector)
chamfer('Projector upper housing',1140,671,277,88,30,89,grey,cols[4],projector)
chamfer('Projector aperture recess',1193,690,169,45,22,91,dark,cols[4],projector)
chamfer('Projector emitter',1207,699,141,24,12,92,lamp,cols[4],projector)
for x in [1157,1397]:bolt('Projector housing bolt',x,750,93,cols[4],projector)
for x in [1148,1162,1176,1377,1391]:rect('Projector fin',x,695,6,33,93,dark,cols[4],projector)

holo=empty('HOLOGRAM - preserved captured preview flights',cols[4])
plate,holo_alpha=image_plane('Actual preview flight sequence',ROOT/'inputs/preview-flight/frame-0000.png',70,0,1728,972,42,cols[4],True,True,.7,1.5)
plate.parent=holo
held,held_alpha=image_plane('Storyboard hologram - held Valis capture',ROOT/'inputs/preview-flight/frame-0045.png',70,0,1728,972,42,cols[4],True,False,.7,1.5)
held.parent=holo
for obj,story in [(plate,False),(held,True)]:
    for f,hide in [(1,story),(180,story),(181,not story),(390,not story)]:
        obj.hide_render=hide;obj.hide_viewport=hide
        obj.keyframe_insert('hide_render',frame=f);obj.keyframe_insert('hide_viewport',frame=f)
holo['Opacity']=.7
for alpha in [holo_alpha,held_alpha]:
    dr=alpha.outputs[0].driver_add('default_value').driver
    dr.expression='opacity';var=dr.variables.new();var.name='opacity';var.type='SINGLE_PROP';var.targets[0].id=holo;var.targets[0].data_path='["Opacity"]'

def ship_visible(obj,nightshade):
    for f,h in [(1,nightshade),(60,nightshade),(61,not nightshade),(120,not nightshade),(121,nightshade),(390,nightshade)]:
        obj.hide_render=h;obj.hide_viewport=h
        obj.keyframe_insert('hide_render',frame=f);obj.keyframe_insert('hide_viewport',frame=f)

spec=empty('MAIN DISPLAY - face-on native text and bars',cols[6])
rect('Spec inverse heading',109,157,586,43,102,ink,cols[6],spec)
text('Main heading','SHIP SPECIFICATION',126,179,29,cols[6],spec,material=screen_mat)
text('Ship previous','\u2039',644,231,48,cols[6],spec)
text('Ship next','\u203a',683,231,48,cols[6],spec)
text('Tagline placeholder','[PLACEHOLDER TAGLINE]',112,279,21,cols[6],spec,material=dim)
text('Description placeholder 1','[Placeholder: ship description,',112,310,22,cols[6],spec,material=dim)
text('Description placeholder 2','one or two short sentences.]',112,337,22,cols[6],spec,material=dim)
for y in [360,438,555,726,814]:rect('Spec divider',112,y,580,1,102,dim,cols[6],spec)

ships={v['ship']:v for v in DATA['fleet']}
for name in ['Valis','Nightshade']:
    ns=name=='Nightshade';v=ships[name]
    objs=[text(name+' name',name,112,235,47,cols[6],spec),
          text(name+' shield',v['shield']['name'],112,464,26,cols[6],spec),
          text(name+' engine',v['engine']['name'],112,580,26,cols[6],spec),
          text(name+' keystone',v['keystone']['name'],195,759,29,cols[6],spec),
          text(name+' cooldown',f"{v['keystone']['cooldown']:g}s COOLDOWN",195,791,20,cols[6],spec,font=mono)]
    for obj in objs:ship_visible(obj,ns)
text('Fitted shield label','FITTED SHIELD',692,464,17,cols[6],spec,material=dim,align='RIGHT')
text('Fitted engine label','FITTED ENGINE',692,580,17,cols[6],spec,material=dim,align='RIGHT')
text('Keystone fitted label','FITTED KEYSTONE',692,756,17,cols[6],spec,material=dim,align='RIGHT')
poly('Keystone - shared canopy placeholder',[(151,744),(169,768),(151,792),(133,768)],110,ink,cols[6],spec)
text('Scale label','BARS RELATIVE TO THE FLEET',112,840,18,cols[6],spec,material=dim)

stats=[('Hull','HULL',395,''),('Shield capacity','Capacity',503,''),('Shield recharge','Recharge',538,'/s'),
       ('Top speed','Top speed',620,' u/s'),('Acceleration','Acceleration',660,' u/s2'),('Turn','Turn',700,' deg/s')]
for key,label,y,unit in stats:
    text(key+' label',label,112,y,23,cols[6],spec)
    for name in ['Valis','Nightshade']:
        value=ships[name]['stats'][key]
        number=f'{value:.2f}'.rstrip('0').rstrip('.') if value%1 else str(int(value))
        obj=text(name+' '+key+' exact',number+unit,692,y,21,cols[6],spec,font=mono,align='RIGHT')
        ship_visible(obj,name=='Nightshade')
    for i in range(20):
        x=286+i*12.8
        rect(key+' segment track',x,y-7,9.5,14,103,track,cols[6],spec)
        fill=rect(key+' segment '+str(i),0,y-7,9.5,14,104,ink,cols[6],spec)
        fill.location.x=x
        for f,name in [(1,'Valis'),(60,'Valis'),(76,'Nightshade'),(120,'Nightshade'),(136,'Valis'),(390,'Valis')]:
            ratio=max(DATA['barFloor'],ships[name]['stats'][key]/DATA['barMaximums'][key])
            fill.scale.x=max(.00001,min(1,ratio*20-i));fill.keyframe_insert('scale',frame=f)

hardpoints=empty('DECK DISPLAY - two face-on hardpoints',cols[7])
chamfer('Hardpoint housing edge',825,810,644,272,35,94,dark,cols[7],hardpoints)
chamfer('Hardpoint housing face',833,817,627,259,28,96,grey,cols[7],hardpoints)
chamfer('Hardpoint display',850,834,592,226,16,99,screen_mat,cols[7],hardpoints)
text('Hardpoint heading','HARDPOINTS',871,856,22,cols[7],hardpoints,material=dim)
rect('Hardpoint divider',871,963,548,1,104,dim,cols[7],hardpoints)
for row,y,weapon in [('PRIMARY',889,'Lasers'),('SECONDARY',987,'Missiles')]:
    text(row+' label',row,930,y-2,17,cols[7],hardpoints,material=dim)
    text(row+' previous','\u2039',1372,y+10,34,cols[7],hardpoints)
    text(row+' next','\u203a',1410,y+10,34,cols[7],hardpoints)
    if row=='PRIMARY':
        for i in range(3):
            x=878+i*9
            poly('Laser glyph from shared canopy',[(x,y+22),(x+25,y+1),(x+30,y+1),(x+5,y+22)],110,ink,cols[7],hardpoints)
    else:
        pts=[(893,y-3),(900,y+9),(900,y+29),(906,y+37),(895,y+31),(884,y+37),(889,y+29),(889,y+9)]
        poly('Missile glyph from shared canopy',pts,110,ink,cols[7],hardpoints)
    variants=[('Valis','Lasers'),('Nightshade','TurboLasers')] if row=='PRIMARY' else [('All','Missiles')]
    for ship,w in variants:
        obj=text(ship+' '+row+' name',w,1045,y-2,25,cols[7],hardpoints)
        line=text(ship+' '+row+' stats',DATA['weapons'][w]['statLine'],930,y+43,17,cols[7],hardpoints,font=mono)
        extra=text(ship+' '+row+' detail',DATA['weapons'][w]['extraLine'],930,y+65,16,cols[7],hardpoints,font=mono,material=dim)
        if ship!='All':
            for item in [obj,line,extra]:ship_visible(item,ship=='Nightshade')
    text(row+' description','[Placeholder weapon description.]',930,y+21,18,cols[7],hardpoints,material=dim)

call=empty('CALL - physical deck button',cols[8])
chamfer('Call bolted socket',1585,918,305,136,35,96,dark,cols[8],call)
chamfer('Call housing',1593,910,290,132,32,98,shade,cols[8],call)
chamfer('Call cap lower edge',1616,925,241,96,29,100,dark,cols[8],call)
cap=chamfer('CALL cap - editable press',1616,914,241,96,29,102,lit,cols[8],call)
cap_material=lit.copy();cap_material.name='Call cap press feedback'
cap.data.materials.clear();cap.data.materials.append(cap_material)
for f,value in [(1,.37),(200,.37),(211,.20),(390,.20)]:
    socket=cap_material.node_tree.nodes.get('Emission').inputs['Color']
    socket.default_value=(value,value,value,1);socket.keyframe_insert('default_value',frame=f)
label=text('Call label','CALL',1736,958,40,cols[8],call,material=dark,align='CENTER',z=112)
for x in [1602,1871]:bolt('Call mounting bolt',x,1031,104,cols[8],call)
rect('Call status lens',1670,889,130,9,102,lamp,cols[8],call)

# The storyboard keeps the camera still and changes only the layers.
for f,opacity in [(1,.7),(180,.7),(211,.7),(241,.18),(256,0.0),(390,0.0)]:
    holo['Opacity']=opacity;holo.keyframe_insert(data_path='["Opacity"]',frame=f)
for obj in [cap,label]:
    base_y=obj.location.y
    for f,y in [(1,0),(200,0),(211,-8),(390,-8)]:
        obj.location.y=base_y+y;obj.keyframe_insert('location',frame=f)

arriving,arr_alpha=image_plane('ARRIVING REAL HULL - storyboard capture',ROOT/'inputs/preview-flight/frame-0045.png',-600,-370,1200,740,35,cols[3],True,False,1,1,
    uv=[(673/1280,1-195/720),(1084/1280,1-195/720),(1084/1280,1-448/720),(673/1280,1-448/720)])
for f,x,y,scale,bank in [(1,1540,175,.00001,0),(270,1540,175,.00001,0),(271,1540,175,.014,0),
                          (282,1530,245,.12,0),(292,1510,410,.44,8),(301,1570,537,1,-19),(320,2030,680,2.1,-32),(329,2520,900,2.5,-32)]:
    arriving.location=(x,-y,35);arriving.scale=(scale,scale,1);arriving.rotation_euler.z=math.radians(bank)
    arriving.keyframe_insert('location',frame=f);arriving.keyframe_insert('scale',frame=f);arriving.keyframe_insert('rotation_euler',frame=f)
for f,hidden in [(1,True),(270,True),(271,False),(328,False),(329,True),(390,True)]:
    arriving.hide_render=hidden;arriving.hide_viewport=hidden
    arriving.keyframe_insert('hide_render',frame=f);arriving.keyframe_insert('hide_viewport',frame=f)
far_light=poly('Arrival distant light',[(1540,168),(1543,175),(1540,182),(1537,175)],36,lamp,cols[3])
for f,hidden in [(1,True),(270,True),(271,False),(281,False),(282,True),(390,True)]:
    far_light.hide_render=hidden;far_light.hide_viewport=hidden
    far_light.keyframe_insert('hide_render',frame=f);far_light.keyframe_insert('hide_viewport',frame=f)

canopy,canopy_alpha=image_plane('Canopy - shared combat review',ROOT.parents[1]/'combat-hud/review-04-canopy.png',0,0,1920,1080,200,cols[9])
for f,alpha in [(1,0),(329,0),(330,.15),(345,.45),(361,1),(390,1)]:
    canopy_alpha.outputs[0].default_value=alpha;canopy_alpha.outputs[0].keyframe_insert('default_value',frame=f)
for i,(y,h) in enumerate([(170,26),(285,8),(390,56),(557,20),(665,80),(823,15)]):
    g=rect('Glitch cut band '+str(i),0,y,1920,h,220,ink if i%2 else dark,cols[9])
    for f,hidden in [(1,True),(329,True),(330,False),(336,False),(337,True),(390,True)]:
        g.hide_render=hidden;g.hide_viewport=hidden;g.keyframe_insert('hide_render',frame=f);g.keyframe_insert('hide_viewport',frame=f)
for i,(x,y,w,h) in enumerate([(110,220,155,50),(1450,500,470,12),(750,790,400,60),(520,380,160,12)]):
    g=rect('Glitch cut block '+str(i),x,y,w,h,221,shade,cols[9])
    for f,hidden in [(1,True),(329,True),(330,False),(336,False),(337,True),(390,True)]:
        g.hide_render=hidden;g.hide_viewport=hidden;g.keyframe_insert('hide_render',frame=f);g.keyframe_insert('hide_viewport',frame=f)

for f,name in [(1,'Preview entry'),(46,'Valis layout review'),(61,'Next ship'),(106,'Nightshade layout review'),(121,'Previous ship'),
               (181,'01 Idle'),(211,'02 Call pressed'),(241,'03 Hologram powers down'),(271,'04 Light in deep space'),
               (301,'05 Bank past console edge'),(331,'06 Glitch cut'),(361,'07 Canopy powers on')]:
    s.timeline_markers.new(name,frame=f)

reference=empty('REFERENCE C - framing and later lighting',cols[10])
reference.empty_display_type='IMAGE';reference.data=bpy.data.images.load(str(ROOT.parent/'lighting-01/electronic-light-contrast-nb2.jpg'))
reference.data.pack();reference.location=(-2300,-540,0);reference.empty_display_size=1920;reference.hide_render=True
guide=bpy.data.texts.new('START HERE - editing and review')
guide.write('''FINAL CAMERA FIRST / HANGAR BLOCKOUT 02\n\nThe scene is a stack of flat editable shapes. Camera is fixed at 1920 x 1080.\nMove layers or edit polygon vertices in camera view. The console crops left and bottom.\nBoth displays are face-on native text and quantity segments, standing in for uGUI.\nNo perspective-modeled screen and no room geometry.\n\nHOLOGRAM retains the real preview sequence. Opacity is a keyed custom property.\nFrames 1-180 replay entry, next and previous with data and animated bars.\n46: Valis. 106: Nightshade. 166: Valis returned.\n\nStoryboard markers: 181 idle, 211 Call, 241 power-down, 271 deep-space light,\n301 bank past the console edge, 331 glitch cut, 361 canopy power-on.\nThe arrival is a storyboard using the real captured hull, not new gameplay code.\nThe shared combat capture is the canopy destination, not a rebuilt HUD.\n\nAll numeric data is in inputs/spec-data.json. Placeholder prose is labeled.\nThe diamond is the shared canopy placeholder until ability glyphs are authored.\nFont choices are reused, with Plex Mono numbers per the spec-sheet ruling.\n\nFonts and stills are packed. Keep inputs/preview-flight alongside this file.\nThis blend becomes the source of record when the owner edits it.\nDo not rebuild over hand edits. Paint-over waits for owner approval.\n''')
s['Review state']='Blockout and arrival storyboard awaiting owner pick. No paint-over.'
s['House angle']=40
s['Data commit']=DATA['sourceCommit']
s.frame_set(46)
bpy.context.view_layer.update()
for o in bpy.context.selected_objects:o.select_set(False)
console.select_set(True);bpy.context.view_layer.objects.active=console
for sc in bpy.data.screens:
    for area in sc.areas:
        if area.type=='VIEW_3D':
            area.spaces.active.region_3d.view_perspective='CAMERA'
            area.spaces.active.shading.type='MATERIAL'
            area.spaces.active.overlay.show_extras=False
s.render.filepath=str(ROOT/'renders/blockout-valis.png')
bpy.ops.wm.save_as_mainfile(filepath=str(OUT))
bpy.ops.file.make_paths_relative()
bpy.ops.wm.save_as_mainfile(filepath=str(OUT))
bpy.ops.render.render(write_still=True)
print(json.dumps({'blend':str(OUT),'objects':len(s.objects),'cameras':sum(o.type=='CAMERA' for o in s.objects)}))
