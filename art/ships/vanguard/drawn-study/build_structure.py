import bpy, json, math
from mathutils import Vector
from mathutils.bvhtree import BVHTree


def build(scene, out):
    vertices, faces = [], []
    for line in json.loads((out/'structural-lines.json').read_text())['lines']:
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
                edge.append(hit+normal*.0003)
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
    
    mesh = bpy.data.meshes.new('Selected tapered panel seams')
    mesh.from_pydata(vertices,[],faces)
    mesh.update()
    drawing = bpy.data.objects.new('Vanguard structural ink',mesh)
    scene.collection.objects.link(drawing)
    return drawing
