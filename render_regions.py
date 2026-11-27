"""Read-only Blender region renders and Pillow review sheets.

Round 1: python render_regions.py
Round 2: python render_regions.py --round 2

Uses the house ship_render.py camera basis and temporary evaluation-scene approach.
No blend save operation is performed. All output paths are confined to this folder.
"""
import argparse
import hashlib
import json
import math
from pathlib import Path
import subprocess
import sys

sys.dont_write_bytecode = True
ROOT = Path(__file__).resolve().parent
OUT = ROOT / 'out'
BLENDER = Path('C:/Program Files/Blender Foundation/Blender 5.1/blender.exe')
SHIPS = ('minion', 'rival', 'elite')
SOURCES = {s: Path(f'D:/amind/git/agent-2/art/ships/nightshade{s}/Nightshade{s.title()}.blend') for s in SHIPS}
PALETTE = {'Nightshade Midnight': '#614a87', 'Nightshade Violet': '#3f2964',
           'Nightshade Edge': '#acf549', 'Nightshade Plum': '#acf549',
           'Nightshade Recess': '#0b081b', 'Nightshade Glass': '#0b081b'}
ACCENT = '#e4ffa3'
BACKGROUND = '#7a7a7a'
VARIANTS = ('minion', 'rival-a', 'rival-b', 'elite')
# Each entry changes only the original Midnight faces, on the entire named part.
SECOND_PARTS = {
    'Rebuilt pitched hull', 'Aft engine collar', 'Aft spine armor',
    'Cockpit rear retaining collar', 'Cockpit shoulder latch',
    'Dorsal bridge service cover', 'Dorsal central blade', 'Dorsal shield cap',
    'Dorsal shield inset', 'Spine shoulder armor', 'Tail root mounting blocks',
    'Tail mount service panel', 'Ventral central blade', 'Ventral shield cap',
    'Ventral shield inset', 'Upper small swept fins.001', 'Lower small swept fins',
    'Field array aft strut', 'Field array forward strut',
}


def linear(hexcode):
    values = [int(hexcode[i:i+2], 16) / 255 for i in (1, 3, 5)]
    return tuple(v / 12.92 if v <= 0.04045 else ((v + 0.055) / 1.055) ** 2.4 for v in values)


def blender_main(args):
    import bpy
    import numpy as np
    from mathutils import Matrix, Vector

    bpy.context.preferences.filepaths.use_auto_save_temporary_files = False
    source = bpy.context.scene
    roles = {name: set() for name in ('role.hull', 'role.canopy', 'role.cores')}
    ignored = set()
    def visit(collection):
        nonlocal ignored
        if collection.name in roles:
            roles[collection.name].update(o.name for o in collection.all_objects)
        if collection.name == 'ignore':
            ignored.update(o.name for o in collection.all_objects)
        for child in collection.children:
            visit(child)
    visit(source.collection)
    names = set.union(*roles.values()) - ignored
    names = sorted(n for n in names if source.objects[n].type == 'MESH')
    assert names, 'No eligible visual meshes'

    # Preserve parent and mirror transforms, independent of source collection visibility.
    scene = bpy.data.scenes.new('Region review temporary scene')
    scene.unit_settings.scale_length = source.unit_settings.scale_length
    copies = {}
    for obj in source.objects:
        if obj.type not in ('MESH', 'EMPTY'):
            continue
        copy = obj.copy()
        copy.hide_viewport = copy.hide_render = copy.hide_select = False
        scene.collection.objects.link(copy)
        copies[obj.name] = copy
    for name, copy in copies.items():
        if copy.parent:
            inverse = copy.matrix_parent_inverse.copy()
            copy.parent = copies[copy.parent.name]
            copy.matrix_parent_inverse = inverse
        for modifier in copy.modifiers:
            modifier.show_viewport = modifier.show_render
            if modifier.type == 'MIRROR' and modifier.mirror_object:
                modifier.mirror_object = copies[modifier.mirror_object.name]
        copy.hide_render = name not in names
    bpy.context.window.scene = scene
    bpy.context.view_layer.update()
    dg = bpy.context.evaluated_depsgraph_get()
    space = copies['SymmetryOrigin'].evaluated_get(dg).matrix_world.inverted()
    points = []
    for name in names:
        evaluated = copies[name].evaluated_get(dg)
        mesh = evaluated.to_mesh()
        mat = space @ evaluated.matrix_world
        points.extend(tuple(mat @ v.co) for v in mesh.vertices)
        evaluated.to_mesh_clear()
    points = np.array(points)
    low, high = points.min(axis=0), points.max(axis=0)
    info = {'ship': args.ship, 'source': str(SOURCES[args.ship]), 'parts': names,
            'bounds': [low.tolist(), high.tolist()], 'top_scale': float(max((high-low)[:2]) / .85),
            'accent_mapping': [], 'canopy_parts': sorted(roles['role.canopy'] - ignored)}
    if args.inspect:
        info['part_geometry'] = {}
        for name in names:
            obj = copies[name]
            transform = space @ obj.matrix_world
            vertices = np.array([tuple(transform @ v.co) for v in obj.data.vertices])
            detail = {'bounds': [vertices.min(axis=0).tolist(), vertices.max(axis=0).tolist()],
                      'midpoint': ((vertices.min(axis=0)+vertices.max(axis=0))/2).tolist()}
            if name == 'Dorsal central blade':
                normal_matrix = transform.to_3x3().inverted().transposed()
                detail['faces'] = [{'index': p.index, 'slot': p.material_index,
                                    'center': list(transform @ p.center),
                                    'normal': list((normal_matrix @ p.normal).normalized()),
                                    'vertices': [list(transform @ obj.data.vertices[i].co) for i in p.vertices]}
                                   for p in obj.data.polygons]
            info['part_geometry'][name] = detail
        (OUT / f'{args.ship}-geometry.json').write_text(json.dumps(info, indent=2))
        return

    materials = {}
    for key, colour in list(PALETTE.items()) + [('Elite accent', ACCENT)]:
        material = bpy.data.materials.new('Review ' + key)
        material.diffuse_color = (*linear(colour), 1)
        materials[key] = material
    info['midnight_mapping'] = []
    for name in names:
        obj = copies[name]
        for i, slot in enumerate(obj.material_slots):
            original = slot.material.name if slot.material else None
            assert original in PALETTE, (name, i, original)
            key = original
            if name in roles['role.canopy']:
                key = 'Nightshade Glass'
            elif args.ship == 'elite' and name == 'Main wing joint beds' and original == 'Nightshade Plum':
                key = 'Elite accent'
                info['accent_mapping'].append({'part': name, 'material': original, 'slot': i,
                                               'source_faces': sum(p.material_index == i for p in obj.data.polygons)})
            elif args.round == 2 and original == 'Nightshade Midnight' and name in SECOND_PARTS:
                key = 'Nightshade Violet'
            if original == 'Nightshade Midnight':
                count = sum(p.material_index == i for p in obj.data.polygons)
                if count:
                    info['midnight_mapping'].append({'part': name, 'slot': i, 'source_faces': count,
                                                     'before': PALETTE[original], 'after': PALETTE[key]})
            slot.link = 'OBJECT'
            slot.material = materials[key]

    if args.round == 2 and args.variant == 'rival-b':
        obj = copies['Dorsal central blade']
        obj.data = obj.data.copy()
        mark_slot = len(obj.data.materials)
        obj.data.materials.append(materials['Nightshade Edge'])
        transform = space @ obj.matrix_world
        normal_matrix = transform.to_3x3().inverted().transposed()
        marked = []
        # Narrow upper shoulder bevels, mirrored into a symmetric pair of ticks.
        for p in obj.data.polygons:
            center_p = transform @ p.center
            normal = (normal_matrix @ p.normal).normalized()
            if normal.z > .85 and .115 < center_p.x < .13 and .05 < center_p.y < .15:
                marked.append({'index': p.index, 'original_slot': p.material_index,
                               'center': list(center_p), 'normal': list(normal)})
                p.material_index = mark_slot
        assert [p['index'] for p in marked] == [37, 44], marked
        info['rank_mark'] = {'part': 'Dorsal central blade', 'source_faces': marked,
                             'colour': '#acf549', 'mirrored_faces': 4}

    scene.render.engine = 'BLENDER_WORKBENCH'
    scene.render.resolution_x = scene.render.resolution_y = 1024
    scene.render.resolution_percentage = 100
    scene.render.image_settings.file_format = 'PNG'
    scene.render.image_settings.color_mode = 'RGB'
    scene.render.image_settings.color_depth = '8'
    scene.render.film_transparent = False
    scene.render.use_compositing = False
    scene.render.use_sequencer = False
    scene.render.dither_intensity = 0
    shading = scene.display.shading
    shading.light = 'FLAT'
    shading.color_type = 'MATERIAL'
    shading.show_shadows = False
    shading.show_cavity = False
    shading.show_object_outline = False
    shading.show_specular_highlight = False
    shading.show_xray = False
    shading.background_type = 'WORLD'
    scene.world = bpy.data.worlds.new('Region review grey')
    scene.world.color = linear(BACKGROUND)
    scene.display.render_aa = '32'
    scene.view_settings.view_transform = 'Standard'
    scene.view_settings.look = 'None'
    scene.view_settings.exposure = 0
    scene.view_settings.gamma = 1
    scene.view_settings.use_curve_mapping = False
    scene.display_settings.display_device = 'sRGB'
    camera = bpy.data.objects.new('Region review camera', bpy.data.cameras.new('Region review camera'))
    camera.data.type = 'ORTHO'
    scene.collection.objects.link(camera)
    scene.camera = camera
    center = (low + high) / 2
    radius = float(np.linalg.norm(high-low)) / 2
    camera.data.clip_start = .01
    camera.data.clip_end = radius * 6 + 10

    def render(view, direction, up, fixed_scale=None, transparent=False):
        back = Vector(direction).normalized()
        right = Vector(up).cross(back).normalized()
        basis = Matrix((right, back.cross(right), back)).transposed()
        projected = (points - center) @ np.array(basis)
        # Actual evaluated vertices provide tighter framing than bounding-box corners.
        pcenter = (projected.min(axis=0) + projected.max(axis=0)) / 2
        target = Vector(center) + basis @ Vector(pcenter)
        transform = Matrix.Translation(target + back * (radius * 3 + 1)) @ basis.to_4x4()
        camera.matrix_world = space.inverted() @ transform
        camera.data.ortho_scale = fixed_scale or float(max(np.ptp(projected[:, 0]), np.ptp(projected[:, 1])) / .85)
        scene.render.film_transparent = transparent
        scene.render.image_settings.color_mode = 'RGBA' if transparent else 'RGB'
        filename = f'{args.variant}-{view}.png' if args.round == 2 else f'{args.ship}-regions-{view}.png'
        scene.render.filepath = str(OUT / filename)
        bpy.ops.render.render(write_still=True)
        info.setdefault('views', {})[view] = {'direction': list(direction), 'ortho_scale': camera.data.ortho_scale}

    render('top', (0, 0, 1), (0, 1, 0))
    if args.ship == 'elite' and args.round == 1:
        probes = []
        reverse_names = {o.name: n for n, o in copies.items()}
        bpy.context.view_layer.update()
        for x, y in ((330, 420), (330, 520), (255, 320), (217, 627), (220, 740), (193, 520), (360, 735)):
            origin = camera.matrix_world @ Vector(((x+.5-512)/1024*camera.data.ortho_scale,
                                                  (512-y-.5)/1024*camera.data.ortho_scale, 0))
            direction = camera.matrix_world.to_quaternion() @ Vector((0, 0, -1))
            hit, location, normal, index, obj, matrix = scene.ray_cast(dg, origin, direction)
            if hit:
                evaluated = obj.evaluated_get(dg)
                slot_index = evaluated.data.polygons[index].material_index
                original_name = reverse_names[obj.name]
                probes.append({'xy': [x, y], 'part': original_name, 'slot': slot_index,
                               'source_material': source.objects[original_name].material_slots[slot_index].material.name})
        (OUT / 'elite-face-probes.json').write_text(json.dumps(probes, indent=2))
    # Front-right means +X,+Y. Keep the house turn045's 20-degree elevation.
    elevation = math.radians(20)
    render('34', (math.cos(elevation)/math.sqrt(2), math.cos(elevation)/math.sqrt(2), math.sin(elevation)), (0, 0, 1))
    if args.round == 2:
        render('top-alpha', (0, 0, 1), (0, 1, 0), transparent=True)
        render('34-alpha', (math.cos(elevation)/math.sqrt(2), math.cos(elevation)/math.sqrt(2), math.sin(elevation)), (0, 0, 1), transparent=True)
    render('common-top', (0, 0, 1), (0, 1, 0), args.common_scale)
    (OUT / f'{args.variant if args.round == 2 else args.ship}-render.json').write_text(json.dumps(info, indent=2))


def compose_and_check():
    import numpy as np
    from PIL import Image, ImageDraw, ImageFont
    font = ImageFont.truetype('C:/Windows/Fonts/arial.ttf', 24)
    swatches = [('Hull', '#614a87'), ('Second panels', '#3f2964'), ('Edge / Plum', '#acf549'),
                ('Recess / Glass', '#0b081b'), ('Elite accent', ACCENT)]
    checks = {}
    for ship in SHIPS:
        concept = Image.open(ROOT / 'concepts' / f'{ship}-codex-1.png').convert('RGB')
        concept = concept.resize((round(concept.width * 1024 / concept.height), 1024), Image.Resampling.LANCZOS)
        sheet = Image.new('RGB', (concept.width + 2048, 1116), BACKGROUND)
        sheet.paste(concept, (0, 0))
        for index, view in enumerate(('top', '34')):
            im = Image.open(OUT / f'{ship}-regions-{view}.png').convert('RGB')
            sheet.paste(im, (concept.width + index * 1024, 0))
        draw = ImageDraw.Draw(sheet)
        active = swatches if ship == 'elite' else swatches[:-1]
        spacing = sheet.width // len(active)
        for i, (label, colour) in enumerate(active):
            x = spacing * i + 24
            draw.rectangle((x, 1040, x+90, 1096), fill=colour)
            draw.text((x+106, 1052), f'{label}  {colour}', font=font, fill='white')
        sheet.save(OUT / f'{ship}-sheet.png')
        for view in ('top', '34', 'common-top'):
            path = OUT / f'{ship}-regions-{view}.png'
            pixels = np.array(Image.open(path).convert('RGB'))
            assert pixels.shape == (1024, 1024, 3)
            checks[path.name] = {}
            expected = [c for _, c in active] + [BACKGROUND]
            for colour in expected:
                target = np.array([int(colour[j:j+2], 16) for j in (1, 3, 5)])
                error = np.abs(pixels.astype(int) - target).max(axis=2)
                match = error <= 2
                # Require a flat 5x5 interior, excluding antialiasing and isolated pixels.
                interior = match.copy()
                for dy in range(-2, 3):
                    for dx in range(-2, 3):
                        interior &= np.roll(match, (dy, dx), (0, 1))
                ys, xs = np.where(interior)
                assert len(xs), f'Missing flat region {colour} in {path.name}'
                n = len(xs)//2
                x, y = int(xs[n]), int(ys[n])
                checks[path.name][colour] = {'xy': [x, y], 'rgb': pixels[y, x].tolist(),
                                             'max_channel_error': int(error[y, x]), 'interior_pixels': int(len(xs))}
    combined = Image.new('RGB', (3072, 1024), BACKGROUND)
    for i, ship in enumerate(SHIPS):
        combined.paste(Image.open(OUT / f'{ship}-regions-common-top.png'), (i*1024, 0))
    combined.save(OUT / 'all-regions-top.png')
    (OUT / 'pixel-checks.json').write_text(json.dumps(checks, indent=2))


def compose_round2():
    import numpy as np
    from PIL import Image, ImageDraw, ImageFont
    font = ImageFont.truetype('C:/Windows/Fonts/arial.ttf', 24)
    small_font = ImageFont.truetype('C:/Windows/Fonts/arial.ttf', 18)
    swatches = [('Hull', '#614a87'), ('Inner / spine', '#3f2964'), ('Edges / tips', '#acf549'),
                ('Glass / recess', '#0b081b'), ('Elite strips', ACCENT)]
    checks, game_checks = {}, {}
    common = Image.new('RGB', (4096, 1024), BACKGROUND)
    game = Image.new('RGB', (1040, 864), '#05040c')
    game_draw = ImageDraw.Draw(game)
    game_draw.text((24, 12), '80 px longest side: 1x and nearest-neighbour 4x', font=font, fill='white')
    comparison = Image.new('RGB', (2048, 4 * 1072), BACKGROUND)
    comp_draw = ImageDraw.Draw(comparison)
    for index, variant in enumerate(VARIANTS):
        ship = variant.split('-')[0]
        active = swatches if ship == 'elite' else swatches[:-1]
        concept = Image.open(ROOT / 'concepts' / f'{ship}-codex-1.png').convert('RGB')
        concept = concept.resize((round(concept.width * 1024 / concept.height), 1024), Image.Resampling.LANCZOS)
        sheet = Image.new('RGB', (concept.width + 2048, 1116), BACKGROUND)
        sheet.paste(concept, (0, 0))
        for j, view in enumerate(('top', '34')):
            sheet.paste(Image.open(OUT / f'{variant}-{view}.png'), (concept.width + j*1024, 0))
        draw = ImageDraw.Draw(sheet)
        for j, (label, colour) in enumerate(active):
            x = sheet.width // len(active) * j + 24
            draw.rectangle((x, 1040, x+90, 1096), fill=colour)
            draw.text((x+106, 1052), f'{label}  {colour}', font=font, fill='white')
        sheet.save(OUT / f'{variant}-sheet.png')
        for view in ('top', '34', 'top-alpha', '34-alpha', 'common-top'):
            path = OUT / f'{variant}-{view}.png'
            im = Image.open(path)
            assert im.size == (1024, 1024)
            pixels = np.array(im.convert('RGBA'))
            alpha = pixels[:, :, 3]
            if view.endswith('alpha'):
                assert im.mode == 'RGBA' and alpha[0, 0] == 0 and alpha.max() == 255
                assert (alpha == 0).sum() > 100000 and ((alpha > 0) & (alpha < 255)).any()
            expected = [colour for _, colour in active] + ([] if view.endswith('alpha') else [BACKGROUND])
            checks[path.name] = {}
            for colour in expected:
                target = np.array([int(colour[k:k+2], 16) for k in (1, 3, 5)])
                error = np.abs(pixels[:, :, :3].astype(int)-target).max(axis=2)
                match = (error <= 2) & (alpha == 255)
                interior = match.copy()
                for dy in range(-2, 3):
                    for dx in range(-2, 3):
                        interior &= np.roll(match, (dy, dx), (0, 1))
                ys, xs = np.where(interior)
                assert len(xs), f'Missing flat interior {colour}: {path.name}'
                n = len(xs)//2
                x, y = int(xs[n]), int(ys[n])
                checks[path.name][colour] = {'xy': [x, y], 'rgb': pixels[y, x, :3].tolist(),
                                             'max_channel_error': int(error[y, x]), 'interior_pixels': len(xs)}
        common.paste(Image.open(OUT / f'{variant}-common-top.png'), (index*1024, 0))
        transparent = Image.open(OUT / f'{variant}-top-alpha.png').convert('RGBA')
        bbox = transparent.getchannel('A').getbbox()
        cropped = transparent.crop(bbox)
        dimensions = tuple(max(1, round(n * 80 / max(cropped.size))) for n in cropped.size)
        tiny = cropped.resize(dimensions, Image.Resampling.LANCZOS)
        composite = Image.new('RGBA', dimensions, '#05040c')
        composite.alpha_composite(tiny)
        composite = composite.convert('RGB')
        col, row = index % 2, index // 2
        x, y = 24 + col*520, 62 + row*398
        game_draw.text((x, y), variant, font=font, fill='white')
        game_draw.text((x, y+42), '1x', font=small_font, fill='#aaaaaa')
        game_draw.text((x+132, y+42), '4x', font=small_font, fill='#aaaaaa')
        game.paste(composite, (x, y+80))
        game.paste(composite.resize((dimensions[0]*4, dimensions[1]*4), Image.Resampling.NEAREST), (x+132, y+80))
        tiny_pixels = np.array(composite).astype(int)
        region_counts = {}
        for label, colour in active:
            target = np.array([int(colour[k:k+2], 16) for k in (1, 3, 5)])
            count = int((np.abs(tiny_pixels-target).max(axis=2) <= 2).sum())
            region_counts[label] = count
        assert region_counts['Hull'] > 30 and region_counts['Inner / spine'] > 30
        game_checks[variant] = {'size': dimensions, 'source_alpha_bbox': bbox, 'pixels_within_2': region_counts}
        composite.save(OUT / f'{variant}-game-80.png')
        y = index*1072
        comp_draw.text((24, y+10), f'{ship}: round 1', font=font, fill='white')
        comp_draw.text((1048, y+10), f'{variant}: round 2', font=font, fill='white')
        comparison.paste(Image.open(ROOT / 'out' / f'{ship}-regions-top.png'), (0, y+48))
        comparison.paste(Image.open(OUT / f'{variant}-top.png'), (1024, y+48))
    common.save(OUT / 'all-top.png')
    game.save(OUT / 'game-scale.png')
    comparison.save(OUT / 'round1-vs-round2.png')
    (OUT / 'pixel-checks.json').write_text(json.dumps(checks, indent=2))
    (OUT / 'game-scale-checks.json').write_text(json.dumps(game_checks, indent=2))
    a = np.array(Image.open(OUT / 'rival-a-top.png')).astype(int)
    b = np.array(Image.open(OUT / 'rival-b-top.png')).astype(int)
    changed = np.any(a != b, axis=2)
    assert changed.any() and changed.sum() < 5000
    ys, xs = np.where(changed)
    (OUT / 'rank-mark-check.json').write_text(json.dumps({'changed_top_pixels': int(changed.sum()),
        'bbox': [int(xs.min()), int(ys.min()), int(xs.max()+1), int(ys.max()+1)]}, indent=2))


def run_round2():
    before = {s: hashlib.sha256(p.read_bytes()).hexdigest() for s, p in SOURCES.items()}
    old_outputs = {str(p.relative_to(ROOT)): hashlib.sha256(p.read_bytes()).hexdigest()
                   for p in (ROOT / 'out').rglob('*') if p.is_file()}
    commands = []
    def run(ship, variant, extra):
        command = [str(BLENDER), '-b', '--factory-startup', '--disable-autoexec', str(SOURCES[ship]),
                   '--python-exit-code', '1', '-P', str(Path(__file__).resolve()), '--',
                   '--round', '2', '--ship', ship, '--variant', variant] + extra
        commands.append(command)
        print('Running round 2', variant, ' '.join(extra), flush=True)
        with (OUT / f'{variant}-{"inspect" if "--inspect" in extra else "render"}.log').open('w') as log:
            subprocess.run(command, cwd=ROOT, stdout=log, stderr=subprocess.STDOUT, check=True)
    for ship in SHIPS:
        run(ship, ship if ship != 'rival' else 'rival-a', ['--inspect'])
    scale = max(json.loads((OUT / f'{s}-geometry.json').read_text())['top_scale'] for s in SHIPS)
    for variant in VARIANTS:
        run(variant.split('-')[0], variant, ['--common-scale', str(scale)])
    compose_round2()
    after = {s: hashlib.sha256(p.read_bytes()).hexdigest() for s, p in SOURCES.items()}
    old_after = {name: hashlib.sha256((ROOT / name).read_bytes()).hexdigest() for name in old_outputs}
    assert before == after, 'Source file changed'
    assert old_outputs == old_after, 'Round 1 output changed'
    (OUT / 'run-manifest.json').write_text(json.dumps({'commands': commands, 'common_ortho_scale': scale,
        'source_sha256_before': before, 'source_sha256_after': after,
        'round1_sha256_before': old_outputs, 'round1_sha256_after': old_after}, indent=2))
    print('Round 2 completed; source and round 1 hashes unchanged.', flush=True)


def main():
    global OUT
    parser = argparse.ArgumentParser()
    parser.add_argument('--round', type=int, choices=(1, 2), default=1)
    parser.add_argument('--variant', choices=VARIANTS)
    parser.add_argument('--ship', choices=SHIPS)
    parser.add_argument('--inspect', action='store_true')
    parser.add_argument('--common-scale', type=float)
    if '--' in sys.argv:
        args = parser.parse_args(sys.argv[sys.argv.index('--')+1:])
        OUT = ROOT / ('out2' if args.round == 2 else 'out')
        OUT.mkdir(exist_ok=True)
        blender_main(args)
        return
    args = parser.parse_args()
    OUT = ROOT / ('out2' if args.round == 2 else 'out')
    OUT.mkdir(exist_ok=True)
    if args.round == 2:
        run_round2()
        return
    before = {s: hashlib.sha256(p.read_bytes()).hexdigest() for s, p in SOURCES.items()}
    commands = []
    def run(ship, extra):
        command = [str(BLENDER), '-b', '--factory-startup', '--disable-autoexec', str(SOURCES[ship]),
                   '--python-exit-code', '1', '-P', str(Path(__file__).resolve()), '--', '--ship', ship] + extra
        commands.append(command)
        print('Running', ship, ' '.join(extra), flush=True)
        with (OUT / f'{ship}-{"inspect" if "--inspect" in extra else "render"}.log').open('w') as log:
            subprocess.run(command, cwd=ROOT, stdout=log, stderr=subprocess.STDOUT, check=True)
    for ship in SHIPS:
        run(ship, ['--inspect'])
    scale = max(json.loads((OUT / f'{s}-geometry.json').read_text())['top_scale'] for s in SHIPS)
    for ship in SHIPS:
        run(ship, ['--common-scale', str(scale)])
    compose_and_check()
    after = {s: hashlib.sha256(p.read_bytes()).hexdigest() for s, p in SOURCES.items()}
    assert before == after, 'Source file changed'
    (OUT / 'run-manifest.json').write_text(json.dumps({'commands': commands, 'common_ortho_scale': scale,
        'source_sha256_before': before, 'source_sha256_after': after}, indent=2))
    print('Completed all renders, sheets, pixel checks; source hashes unchanged.', flush=True)


if __name__ == '__main__':
    main()
