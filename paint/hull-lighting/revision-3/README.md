# Independent brushstrokes under Unity lighting

Source commit: `83aa4b156219d4577a49c00d9ed09178f26fb1e2`. Previous correction: `1ed9aea91750e6c95121b07f533b8c9670d0e238`. PR #1029, issue #1027.

## Why the brushstrokes disappeared

The three-mask recipe folds brush darkening into Shadow and brightening into Light (decision in #1014, comment 6186465191). Valis's ORA contains only Shadow, Light, and Ink. The preceding correction stopped those masks contributing fixed color, removing their brush texture as well. Fully lit armor therefore looked flat.

## What changed

The original house brush-source PNG is now an independent UV-bound shader input on the same Ivory and Cool gray armor materials. It varies their base palette before main-light shading; it does not derive texture from a shadow-colored denominator. The source PNG is copied unchanged from `art/ships/crimson/textures/painted-brush-source.png`, SHA256 `d17bebf965a771c386ab3534006cb4ca97ed263495e573b5b4c46f935582a76c`. The house recipe's neutral value 0.625 and contrast 3.2 are retained. Final UV scale 3.3 avoids an exact mirrored repeat between the atlas halves. This reuses the brush texture with new UV placement; it does not reconstruct the old world-projected stroke positions.

Shadow and Light continue to shape the dynamic lighting transition, while Ink stays separate. No source masks, geometry, UVs or paint generator were changed. The adjacent geometric triangle is unchanged; the preceding revision contains its normals-only diagnostic.

## Comparison

Left: previous correction, with flat fully lit armor. Right: independent brushstrokes. Both use the same Valis prefab, material colors, pose, camera and light. The left shader is compiled from `v2.shader`. Frontal light in `direct-light.png` shows strokes without the old baked diagonal. `closeup.png` uses the clip's angled light.

The ten-second clip shows ship bank/turn under a fixed light, a light sweep over a stationary ship, then camera-only orbit. Inspection and approximately 100-pixel ship sizes appear together. Captures use Performant quality; graphics tests use HighFidelity. These are controlled Unity Editor renders, not gameplay, performance measurements, or fleet validation. `brush-options.png` shows preliminary UV scales 2, 4 and 8; the final value is 3.3.

## Verification

The strengthened regression requires color variation from an independent brush texture under frontal light while a sharp Shadow edge disappears, on flat and 55-degree local-space surfaces. Both cases fail on the preceding shader (zero brush variation, 3/5 total passing) and all five checks pass after the fix. The fixture also checks light response, fixed ink, camera independence, and damage-flash restoration in both shader modes. ReSharper has zero blocking findings. These are scoped resident-editor checks, not full-suite merge proof.

## Reproduction

Adjust the output root in `BrushCapture.cs`; run its `Revision` and `Direct` entries in a coordinator-leased Unity Editor, then `publish_brush.py` to label and encode. The capture source is evidence only and does not ship in the production PR.
