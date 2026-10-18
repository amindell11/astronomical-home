# Authored Vanguard region masks — 2026-10-18

Issue: #1016. The owner approved extending the shared painter and DrawnSurface shader with authored neutral region coverage. This replaces the rejected region redraw with the original livery and linework, removes the broad baked gray lighting, and adds Crimson's brush layers. The existing geometry, UV, and vent-clearance approvals remain; the owner waived the flight check.

## Implementation

`implementation/art/ships/vanguard/` is the canonical source, paint settings, palettes, authored placements, and owner-editable masks including their regeneration baseline. The 89 ordered placements recover the original base fields, 37 retained small markings and 37 service patches. Additional alias targets align the paint with the three approved detail assemblies. Colors live in `palettes.json`; the coverage masks carry no colors.

`implementation/art/tools/ship/` and the shader under `implementation/src/` snapshot the shared extension reviewed in the task branch. Up to 16 grayscale region masks pack into four linear RGBA textures. Palette materials use the same masks with different colors. Existing ships keep the material-slot-only path. `runtime-bundle/` and `export/` are generated inputs for the pending Unity import, not installed runtime assets.

## Checks

- Sixteen Python tests pass.
- Authored-region Blender integration passes: face-crossing stripe, RGBA alpha channel, palette swap without mask changes, preserved owner edits, rejected unknown part without modifying outputs.
- Existing full Blender ship-tool smoke passes, including intact/debris export and PaintUV in UV0.
- Vanguard geometry lock, UV density, full-resolution paint, and FBX export pass.
- Every evaluated world-space vertex in the 12 original visible meshes is byte-identical. Only the three previously approved detail assemblies are additional.
- ReSharper ratchet reports no changed C# under Assets/Scripts and skips. The required combined review found no functional issues; its stale PNG docstring finding was corrected.

Reports and logs are under `checks/`. Producer scripts are under `producers/`; the canonical shared painter is the only mask generator.

## Visual evidence and remaining work

`preview/` shows the actual canonical mask composition on the current source in Blender, using the original diagnostic settings. These images verify texture placement; they are not Unity shader or lighting approval. The original structural-ink mesh and separate canopy/core materials are retained in the source diagnostic. The all-textured evaluated diagnostic used different object outlines and is not an acceptance reference.

Unity import, shader compilation, runtime palette binding, and a lineup beside Crimson at large/game scale are still pending. The owner explicitly deferred Unity review and asked to keep the primary editor open. The unused Vanguard launch ticket was cancelled; no Unity editor was attached to or changed. No runtime prefab, existing ship materials, or FBX were changed in this checkpoint. No PR or merge is claimed. The attached reference is the owner's selected visual target, not approval of an unperformed Unity check. Once the lineup passes, obtain the owner's mask approval before the breakup stage.

## Continue

Resume lease `vanguard-redo` through the pool script. Use the returned slot, which may differ from agent-4. After the primary editor closes, start the leased editor through `unity_access.ps1`; never launch Unity beside the coordinator. Import the canonical export and bundle, preserving existing GUIDs through AssetDatabase moves/overwrites. Keep the original runtime material/geometry available for an in-memory before/after lineup. Complete shader/import/anatomy checks, then publish the actual Unity lineup. The earlier source diagnostic cannot substitute for this check.

## Selected line treatment

The owner selected `preview/owner-reference.png` after rejecting the darker last pass. Its pixels exactly match the earlier preservation preview. The extra darkness came from the source scene's inherited Freestyle crease selection adding black outlines to every new hatch/slat edge. This was an authoring input error: the line pass now excludes the three new detail assemblies via the `Uninked detail` collection, preserving their softer painted lines. Original structural ink remains untouched. `checks/line-selection-audit.json` proves all geometry, UV, and material fingerprints are identical before and after this render-only correction.

The default and alternate bakes precede that line-selection edit. Their source SHA is recorded accurately; the edit changes only Blender's supplemental line pass and does not invalidate those masks. The final source validation, lock verification and export were run after the edit. `checks/palette-independence.json` confirms all eight runtime PNGs are byte-identical between palette runs.

The quality review found no functional issues. Its sole touched-hunk docstring correction is included. Unity compilation, bindings, native appearance, and breakup remain outstanding. No merge authorization was given.
