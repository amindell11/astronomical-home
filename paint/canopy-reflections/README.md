# Canopy reflections — issue #1017

Unity editor renders of Crimson, Nightshade, Valis and Vanguard using URP-Performant. Light and camera orientation stay fixed while the ships rotate. These are asset renders, not a combat/death playthrough or owner art approval.

- [Latest refinement, previous above/current below](extreme-motion.gif) · [full-resolution MP4](extreme-motion.mp4): 75-degree rolls, then 65-degree pitch, eight seconds.
- [Current turns and banks](canopy-motion.gif) · [full-resolution MP4](canopy-motion.mp4): six seconds.
- [Nightshade's original glints versus smaller roll accents](accents-050.png), at the same pose and with the same current shader.
- [Roll comparison](refinement-050.png) · [Pitch comparison](refinement-140.png)
- [Original static paint/current front](comparison.png) · [bank](comparison-bank.png) · [undersides](underside.png)
- [49 passing graphics/anatomy checks](tests-passed.json)
- [8 passing breakup checks](breakup-passed.json)
- [Previous shader/materials fail all 8 new regression cases](refinement-negative-control.json); the existing Nightshade fixed-glass case passes.
- [16 material/pass bindings compile without shader messages](shader-passes.json)
- [Actual Nightshade mesh: secondary-glint visibility over the roll](accents-visibility.json)

## Latest refinement

The extreme comparison uses PR head `15e476e6` above and `a457e7cc` below. The owner's clarification corrects the earlier interpretation of Valis's painted channels: its Shadow/Ink are fixed colored glass, like Nightshade's. Only Light-channel glints move. Valis's three original palette colors and Shadow 1.02 / Light 0.20 / Ink 1.50 strengths are retained. Its opposite side has no authored glints; the test now requires its glass to remain instead of incorrectly requiring colored glass to move.

Crimson's long stripes previously ended at the silhouette of the extracted canopy. The shared shader extends the interior stripe samples vertically, so pitch cannot expose those end caps. Nightshade's material places two smaller, quieter copies of its original painted glints outside the central drawing. The same reflection mapping brings those accents into view during rolls. No bitmap repainting, geometry edits, per-ship controllers or shader copies are added in this refinement.

The new tests cover all three Valis palettes retaining their glass, Crimson stripes continuing through both ends, smaller Nightshade glints entering from both sides, and Valis's unpainted opposite side retaining fixed glass. They fail against the previous shader/materials, then pass with the current implementation. Existing curvature, movement, projection, damage-flash and debris tests remain green. Routed tests are local graphics proof, not the full cold merge suite. Hull-shadow work remains deferred in #1027.

## Recovery provenance

The source atlas/layers match between the parent of paint update `4dbafabe9` (#1015) and base `22fbd8e1a`. Recovery samples only glass triangles with their existing UVs. Crimson RGB remains sRGB. Nightshade/Valis preserve raw Shadow, Light and Ink channels, with strength, saturation and palette values in materials. Left/right halves store positive/negative local-Z surfaces; alpha records glass coverage. These source drawings live outside generated hull Paint and Materials folders.

Crimson's only source change is a canopy material assignment. Source geometry/UV fingerprints and all 22,854 imported position/normal/UV corners match at 1e-5 precision. Valis's palette material GUIDs are preserved. Its live bounds use the baked skinned frame (1.35 scale), and its debris variant overrides only its rebased projection. Nightshade's detached cockpit shares the live canopy material because its local coordinates match; other glass remains unchanged. See [the debris coordinate inspection](debris-inspection.json).

The extraction scripts and `recovery-inputs.zip` are a one-time recovery record. `source-inspection.json` records source fingerprints; `reflection-bounds.json` records unskinned bounds. The production maps/materials are the continuing authoring assets. Scripts refer to this session's agent-4 scratch paths; substitute your leased worktree when reproducing.

## Earlier curvature refinement

The shared mapping uses the change in the reflected view ray at each surface normal relative to its authored local-Z pose. It replaces global translation/shear/width squeezing, allowing existing curved or faceted surfaces to deform the moving drawing. Vanguard retains its procedural stroke construction and uses the same mapping. This is stylized artwork, not scene reflection or ray tracing.

Historical files `curvature-motion.gif/mp4` and `curvature-*.png` compare planar shader `374b0ef` against curvature refinement `94da014a`; they predate the fixed Valis glass and coverage changes above. The [planar negative control](planar-negative-control.json) fails all four equal-lighting/opposite-slope displacement tests. Earlier [disabled-motion](motion-disabled-negative-control.json) and [incorrect Nightshade glass](moving-glass-negative-control.json) controls document prior regressions. Those historical results do not claim the current material interpretation.

ReSharper passed at a457e7cc: zero blocking findings, 17 report-only property-lookup hints in tests. The required combined quality follow-up reviewed 15e476e6 to a457e7cc and the matched screenshots; it found no actionable correctness, scope, comment or Unity-convention issues and made no edits.
