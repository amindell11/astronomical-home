# First material pass

The source is geometry-18/console.blend. Proceeding to materials does not approve the unresolved joins. All existing geometry, fold lines, transforms, and visibility remain unchanged.

Open console.blend, scene `19 - Neutral materials and electronic light`. Materials prefixed `19 |` hold the editable palette. Warm limestone paint and warm grey covers sit against graphite casing, satin rubber and brushed nickel. Amber is reserved for powered controls. The lights are separate in `19 - Lighting and surface labels`; the compositor group controls restrained glow.

The main and detail screens reuse the selected UI exports from blender-console-06/inputs/ui. The ship reuses its authored hologram materials, muted palette, and contour curves. The starfield reuses reference-scene-03/inputs/nebula-background.png. All images and the shared Rajdhani label font are packed. Added objects are the background, lights, Launch lettering, and reused hologram contour curves; no mechanical parts were added or moved.

The review offers electronic lighting, a neutral-light material inspection, and a clay comparison. Animated canopy/display glitches and phosphor persistence remain in interactive-console-05; this Blender pass is static. The images are material studies, not final ink rendering or a game integration.

apply_materials.py accepts a loaded geometry-18 file and --name, refusing an existing output. It writes the packed Blender scene and a palette/source manifest. render_review.py writes console-camera.png and console-inspection.png without saving rendering-only changes. verify.py compares the saved scene with geometry-18 and writes verification.json, failing on changed original geometry or unpacked image dependencies.
