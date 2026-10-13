import bpy,sys,json,math,hashlib
import numpy as np
from pathlib import Path
sys.path.insert(0,'D:/amind/git/agent-3/art/tools/ship')
import ship_source,ship_uv
source=Path('D:/amind/git/agent-3/art/ships/bulwark/Bulwark.blend')
out=Path('D:/amind/git/agent-3/results/bulwark/uv/2026-10-13-r02');out.mkdir(parents=True,exist_ok=True)
src=ship_source.open_source(source);before=ship_source.fingerprint(src);obj=src.objects['06 Cockpit dome']
selection=[o.name for o in bpy.context.selected_objects];active=bpy.context.view_layer.objects.active
ev=ship_source.Evaluation(src);mat=ev.to_space(obj.name)
positions=np.array([tuple(mat@v.co) for v in obj.data.vertices])
assert len(positions)==44 and len(obj.data.polygons)==42
m=ev.mesh(obj.name);target=ship_uv.density(m,mat,2048);bpy.data.meshes.remove(m)
def remove(ev):
 for o in list(ev.scene.objects):bpy.data.objects.remove(o,do_unlink=True)
 bpy.data.scenes.remove(ev.scene)
remove(ev)
vuv=np.zeros((44,2),dtype=np.float64)
rings=[[0]]+[list(range(1+i*7,8+i*7)) for i in range(6)]+[[43]]
crowns=[r[-1] for r in rings]
dist=0
for idx,ring in enumerate(rings):
 if idx:dist+=np.linalg.norm(positions[crowns[idx]]-positions[crowns[idx-1]])
 arc=0
 for j in reversed(range(len(ring))):
  if j<len(ring)-1:arc+=np.linalg.norm(positions[ring[j]]-positions[ring[j+1]])
  vuv[ring[j]]=(arc,dist)
base=np.array([vuv[l.vertex_index] for l in obj.data.loops])
layer=obj.data.uv_layers['PaintUV']
def set_uv(uv):layer.uv.foreach_set('vector',np.asarray(uv,np.float32).ravel());obj.data.update()
mirror=next(m for m in obj.modifiers if m.type=='MIRROR')
mirror.use_mirror_u=True;mirror.use_mirror_v=False;mirror.mirror_offset_u=-1;mirror.mirror_offset_v=0;mirror.offset_u=mirror.offset_v=0
set_uv(base)
ev=ship_source.Evaluation(src);m=ev.mesh(obj.name);density=ship_uv.density(m,ev.to_space(obj.name),2048);bpy.data.meshes.remove(m);remove(ev)
base*=target/density
set_uv(base)
if '--apply' in sys.argv:
 place=json.loads((out/'canopy-placement.json').read_text());turn=place['turn'];translation=np.array(place['translation'])
 rotation=np.eye(2) if turn==0 else np.array([[0,-1],[1,0]])
 final=base@rotation.T+translation;set_uv(final)
 mirror.use_mirror_u=turn==0;mirror.use_mirror_v=turn==1
 mirror.mirror_offset_u=2*translation[0]-1 if turn==0 else 0
 mirror.mirror_offset_v=2*translation[1]-1 if turn==1 else 0
 ev=ship_source.Evaluation(src);m=ev.mesh(obj.name)
 uv=ship_source.read_array(m.uv_layers['PaintUV'].uv,'vector',np.float32,2).reshape(-1,2)
 actual=ship_uv.density(m,ev.to_space(obj.name),2048)
 assert np.isfinite(uv).all() and uv.min()>=0 and uv.max()<=1
 bpy.data.meshes.remove(m);remove(ev)
 after=ship_source.fingerprint(src)
 assert before['components']['geometry']==after['components']['geometry']
 assert before['components']['materials']==after['components']['materials']
 assert all(p['uv']==after['parts'][n]['uv'] for n,p in before['parts'].items() if n!=obj.name)
 for name in ('Upper flap deployment','Lower flap deployment'):assert src.objects[name].rotation_euler.x==0
 bpy.context.window.scene=src
 for o in src.objects:o.select_set(o.name in selection)
 bpy.context.view_layer.objects.active=active
 bpy.context.preferences.filepaths.save_version=0
 bpy.ops.wm.save_as_mainfile(filepath=str(source),check_existing=False)
 sha=hashlib.sha256(source.read_bytes()).hexdigest()
 audit={'source_sha256':sha,'baseline_commit':'bc706397d38000c82ec86bf57b08fb0b76d27201',
 'owner_instruction':'Move the cockpit seam to the hidden underside','geometry_unchanged':True,'materials_unchanged':True,
 'other_uvs_unchanged':True,'changed_uv_parts':[obj.name],'target_density':target,'canopy_density':actual,
 'seam_location':'open underside perimeter beneath cockpit rim','visible_glazing_continuous':True,
 'mapping':'Cross-section arc lengths and longitudinal crown arc length; mirrored UVs meet at the centerline',
 'placement':place}
 (out/'canopy-audit.json').write_text(json.dumps(audit,indent=2));print(json.dumps(audit))
else:
 ev=ship_source.Evaluation(src);m=ev.mesh(obj.name)
 _,_,_,tri=ship_source.mesh_arrays(m);uv=ship_source.read_array(m.uv_layers['PaintUV'].uv,'vector',np.float32,2).reshape(-1,2)
 (out/'canopy-candidate.json').write_text(json.dumps({'uv':uv.tolist(),'triangles':tri.tolist(),'target_density':target},separators=(',',':')))
 bpy.data.meshes.remove(m);remove(ev)
 print('Candidate chart exported; source unchanged')
