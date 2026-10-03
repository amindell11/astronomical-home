import bpy
import bmesh
import numpy as np
from math import sin, pi
from mathutils import Vector
from pathlib import Path
import json

OUT = Path('D:/amind/git/astronomical-home/results/valis-wing-motion/v02')
GROUPS = {
    '10 upper swept wings': ['10 upper swept wings', '11 upper wing gray planes', '12 upper wing lavender tips'],
    '13 charcoal trailing spars': ['13 charcoal trailing spars', '12 upper wing lavender tips underside'],
    '20 lower swept blades': ['20 lower swept blades', '21 lower blade lavender tips', '21 lower blade lavender tips underside', '22 lower blade dark inner facets'],
    '50 aft split prongs': ['50 aft split prongs'],
}

def frame(obj):
    points = np.array([list(obj.matrix_world @ v.co) for v in obj.data.vertices])
    plane = np.linalg.lstsq(np.c_[points[:, :2], np.ones(len(points))], points[:, 2], rcond=None)[0]
    direction = points[points[:, 1].argmax(), :2] - points[points[:, 1].argmin(), :2] if obj.name.startswith('50 ') else points[points[:, 0].argmax(), :2] - points[points[:, 0].argmin(), :2]
    direction /= np.linalg.norm(direction)
    transverse = np.array([-direction[1], direction[0]])
    st = np.c_[points[:, :2] @ direction, points[:, :2] @ transverse]
    edges = {}
    for face in obj.data.polygons:
        for a, b in face.edge_keys:
            key = tuple(sorted((a, b)))
            edges[key] = edges.get(key, 0) + 1
    boundary = [(st[a], st[b]) for (a, b), count in edges.items() if count == 1]
    solid = next(m for m in obj.modifiers if m.type == 'SOLIDIFY')
    normal = np.cross(points[1] - points[0], points[2] - points[0])
    normal /= np.linalg.norm(normal)
    normal = normal if normal[2] >= 0 else -normal
    # Solve vertical separation between the two transformed parallel faces.
    local_normal = obj.data.polygons[0].normal
    delta = obj.matrix_world.to_3x3() @ (local_normal * -solid.thickness)
    thickness = abs(delta.z - plane[0] * delta.x - plane[1] * delta.y)
    return plane, direction, transverse, st[:, 0].min(), st[:, 0].max(), boundary, thickness

def section(p, f, prong):
    plane, direction, transverse, start, end, boundary, thickness = f
    s = float(np.dot(p[:2], direction))
    v = float(np.dot(p[:2], transverse))
    u = np.clip((s - start) / (end - start), 0, 1)
    intersections = []
    for a, b in boundary:
        if min(a[0], b[0]) - 1e-6 <= s <= max(a[0], b[0]) + 1e-6:
            if abs(b[0] - a[0]) < 1e-8:
                intersections.extend((a[1], b[1]))
            else:
                intersections.append(float(a[1] + (b[1] - a[1]) * (s - a[0]) / (b[0] - a[0])))
    q = 0 if len(intersections) < 2 or max(intersections) - min(intersections) < 1e-6 else np.clip((v - min(intersections)) / (max(intersections) - min(intersections)), 0, 1)
    taper = (.48 * (1-u) ** .7 + .08) if prong else (.40 * (1-u) ** .65 + .12)
    ridge = (.055 if prong else .060) * sin(pi * q) * (.75 * (1-u) + .25)
    center = plane[0] * p[0] + plane[1] * p[1] + plane[2] - thickness * .5
    return center + (p[2] - center) * taper + ridge

report = []
for primary, names in GROUPS.items():
    reference = bpy.data.objects[primary]
    f = frame(reference)
    for name in names:
        obj = bpy.data.objects[name]
        mirrors = [m for m in obj.modifiers if m.type == 'MIRROR']
        for m in mirrors:
            m.show_viewport = False
        bpy.context.view_layer.update()
        evaluated = obj.evaluated_get(bpy.context.evaluated_depsgraph_get())
        closed = bpy.data.meshes.new_from_object(evaluated, preserve_all_data_layers=True, depsgraph=bpy.context.evaluated_depsgraph_get())
        for m in mirrors:
            m.show_viewport = True
        source_points = [list(obj.matrix_world @ v.co) for v in closed.vertices]
        original = obj.data
        original.use_fake_user = True
        bm = bmesh.new()
        bm.from_mesh(closed)
        bmesh.ops.subdivide_edges(bm, edges=list(bm.edges), cuts=2, use_grid_fill=True)
        bm.to_mesh(closed)
        bm.free()
        obj.data = closed
        closed.name = name + ' tapered section'
        for modifier in list(obj.modifiers):
            if modifier.type == 'SOLIDIFY':
                obj.modifiers.remove(modifier)
        obj.shape_key_add(name='Original plate profile')
        profile = obj.shape_key_add(name='Thin sculpted profile')
        inverse = obj.matrix_world.inverted()
        max_xy_error = 0
        for original_vertex, shaped in zip(closed.vertices, profile.data):
            p = obj.matrix_world @ original_vertex.co
            shaped_world = Vector((p.x, p.y, section(np.array(p), f, primary.startswith('50 '))))
            shaped.co = inverse @ shaped_world
            actual = obj.matrix_world @ shaped.co
            max_xy_error = max(max_xy_error, abs(actual.x - p.x), abs(actual.y - p.y))
        profile.value = 1
        closed.update()
        report.append({'name':name,'vertices':len(closed.vertices),'xy_max_error':max_xy_error,'original_vertical_thickness':f[-1],'tip_thickness':f[-1]*(.08 if primary.startswith('50 ') else .12),'root_thickness':f[-1]*(.56 if primary.startswith('50 ') else .52)})
        assert max_xy_error < 2e-6, (name, max_xy_error)
bpy.context.view_layer.update()
OUT.joinpath('profile-proof.json').write_text(json.dumps(report, indent=2))
bpy.ops.wm.save_as_mainfile(filepath=str(OUT / 'Valis-profile-pass.blend'), copy=True)
result = {'parts':report,'saved_copy':str(OUT / 'Valis-profile-pass.blend'),'current_path':bpy.data.filepath}
