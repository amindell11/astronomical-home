"""Refresh measured tables in NOTES.md from the completed Blender and image audits."""
from pathlib import Path
import json

ROOT=Path(__file__).resolve().parent
OUT=ROOT/'previews'
audit=json.loads((OUT/'mesh-audit.json').read_text())
build=json.loads((OUT/'build-report.json').read_text())
rectangles=json.loads((OUT/'screen-rectangles.json').read_text())
comparison=json.loads((OUT/'silhouette-comparison.json').read_text())
aspect=json.loads((OUT/'aspect-comparison.json').read_text())
recess=json.loads((OUT/'recess-audit.json').read_text())
coverage=json.loads((OUT/'coverage.json').read_text())
ink=json.loads((OUT/'ink-verification.json').read_text())
geometry=json.loads((OUT/'gap-geometry.json').read_text())
wall_angles=[angle for angles in recess['measured_wall_angles_degrees'].values() for angle in angles]
def vec(values):
    return '('+', '.join(f'{v:.6f}' for v in values)+')'
def name(value):
    return '`'+value.replace('|','\\|')+'`'
lines=['## Measured geometry','', '### Pod corners and recessed screens, 2026-11-19','',
       'Both upper inner housing chamfers now use 1.02 by 0.72 setbacks, parallel to the unchanged 0.85 by 0.60 glass clips. The middle and rear shell follow that same outline, removing the old notch. The top is lowered by 0.023076 local units to compensate for banking, so the wider corner does not increase the pod height. The front housing bounds remain Z = 1.057527 to 4.738953 on both sides. The other three housing corners already follow their glass clips at 45 degrees and retain their outlines.', '',
       'The pod face stays at local depth -0.10. Each glass moves from -0.008 to -0.20, placing it 0.10 units below the face. The raised screen lips are removed. Each charcoal tray flares outward at 45 degrees, with 0.008 units of clearance around the glass at its depth and a floor 0.015 units behind it. The mouth is an exact perpendicular offset of the clipped glass outline. The face bars between the two mouths are 0.044 units wide on the left and 0.084 on the right. Weapon step keys keep their frames and travel; their wells now taper outward toward the frame opening.', '',
       'Recess cuts run after the structural unions. Boolean coordinates are welded within 0.000001 units during construction and within 0.00001 before final triangulation. This removes duplicate vertices from Boolean rounding without changing visible contours. The final mesh must pass the closed-manifold and zero-area checks before it is saved.', '',
       'Corner margins below are measured at 1920 by 1080. Each value is the shortest pixel distance from the midpoint of a glass clipped corner to the projected outer perimeter of its pod face. They measure housing coverage, including the recess, rather than the dark wall alone. Large values occur beside the other display or the weapon keys. Left and right in this table mean image left and image right. The exact projected polygons and measurements are in `previews/recess-audit.json`.', '',
       '| Screen | Corner | Previous margin, px | Updated margin, px |',
       '|---|---|---:|---:|']
for c in recess['corners']:
    lines.append(f'| {c["screen"]} | {c["corner"]} | {c["before_px"]:.2f} | {c["after_px"]:.2f} |')
lines += ['', 'The two upper inner margins increase from 9.57 to 30.79 pixels. All sixteen glass corners were checked. The smallest updated corner margin is 19.12 pixels at DETAIL\'s bottom right. Glass depth changes account for the small perspective changes at the other corners; their sizes and orientations are unchanged.', '',
          f'The finished mesh has charcoal material on all 32 screen walls. Wall normals measured at their midpoints give angles from {min(wall_angles):.6f} to {max(wall_angles):.6f} degrees to the pod face.', '',
          'The six camera renders use the supplied game projection, position and direction. For each view, the audit casts rays to 715 points around and inside every glass against the finished body. All rays reach the glass without a body intersection. Every glass vertex also remains inside its projected mouth. The minimum projected glass-to-mouth clearances are listed below. These margins keep Freestyle ink away from the glass boundary.', '',
          '| View | SHIP, px | DETAIL, px | PRIMARY, px | ORDNANCE, px |',
          '|---|---:|---:|---:|---:|']
for view, screens in recess['views'].items():
    margins = ' | '.join(f'{screens[label]["minimum_projected_mouth_clearance_px"]:.2f}' for label in ('SHIP', 'DETAIL', 'PRIMARY', 'ORDNANCE'))
    lines.append(f'| {view} | {margins} |')
lines += ['', 'The original 16:9 render is preserved in `previews/deck-recess-original.png`. `previews/deck-recess-compare.png` places it beside the updated render and includes four 3x crops of the upper inner corners. These are Blender camera renders with the existing placeholder glasses and hologram, not a new Unity capture.', '',
          '### Exporter parts','',
       '| Exported part | Blender object |','|---|---|']
for role,objname in build['parts'].items():
    lines.append(f'| {role} | {name(objname)} |')
body=[o for o in audit['objects'] if o['role']=='ConsoleBody']
for obj in body:
    lines.append(f'| ConsoleBody | {name(obj["name"])} |')
lines += ['', 'The body includes the housings, canopy, strut shoulders, wraparound band, raked panels, pedestal, sill and eight decorative switches. The seven cyan strips are separate static solids assigned to ConsoleBody; none has a name ending in ` lens`. The projector is the only object with that suffix and uses the exporter\'s existing exception.',
          '', '### Moving control frames','',
          'Coordinates below are Blender world coordinates. Every mesh uses local X across its face, local Y up its face, and local +Z outward. Its pivot has unit scale. The press direction is local -Z, becoming Unity-local -Y; the knob turns around local +Z, becoming Unity-local +Y. The table gives the origin and the three world-space columns of the local frame. Full-precision values are in `previews/mesh-audit.json`.', '',
          '| Part | Origin | Local X in world | Local Y in world | Local +Z in world | AuthoredUp cap test |',
          '|---|---|---|---|---|---|']
for c in audit['controls']:
    axes=c['local_frame_columns']
    lines.append(f'| {c["role"]} | {vec(c["position"])} | {vec(axes[0])} | {vec(axes[1])} | {vec(axes[2])} | {"Pass" if c["passes_authored_up_after_export"] else "FAIL"} |')
lines += ['', '### Screen planes and content rectangles','',
          'Each glass is one planar eight-vertex polygon at local Z = -0.20, with its normal along local +Z. It triangulates to six triangles at export. Its local X and Y dimensions retain D13\'s exact sizes. Corners use the 0.20-unit clip, except SHIP\'s top inboard corner and PRIMARY\'s top inboard corner, which use 0.85 horizontal by 0.60 vertical setbacks.', '',
          'A largest plain rectangle means the maximum-area rectangle aligned with the glass\'s local X/Y axes and contained in its clipped outline. Bounds are given as left, right, bottom, top relative to the glass centre. These are geometric limits; typography may add padding inside them. The normalized bounds use the whole glass bounding rectangle as 0..1.', '',
          '| Glass | Full size | Content size | Local bounds L, R, B, T | Normalized bounds L, R, B, T |',
          '|---|---|---|---|---|']
for r in rectangles:
    lines.append(f'| {r["role"]} | {r["glass_size"][0]:.2f} x {r["glass_size"][1]:.2f} | {r["width"]:.6f} x {r["height"]:.6f} | {vec(r["bounds_l_r_b_t"])} | {vec(r["normalized_bounds_l_r_b_t"])} |')
lines += ['', '| Glass | World centre of glass plane | World local-X vector | World local-Y vector | Outward plane normal |',
          '|---|---|---|---|---|']
for s in build['screens']:
    lines.append(f'| {s["label"]} | {vec(s["origin"])} | {vec(s["u"])} | {vec(s["v"])} | {vec(s["normal"])} |')
lines += ['', 'For a plane centre C and the listed unit vectors U, V and N, a glass point is C + xU + yV and its plane equation is N dot (P - C) = 0. The two left glasses share the left pod plane; the two right glasses share the right pod plane. Screen positions are not independently rotated.',
          '', '### Mesh report','',
          '| Object | Closed | Manifold | Triangles |', '|---|---|---|---:|']
for o in audit['objects']:
    status='Yes' if o['closed_manifold'] else 'No, planar glass'
    lines.append(f'| {name(o["name"])} | {status} | {status} | {o["triangles"]:,} |')
solid_count=sum(o['closed_manifold'] for o in audit['objects'])
zero=sum(o['degenerate_faces'] for o in audit['objects'])
tiny=sum(o.get('tiny_faces_below_1e10',0) for o in audit['objects'])
lines += ['', f'The model has {len(audit["objects"])} mesh objects: {solid_count} closed manifold solids and four intentional planar glasses. Hardware total: **{audit["total_triangles"]:,} triangles** and {sum(obj["vertices"] for obj in audit["objects"]):,} vertices. All solid windings are consistent and signed volumes are positive. There are no loose vertices or unapplied modifiers. Zero-area faces: {zero}.',
          '', f'An independent Boolean-volume audit checked {audit["intersection_pairs_checked"]} object pairs whose world bounding boxes overlap. It found {len(audit["positive_volume_intersections"])} intersections above 0.0000001 cubic units. All eight moving controls and their carried legends were also checked against the body at half and full travel, using the existing builder\'s travel fractions: {len(audit["pressed_intersections"])} intersections. The report is `previews/mesh-audit.json`.',
          '', f'There are {tiny} positive-area Boolean sliver triangles smaller than 0.0000000001 square units. They remain connected to their closed surfaces; none is a loose or zero-area face.' if tiny else 'The audit found no triangles below 0.0000000001 square units.',
          '', 'The inherited hologram and backdrop below are preview objects, excluded by the old exporter\'s existing name prefixes. They are not console construction. Their open planes are intentional visual staging.', '',
          '| Excluded preview object | Closed/manifold | Evaluated triangles |', '|---|---|---:|']
for o in audit['preview_objects']:
    lines.append(f'| {name(o["name"])} | {"Yes" if o["closed_manifold"] else "No, preview surface"} | {o["triangles"]:,} |')
lines += ['', f'The entire scene, including evaluated preview curves and the backdrop, has **{audit["scene_total_triangles"]:,} triangles**, below the 70,000-triangle limit.',
          '', '### D13 departures and comparison','']
for d in build['departures']:
    lines.append('- '+d)
lines += ['', 'The camera overlay is an exact 50% blend at 1920 by 1080. The comparison sheet places the two native-resolution renders side by side with labels. The false-colour parts sheet includes each exporter role and its corresponding index.',
          '', f'The unlit hardware masks have intersection-over-union {comparison["hardware_intersection_over_union"]*100:.4f}%. They differ at {comparison["changed_pixels"]:,} of {comparison["image_pixels"]:,} image pixels. The 95th-percentile boundary distance is {comparison["edge_distance_95_percentile_px"]:.3f} pixels; the largest is {comparison["edge_distance_max_px"]:.3f} pixels. These values include filled massing gaps. They are not a claim that every pixel is identical. See `previews/silhouette-comparison.json`, `previews/silhouette-difference.png` and the overlay for the exact comparison.',
          '']
lines += ['### Screen-shape extension', '',
          'The header, outer pod housings and wraparound band continue to X = +/-27. Folded side walls join the header to the lower structure, leaving one additional side window on each side. These pieces are Boolean-unioned into the existing ConsoleBody object. The lower return continues down and outward behind the console and pod housings. The header continuation overlaps the existing beam behind its visible faces. The construction views are refitted to show the wider model.', '',
          f'Against the saved original deck hardware mask, the 1920 by 1080 silhouette has intersection-over-union {aspect["hardware_intersection_over_union"]*100:.4f}%. Changed pixels: {aspect["changed_pixels"]:,} of {aspect["image_pixels"]:,}. The 95th-percentile boundary displacement is {aspect["edge_distance_95_percentile_px"]:.3f} pixels; the maximum is {aspect["edge_distance_max_px"]:.3f} pixels. This comparison uses `previews/reference/deck-mask.png`, not the older D13 massing. The original rendered camera view is preserved beside it. See `previews/aspect-comparison.json` and `previews/deck-extension-difference.png`.', '',
          f'Moving-control frames and cap geometry are unchanged: {aspect["moving_controls_unchanged"]}. Screen-glass outlines, orientations and frames are unchanged, with only local depth changed: {aspect["screen_shapes_frames_unchanged"]}. The object names, part roles, palette, ink linesets and lamp-free construction remain intact.', '',
          '| View | Resolution | Horizontal FOV | Vertical FOV |',
          '|---|---|---:|---:|']
for view, camera in aspect['camera_views'].items():
    width, height = camera['size']
    lines.append(f'| {view} | {width} x {height} | {camera["horizontal_fov_degrees"]:.6f} | {camera["vertical_fov_degrees"]:.6f} |')
lines += ['', 'The table above gives effective fields of view after the 1.01 diagonal scale. All views use the same camera position and direction.', '',
          '### Game projection and measured edge coverage', '',
          '`deck_projection.py` applies the supplied projection to all camera previews, masks, recess checks and magenta evidence. Narrower views increase the vertical FOV by the supplied tangent ratio, then divide that tangent by 1.01. Blender uses horizontal sensor fit and `shift_y = m12 / (2 * aspect)`. A negative shift looks farther down. The 16:9 camera retains AUTO fit and its 33 mm lens.', '',
          '| View | Initial vertical FOV | Effective vertical FOV | Unity m12 | Blender shift_y | Top 3% magenta | Bottom 3% magenta | Above header magenta | Below console magenta | Outside windows magenta |',
          '|---|---:|---:|---:|---:|---:|---:|---:|---:|---:|']
for view, record in coverage['views'].items():
    lines.append(f'| {view} | {record["vertical_fov_degrees"]:.8f} | {record["effective_vertical_fov_degrees"]:.8f} | {record["m12"]:.8f} | {record["shift_y"]:.8f} | {record["top_magenta_pixels"]:,} | {record["bottom_magenta_pixels"]:,} | {record["above_header_magenta_pixels"]:,} | {record["below_console_magenta_pixels"]:,} | {record["outside_windows_magenta_pixels"]:,} |')
match=coverage['reference_16x9']
rendered=coverage['rendered_rgb_16x9']
coverage_status = 'All six aspects pass the edge-band and window-opening checks.' if coverage['all_coverage_pass'] else 'The coverage report still contains failures; inspect the counts above.'
rendered_status = 'Every changed camera pixel was backdrop above the header or below the console and is now structure.' if rendered['scope_pass'] else coverage['limitation']
lines += ['', 'The magenta renders hide the hologram, projector fan and original backdrop. A saturated magenta world is visible only to camera rays, so it does not tint the hardware. Bands contain `ceil(height * 0.03)` rows. Counts use saved PNG values R > 180, G < 80 and B > 180. The window-opening check tests the entire frame against the projected header lower edge, console upper surface and side-window boundaries. Its frozen baseline window mask also retains existing isolated antialias pixels within three pixels of a window, with every RGB channel below 80. Only those original pixels are included; this does not permit new pixels around the window. The JSON lists their coordinates, values and current differences, plus a looser chroma test and the raw counts before those baseline pixels are included. `previews/deck-coverage-sheet.png` labels all six full renders. Blender\'s projection matrix matches the supplied matrix within 0.000001.', '',
          f'The comparison reference is the camera image present at the start of this task, preserved in `previews/coverage-reference/deck-camera.png`, with its hardware mask beside it. Hardware intersection-over-union is {match["hardware_intersection_over_union"]*100:.6f}%. Changed pixels: {match["changed_pixels"]:,} of {match["image_pixels"]:,}. Added: {match["added_pixels"]:,}; removed: {match["removed_pixels"]:,}. Added mask pixels inside frozen windows: {match["added_mask_pixels_inside_frozen_windows"]:,}, including {match["added_mask_pixels_inside_frozen_strict_magenta_windows"]:,} formerly saturated magenta pixels. Mask comparisons use alpha > 127. Boundary distance is {match["edge_distance_95_percentile_px"]:.3f} pixels at the 95th percentile and {match["edge_distance_max_px"]:.3f} pixels maximum.', '',
          f'`previews/gap-geometry.json` checks all {geometry["resolution"][0] * geometry["resolution"][1]:,} pixel-centre rays. It finds {geometry["pixels"]["background_to_structure"]["pixels"]:,} backdrop-to-structure changes, no existing hit-position changes above {geometry["position_tolerance_units"]:g} units and no material or object changes. The maximum existing position difference is {geometry["maximum_existing_surface_error"]["position_units"]:.8g} units. Non-body meshes, materials, ink, linesets and camera match exactly. The {geometry["pixels"]["normal_changed"]["pixels"]} raw normal differences come from unchanged overlapping or shared-edge triangles; exact triangle comparisons and nearby rays explain them in the report.', '',
          f'The rendered 16:9 RGB image differs at {rendered["changed_pixels"]:,} pixels. The changed bounds are {rendered["changed_bounds_xy_inclusive"]}, inclusive X/Y pixel coordinates. Top-region changes: {rendered["changed_top_region_pixels"]:,} at {rendered["changed_top_region_bounds_xy_inclusive"]}; bottom-region changes: {rendered["changed_bottom_region_pixels"]:,} at {rendered["changed_bottom_region_bounds_xy_inclusive"]}. Window changes: {rendered["changed_window_pixels"]:,}; non-backdrop changes: {rendered["changed_non_backdrop_pixels"]:,}; antialias boundary changes: {rendered["changed_antialias_boundary_pixels"]:,}. Changes outside the permitted top and bottom backdrop regions: {rendered["disallowed_changed_pixels"]:,}. Individual changed-region bounds are in `previews/coverage.json`.', '',
          rendered_status, '',
          f'Of the disallowed changes, {rendered["disallowed_maximum_channel_difference_counts"]["one"]:,} differ by one RGB value. The larger changes cluster around the former top outline. `previews/deck-rgb-difference-amplified.png` shows absolute RGB differences multiplied by ten, with values capped at 255.', '',
          'The earlier reference-frustum subtraction preserved sky already visible at 16:9 and left a seam between the cap and header. The original body, including its clipped extensions, is completed before the final unions. The new crown spans X = +/-27, Y = 3.35 to 7.65 and Z = 10.61 to 15, with 0.01 overlap at the header top. The new lower skirt spans X = +/-27 and Y = -2 to 2.35, reaching down to Z = -5. Its top rises from Z = 0.20 at the front to 0.65 at the rear, behind the console and pod housings. These closed solids join the existing body without new objects. Existing controls, recesses, pod corners, materials, names and linesets are preserved.', '',
          coverage_status, '',
          '### Black side bars', '',
          'The shared exporter\'s projected Freestyle ink image causes the bars. Its `EXTEND` sampling repeats the 16:9 border texel onto geometry outside that image, turning pod outlines into horizontal black bands. The bars reproduce with assigned source materials, closed outward-facing solids and no paint bake. Missing materials and zero-thickness faces are not the cause.', '',
          'The shared exporter\'s `projected_ink` function returns white outside the reference image, which leaves the original paint unchanged. The shared exporter owns FBX, atlas generation, part mapping and the manifest beside it. This task does not execute its top-level export code.', '',
          f'`verify_ink.py` extracts only `projected_ink` from the shared exporter with Python\'s AST. It compares the returned bounded socket with the same graph\'s pre-bound input in headless Blender. The left/right black-pixel counts are {ink["unbounded_black_pixels"]} before the bound and {ink["bounded_black_pixels"]} with the shared function\'s returned socket. Changed central 16:9 pixels: {ink["central_16x9_changed_pixels"]:,}. `previews/ink-verification.json` records the sample boxes and threshold. This tests the shader graph used for paint baking without running an atlas bake or Unity import.', '',
          '`previews/local-export-verification.json` and `previews/local-export-manifest.json` record an earlier revision\'s local export. They do not verify this gap-closure revision. No new atlas bake, FBX export or Unity export was run.', '']
path=ROOT/'NOTES.md'
text=path.read_text(encoding='utf-8').split('## Measured geometry')[0]
path.write_text(text+'\n'.join(lines),encoding='utf-8')
print('Updated NOTES.md from completed audits')
