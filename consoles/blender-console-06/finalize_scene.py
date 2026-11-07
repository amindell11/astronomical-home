"""Finish a reviewed adaptation without changing its source file.

Run Blender --background draft-04.blend --python finalize_scene.py -- --output review-01.blend.
Writes the new packed blend, hero.png, angled.png, and scene-manifest.json. Existing output fails.
"""
import bpy, math, json, hashlib, sys
from pathlib import Path
from mathutils import Vector, Matrix
from bpy_extras.object_utils import world_to_camera_view

ROOT=Path(__file__).resolve().parent
args=sys.argv[sys.argv.index('--')+1:]
out=ROOT/args[args.index('--output')+1]
if out.exists():raise RuntimeError('Output already exists: '+str(out))
s=bpy.context.scene
bpy.context.view_layer.update()
hero=bpy.data.objects['Hero camera']
s.camera=hero
original=ROOT.parent/'refinement-04/inputs/owner-edited.blend'
source_hash=hashlib.sha256(original.read_bytes()).hexdigest()
archive=bpy.data.collections['99 Owner source archive — hidden']
archive.name='99 Source archive (hidden)'
active=bpy.data.collections['11 Composition additions']
def update():bpy.context.view_layer.update()
def preserve_children(root,worlds):
    update()
    for o,matrix in worlds.items():o.matrix_parent_inverse=root.matrix_world.inverted()@matrix@o.matrix_basis.inverted()
    update()
def orient_root(name,normal):
    root=bpy.data.objects[name];update();worlds={o:o.matrix_world.copy() for o in root.children}
    right=Vector((1,0,0));right=(right-normal*right.dot(normal)).normalized();up=normal.cross(right).normalized()
    m=Matrix((right,up,normal)).transposed().to_4x4();m.translation=root.matrix_world.translation
    if root.parent:root.matrix_parent_inverse=root.parent.matrix_world.inverted()@m@root.matrix_basis.inverted()
    else:root.matrix_world=m
    preserve_children(root,worlds)

glass=bpy.data.objects['Display glass with rear enclosure']
normal=(glass.matrix_world.to_3x3().inverted().transposed()@Vector((0,-1,0))).normalized()
for name in ['Selector knob','Previous button','Next button','Detail display']:orient_root(name,normal)
orient_root('Launch control',Vector((0,-.60,.80)).normalized())
orient_root('Projection controls',Vector((0,-.48,.88)).normalized())

origin_adjustments=[]
for o in list(s.objects):
    if o.hide_render or o.name.startswith(('SOURCE |','Vanguard /')) or o.type not in {'MESH','CURVE'}:continue
    if archive in o.users_collection:continue
    update();center=sum((Vector(v)for v in o.bound_box),Vector())/8
    worlds={child:child.matrix_world.copy()for child in o.children}
    desired=o.matrix_world@Matrix.Translation(center)
    o.data.transform(Matrix.Translation(-center))
    if o.parent:o.matrix_parent_inverse=o.parent.matrix_world.inverted()@desired@o.matrix_basis.inverted()
    else:o.matrix_world=desired
    preserve_children(o,worlds);origin_adjustments.append(o.name)

for name,target_height in [('DISPLAY | engraved label',14),('PROJECTION | engraved label',11),('LAUNCH | current editable face',22),('Previous | engraved arrow',16),('Next | engraved arrow',16)]:
    o=bpy.data.objects[name];update()
    projected=[world_to_camera_view(s,hero,o.matrix_world@Vector(v))for v in o.bound_box]
    height=(max(p.y for p in projected)-min(p.y for p in projected))*941
    o.data.size*=target_height/height
    update()

label=bpy.data.objects['PROJECTION | engraved label']
desired=label.matrix_world.copy();desired.translation.x+=.75
label.matrix_parent_inverse=label.parent.matrix_world.inverted()@desired@label.matrix_basis.inverted()
update()

for o in s.objects:
    if o.name.startswith('Console | captive corner bolt') and not o.hide_render:
        p=world_to_camera_view(s,hero,o.matrix_world.translation)
        if p.x*1672>480 and (1-p.y)*941>830:o.hide_render=True;o.hide_set(True)
trim=bpy.data.materials.new('Viewport | dark inner lip');trim.use_nodes=True
bsdf=trim.node_tree.nodes.get('Principled BSDF');bsdf.inputs['Base Color'].default_value=(.075,.065,.055,1);bsdf.inputs['Metallic'].default_value=.25;bsdf.inputs['Roughness'].default_value=.6
lip=bpy.data.objects['Viewport | inner top metal lip'];lip.data.materials.clear();lip.data.materials.append(trim)
volume=bpy.data.materials['06 | Warm volumetric projection'].node_tree.nodes.get('Principled Volume')
volume.inputs['Density'].default_value=.014;volume.inputs['Emission Strength'].default_value=.045
for state in ['lasers','missiles','dash']:
    image=bpy.data.images.load(str(ROOT/'inputs/ui'/('detail-'+state+'.png')),check_existing=True);image.pack();image.use_fake_user=True

camdata=bpy.data.cameras.new('Angled construction camera');angle=bpy.data.objects.new('Angled construction camera',camdata);active.objects.link(angle)
angle.location=(15,-24,16);target=Vector((0,-.6,3.6));angle.rotation_euler=(target-angle.location).to_track_quat('-Z','Y').to_euler()
camdata.type='ORTHO';camdata.ortho_scale=23;camdata.clip_end=200
guide=bpy.data.texts.get('START HERE - editable 3D console') or bpy.data.texts.new('START HERE - editable 3D console')
guide.clear();guide.write('''CONSOLE B / MEDIUM DETAIL REVIEW

This is a new copy of the saved owner-edited scene, fitted to the approved composition.
The original remains untouched at refinement-04/inputs/owner-edited.blend.

MAIN CAMERA: Hero camera. ANGLED CAMERA: Angled construction camera.
The file opens at Hero camera. Numpad 0 toggles camera view.
Camera background images contains the packed approved composition; enable it for alignment.

ASSEMBLIES: Main console, Detail display, Selector knob, Previous button, Next button,
Deck, Projector, Three broad cooling fins, Projection controls, Launch control,
Cable looms, Hologram, Viewport.
Move an assembly parent to move all of its parts. Individual geometry origins sit near their parts.
Selector knob and button parents use local Z normal to their faces.
Selector knob: rotate local Z. Button caps: move along their face normal for press travel.
Cables remain editable Bezier curves; bevel modifiers remain live.

Screens are packed replaceable images on separate planes, not separate editable text glyphs.
Detail screen material: choose detail-hud.png, detail-lasers.png, detail-missiles.png,
or detail-dash.png in its Image Texture node. Graphics source remains the HTML prototype.
The external DISPLAY, PROJECTION, arrow, and LAUNCH labels are editable text objects.

99 Source archive (hidden) preserves the starting objects. Do not enable it for normal rendering.
The angled review render hides the distant nebula backdrop and projection light volume for clarity.
Final ink finish and surface wear have not been applied.

After you edit this file, preserve it as the source; do not rebuild over your saved edits.
''')
s['Source owner scene SHA256']=source_hash
s['Approved composition']='consoles/interactive-console-05/preview.png'
s['Stage']='Medium detail, before full surface finish'
s.render.resolution_x=1672;s.render.resolution_y=941;s.render.resolution_percentage=100
s.cycles.samples=64;s.cycles.use_denoising=True
for image in bpy.data.images:
    if image.has_data and not image.packed_file:image.pack()
for font in bpy.data.fonts:
    if font.filepath and font.filepath!='<builtin>' and not font.packed_file:font.pack()
for screen in bpy.data.screens:
    for a in screen.areas:
        if a.type=='VIEW_3D':
            a.spaces.active.region_3d.view_perspective='CAMERA';a.spaces.active.shading.type='MATERIAL'
            a.spaces.active.overlay.show_extras=False
for o in bpy.context.selected_objects:o.select_set(False)
main=bpy.data.objects['Main console'];main.select_set(True);bpy.context.view_layer.objects.active=main
s.render.filepath=str(ROOT/'hero.png')
bpy.ops.wm.save_as_mainfile(filepath=str(out))
bpy.ops.render.render(write_still=True)
roots=['Main console','Detail display','Selector knob','Previous button','Next button','Deck','Projector','Three broad cooling fins','Projection controls','Launch control','Cable looms','Hologram','Viewport']
manifest={'ownerSource':str(original),'ownerSourceSha256':source_hash,'copiedSourceSha256':hashlib.sha256((ROOT/'inputs/owner-source.blend').read_bytes()).hexdigest(),'blend':out.name,'heroCamera':hero.name,'angledCamera':angle.name,'assemblyRoots':roots,'centeredOrigins':origin_adjustments,'packedImages':[im.name for im in bpy.data.images if im.packed_file],'fonts':[f.name for f in bpy.data.fonts if f.packed_file],'mainResolution':[1672,941],'notes':['Screen content is packed native UI artwork; hardware labels remain Blender text.','Owner source is unchanged; real ship geometry is reused.','Angled render hides sky backdrop and light volume, using the same geometry.']}
s.camera=angle;s.render.resolution_y=1100
for name in ['Viewport | distant nebula backdrop','Projector | editable light volume']:bpy.data.objects[name].hide_render=True
s.world.node_tree.nodes.get('Background').inputs[0].default_value=(.045,.055,.07,1)
s.world.node_tree.nodes.get('Background').inputs[1].default_value=.45
s.render.filepath=str(ROOT/'angled.png');bpy.ops.render.render(write_still=True)
manifest['ownerSourceUnchanged']=hashlib.sha256(original.read_bytes()).hexdigest()==source_hash
(ROOT/'scene-manifest.json').write_text(json.dumps(manifest,indent=2))
if not manifest['ownerSourceUnchanged']:raise RuntimeError('Owner source changed')
print('FINALIZED '+str(out))
