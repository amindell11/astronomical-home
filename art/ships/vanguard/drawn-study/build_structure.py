import bpy, json, math
from mathutils import Vector
from mathutils.bvhtree import BVHTree


def build(scene, out, layout_name, object_name):
    vertices, faces = [], []
    for line in json.loads((out/layout_name).read_text())['lines']:
        obj = scene.objects[line['object']]
        mesh = obj.data
        tree = BVHTree.FromPolygons([obj.matrix_world@v.co for v in mesh.vertices],
                                   [list(p.vertices) for p in mesh.polygons])
        knots = [Vector(p) for p in line['points']]
        samples = []
        for a, b in zip(knots, knots[1:]):
            steps = max(2, math.ceil((b-a).length/.004))
            samples.extend(a.lerp(b, i/steps) for i in range(steps))
        samples.append(knots[-1])
        previous = None
        for i, point in enumerate(samples):
            tangent = (samples[min(i+1,len(samples)-1)]-samples[max(0,i-1)]).normalized()
            side = Vector((-tangent.y,tangent.x))
            progress = i/(len(samples)-1)
            pressure = min(1,progress*12,(1-progress)*12) * (.86+.14*math.sin(progress*9))
            edge = []
            for sign in (-1,1):
                xy = point + side * sign * line['width'] * pressure / 2
                hit, normal, _, _ = tree.ray_cast(Vector((xy.x,xy.y,2)),Vector((0,0,-1)))
                if hit is None: break
                edge.append(hit+normal*line.get('offset', .0003))
            if len(edge) != 2:
                previous = None
                continue
            start = len(vertices)
            vertices.extend(edge)
            if previous is not None:
                for tri in [(previous,previous+1,start),(previous+1,start+1,start)]:
                    a,b,c = [vertices[j] for j in tri]
                    faces.append(tri if (b-a).cross(c-a).z>0 else tuple(reversed(tri)))
            previous = start
    
    mesh = bpy.data.meshes.new(object_name)
    mesh.from_pydata(vertices,[],faces)
    mesh.update()
    drawing = bpy.data.objects.new(object_name,mesh)
    scene.collection.objects.link(drawing)
    return drawing


def build_panels(scene, out):
    layout = json.loads((out/'service-panels.json').read_text())
    vertices, faces, colors = [], [], []
    for patch in layout['patches']:
        obj = scene.objects[patch['object']]
        obj.data.calc_loop_triangles()
        for sign in ([1, -1] if patch['mirror'] else [1]):
            polygon = [Vector((sign*x, y)) for x, y in patch['points']]
            area = sum(a.x*b.y-b.x*a.y for a, b in zip(polygon, polygon[1:]+polygon[:1]))
            if area < 0: polygon.reverse()
            count = len(faces)
            for triangle in obj.data.loop_triangles:
                normal = (obj.matrix_world.to_3x3() @ triangle.normal).normalized()
                if normal.z < .15: continue
                clipped = [obj.matrix_world @ obj.data.vertices[v].co for v in triangle.vertices]
                for a, b in zip(polygon, polygon[1:]+polygon[:1]):
                    edge = b-a
                    def distance(p): return edge.x*(p.y-a.y)-edge.y*(p.x-a.x)
                    result = []
                    for p, q in zip(clipped, clipped[1:]+clipped[:1]):
                        dp, dq = distance(p), distance(q)
                        if dp >= 0: result.append(p)
                        if (dp >= 0) != (dq >= 0): result.append(p.lerp(q, dp/(dp-dq)))
                    clipped = result
                if len(clipped) < 3: continue
                start = len(vertices)
                vertices.extend(p+normal*patch.get('offset', .0004) for p in clipped)
                faces.extend((start, start+i, start+i+1) for i in range(1, len(clipped)-1))
                colors.extend([patch['color']]*(len(clipped)-2))
            if len(faces) == count: raise ValueError('Panel misses its surface: '+patch['name'])
    mesh = bpy.data.meshes.new('Fitted charcoal service panels')
    mesh.from_pydata(vertices, [], faces)
    mesh.update()
    for name, rgb in layout['palette'].items():
        material = bpy.data.materials.new(name)
        material.diffuse_color = (*[v/12.92 if v <= .04045 else ((v+.055)/1.055)**2.4 for v in rgb], 1)
        mesh.materials.append(material)
    names = list(layout['palette'])
    for face, color in zip(mesh.polygons, colors): face.material_index = names.index(color)
    panels = bpy.data.objects.new('Vanguard service panels', mesh)
    scene.collection.objects.link(panels)
    return panels
