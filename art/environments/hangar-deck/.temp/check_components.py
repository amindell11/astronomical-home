from pathlib import Path
import json
import bpy
import bmesh

root = Path(__file__).resolve().parents[1]
reports = {}
for label, path in [('reference', root / '.temp/reference.blend'), ('extended', root / 'deck.blend')]:
    bpy.ops.wm.open_mainfile(filepath=str(path))
    obj = bpy.data.objects['Deck | joined closed structure']
    bm = bmesh.new()
    bm.from_mesh(obj.data)
    remaining = set(bm.verts)
    groups = []
    while remaining:
        first = remaining.pop()
        connected, pending = {first}, [first]
        while pending:
            vertex = pending.pop()
            for edge in vertex.link_edges:
                other = edge.other_vert(vertex)
                if other in remaining:
                    remaining.remove(other)
                    connected.add(other)
                    pending.append(other)
        faces = {face for vertex in connected for face in vertex.link_faces}
        volume = 0
        for face in faces:
            points = [v.co for v in face.verts]
            volume += sum(points[0].dot(points[i].cross(points[i + 1])) / 6 for i in range(1, len(points)-1))
        groups.append({'vertices': len(connected), 'signed_volume': volume,
                       'bounds': [[min(v.co[i] for v in connected), max(v.co[i] for v in connected)] for i in range(3)]})
    bm.free()
    reports[label] = sorted(groups, key=lambda g: -g['signed_volume'])
(root / 'previews/structure-components.json').write_text(json.dumps(reports, indent=2))
print(json.dumps(reports))
reference_parts = [g for g in reports['reference'][1:] if g['signed_volume'] > 0]
extended_parts = [g for g in reports['extended'][1:] if g['signed_volume'] > 0]
assert [(g['vertices'], g['bounds']) for g in reference_parts] == [(g['vertices'], g['bounds']) for g in extended_parts]
