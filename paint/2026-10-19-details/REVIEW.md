# Bulwark approved detail pass

Applied owner-approved concept r03 to the existing ship as authored neutral-mask edits, without projecting generated art onto the mesh. Clean nose with two small fasteners; restrained rear cross-panel seams; vent banks on armor and shrouds; segmented lilac canister rings and three-segment shoulder/skid inserts. Metal centers remain metal. No wear, chips or grunge.

The approved livery remains independent in BaseRegion. Ink and Light contain structural detail. DetailEmission is an independently editable coverage image, outside the shared generator mask set. Existing generated baseline files are unchanged; do not remove them. Retain the emission image and material bindings when regenerating.

## Verification

Fresh reload confirms identical geometry, UV and face-material fingerprints; protected BaseRegion and Shadow packed-pixel hashes; every edited packed mask matches the saved image. Cockpit and seating-rim sampled mask texels are unchanged. Geometry-lock verification passes. See verification.json and ship_lock.json. UV density previously passed and UV fingerprint is unchanged. No gameplay assets changed or Unity tests run.

Rendered front, opposite, side, top and underside views plus game-scale top. Glow uses crisp lilac shapes with 1.15 preview energy; no bloom effect is baked. Final Unity BaseRegion and emission binding, flight, breakup and in-game review remain pending. No merge.

A first scratch preview exposed Blender retaining old packed bytes after an in-place pixel update. Replacement image datablocks, remapped before save, resolved it; fresh-load exact hashes verify the source uses the edited masks.
