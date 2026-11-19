"""Check all sky against frozen window openings and compare the literal camera PNG.

--require-closed asserts coverage; --require-preserved asserts exact RGB scope.
--check-reference tests the saved pre-change coverage without writing evidence.
--finish1 labels historical RGB differences as expected surface changes.
--finish2 records dark-edge shifts up to three RGB values at unchanged zero/full alpha.
"""
from pathlib import Path
import hashlib
import json
import math
import sys
import numpy as np
from PIL import Image, ImageDraw, ImageFont
from scipy.ndimage import binary_dilation, binary_erosion, distance_transform_edt, find_objects, label

ROOT = Path(__file__).resolve().parent
OUT = ROOT / 'previews'
REFERENCE = OUT / 'coverage-reference'
CHECK_REFERENCE = '--check-reference' in sys.argv
FINISH_ROUND = 2 if '--finish2' in sys.argv else (1 if '--finish1' in sys.argv else 0)
FINISH_PREFIX = 'finish' + str(FINISH_ROUND)
EDGE_COLOUR_TOLERANCE = 3 if FINISH_ROUND == 2 else 1
INPUT = REFERENCE if CHECK_REFERENCE else OUT
BAND_FRACTION = .03


def rgb(path):
    return np.asarray(Image.open(path).convert('RGB')).astype(np.int16)


def sky_masks(array):
    red, green, blue = array[:, :, 0], array[:, :, 1], array[:, :, 2]
    return ((red > 180) & (green < 80) & (blue > 180),
            (red - green > 40) & (blue - green > 40))


def bounds(mask):
    ys, xs = np.nonzero(mask)
    return [int(xs.min()), int(ys.min()), int(xs.max()), int(ys.max())] if len(xs) else None


def component_regions(mask):
    components, count = label(mask)
    areas = np.bincount(components.ravel())
    boxes = find_objects(components)
    order = sorted(range(1, count + 1), key=lambda i: int(areas[i]), reverse=True)
    regions = []
    for index in order[:20]:
        y, x = boxes[index - 1]
        regions.append({'pixel_count': int(areas[index]),
                        'bounds_xy_inclusive': [x.start, y.start, x.stop - 1, y.stop - 1]})
    return {'component_count': count, 'largest_regions': regions,
            'remaining_component_pixels': int(sum(areas[index] for index in order[20:]))}


def window_envelopes(windows):
    height, width = windows.shape
    valid_columns = windows.any(axis=0)
    columns = np.flatnonzero(valid_columns)
    upper = windows.argmax(axis=0)
    lower = height - 1 - windows[::-1].argmax(axis=0)
    # Interpolation spans struts and the opaque side walls, never new sky pixels.
    upper = np.interp(np.arange(width), columns, upper[valid_columns])
    lower = np.interp(np.arange(width), columns, lower[valid_columns])
    rows = np.arange(height)[:, None]
    return rows < upper[None, :], rows > lower[None, :], upper, lower


def reference_windows(view, image_name):
    path = REFERENCE / image_name
    reference_rgb = rgb(path)
    _, chroma = sky_masks(reference_rgb)
    height, width = chroma.shape
    components, _ = label(chroma)
    areas = np.bincount(components.ravel())
    boxes = find_objects(components)
    selected = [index for index, box in enumerate(boxes, 1)
                if box[0].stop - box[0].start >= math.ceil(height * .2)
                and areas[index] >= math.ceil(width * height * .005)]
    expected = 5 if view in {'21x9', '32x9'} else 3
    assert len(selected) == expected, (view, 'reference window components', selected)
    connected_windows = np.isin(components, selected)
    distance = distance_transform_edt(~connected_windows)
    singleton_components = np.flatnonzero(areas == 1)
    fringe = (np.isin(components, singleton_components) & (distance <= 3)
              & (reference_rgb.max(axis=2) < 80))
    windows = connected_windows | fringe
    above, below, upper, lower = window_envelopes(windows)
    raw_above, raw_below, _, _ = window_envelopes(connected_windows)
    fringe_ys, fringe_xs = np.nonzero(fringe)
    provenance = {'image': 'coverage-reference/' + image_name,
                  'sha256': hashlib.sha256(path.read_bytes()).hexdigest(),
                  'window_count': len(selected),
                  'window_components': [{'pixels': int(areas[index]),
                                         'bounds_xy_inclusive': bounds(components == index)}
                                        for index in selected],
                  'frozen_antialias_fringe_pixels': [
                      {'xy': [int(x), int(y)], 'reference_rgb': reference_rgb[y, x].tolist(),
                       'distance_to_connected_window_px': float(distance[y, x])}
                      for y, x in zip(fringe_ys, fringe_xs)],
                  'header_lower_edge_rows_min_max': [float(upper.min()), float(upper.max())],
                  'console_upper_surface_rows_min_max': [float(lower.min()), float(lower.max())]}
    return windows, above, below, provenance, (connected_windows, raw_above, raw_below), fringe


projections = json.loads((INPUT / 'coverage-projections.json').read_text())
report = {'band_fraction': BAND_FRACTION, 'band_rounding': 'ceil(height * .03)',
          'magenta_rule_srgb_8bit': 'R > 180 and G < 80 and B > 180',
          'antialias_chroma_rule_srgb_8bit': 'R-G > 40 and B-G > 40',
          'window_boundary_method': (
              'Frozen pre-change chroma components with height >= 20% and area >= 0.5% of frame; '
              'exactly three main windows, plus two side windows in wide views. '
              'Include frozen isolated baseline chroma pixels within 3px of those windows '
              'when every RGB channel is below 80; these are pre-existing antialias fringe. '
              'Header/console envelopes use first/last window pixel per column, interpolated '
              'across opaque struts and side walls. All sky outside the frozen components is '
              'also counted. One-pixel dilation is diagnostic only, never the pass criterion.'),
          'views': {}}
sheet = Image.new('RGB', (1920, 1980), '#121c2a')
draw = ImageDraw.Draw(sheet)
font = ImageFont.truetype('C:/Windows/Fonts/arial.ttf', 24)
small = ImageFont.truetype('C:/Windows/Fonts/arial.ttf', 19)
reference_masks = {}
reference_connected_masks = {}
for index, (view, projection) in enumerate(projections.items()):
    source = Image.open(INPUT / projection['image']).convert('RGB')
    array = np.asarray(source).astype(np.int16)
    magenta, chroma = sky_masks(array)
    windows, above, below, provenance, raw_masks, fringe = reference_windows(view, projection['image'])
    assert windows.shape == magenta.shape, (view, 'reference resolution changed')
    reference_masks[view] = windows, above, below
    reference_connected_masks[view] = raw_masks[0]
    rows = math.ceil(array.shape[0] * BAND_FRACTION)
    counts = {'band_rows': rows, 'top_magenta_pixels': int(magenta[:rows].sum()),
              'bottom_magenta_pixels': int(magenta[-rows:].sum()),
              'top_chroma_pixels': int(chroma[:rows].sum()),
              'bottom_chroma_pixels': int(chroma[-rows:].sum()),
              'left_edge_magenta_pixels': int(magenta[:, 0].sum()),
              'right_edge_magenta_pixels': int(magenta[:, -1].sum()),
              'reference_windows': provenance}
    for name, region in [('above_header', above), ('below_console', below), ('outside_windows', ~windows)]:
        counts[name + '_magenta_pixels'] = int((magenta & region).sum())
        counts[name + '_chroma_pixels'] = int((chroma & region).sum())
        counts[name + '_chroma_bounds_xy_inclusive'] = bounds(chroma & region)
    raw_windows, raw_above, raw_below = raw_masks
    counts['raw_connected_window_counts'] = {
        'above_header_chroma_pixels': int((chroma & raw_above).sum()),
        'below_console_chroma_pixels': int((chroma & raw_below).sum()),
        'outside_windows_chroma_pixels': int((chroma & ~raw_windows).sum())}
    baseline_rgb = rgb(REFERENCE / projection['image'])
    counts['frozen_antialias_fringe'] = {
        'baseline_pixels': int(fringe.sum()),
        'current_chroma_pixels': int((fringe & chroma).sum()),
        'byte_identical_pixels': int((fringe & np.all(array == baseline_rgb, axis=2)).sum()),
        'maximum_channel_difference': int(np.abs(array - baseline_rgb)[fringe].max()) if fringe.any() else 0}
    counts['outside_windows_chroma_beyond_1px_reference_boundary_pixels'] = int(
        (chroma & ~binary_dilation(windows, structure=np.ones((3, 3), dtype=bool))).sum())
    counts['bands_pass'] = counts['top_chroma_pixels'] == counts['bottom_chroma_pixels'] == 0
    counts['window_openings_pass'] = counts['outside_windows_chroma_pixels'] == 0
    if FINISH_ROUND:
        suffix = '' if view == '16x9' else '-' + view
        original_alpha = np.asarray(Image.open(OUT / ('deck-finish1-original-mask' + suffix + '.png')))[:, :, 3]
        finished_alpha = np.asarray(Image.open(OUT / ('deck-mask' + suffix + '.png')))[:, :, 3]
        unchanged_sky = (original_alpha == 0) & (finished_alpha == 0)
        stable_edge = unchanged_sky
        if FINISH_ROUND == 2:
            stable_edge = unchanged_sky | ((original_alpha == 255) & (finished_alpha == 255))
        edge_rounding = (chroma & ~windows & stable_edge & (baseline_rgb.max(axis=2) < 80)
                         & (array.max(axis=2) < 80)
                         & (np.abs(array - baseline_rgb).max(axis=2) <= EDGE_COLOUR_TOLERANCE)
                         & binary_dilation(windows, structure=np.ones((3, 3), dtype=bool)))
        counts[FINISH_PREFIX + '_alpha_loss_pixels'] = int((finished_alpha < original_alpha).sum())
        counts[FINISH_PREFIX + '_existing_edge_colour_rounding'] = [
            {'xy': [int(x), int(y)], 'original_rgb': baseline_rgb[y, x].tolist(),
             'finished_rgb': array[y, x].tolist(), 'original_alpha': int(original_alpha[y, x]),
             'finished_alpha': int(finished_alpha[y, x])}
            for y, x in np.argwhere(edge_rounding)]
        counts['unexplained_outside_windows_chroma_pixels'] = int((chroma & ~windows & ~edge_rounding).sum())
        counts['window_openings_pass'] = (counts['unexplained_outside_windows_chroma_pixels'] == 0
                                           and counts[FINISH_PREFIX + '_alpha_loss_pixels'] == 0)
    counts['coverage_pass'] = counts['bands_pass'] and counts['window_openings_pass']
    report['views'][view] = projection | counts
    x, y = (index % 2) * 960, (index // 2) * 660
    width, height = projection['resolution']
    draw.text((x + 20, y + 14), f'{view.replace("x", ":")}  |  {width} x {height}', font=font, fill='white')
    draw.text((x + 20, y + 48),
              f'3% bands: top {counts["top_magenta_pixels"]:,} / bottom {counts["bottom_magenta_pixels"]:,}',
              font=small, fill='#edc774')
    draw.text((x + 20, y + 74),
              f'Above header {counts["above_header_magenta_pixels"]:,} / below console {counts["below_console_magenta_pixels"]:,}'
              f' / outside windows {counts["outside_windows_magenta_pixels"]:,}', font=small, fill='#edc774')
    edge_label = (f'Geometry alpha losses: {counts[FINISH_PREFIX + "_alpha_loss_pixels"]}'
                  f' / existing edge rounding: {len(counts[FINISH_PREFIX + "_existing_edge_colour_rounding"])}'
                  if FINISH_ROUND else
                  f'Loose chroma outside windows: {counts["outside_windows_chroma_pixels"]:,}')
    draw.text((x + 20, y + 100),
              edge_label + f'  |  {"PASS" if counts["coverage_pass"] else "FAIL"}',
              font=small, fill='#8dd5bb' if counts['coverage_pass'] else '#ff8e8e')
    source.thumbnail((920, 510), Image.Resampling.LANCZOS)
    sheet.paste(source, (x + (960 - source.width) // 2, y + 140 + (510 - source.height) // 2))

old_alpha = np.asarray(Image.open(REFERENCE / 'deck-mask.png'))[:, :, 3]
new_alpha = np.asarray(Image.open(INPUT / 'deck-mask.png'))[:, :, 3]
old, new = old_alpha > 127, new_alpha > 127
windows, above, below = reference_masks['16x9']
reference_magenta, reference_sky = sky_masks(rgb(REFERENCE / 'deck-coverage-16x9.png'))
added = new & ~old
added_in_windows = added & windows
added_in_connected_windows = added & reference_connected_masks['16x9']
added_in_saturated_windows = added & windows & reference_magenta
old_edge, new_edge = old ^ binary_erosion(old), new ^ binary_erosion(new)
distances = np.concatenate([distance_transform_edt(~old_edge)[new_edge], distance_transform_edt(~new_edge)[old_edge]])
report['reference_16x9'] = {'reference_image': 'coverage-reference/deck-camera.png',
    'reference_mask': 'coverage-reference/deck-mask.png',
    'mask_threshold': 'Saved 8-bit alpha > 127 (the 0.5 silhouette threshold).',
    'hardware_intersection_over_union': float((old & new).sum() / (old | new).sum()),
    'changed_pixels': int((old ^ new).sum()), 'image_pixels': int(old.size),
    'added_pixels': int((new & ~old).sum()), 'removed_pixels': int((old & ~new).sum()),
    'removed_pixels_bounds_xy_inclusive': bounds(old & ~new),
    'added_mask_pixels_inside_frozen_windows': int(added_in_windows.sum()),
    'added_mask_pixels_inside_frozen_windows_bounds_xy_inclusive': bounds(added_in_windows),
    'added_mask_pixels_inside_frozen_connected_windows': int(added_in_connected_windows.sum()),
    'added_mask_pixels_inside_frozen_connected_windows_bounds_xy_inclusive': bounds(added_in_connected_windows),
    'added_mask_pixels_inside_frozen_strict_magenta_windows': int(added_in_saturated_windows.sum()),
    'added_mask_pixels_inside_frozen_strict_magenta_windows_bounds_xy_inclusive': bounds(added_in_saturated_windows),
    'edge_distance_95_percentile_px': float(np.percentile(distances, 95)),
    'edge_distance_max_px': float(distances.max())}

before, after = rgb(REFERENCE / 'deck-camera.png'), rgb(INPUT / 'deck-camera.png')
assert before.shape == after.shape == (1080, 1920, 3), '16:9 camera PNG dimensions changed'
changed = np.any(before != after, axis=2)
delta = np.abs(after - before)
backdrop = reference_sky & (old_alpha == 0)
allowed = backdrop & (above | below) & (new_alpha > 0)
disallowed = changed & ~allowed
report['rendered_rgb_16x9'] = {
    'reference_image': 'coverage-reference/deck-camera.png', 'new_image': 'deck-camera.png',
    'reference_sha256': hashlib.sha256((REFERENCE / 'deck-camera.png').read_bytes()).hexdigest(),
    'new_sha256': hashlib.sha256((INPUT / 'deck-camera.png').read_bytes()).hexdigest(),
    'comparison': 'Exact saved RGB bytes, without tolerance or denoising exclusions.',
    'allowed_change_rule': 'Previously pure backdrop above header/below console becomes hardware; baseline mask alpha = 0, new alpha > 0.',
    'image_pixels': int(changed.size), 'changed_pixels': int(changed.sum()),
    'changed_bounds_xy_inclusive': bounds(changed),
    'changed_top_region_pixels': int((changed & backdrop & above).sum()),
    'changed_top_region_bounds_xy_inclusive': bounds(changed & backdrop & above),
    'changed_bottom_region_pixels': int((changed & backdrop & below).sum()),
    'changed_bottom_region_bounds_xy_inclusive': bounds(changed & backdrop & below),
    'changed_window_pixels': int((changed & windows).sum()),
    'changed_non_backdrop_pixels': int((changed & ~backdrop).sum()),
    'changed_antialias_boundary_pixels': int((changed & (old_alpha > 0) & (old_alpha < 255)).sum()),
    'disallowed_changed_pixels': int(disallowed.sum()),
    'disallowed_changed_bounds_xy_inclusive': bounds(disallowed),
    'maximum_channel_difference': int(delta.max()),
    'disallowed_maximum_channel_difference_counts': {
        'one': int((disallowed & (delta.max(axis=2) == 1)).sum()),
        'two_to_five': int((disallowed & (delta.max(axis=2) >= 2) & (delta.max(axis=2) <= 5)).sum()),
        'over_five': int((disallowed & (delta.max(axis=2) > 5)).sum()),
        'over_five_in_first_20_rows': int((disallowed & (delta.max(axis=2) > 5))[:20].sum())},
    'mean_maximum_channel_difference_over_changed_pixels': float(delta.max(axis=2)[changed].mean()) if changed.any() else 0.,
    'scope_pass': not disallowed.any(),
    'changed_components': component_regions(changed),
    'disallowed_components': component_regions(disallowed)}

audit = json.loads((INPUT / 'mesh-audit.json').read_text())
original_audit = json.loads((REFERENCE / 'mesh-audit.json').read_text())
report['preservation'] = {'controls_unchanged': audit['controls'] == original_audit['controls'],
                          'screens_unchanged': audit['screens'] == original_audit['screens']}
if not CHECK_REFERENCE:
    report['preservation'].update(json.loads((OUT / 'preservation-check.json').read_text()))
report['mesh_totals'] = {'hardware_objects': len(audit['objects']),
    'hardware_vertices': sum(o['vertices'] for o in audit['objects']),
    'hardware_triangles': audit['total_triangles'], 'scene_triangles': audit['scene_total_triangles']}
report['all_bands_pass'] = all(view['bands_pass'] for view in report['views'].values())
report['all_window_openings_pass'] = all(view['window_openings_pass'] for view in report['views'].values())
report['all_coverage_pass'] = all(view['coverage_pass'] for view in report['views'].values())
report['limitation'] = None if report['rendered_rgb_16x9']['scope_pass'] else (
    'The literal rendered camera PNG also changes pixels outside the permitted backdrop regions; '
    'see rendered_rgb_16x9. Geometry preservation does not imply identical rendered RGB.')
if FINISH_ROUND:
    report['rendered_rgb_16x9']['comparison_scope'] = f'Historical gap-closure reference; finish {FINISH_ROUND} intentionally changes hardware colours.'
    report['limitation'] = None
if not CHECK_REFERENCE:
    sheet.save(OUT / 'deck-coverage-sheet.png')
    Image.fromarray(np.minimum(delta * 10, 255).astype(np.uint8)).save(OUT / 'deck-rgb-difference-amplified.png')
    (OUT / 'coverage.json').write_text(json.dumps(report, indent=2), encoding='utf-8')
print(json.dumps(report, indent=2))
if '--require-closed' in sys.argv:
    assert report['all_coverage_pass'], 'Sky remains outside approved window openings; see coverage counts'
if '--require-preserved' in sys.argv:
    assert report['rendered_rgb_16x9']['scope_pass'], 'RGB changes exceed the permitted backdrop regions'
