# Residual hull shading correction

Source commit: `1ed9aea91750e6c95121b07f533b8c9670d0e238`. Previous prototype: `c51bfd8f05b726911e9e019f1329713a3d322320`. PR #1029, issue #1027.

The owner identified two diagonals behind the canopy. The central diagonal came from reconstructing fine color detail by dividing the painted color by a coarse mip of the same masks. That ratio retained baked lighting and became unstable near zero at the materials' shadow strength of 1.02. The correction removes that operation and the surface-angle blend back into the old shaded color. Shadow and Light now bias the lighting transition only; their influence fades to zero at fully front/back-facing light. Ink is separate. All faces of the two enabled armor materials participate.

The adjacent dark triangle survives removing masks, ink and cast shadows, and appears in the normals-only probe. It is a geometric surface-direction change. This correction preserves that response. No geometry, UVs or source masks were changed. Fully lit armor is smoother; the combined masks cannot supply lighting-independent brush texture without reintroducing the old shading.

## Comparison

Left is the previous prototype shader compiled from `v1.shader`; right is the corrected shader. Both use the same Valis prefab, jade-iris materials, pose, camera, and main light. Only Ivory and Cool gray use the prototype. `direct-light.png` uses frontal light to demonstrate the absence of fixed painted diagonals; `closeup.png` uses the same angled light as the motion comparison. Authored mask shapes remain visible at lighting transitions.

The ten-second clip has three equal sections: ship bank/turn with a fixed light; moving light with a fixed ship; camera orbit with fixed ship and light. Inspection and approximately 100-pixel ship sizes appear together. These are controlled Unity Editor renders under Performant quality, not gameplay, a performance measurement, or validation of other ships and palettes.

## Diagnosis

`ablation-normals.png`, left to right: previous prototype; candidate with the final formula; normals-only on the two armor materials; flat unlit palette on those materials. Other materials retain their shader. The side triangle is present in the normals pass and absent only in flat unlit color. `materials.png` identifies the central armor as Ivory (red) and the adjacent armor as Cool gray (green).

## Verification

The regression uses a sharp shadow-mask edge reaching white at strength 1.02, on flat and 55-degree local-space surfaces. Both cases failed against the previous shader (3/5 checks passed) and pass after correction (5/5). It also checks light response, fixed ink, camera-independent diffuse color, and damage-flash restoration with the feature enabled and disabled. These HighFidelity graphics tests are scoped resident-editor checks, not full-suite merge proof. ReSharper has zero blocking findings. See the attached JSON summaries.

## Reproduction

Adjust the output path in `HullCapture.cs`, then run `Revision` and `Direct` in a coordinator-leased Unity Editor. `Ablation` reconstructs the diagnostic from `v1.shader`. `publish_revision.py` labels and encodes the frames. No capture scaffolding is part of the production PR.
