"""Author the five canopy panes and the UV-space strokes used by export_glass.py."""
import json
import math

import bpy
from mathutils import Vector
from bpy_extras.object_utils import world_to_camera_view


GLASS_NAME = '25 | Viewport glass'


def _clip(points, a, b, inside):
    def distance(p):
        value = inside * ((b.x - a.x) * (p.y - a.y) - (b.y - a.y) * (p.x - a.x)) / (b - a).length
        return 0 if abs(value) < 1e-3 else value
    output = []
    for p, q in zip(points, points[1:] + points[:1]):
        dp, dq = distance(p), distance(q)
        if dp >= 0:
            output.append(p)
        if dp * dq < 0:
            output.append(p + (q - p) * dp / (dp - dq))
    return output


def _inset(points, amount):
    area = sum(p.x * q.y - q.x * p.y for p, q in zip(points, points[1:] + points[:1]))
    edges = []
    for p, q in zip(points, points[1:] + points[:1]):
        d = (q - p).normalized()
        n = Vector((-d.y, d.x)) * (1 if area > 0 else -1)
        edges.append((p + n * amount, d))
    result = []
    for (p, a), (q, b) in zip(edges[-1:] + edges[:-1], edges):
        cross = a.x * b.y - a.y * b.x
        if abs(cross) < 1e-7:
            result.append(q)
        else:
            d = q - p
            result.append(p + a * ((d.x * b.y - d.y * b.x) / cross))
    return result


def add_glass(dimensions, scene, camera, out_dir):
    eye = camera.matrix_world.translation.copy()
    rotation = camera.matrix_world.to_3x3()
    focal = scene.render.resolution_x * camera.data.lens / camera.data.sensor_width

    def project(p):
        p = world_to_camera_view(scene, camera, Vector(p))
        return Vector((p.x * 1920, p.y * 1080))

    def ray(pixel):
        # The calibrated camera shift belongs to deck_projection.game_projection.
        sample = camera.matrix_world @ Vector((0, 0, -1))
        centre = project(sample)
        return rotation @ Vector(((pixel.x - centre.x) / focal,
                                  (pixel.y - centre.y) / focal, -1))

    left_lo, right_lo = map(Vector, dimensions['STRUT_LANDING_POINTS'])
    left_hi, right_hi = map(Vector, dimensions['STRUT_TOP_POINTS'])
    stations = [(-27, 8.4)] + list(dimensions['HEADER_STATIONS']) + [(27, 8.4)]
    envelope = [project((-27, 2.35, .15)), project((27, 2.35, .15))]
    envelope += [project((x, dimensions['HEADER_EDGE_Y'], z)) for x, z in reversed(stations)]
    strut_lines = [(project(lo), project(hi)) for lo, hi in ((left_lo, left_hi), (right_lo, right_hi))]
    pillar_lines = []
    for hand in (-1, 1):
        pillar_lines.append(tuple(project((hand * p[0], p[1], p[2]))
                                  for p in (dimensions['SIDE_PILLAR_FOOT'], dimensions['SIDE_PILLAR_TOP'])))
    centre = _clip(_clip(envelope, *strut_lines[0], -1), *strut_lines[1], 1)
    definitions = [('Centre', centre, (left_lo + right_lo) / 2 + Vector((0, .08, 0)),
                    Vector((1, 0, 0)), right_hi - right_lo)]
    for index, hand in enumerate((-1, 1)):
        lo, hi = ((left_lo, left_hi), (right_lo, right_hi))[index]
        side = _clip(_clip(envelope, *strut_lines[index], -hand), *pillar_lines[index], hand)
        outer = _clip(envelope, *pillar_lines[index], -hand)
        definitions.append(('Left side' if hand < 0 else 'Right side', side,
                            lo + Vector((0, .08, 0)), Vector((hand, -.08, 0)), hi - lo))
        foot, top = (Vector((hand * p[0], p[1], p[2]))
                     for p in (dimensions['SIDE_PILLAR_FOOT'], dimensions['SIDE_PILLAR_TOP']))
        definitions.append(('Left outer' if hand < 0 else 'Right outer', outer,
                            foot + Vector((0, .14, 0)), Vector((hand, -.025, 0)), top - foot))

    panes = []
    for name, outline, origin, across, along in definitions:
        u = across.normalized()
        v = (along - u * along.dot(u)).normalized()
        normal = u.cross(v)

        def intersect(pixel):
            direction = ray(pixel)
            return eye + direction * normal.dot(origin - eye) / normal.dot(direction)

        world = [intersect(p) for p in outline]
        local = [Vector(((p - origin).dot(u), (p - origin).dot(v))) for p in world]
        minimum = Vector((min(p.x for p in local), min(p.y for p in local)))
        maximum = Vector((max(p.x for p in local), max(p.y for p in local)))
        panes.append({'name': name, 'screen': outline, 'world': world, 'local': local,
                      'origin': origin, 'u': u, 'v': v, 'normal': normal,
                      'minimum': minimum, 'size': maximum - minimum})

    padding = .16
    rows = [[panes[0], panes[1], panes[3]], [panes[2], panes[4]]]
    used_width = max(sum(p['size'].x + padding for p in row) + padding for row in rows)
    used_height = sum(max(p['size'].y for p in row) + padding for row in rows) + padding
    atlas_height = max(used_height, used_width / 2)
    atlas_width = atlas_height * 2
    y = padding
    for row in rows:
        x = padding
        for pane in row:
            pane['offset'] = Vector((x, y))
            pane['uv'] = [Vector(((p.x - pane['minimum'].x + x) / atlas_width,
                                  (p.y - pane['minimum'].y + y) / atlas_height)) for p in pane['local']]
            x += pane['size'].x + padding
        y += max(p['size'].y for p in row) + padding

    vertices, faces, uv_values = [], [], []
    for pane in panes:
        indices = list(range(len(vertices), len(vertices) + len(pane['world'])))
        vertices.extend(pane['world'])
        uv_values.extend(pane['uv'])
        area = sum(p.x * q.y - q.x * p.y for p, q in zip(pane['local'], pane['local'][1:] + pane['local'][:1]))
        if area * pane['normal'].dot(eye - pane['origin']) < 0:
            indices.reverse()
        faces.append(indices)
    mesh = bpy.data.meshes.new('Canopy | five planar glazing panes')
    mesh.from_pydata(vertices, [], faces)
    mesh.update()
    uv = mesh.uv_layers.new(name='GlassUV')
    for polygon in mesh.polygons:
        for loop_index in polygon.loop_indices:
            uv.data[loop_index].uv = uv_values[mesh.loops[loop_index].vertex_index]
    obj = bpy.data.objects.new(GLASS_NAME, mesh)
    scene.collection.objects.link(obj)
    obj['export_role'] = 'ViewportGlass'

    protected = [project((x, y, z)) for x in (-2.5, 2.5) for y in (-2, 2) for z in (2.5, 6.5)]
    protected_bounds = [min(p.x for p in protected) - 14, max(p.x for p in protected) + 14,
                        min(p.y for p in protected) - 14, max(p.y for p in protected) + 14]
    strokes = []

    def stroke(pane, a, b, strength=.18):
        if (a - b).length < 1:
            return
        xmin, xmax, ymin, ymax = protected_bounds
        if min(a.x, b.x) <= xmax and max(a.x, b.x) >= xmin and min(a.y, b.y) <= ymax and max(a.y, b.y) >= ymin:
            return

        def physical(pixel):
            direction = ray(pixel)
            p = eye + direction * pane['normal'].dot(pane['origin'] - eye) / pane['normal'].dot(direction)
            return Vector(((p - pane['origin']).dot(pane['u']), (p - pane['origin']).dot(pane['v'])))

        pa, pb = physical(a), physical(b)
        midpoint = (a + b) / 2
        d = (b - a).normalized()
        width_offset = physical(midpoint + Vector((-d.y, d.x)) * .875) - physical(midpoint)
        tangent = (pb - pa).normalized()
        half_width = abs(width_offset.x * tangent.y - width_offset.y * tangent.x)
        shift = pane['offset'] - pane['minimum']
        strokes.append({'pane': pane['name'], 'a': list(pa + shift), 'b': list(pb + shift),
                        'screen_a': list(a), 'screen_b': list(b),
                        'half_width': half_width, 'strength': strength})

    for pane in panes:
        inset = _inset(pane['screen'], 22)
        for index, p in enumerate(inset):
            before, after = inset[index - 1], inset[(index + 1) % len(inset)]
            incoming, outgoing = (before - p).normalized(), (after - p).normalized()
            if abs(incoming.x * outgoing.y - incoming.y * outgoing.x) < 1e-5:
                continue
            for q in (before, after):
                distance = min(48, (q - p).length * .23)
                stroke(pane, p, p + (q - p).normalized() * distance)
        edges = list(zip(inset, inset[1:] + inset[:1]))
        # The long sloping edge beside each strut carries one broken registration line.
        slopes = [(a, b) for a, b in edges if abs(b.y - a.y) > 170 and abs(b.x - a.x) < abs(b.y - a.y) * .9]
        for a, b in slopes[:2]:
            d = b - a
            stroke(pane, a + d * .18, a + d * .43, .115)
            stroke(pane, a + d * .56, a + d * .81, .115)
        a, b = max(edges, key=lambda pair: (pair[0].y + pair[1].y) / 2)
        if pane['name'].endswith('outer') and abs(a.x - 960) > abs(b.x - 960):
            a, b = b, a
        d = (b - a).normalized()
        midpoint = (a + b) / 2
        interior = sum(inset, Vector((0, 0))) / len(inset) - midpoint
        n = Vector((-d.y, d.x))
        if n.dot(interior) < 0:
            n = -n
        for index in range(6):
            p = a + d * (min(70, (b - a).length * .3) + index * 9)
            stroke(pane, p, p + n * (6 if index % 3 else 10), .13)
        anchor = a + d * min(105, (b - a).length * .32) + n * 28
        for index, offset in enumerate((Vector((0, 0)), d * 25 + n * 14, d * 50)):
            centre = anchor + offset
            radius = 14 if index != 1 else 12
            points = [centre + d * math.cos(i * math.pi / 3) * radius + n * math.sin(i * math.pi / 3) * radius
                      for i in range(6)]
            for p, q in zip(points, points[1:] + points[:1]):
                stroke(pane, p, q, (.15, .105, .075)[index])

    material = bpy.data.materials.new('25 | Etched canopy perimeter')
    material.use_nodes = True
    material.diffuse_color = (.62, .79, 1, .02)
    nodes, links = material.node_tree.nodes, material.node_tree.links
    nodes.clear()

    def math_node(operation, a, b=None):
        node = nodes.new('ShaderNodeMath')
        node.operation = operation
        for index, value in enumerate((a, b)):
            if value is None:
                continue
            if isinstance(value, (int, float)):
                node.inputs[index].default_value = value
            else:
                links.new(value, node.inputs[index])
        return node.outputs[0]

    def vector_node(operation, a, b=None):
        node = nodes.new('ShaderNodeVectorMath')
        node.operation = operation
        for index, value in enumerate((a, b)):
            if value is None:
                continue
            if isinstance(value, (tuple, list, Vector)):
                node.inputs[index].default_value = tuple(value) + (0,) if len(value) == 2 else value
            else:
                links.new(value, node.inputs[index])
        return node.outputs['Value'] if operation in {'DOT_PRODUCT', 'LENGTH'} else node.outputs['Vector']

    uv_node = nodes.new('ShaderNodeUVMap')
    uv_node.uv_map = 'GlassUV'
    coordinates = vector_node('MULTIPLY', uv_node.outputs['UV'], (atlas_width, atlas_height, 0))
    union = None
    for item in strokes:
        a, b = Vector(item['a']), Vector(item['b'])
        delta = vector_node('SUBTRACT', coordinates, a)
        d = b - a
        parameter = math_node('MULTIPLY', vector_node('DOT_PRODUCT', delta, d), 1 / d.length_squared)
        parameter = math_node('MINIMUM', math_node('MAXIMUM', parameter, 0), 1)
        scale = nodes.new('ShaderNodeVectorMath')
        scale.operation = 'SCALE'
        scale.inputs[0].default_value = (*d, 0)
        links.new(parameter, scale.inputs['Scale'])
        distance = vector_node('LENGTH', vector_node('SUBTRACT', delta, scale.outputs['Vector']))
        feather = item['half_width'] * .25
        line = math_node('MULTIPLY', math_node('SUBTRACT', item['half_width'] + feather, distance), 1 / (2 * feather))
        line = math_node('MINIMUM', math_node('MAXIMUM', line, 0), 1)
        line = math_node('MULTIPLY', line, item['strength'])
        union = line if union is None else math_node('MAXIMUM', union, line)
    clear = nodes.new('ShaderNodeBsdfTransparent')
    emission = nodes.new('ShaderNodeEmission')
    emission.inputs['Color'].default_value = (.66, .82, 1, 1)
    emission.inputs['Strength'].default_value = .35
    mix = nodes.new('ShaderNodeMixShader')
    mix.name = 'Only the perimeter catches light'
    links.new(union, mix.inputs[0])
    links.new(clear.outputs[0], mix.inputs[1])
    links.new(emission.outputs[0], mix.inputs[2])
    output = nodes.new('ShaderNodeOutputMaterial')
    output.name = 'Material Output'
    links.new(mix.outputs[0], output.inputs['Surface'])
    mesh.materials.append(material)

    report = {'object': obj.name, 'uv_map': 'GlassUV', 'atlas_physical_size': [atlas_width, atlas_height],
              'protected_screen_bounds_l_r_b_t': protected_bounds, 'stroke_width_pixels': 1.75,
              'panes': [{'name': p['name'], 'world': [list(v) for v in p['world']],
                         'uv': [list(v) for v in p['uv']], 'screen': [list(v) for v in p['screen']]}
                        for p in panes], 'strokes': strokes}
    obj['glazing_layout'] = json.dumps({k: report[k] for k in ('atlas_physical_size', 'protected_screen_bounds_l_r_b_t', 'stroke_width_pixels')})
    (out_dir / 'window-etching-layout.json').write_text(json.dumps(report, indent=2), encoding='utf-8')
    return obj
