# Crimson

`Crimson.blend` is the main editable mesh, promoted from the live Blender session
after the fuselage, canopy, center web and lower shoulder corrections. It contains
39 mesh parts with 33 live Mirror modifiers using the shared ship-center anchor.
Preserve the hand-edited geometry; do not rebuild it from the archived generators.

The first texturing pass is applied in this main file. All mesh geometry,
transforms and live modifiers are preserved. `CrimsonPaintUV` maps the separate
parts to `textures/Crimson_BaseColor.png`, a shared 4096×4096 color atlas packed
into the blend and also saved externally. Mirrored parts share their paint.
The material uses this image for base color with high roughness and restrained
specular response. The saved solid viewport uses Texture color and Flat lighting
to show the painted highlights and shadows clearly.

Paint directly on the atlas in Blender's Texture Paint workspace. The original
imagegen brush source and its exact prompt are retained under `textures/`.
`previews/` contains top, side, front, three-quarter and 320-pixel game-scale
checks of the textured mesh. `textures/texture-manifest.json` records the pass.
The untextured live-session backup and bake recipe are in
`C:/Users/amind/.codex/artifact-archives/crimson-texturing-20260928-010254/`.

`orthographic/` contains top, bottom, both sides, front, rear and three-quarter
captures of this mesh. `reference/` retains the original design, profile concept
and hand-drawn game-style reference; the original in-scene references are packed.

`texture-concepts/` contains two hand-painted concept boards generated with the
built-in imagegen tool. These are paintover proposals, not UV textures applied to
the mesh. The intended style is illustrated: dark outlines, broad painted light
and shadow shapes, warm silver armor, red inserts and charcoal structure. Keep
the Blender mesh authoritative wherever a generated view deviates.

Approved texturing reference (2026-09-28): `reference/approved-texture-concept.png`,
the final hand-drawn version, copied from `texture-concepts/02-hand-painted-worn.png`.
Preserve its bold dark outlines, broad painted highlights and shadows, restrained
wear, silver armor, red inserts and charcoal structure when texturing the mesh.

The exact prompts are in `texture-concepts/prompts.json`. Earlier metallic
concepts and the classic painted alternative are superseded by this approval.

Study files and scratch results were moved outside the project to
`C:/Users/amind/.codex/artifact-archives/crimson-20260928/` for recovery.
