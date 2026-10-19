# Vanguard: independent paint and nose cleanup

Evidence for #1016. Work checkpoint: `9e4a5e0c` (independent-paint implementation `15127348e`). This is a Blender comparison; the owner deferred the Unity lineup and waived the flight check. No merge or Unity approval is implied.

The owner approved building the Valis comparison, then said the asymmetric result looked great except for the gray rectangle on the nose. `preview/final-top.png` and `preview/final-hero.png` contain that correction. `preview/asymmetric-top.png` records the first comparison, before the correction. `preview/comparison.html` compares the previous mirrored pass, the corrected result, and Valis at large size or 60/100/150 CSS-pixel ship heights. Its thumbnails are display exports; the native PNGs remain available beside it.

## Why the paint changed

The previous recipe folded X coordinates for both brush sampling and the broad sweeps. That deliberately supports shared-UV ships, but it mirrored Vanguard's marks despite Vanguard having separate paint space on opposite sides. Claude's earlier discussion and two fresh read-only CLI consultations confirmed that distinction. The consultation responses are in `consult/`; #1016 records the source discussion and the owner's scope approval.

Vanguard now uses the shared painter's optional signed coordinates, with a 30-degree diagonal centered on each panel's area centroid. Other ships retain the folded default and its existing fields. This changes Shadow and Light only; authored color regions, the approved line masks, source geometry and UVs stay intact. Valis supplies the visual reference; its historical image-projection method was not reused.

The nose's small four-face cap received a separate full-strength shadow sweep, producing the gray patch the owner rejected. The correction removes that cap's shadow sweep while retaining brush texture, facing shading, and its light layer. Only 2,725 Shadow texels changed. It is stored as an owner mask edit against the unchanged generated baseline, using the existing edit-preservation path; no nose-specific rule was added to the shared painter. The correction producer uses the canonical painter in scratch and copies only the verified Shadow result. A normal, unmodified painter run then reproduced the corrected mask and preview byte for byte.

## Verification

- Geometry lock passes; source SHA256 remains `e4c3dbf9552873ddf0a461f361d59245da13edf76208f27b0f0e56b3920fa031`.
- 18 Python unit tests pass, including signed/folded schema separation and opposite-side UV collision checks.
- `blender_paint_space.py` passes real isolated brush/sweep bakes. The folded fixture is byte-identical to the previous generator; this is fixture parity, not a full Crimson rebake.
- Full 4096-pixel bake passes. Ink, Emission and all four packed region textures are byte-identical to the previous mirrored pass.
- Nose cleanup leaves every other source mask unchanged. The normal rebake reports exactly 2,725 edited Shadow texels and zero edits elsewhere; baseline remains unchanged. See `checks/final-verification.json`.
- Independent code-quality review: no findings. ReSharper skipped because no changed C# under Assets/Scripts. `git diff --check` passes.
- Top and oblique views inspected, plus the comparison at 60/100/150 CSS-pixel ship heights. These are preview checks, not Unity runtime proof.

The opposite-side UV check refuses shared paint space before writing masks. A broader diagnostic also found three pre-existing same-side cockpit UV overlaps, deferred separately in #1019; this pass does not reopen the approved UV layout.

## Files and continuation

`implementation/` contains the changed tracked files relative to the preceding `d4f68ad3c` painted-layer checkpoint. The complete earlier authored-region implementation is in the sibling evidence folder `../authored-regions/implementation/`; `../painted-layers/` holds the preceding layer pass. `runtime-bundle/Vanguard/` is the final generated copy-ready bundle, not an installed Unity change. Producers retain the exact scratch paths used for reproducibility.

For another ship: optional `paint_space: signed` requires independent left/right PaintUV space and `sweep.angle_deg` instead of `weight_x`. This only affects brushwork and broad sweeps. It does not change base-color regions or line masks. Do not mirror UVs to opt in; preserve livery independently. Existing ships remain folded until explicitly opted in.
