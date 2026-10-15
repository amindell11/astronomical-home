# Bulwark owner livery cleanup

Applied to the owner's saved rough paint, checkpoint fbee1779, after the owner instructed us to open Blender and apply the preview. The reopened source matched that checkpoint. Geometry, UVs, material assignments and Shadow pixels matched before and after application.

The asymmetric layout is preserved. Paired side stripes and white panel borders are straightened, brush wobble and isolated marks removed, and broad shading retained. Per the owner's clarification, the two extra plum nose bars wrap over the nose edge toward the underside. Their fitted boundaries project through the nose side faces.

The earlier edge-wear cleanup reached external PNGs but its packed image payloads remained older. The application embeds explicit PNG bytes. A fresh Blender load passed geometry, UV, material, packed payload and decoded pixel hash checks; see `cleanup-result.json` and `saved-verification.json`.

`saved-side.png`, `saved-top.png`, `saved-quarter.png`, and `saved-front-quarter.png` render the saved source using the same forest studio lighting as the owner's viewport. `clean-BaseRegion.png` matches the applied source texture. Native Unity integration remains separate.

An earlier application attempt stopped before edits when the owner continued working. The subsequently reopened file contained the checkpointed paint used for this cleanup. No unsaved live session was overwritten.
