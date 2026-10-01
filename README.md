# Valis geometry review evidence

Non-merge evidence branch for the editable Valis geometry milestone. Approved concept C, top view and turnaround are in history/concepts. Actual model renders and the 72-frame, six-second turntable are in history/review. Flat materials are geometry-review color blocks, not production textures. Game-scale images are 60, 100 and 150 pixel Blender approximations, not Unity captures.

history/helpers/build_valis.py records geometry construction and render cameras. Its OUT and SOURCE paths name the original agent-2 lease; change those explicitly for another checkout. Run with Blender 5.1 in background mode. validate.py reopens the source and verifies finite vertices, anchored live mirrors, evaluated bilateral symmetry and no external image/library dependencies. package_review.py assembles renders with Pillow and imageio. inspect_normals.py and refine.py record intermediate diagnosis/edits and are not needed to reproduce the final source.

The side comparison includes the approved concept. fuselage-side.png hides wings to expose the revised rear-high, forward-low center-body profile. left-before-slope.png preserves the prior iteration. All other views show the latest complete model.
