# Valis geometry review evidence

Non-merge evidence branch for the editable Valis geometry milestone. Approved concept C, top view and turnaround are in history/concepts. Actual model renders and the 72-frame, six-second turntable are in history/review. Flat materials are geometry-review color blocks, not production textures. Game-scale images are 60, 100 and 150 pixel Blender approximations, not Unity captures.

history/helpers/build_valis.py records geometry construction and render cameras. Its OUT and SOURCE paths name the original agent-2 lease; change those explicitly for another checkout. Run with Blender 5.1 in background mode. validate.py reopens the source and verifies finite vertices, anchored live mirrors, evaluated bilateral symmetry and no external image/library dependencies. package_review.py assembles renders with Pillow and imageio. inspect_normals.py and refine.py record intermediate diagnosis/edits and are not needed to reproduce the final source.

The side comparison includes the approved concept. fuselage-side.png hides wings to expose the revised rear-high, forward-low center-body profile. left-before-slope.png preserves the prior iteration. All other views show the latest complete model.

## User shape cleanup

history/user-shape-cleanup preserves the user's complete live Blender state before cleanup, including unsaved edits. Cleanup fits the neighboring dorsal borders, cheeks, underside armor/stripe and accents to those edited forms. The central fuselage, canopy, primary wing outlines, outriggers, tail prongs and engine shape remain unchanged. A support's Solidify modifier now precedes Mirror to correct its asymmetric thickness. The final world-space bilateral error is below 0.000001 units. The original build helper is historical and must not overwrite the user's reshaped source.

Shoulder repair: history/shoulder-repair/ preserves the pre-repair live model, replacement topology, validation, and actual-model before/after renders. The shoulder surface follows short cross sections through the notch; all original boundary corners and other parts remain unchanged. Mirror and thickness remain live.

Cockpit fit: history/cockpit-fit/ preserves the live pre-edit model, final topology and actual-model renders. The cockpit upper hull extends beyond the canopy outline; cheek panels follow the widened hull. The canopy and all other parts are unchanged.

Profile refinement: history/profile-refinement/ preserves the user-edited starting model and before/after side profiles. The lower hull uses a tucked chin and straighter rear taper; underside armor follows. Upper hull, canopy fit, width, and other parts are unchanged.

Flat shoulder and symmetry repair: history/flat-shading-symmetry/ preserves the live starting state and checks. Shoulder smooth shading is disabled. Outrigger, toe and socket Solidify modifiers precede Mirror so the rotated and scaled parts have symmetric thickness. Source vertices and transforms are unchanged.
