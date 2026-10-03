import bpy, bmesh, math
from pathlib import Path
from mathutils.bvhtree import BVHTree
from mathutils.kdtree import KDTree

root = Path('D:/amind/git/agent-4')
for number in range(1, 11):
    path = root / f'art/asteroid-field/Asteroid{number}.blend'
    bpy.ops.wm.open_mainfile(filepath=str(path))
    rock = bpy.data.objects[f'Asteroid{number}']
    drawing = bpy.data.objects[f'Asteroid{number}Drawing']
    assert drawing.vertex_groups.get('Fine crosshatching') is None
    mesh = rock.data
    radius = max(v.co.length for v in mesh.vertices)
    mesh.calc_loop_triangles()
    bvh = BVHTree.FromPolygons([v.co for v in mesh.vertices], [list(t.vertices) for t in mesh.loop_triangles], all_triangles=True)
    tree = KDTree(len(drawing.data.vertices))
    for v in drawing.data.vertices:
        tree.insert(v.co, v.index)
    tree.balance()
    bm = bmesh.new()
    bm.from_mesh(mesh)
    bm.normal_update()
    candidates = []
    for edge in bm.edges:
        angle = edge.calc_face_angle(0)
        if not .12 < angle < 1.15:
            continue
        midpoint = (edge.verts[0].co + edge.verts[1].co) * .5
        direction = (edge.verts[1].co - edge.verts[0].co).normalized()
        for face in edge.link_faces:
            inward = face.calc_center_median() - midpoint
            inward -= direction * inward.dot(direction)
            if inward.length < .001 * radius:
                continue
            center, normal, _, _ = bvh.find_nearest(midpoint + inward.normalized() * radius * .12)
            gap = tree.find(center)[2] / radius
            if .065 < gap < .23:
                candidates.append((angle * edge.calc_length(), center.copy(), normal.copy(), direction.copy()))
    candidates.sort(key=lambda item: item[0], reverse=True)
    verts = [v.co.copy() for v in drawing.data.vertices]
    faces = [list(f.vertices) for f in drawing.data.polygons]
    original_count = len(verts)
    centers = []
    stroke_count = 0

    def stroke(points, width):
        samples = []
        for a, b in zip(points, points[1:]):
            count = max(2, math.ceil((b - a).length / (radius * .004)))
            samples.extend(a.lerp(b, i / count) for i in range(count))
        samples.append(points[-1])
        projected = [bvh.find_nearest(p) for p in samples]
        if any(tree.find(p)[2] < radius * .023 for p, n, _, d in projected):
            return False
        if any(n.dot(projected[0][1]) < .72 or d > radius * .035 for p, n, _, d in projected):
            return False
        start = len(verts)
        for i, (p, n, _, _) in enumerate(projected):
            tangent = (samples[min(i + 1, len(samples) - 1)] - samples[max(0, i - 1)]).normalized()
            side = n.cross(tangent).normalized()
            t = i / (len(samples) - 1)
            pressure = math.sin(math.pi * t) ** .5 * (.87 + .13 * math.sin(t * 8 + number))
            for sign in (-1, 1):
                q, normal, _, _ = bvh.find_nearest(p + side * (width * radius * pressure * sign))
                verts.append(q + normal * (radius * .0075))
        for i in range(len(samples) - 1):
            a = start + i * 2
            faces.append((a, a + 1, a + 3, a + 2))
        return True

    for _, center, normal, ridge in candidates:
        if len(centers) >= 14:
            break
        if any((center - other).length < radius * .29 for other in centers):
            continue
        tangent = (ridge - normal * ridge.dot(normal)).normalized()
        across = normal.cross(tangent).normalized()
        direction = (tangent * .78 + across * .63).normalized()
        side = normal.cross(direction).normalized()
        patch_strokes = 0
        for j in range(4):
            c = center + side * radius * ((j - 1.5) * .037)
            length = radius * (.15, .19, .175, .12)[j]
            points = [c - direction * length * .5, c + side * radius * .004, c + direction * length * .5]
            patch_strokes += stroke(points, .0045)
        if patch_strokes >= 3:
            for j in range(2):
                c = center + direction * radius * ((j - .5) * .052)
                crossing = (side * .94 + direction * .34).normalized()
                patch_strokes += stroke([c - crossing * radius * .072, c + crossing * radius * .06], .0036)
            nick = center + side * radius * .12 + direction * radius * .035
            patch_strokes += stroke([nick, nick + direction * radius * .035 + side * radius * .018, nick + direction * radius * .055], .004)
        if patch_strokes:
            centers.append(center)
            stroke_count += patch_strokes
    bm.free()
    assert len(centers) == 14, (number, len(centers))
    old = drawing.data
    new = bpy.data.meshes.new('Ridge graphite with fine crosshatching')
    new.from_pydata(verts, [], faces)
    new.update()
    for material in old.materials:
        new.materials.append(material)
    for face in new.polygons:
        _, normal, _, _ = bvh.find_nearest(face.center)
        if face.normal.dot(normal) < 0:
            face.flip()
        face.use_smooth = True
    drawing.data = new
    bpy.data.meshes.remove(old)
    group = drawing.vertex_groups.new(name='Fine crosshatching')
    group.add(list(range(original_count, len(verts))), 1, 'REPLACE')
    drawing['hatch_patch_count'] = len(centers)
    drawing['hatch_stroke_count'] = stroke_count
    bpy.ops.object.select_all(action='DESELECT')
    drawing.select_set(True)
    bpy.context.view_layer.objects.active = drawing
    out = root / f'src/Asteroids3D/Assets/Visuals/Environment/Asteroids/DrawnField/Shape{number:02}/Asteroid{number}Drawing.fbx'
    bpy.ops.export_scene.fbx(filepath=str(out), use_selection=True, axis_forward='-Z', axis_up='Y', apply_unit_scale=True, bake_space_transform=True, object_types={'MESH'}, add_leaf_bones=False, bake_anim=False)
    bpy.ops.wm.save_as_mainfile(filepath=str(path))
    print('HATCH_FIELD', number, 'patches', len(centers), 'strokes', stroke_count, 'added_vertices', len(verts) - original_count, flush=True)
