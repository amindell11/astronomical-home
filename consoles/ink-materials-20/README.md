# Ink material study

This copies materials-19/console.blend. The geometry remains provisional. No object positions, mesh vertices, fold lines, camera settings or part visibility have changed.

Open console.blend, scene `20 - Ink and painted surfaces`. Materials prefixed `20 |` contain the revised console surfaces. Paint uses diffuse shading with broad pigment variation, three restrained face tones and sparse elongated scuffs. Rubber, graphite and metal keep their own roughness and muted highlights. Launch retains a clean amber face.

The view layer uses Freestyle for visible console silhouettes and creases. The `20 | Warm black drawing ink` line style controls ink color, opacity and width. The `20 - Console ink selection` collection selects existing console objects for this render treatment. It adds no mesh or curve outlines. Hologram contours, UI exports and fonts are carried forward from materials-19. Lighting is retained except for disabling the extra Launch fill light, which cast a hard diagonal patch across the button. Its own emission remains. All file images remain packed.

The review page compares the earlier camera render with this pass and includes an angled neutral-light inspection. Animated screen effects remain in the interactive prototype. This is a Blender material study, not a game integration.

apply_ink.py accepts a loaded materials-19 scene and --name, refusing an existing output. It writes a new Blender file and source manifest. render_review.py writes both PNG views without saving its temporary inspection lighting. verify.py compares the saved scene against materials-19 and fails on any change to the original geometry, transforms, camera or object inventory.

The user's standing instruction is to have a Sol sub-agent review every subsequent result and address its actionable feedback before handoff. Review rounds for this pass are recorded in review.json.
