import bpy,json,hashlib
from pathlib import Path
BASE=Path('D:/amind/git/agent-7/art/ships/crimson');WORK=Path('C:/Users/amind/.codex/artifact-archives/crimson-texturing-20260928-010254')
before=json.loads((WORK/'before.json').read_text())
bpy.ops.wm.open_mainfile(filepath=str(WORK/'Textured-candidate.blend'))
def geom(o):return {'vertices':[list(v.co) for v in o.data.vertices],'faces':[list(p.vertices) for p in o.data.polygons],'matrix':[list(r) for r in o.matrix_world],'modifiers':[[m.name,m.type,m.mirror_object.name if m.type=='MIRROR' and m.mirror_object else None] for m in o.modifiers]}
assert {o.name:geom(o) for o in bpy.context.scene.objects if o.type=='MESH'}==before['meshes'],'Geometry changed during texturing'
# Keep the approved part colors available in solid Material mode as well.
for o in bpy.context.scene.objects:
 if o.type=='MESH':
  assert len(o.data.uv_layers.active.data)==len(o.data.loops)
  assert all(0<=c<=1 for d in o.data.uv_layers.active.data for c in d.uv)
for m in list(bpy.data.materials):
 if m.name.startswith('Paint source - '):bpy.data.materials.remove(m)
for i in bpy.data.images:
 if i.name=='Crimson painted base color':i.filepath='//textures/Crimson_BaseColor.png'
 if Path(i.filepath).name in ('original-top.png','approved-profile.png'):
  filename=Path(i.filepath).name;i.filepath=str(BASE/'reference'/filename);i.reload();i.pack();i.filepath='//reference/'+filename
bpy.ops.object.select_all(action='DESELECT')
for name in before['selected']:bpy.data.objects[name].select_set(True)
if before['active']:bpy.context.view_layer.objects.active=bpy.data.objects[before['active']]
for screen in bpy.data.screens:
 for area in screen.areas:
  if area.type=='VIEW_3D':
   sh=area.spaces.active.shading;sh.type='SOLID';sh.color_type='TEXTURE';sh.light='FLAT';sh.show_cavity=True;sh.cavity_type='BOTH';sh.show_object_outline=True;sh.object_outline_color=(.008,.01,.016)
bpy.context.scene.render.engine='BLENDER_EEVEE'
bpy.ops.wm.save_as_mainfile(filepath=str(BASE/'Crimson.blend'),relative_remap=False)
report={'source':'Crimson.blend','geometry_unchanged':True,'mesh_parts':len(before['meshes']),'live_mirrors':sum(m.type=='MIRROR' for o in bpy.context.scene.objects for m in o.modifiers),'uv_map':'CrimsonPaintUV','base_color':'textures/Crimson_BaseColor.png','texture_size':[4096,4096],'image_packed':True,'style_reference':'reference/approved-texture-concept.png','texture_sha256':hashlib.sha256((BASE/'textures/Crimson_BaseColor.png').read_bytes()).hexdigest()}
(BASE/'textures/texture-manifest.json').write_text(json.dumps(report,indent=2));print(json.dumps(report))


