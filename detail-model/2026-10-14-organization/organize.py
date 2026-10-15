import bpy
import hashlib
import json
from pathlib import Path

import numpy as np

STAGE = Path(__file__).parent
SOURCE = Path('D:/amind/git/agent-3/art/ships/bulwark/Bulwark.blend')
GROUPS = {
    '01 Hull': {
        '01 Ram nose': 'Hull - Ram nose',
        '02 Upper hull.001': 'Hull - Main deck',
        '02 Upper hull': 'Hull - Aft bridge',
        '03 Lower hull': 'Hull - Lower keel',
        '09 Side armor.001': 'Hull - Dorsal spine',
    },
    '02 Armor plates': {
        '15 Front armor': 'Armor - Front plate',
        '09 Side armor.002': 'Armor - Side plates',
        '05 Tail tips.002': 'Armor - Upper shoulder guards',
        '05 Tail tips.003': 'Armor - Lower skids',
    },
    '03 Cockpit': {
        '06 Cockpit dome': 'Cockpit - Canopy',
        '06 Cockpit surround': 'Cockpit - Seating rim',
    },
    '04 Propulsion and canisters': {
        '04 Tail booms': 'Engine - Shoulder booms',
        '05 Tail tips': 'Engine - Shrouds',
        '08 Nested engines': 'Engine - Recessed nozzles',
        '07 Side insets': 'Canisters - Metal side modules',
    },
    '05 Upper flaps': {
        '10 Tail fins': 'Upper flaps - Fins',
        '11 Upper hinge brackets': 'Upper flaps - Fixed brackets',
        '12 Upper hinge leaves': 'Upper flaps - Moving leaves',
        'Upper flap axis': 'Upper flaps - Hinge axis',
        'Upper flap deployment': 'Upper flaps - DEPLOY control',
    },
    '06 Lower flaps': {
        '10 Tail fins.001': 'Lower flaps - Fins',
        '13 Lower hinge brackets': 'Lower flaps - Fixed brackets',
        '14 Lower hinge leaves': 'Lower flaps - Moving leaves',
        'Lower flap axis': 'Lower flaps - Hinge axis',
        'Lower flap deployment': 'Lower flaps - DEPLOY control',
    },
}
HELPERS = {
    'SymmetryOrigin': 'SymmetryOrigin',
    'Body': 'Assembly - Hull', 'Tail': 'Assembly - Propulsion',
    'Ram nose': 'Pivot - Ram nose', 'Upper hull': 'Pivot - Aft bridge',
    'Lower hull': 'Pivot - Lower keel', 'Tail booms': 'Pivot - Engine shoulders',
    'Tail tips': 'Pivot - Engine shrouds', 'Cockpit': 'Pivot - Cockpit',
    'Side insets': 'Pivot - Metal canisters', 'Side armor': 'Pivot - Front armor',
    'SymmetryOrigin.002': 'Mirror - Main deck', 'Body.001': 'Assembly - Main deck',
    'Upper hull.001': 'Pivot - Main deck',
    'SymmetryOrigin.003': 'Mirror - Dorsal spine', 'Body.002': 'Assembly - Dorsal spine',
    'Side armor.001': 'Pivot - Dorsal spine',
    'SymmetryOrigin.004': 'Mirror - Shoulder guards', 'Tail.002': 'Assembly - Shoulder guards',
    'Tail tips.002': 'Pivot - Shoulder guards',
    'SymmetryOrigin.006': 'Mirror - Lower skids', 'Tail.003': 'Assembly - Lower skids',
    'Tail tips.003': 'Pivot - Lower skids',
    'SymmetryOrigin.008': 'Mirror - Lower flaps',
    'SymmetryOrigin.010': 'Mirror - Side armor', 'Body.005': 'Assembly - Side armor',
    'Side armor.004': 'Pivot - Side armor',
}


def digest(data):
    return hashlib.sha256(data).hexdigest()


def capture(objects):
    bpy.context.view_layer.update()
    graph = bpy.context.evaluated_depsgraph_get()
    records = {}
    for key, obj in objects.items():
        record = {
            'type': obj.type,
            'parent': next((k for k, v in objects.items() if v == obj.parent), None),
            'basis': [list(row) for row in obj.matrix_basis],
            'parent_inverse': [list(row) for row in obj.matrix_parent_inverse],
            'world': [list(row) for row in obj.matrix_world],
            'modifiers': [(m.name, m.type, m.show_viewport, m.show_render,
                           next((k for k, v in objects.items() if v == m.mirror_object), None)
                           if m.type == 'MIRROR' else None) for m in obj.modifiers],
        }
        if obj.type == 'MESH':
            mesh = obj.data
            record['vertices'] = digest(np.array([v.co[:] for v in mesh.vertices], dtype='<f4').tobytes())
            record['faces'] = [list(p.vertices) for p in mesh.polygons]
            record['uv'] = {u.name: digest(np.array([l.uv[:] for l in u.data], dtype='<f4').tobytes()) for u in mesh.uv_layers}
            record['materials'] = [s.material.name if s.material else None for s in obj.material_slots]
            record['face_materials'] = [p.material_index for p in mesh.polygons]
            record['visible'] = obj.visible_get()
            record['hide_render'] = obj.hide_render
            evaluated = obj.evaluated_get(graph)
            evaluated_mesh = evaluated.to_mesh(preserve_all_data_layers=True, depsgraph=graph)
            record['evaluated_world'] = digest(np.array([evaluated.matrix_world @ v.co for v in evaluated_mesh.vertices], dtype='<f4').tobytes())
            record['evaluated_faces'] = digest(json.dumps([list(p.vertices) for p in evaluated_mesh.polygons]).encode())
            evaluated.to_mesh_clear()
        records[key] = record
    images = {}
    for im in bpy.data.images:
        if im.type == 'RENDER_RESULT' or not len(im.pixels):
            continue
        pixels = np.empty(len(im.pixels), dtype=np.float32)
        im.pixels.foreach_get(pixels)
        images[im.name] = {'pixels': digest(pixels.tobytes()), 'packed': digest(bytes(im.packed_file.data)) if im.packed_file else None}
    return {'objects': records, 'images': images}


def main():
    assert Path(bpy.data.filepath).resolve() == SOURCE.resolve()
    assert not bpy.data.is_dirty, 'Owner has new unsaved edits; stop before changing scene.'
    assert bpy.context.mode == 'OBJECT'
    objects = {o.name: o for o in bpy.context.scene.objects}
    before = capture(objects)
    (STAGE / 'before-data.json').write_text(json.dumps(before, indent=2))
    bpy.ops.screen.screenshot(filepath=str(STAGE / 'before-outliner.png'))
    scene = bpy.context.scene
    ship = bpy.data.collections['Ship']
    ship.name = 'Bulwark - Parts'
    ignore = bpy.data.collections['ignore']
    names = {}
    def collection(name, parent):
        col = bpy.data.collections.new(name)
        parent.children.link(col)
        return col
    def move(obj, target):
        target.objects.link(obj)
        for col in list(obj.users_collection):
            if col != target and not col.name.startswith('role.'):
                col.objects.unlink(obj)
    for group, mapping in GROUPS.items():
        col = collection(group, ship)
        col.color_tag = 'COLOR_04'
        for old, new in mapping.items():
            obj = objects[old]
            obj.name = new
            names[old] = new
            move(obj, col)
            if obj.type == 'MESH':
                if obj.data.users == 1:
                    obj.data.name = new
                obj['editing_group'] = group
                obj['part_purpose'] = new.split(' - ', 1)[1]
                for obsolete in ('edit_note', 'blockout_intent'):
                    if obsolete in obj:
                        del obj[obsolete]
    helpers = collection('90 Pivots and mirror planes', ship)
    helpers.color_tag = 'COLOR_05'
    for old, new in HELPERS.items():
        obj = objects[old]
        obj.name = new
        names[old] = new
        move(obj, helpers)
    archive = collection('Unused parts and helpers', ignore)
    archive.color_tag = 'COLOR_01'
    for old, obj in objects.items():
        if old in names or obj.type == 'CAMERA' or old.startswith('reference.'):
            continue
        assert obj.type == 'EMPTY' or obj.hide_get(), f'Unexpected used object: {old}'
        obj.name = 'Unused - ' + old
        names[old] = obj.name
        move(obj, archive)
    side = objects['09 Side armor.002']
    bpy.data.collections['role.hull'].objects.link(side)
    canisters = objects['07 Side insets']
    bpy.data.collections['role.canopy'].objects.unlink(canisters)
    bpy.data.collections['role.hull'].objects.link(canisters)
    for col in list(bpy.data.collections):
        if col.name in ['Dorsal spine', 'Engine root collars', 'Flap hinges', 'Flap mounts', 'Lower armor skids', 'Tail fins', 'Upper shoulder guards']:
            assert not col.objects and not col.children
            bpy.data.collections.remove(col)
    for screen in bpy.data.screens:
        for area in screen.areas:
            if area.type == 'OUTLINER':
                area.spaces.active.use_filter_object_content = False
                area.spaces.active.use_filter_children = False
    after = capture(objects)
    differences = [name for name in before['objects'] if before['objects'][name] != after['objects'][name]]
    assert not differences, differences
    assert before['images'] == after['images'], 'Paint changed'
    report = {'renamed': names, 'mesh_count': 21, 'archived': [o.name for o in archive.objects],
              'geometry_uv_materials_transforms_modifiers_and_paint_unchanged': True,
              'objects_preserved': len(objects), 'after': after}
    (STAGE / 'verification.json').write_text(json.dumps(report, indent=2))
    (STAGE / 'name-map.json').write_text(json.dumps(names, indent=2))
    bpy.ops.wm.save_as_mainfile(filepath=str(SOURCE))


if __name__ == '__main__':
    main()
