import bpy
import math
from mathutils import Vector
from pathlib import Path

out = Path('D:/amind/git/agent-4/src/Asteroids3D/Assets/Visuals/Environment/Asteroids/DrawnStudy')
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.mesh.primitive_uv_sphere_add(segments=96, ring_count=64, radius=1)
rock = bpy.context.object
rock.name = 'AsteroidFractureStudy'
mesh = rock.data
uv = mesh.uv_layers.active
for face in mesh.polygons:
    coords=[]
    for li in face.loop_indices:
        d=mesh.vertices[mesh.loops[li].vertex_index].co.normalized()
        coords.append([math.atan2(d.y,d.x)/(2*math.pi)+.5, math.asin(max(-1,min(1,d.z)))/math.pi+.5])
    if max(c[0] for c in coords)-min(c[0] for c in coords)>.5:
        for c in coords:
            if c[0]<.5: c[0]+=1
    for li,c in zip(face.loop_indices,coords): uv.data[li].uv=c

# Cavities follow selected black pockets in the painted UV sheet.
pockets = [(.15,.84,.085,.10,.34),(.28,.72,.065,.10,.22),(.70,.85,.07,.12,.36),
           (.63,.57,.06,.11,.28),(.91,.64,.06,.09,.30),(.23,.32,.055,.13,.26),
           (.76,.22,.075,.085,.32),(.91,.15,.06,.08,.27),(.40,.10,.06,.08,.24)]
for vertex in mesh.vertices:
    d = vertex.co.normalized()
    u = math.atan2(d.y,d.x)/(2*math.pi)+.5
    v = math.asin(max(-1,min(1,d.z)))/math.pi+.5
    radius = 1 + .075*math.sin(d.x*7+d.z*3)*math.cos(d.y*6-d.z*4)
    radius += .09*math.sin(d.z*7+d.y*3) + .05*math.cos(d.x*11+d.y*4)
    for pu,pv,wu,wv,depth in pockets:
        du = min(abs(u-pu),1-abs(u-pu))
        t = math.sqrt((du/wu)**2+((v-pv)/wv)**2)
        radius -= depth * max(0,1-t*t)**.65
        radius += .045*math.exp(-((t-1.04)/.18)**2)
    taper = 1-.40*max(0,d.z+.1)
    vertex.co = Vector((d.x*radius*taper*1.17+.28*d.z+.10*d.z*d.z,
                        d.y*radius*taper*.91+.07*math.sin(d.z*5),
                        d.z*radius*1.60))

decimate = rock.modifiers.new('Uneven fracture planes','DECIMATE')
decimate.ratio = .36
bpy.ops.object.modifier_apply(modifier=decimate.name)
for face in rock.data.polygons:
    face.use_smooth = True
mat=bpy.data.materials.new('Slate and olive fracture paint')
mat.use_nodes=True
tex=mat.node_tree.nodes.new('ShaderNodeTexImage')
tex.image=bpy.data.images.load(str(out/'AsteroidFracturePaint.png'))
tex.image.pack()
bsdf=mat.node_tree.nodes.get('Principled BSDF')
mat.node_tree.links.new(tex.outputs['Color'],bsdf.inputs['Base Color'])
bsdf.inputs['Roughness'].default_value=.95
rock.data.materials.append(mat)
bpy.ops.wm.save_as_mainfile(filepath='D:/amind/git/agent-4/art/asteroid-study/AsteroidFractureStudy.blend')
bpy.ops.export_scene.fbx(filepath=str(out/'AsteroidFractureStudy.fbx'),use_selection=True,axis_forward='-Z',axis_up='Y',global_scale=1,apply_unit_scale=True,bake_space_transform=True,object_types={'MESH'},use_mesh_modifiers=True,add_leaf_bones=False,bake_anim=False,path_mode='AUTO')
print('ROCK',len(rock.data.vertices),len(rock.data.polygons),list(rock.dimensions))


