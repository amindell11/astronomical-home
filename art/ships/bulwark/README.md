# Bulwark

`Bulwark.blend` retains the owner's geometry, live modifiers and organized parts. `lock.json` was refreshed with owner permission to fingerprint that geometry for texturing; flight approval and UV-density correction remain pending.

The approved symmetric plum/bone/charcoal coat remains in `layers/BaseRegion.png`, unchanged pixel for pixel. Its forward stripe stays parallel to the hood edge and crosses the nose joint. Charcoal armor and shoulder insets, plum trim and neutral metal canisters retain their palette colors. Material duplicate suffixes are now named `Secondary` for the paint tool's region schema; face assignments are unchanged.

`paint.json` uses the shared `ship_paint` workflow. `palettes.json` names every source material region in linear sRGB. `layers/Shadow.png`, `Light.png` and `Ink.png` contain generated plate outlines and tonal shapes plus authored canopy reflections. The original generation is preserved under `layers/baseline/`: never remove it. Rerunning the tool preserves the canopy edits as a difference from that baseline. Brush contrast is zero; no wear, chips or grain are added. `textures/painted-brush-source.png` is the shared Crimson tile used by the sweep recipe.

The active Blender materials show a flat drawn composite. Dark cockpit glass has narrow painted reflection bands, with no physical glossy reflection. The bone coat blends before shading. `Bulwark BaseRegion` remains the editable paint canvas: white adds bone, black reveals the substrate. The canopy's active paint image is `Bulwark Drawn Light`; its shadow and ink images can also be edited. Save edited images and repack them before saving the Blender source. After a tool rerun, reload and repack the three active `Bulwark Drawn` images.

Earlier shading images remain packed for preservation but are disconnected from the active output. `FinishShadow.png` and `FinishLight.png` belong to that superseded Blender preview. Current strengths and status are in `paint-settings.json`.

This is a Blender texture review, not Unity approval. The tool-generated Unity bundle still needs the approved BaseRegion coat binding before import: the generic bundle only knows flat material regions, and the old emission-channel workaround puts bone over the shading. Do not ship that incomplete bundle. The next integration must preserve the coat beneath shadow/light/ink, then obtain a fresh in-game lineup review. Flight, UV-density correction, gameplay integration and breakup remain pending.
