import bpy
import bmesh
import math
import random
from mathutils import Vector
from pathlib import Path

out = Path('D:/amind/git/agent-4/src/Asteroids3D/Assets/Visuals/Environment/Asteroids/DrawnStudy')
bpy.ops.wm.read_factory_settings(use_empty=True)
bm = bmesh.new()
bmesh.ops.create_cube(bm, size=4)
rng = random.Random(685)
directions = [Vector(v) for v in [(1,0,0),(-1,0,0),(0,1,0),(0,-1,0),(0,0,1),(0,0,-1)]]
for i in range(24):
    z = 1 - 2*(i+.5)/24
    a = i * math.pi*(3-math.sqrt(5))
    directions.append(Vector((math.sqrt(1-z*z)*math.cos(a), math.sqrt(1-z*z)*math.sin(a), z)))
for i, n in enumerate(directions):
    radius = 1.08 + rng.uniform(-.16,.1)
    result = bmesh.ops.bisect_plane(bm, geom=list(bm.verts)+list(bm.edges)+list(bm.faces), dist=.00001, plane_co=n*radius, plane_no=n, clear_outer=True)
    boundary = [e for e in result['geom_cut'] if isinstance(e,bmesh.types.BMEdge) and e.is_boundary]
    if boundary:
        bmesh.ops.holes_fill(bm, edges=boundary, sides=0)
bmesh.ops.recalc_face_normals(bm, faces=list(bm.faces))
mesh=bpy.data.meshes.new('Asteroid chipped planes')
bm.to_mesh(mesh)
bm.free()
rock=bpy.data.objects.new('AsteroidPaintStudy',mesh)
bpy.context.collection.objects.link(rock)
bpy.context.view_layer.objects.active=rock
rock.select_set(True)
for v in mesh.vertices:
    p=v.co
    p.x *= 1.21
    p.y *= .95
    p.z *= 1.38
    p.x += .10*p.z*p.z-.06
bevel=rock.modifiers.new('Chipped edge bevels','BEVEL')
bevel.width=.055
bevel.segments=3
bevel.affect='EDGES'
bpy.ops.object.modifier_apply(modifier=bevel.name)
for center, scale in [((1.27,-.12,.58),(.44,.55,.65)),((-.88,-.85,-.45),(.48,.34,.52))]:
    bpy.ops.mesh.primitive_ico_sphere_add(subdivisions=1,radius=1,location=center)
    cutter=bpy.context.object
    cutter.scale=scale
    bpy.context.view_layer.objects.active=rock
    cut=rock.modifiers.new('Large fracture recess','BOOLEAN')
    cut.operation='DIFFERENCE'
    cut.object=cutter
    bpy.ops.object.modifier_apply(modifier=cut.name)
    bpy.data.objects.remove(cutter,do_unlink=True)
rock.select_set(True)
bpy.context.view_layer.objects.active=rock
for layer in list(rock.data.uv_layers):
    rock.data.uv_layers.remove(layer)
uv=rock.data.uv_layers.new(name='Paint UV')
for face in rock.data.polygons:
    coords=[]
    for li in face.loop_indices:
        p=rock.data.vertices[rock.data.loops[li].vertex_index].co
        d=Vector((p.x/1.21,p.y/.95,p.z/1.38)).normalized()
        coords.append([math.atan2(d.y,d.x)/(2*math.pi)+.5, math.asin(max(-1,min(1,d.z)))/math.pi+.5])
    if max(c[0] for c in coords)-min(c[0] for c in coords)>.5:
        for c in coords:
            if c[0]<.5: c[0]+=1
    for li,c in zip(face.loop_indices,coords): uv.data[li].uv=c
mat=bpy.data.materials.new('Painted mauve stone')
mat.use_nodes=True
tex=mat.node_tree.nodes.new('ShaderNodeTexImage')
tex.image=bpy.data.images.load(str(out/'AsteroidPaint.png'))
tex.image.pack()
bsdf=mat.node_tree.nodes.get('Principled BSDF')
mat.node_tree.links.new(tex.outputs['Color'],bsdf.inputs['Base Color'])
bsdf.inputs['Roughness'].default_value=.95
rock.data.materials.append(mat)
bpy.ops.wm.save_as_mainfile(filepath='D:/amind/git/agent-4/art/asteroid-study/AsteroidPaintStudy.blend')
bpy.ops.export_scene.fbx(filepath=str(out/'AsteroidPaintStudy.fbx'),use_selection=True,axis_forward='-Z',axis_up='Y',global_scale=1,apply_unit_scale=True,bake_space_transform=True,object_types={'MESH'},use_mesh_modifiers=True,add_leaf_bones=False,bake_anim=False,path_mode='AUTO')
print('ROCK',len(rock.data.vertices),len(rock.data.polygons),list(rock.dimensions))
