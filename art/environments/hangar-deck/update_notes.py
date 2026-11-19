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
          'The five camera renders use the original camera position and direction. For each view, the audit casts rays to 715 points around and inside every glass against the finished body. All rays reach the glass without a body intersection. Every glass vertex also remains inside its projected mouth. The minimum projected glass-to-mouth clearances are listed below. These margins keep Freestyle ink away from the glass boundary.', '',
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
lines += ['', f'The model has {len(audit["objects"])} mesh objects: {solid_count} closed manifold solids and four intentional planar glasses. Hardware total: **{audit["total_triangles"]:,} triangles**. All solid windings are consistent and signed volumes are positive. There are no loose vertices or unapplied modifiers. Zero-area faces: {zero}.',
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
          'The header, outer pod housings and wraparound band continue to X = +/-27. Folded side walls join the header to the lower structure, leaving one additional side window on each side. These pieces are Boolean-unioned into the existing ConsoleBody object. The new lower pieces stop at the original horizontal camera span. The header continuation overlaps the existing beam behind its visible faces. The construction views are refitted to show the wider model.', '',
          f'Against the saved original deck hardware mask, the 1920 by 1080 silhouette has intersection-over-union {aspect["hardware_intersection_over_union"]*100:.4f}%. Changed pixels: {aspect["changed_pixels"]:,} of {aspect["image_pixels"]:,}. The 95th-percentile boundary displacement is {aspect["edge_distance_95_percentile_px"]:.3f} pixels; the maximum is {aspect["edge_distance_max_px"]:.3f} pixels. This comparison uses `previews/reference/deck-mask.png`, not the older D13 massing. The original rendered camera view is preserved beside it. See `previews/aspect-comparison.json` and `previews/deck-extension-difference.png`.', '',
          f'Moving-control frames and cap geometry are unchanged: {aspect["moving_controls_unchanged"]}. Screen-glass outlines, orientations and frames are unchanged, with only local depth changed: {aspect["screen_shapes_frames_unchanged"]}. The object names, part roles, palette, ink linesets and lamp-free construction remain intact.', '',
          '| View | Resolution | Horizontal FOV | Vertical FOV |',
          '|---|---|---:|---:|']
for view, camera in aspect['camera_views'].items():
    width, height = camera['size']
    lines.append(f'| {view} | {width} x {height} | {camera["horizontal_fov_degrees"]:.6f} | {camera["vertical_fov_degrees"]:.6f} |')
lines += ['', 'The wide views retain the 16:9 vertical FOV. The 16:10 and 4:3 views retain its horizontal FOV. All use the same camera position and direction.', '',
          '### Pending height closure', '',
          'The supplied 16:9 reference already exposes a 14-pixel sky strip above the header and small sky gaps along the bottom. Extending the header and apron across those rays would change that reference. The side extension is complete, but the header height and apron remain unchanged pending the owner\'s choice between exact 16:9 preservation and closing those existing edge gaps. The taller previews therefore still show sky above and below the model. This is not a completed height-coverage pass.', '']
path=ROOT/'NOTES.md'
text=path.read_text(encoding='utf-8').split('## Measured geometry')[0]
path.write_text(text+'\n'.join(lines),encoding='utf-8')
print('Updated NOTES.md from completed audits')
