# Baked Valis brush layer

Source commit: `8c8f3933ed85d2ad319f59e16c0ea829acf24371`, PR #1029, scope issue #1027. The owner approved the brush-only exporter after the runtime-projection wing-slip finding.

The raw tiled brush exposed Valis's split atlas coordinates as a seam on top of the hull. Runtime surface projection removed the seam, but moved through vertices during wing skinning. Rung 1 fixes both by baking signed source-surface pigment into the existing PaintUV atlas once, then sampling that atlas at runtime.

`ship_paint.py --brush-only` reads strict `brush.json` settings, uses the approved geometry lock, checks independent UV sides, and reuses the painter's emission bake. It exports only Brush.png and its report under --out. It never runs the old Shadow/Light/Ink generator or creates a mask baseline. The original blend, edited masks and brush source remain unchanged. Runtime uses a Valis-specific 4096 linear atlas with mipmaps, clamp wrapping, and the existing brush GUID; the former shared raw tile is removed. Only jade-iris Ivory and Cool gray enable this prototype.

## Visual review

The left arm is the former raw UV brush; the right is the baked layer. Both receive identical camera, geometry and lighting. `direct-light.png` and `closeup.png` show continuous pigment across the center; `front.png`, `bank.png`, and `grazing-light.png` show broader response. The 160-frame motion comparison covers ship bank/turn, moving key light, camera orbit, then actual wing-bone sweep, with an approximately 100-pixel ship below each close view.

These are controlled High Fidelity camera renders in graphics-enabled batch Unity 6000.1.8f1. They are not gameplay or a performance measurement. The existing geometric facet near the canopy remains; this change targets brush continuity and attachment.

## Verification

- Nineteen Python contract/region tests pass.
- Blender's disconnected-island fixture has a maximum seam mismatch of 0.95455/255, retains visible brush variation, reproduces identical bytes, preserves source/masks without a baseline, rejects mixed generation arguments, and enforces its geometry lock.
- The old folded paint output is byte-identical to the previous painter. Signed brush/sweep isolation tests pass.
- Valis's bake reports unchanged source hash, matching geometry lock, UV fingerprint, tile hash, size and settings in `brush-bake.json`.
- Five Unity graphics tests pass: feature-off/on flash restoration, existing paint import, frontal brush visibility while the old shadow stripe disappears, main-light response, fixed ink, camera independence, whole-object motion, and vertex deformation with preserved UVs on flat and sloped surfaces.
- `BakedWingProbe` draws the actual shader's brush path in fixed atlas coordinates before/after sweeping Valis's four wing bones. Maximum vertex displacement is 0.6552084 units; zero of 99,006 covered texels change at all. The preceding local-projection candidate changed 41,470 of those texels by more than 2/255 (revision-4).
- ReSharper: zero blocking findings. The combined quality review is recorded on the PR.

The Unity result is the Pipeline test runner's direct result from the coordinated batch process, not a cold full-suite merge proof. The batch process exited successfully and its lease was released.

## Reproduction

Run the committed `blender_brush_export.py` and `blender_paint_space.py` tests in Blender 5.1.2. Export Valis using its `ship.json` and `--brush-only`, then import the returned SHIP_BRUSH path with the committed metadata.

`BakedCapture.Revision` and `.Direct` render the matched comparisons. `BakedWingProbe.Run` isolates real wing attachment using `v5.shader`; `publish_baked.py` labels and encodes the images. `brush_batch.ps1` is the coordinator's graphics-batch child used for this review. All capture helpers are evidence only.
