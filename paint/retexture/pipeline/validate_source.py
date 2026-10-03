import bpy,json,hashlib,struct,sys
from pathlib import Path
import argparse
parser=argparse.ArgumentParser(description='Verify preserved Vanguard geometry and UV authoring.')
parser.add_argument('--repo-root',type=Path,required=True)
parser.add_argument('--output-dir',type=Path,required=True)
args=parser.parse_args(sys.argv[sys.argv.index('--')+1:])
ROOT,OUT=args.repo_root,args.output_dir
def digest(values):return hashlib.sha256(struct.pack('<'+'f'*len(values),*values)).hexdigest()
def inspect(path):
    bpy.ops.wm.open_mainfile(filepath=str(path))
    scenes={}
    for scene in bpy.data.scenes:
        objects={}
        for o in scene.objects:
            if o.type!='MESH':continue
            mesh=o.data
            coords=[x for v in mesh.vertices for x in v.co]
            topology=[list(p.vertices) for p in mesh.polygons]
            uvs={layer.name:digest([x for loop in layer.data for x in loop.uv]) for layer in mesh.uv_layers}
            normals=digest([x for normal in mesh.corner_normals for x in normal.vector])
            objects[o.name]={'coordinates':digest(coords),'topology':hashlib.sha256(json.dumps(topology).encode()).hexdigest(),'uvs':uvs,'normals':normals,'transform':digest([x for row in o.matrix_world for x in row]),'modifiers':[(m.name,m.type) for m in o.modifiers]}
        scenes[scene.name]=objects
    return scenes
source=ROOT/'art/ships/vanguard/drawn-study/VanguardStructure.blend'
painted=ROOT/'art/ships/vanguard/drawn-study/VanguardPainted.blend'
a=inspect(source);b=inspect(painted)
assert a==b,'Geometry, UV, normal, transform or modifier changes detected.'
scene=bpy.data.scenes['Vanguard - Texture MVP']
assert scene.objects['Vanguard surface wear'].hide_render
assert scene.objects['Vanguard service panels'].hide_render
atlas=bpy.data.images['Vanguard vivid']
assert tuple(atlas.size)==(4096,4096) and atlas.packed_file
report={'source':str(source.relative_to(ROOT)),'source_sha256':hashlib.sha256(source.read_bytes()).hexdigest(),'painted_source':str(painted.relative_to(ROOT)),'painted_source_sha256':hashlib.sha256(painted.read_bytes()).hexdigest(),'unchanged_all_scene_geometry_uvs_normals_transforms_modifiers':True,'painted_atlas_size':list(atlas.size),'painted_atlas_packed':True,'wear_hidden':True,'service_overlay_hidden':True,'scene_mesh_counts':{n:len(o) for n,o in a.items()},'geometry_fingerprints':a}
(OUT/'source-validation.json').write_text(json.dumps(report,indent=2))
print(json.dumps({k:v for k,v in report.items() if k!='geometry_fingerprints'}),flush=True)
