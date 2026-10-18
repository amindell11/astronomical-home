# Preserve Vanguard's production artwork

The owner clarified on 2026-10-18 that production Vanguard looked better than the
reconstructed region concept. The requested visual changes are removal of its
baked gray lighting and addition of Crimson's brushing layers. The production
artwork is the authority: livery, panel edging, hatches, vents, core-side indicators,
warning marks, and structural line meshes remain in their original locations.

## Comparison

`review/old-versus-preserved.png`: production left; preserved artwork with the old
broad lighting removed and quiet canonical brushing right. Top above, hero below.
`review/paint-separation.png`: production, recovered flat base, recovered base plus
brush. `review/pod-detail-preservation.png` isolates the missing detail from the
rejected reconstruction. `review/preserved-scale.png` shows the same three at
96 pixels, enlarged four times.

These are native Blender placement/paint previews, not a Unity lineup or runtime
bundle. The colored composites and clean base are review images only; they are
not proposed as imported production atlases or a finished recolorable surface.
No source, geometry, UV, runtime asset, or production paint mask was changed.

## What is preserved and what changes

The recovered original generate_base.py/base_palette.py recipe carries all 37
retained authored shapes and all 37 service patches. The original structural ink,
canopy and core meshes/materials stay present. The selected Vivid orange is set
in the recovered layout; no new palette placement is invented.

The old paint_vanguard.py diagonal dark/light sweep and normal ramp are absent
from this recovered flat base. Existing structural color panels remain explicit
artwork; no grayscale threshold is used to guess what is a line or panel.

The brush preview calls the canonical ship_paint.py graph builder and bake
function from PR #1015, with its sweep/plate lines/wall ramp disabled, and keeps
the resulting Shadow/Light weights in memory. Only the combined review image is
saved. This is not a second mask generator. The house brush tile and equation are
unchanged; contrast 1.2 keeps the detail readable on Vanguard. `contrast-reference/`
archives the stronger 3.2 experiment, not the selected quieter proposal.

`review/preservation-review.json` records source/brush hashes and authoring counts.
The production blend hash remains 9cf401f99b3c820d094409d512686a290770d5c9ac999de84d4a8dc8512a74ec.

## Reproduce

Scratch producers use the recorded Windows checkout and scratch roots. The two
original base producers and their layout are present under producers/. Run
`generate_base.py --repo-root <checkout> --output-dir <review>` in background
Blender, then `preserve_production_paint.py`. The latter imports the canonical
ship-tool snapshot at PR #1015 commit 6b2f743613851747d38e5c08e925c90240e894b9;
ship_paint.py is unchanged from the earlier df12417 snapshot used locally.
Both producers read the source without saving it.

## Remaining implementation decision

The current material-slot paint contract assigns a palette color per face. The
old livery and small colored marks cross those faces. The rejected whole-part
reconstruction cannot be the implementation baseline, and face splitting was
never approved.

To retain editable color combinations while preserving the artwork, extend the
shared painter and shader with authored color-region coverage masks: color-neutral
masks identifying where each named palette region appears, independent of the
Shadow/Light/Ink and brush treatment. The old authored shapes supply their exact
coverage. Preserve the existing approved geometry/UVs and the original linework;
add no new geometry edits or palette-selection UI.

This crosses the current issue brief's exclusion of shared shader/tool changes,
so it is a fix-ladder rung-3 scope decision before implementation. The narrower
alternative is a paint-only cleanup of the original colored surface with brush
layers, deferring editable region colors. No shared renderer/tool change is in
this checkpoint.
