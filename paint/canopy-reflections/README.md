# Canopy reflections — issue #1017

Unity editor renders of Crimson, Nightshade, Valis and Vanguard, in that order, using URP-Performant. The close-up sequence holds light and camera orientation fixed while each ship turns and banks; it is an asset render, not a gameplay recording. It does not claim owner art approval or a complete combat/death-playthrough.

- [Shared motion](canopy-motion.gif) · [full-resolution MP4](canopy-motion.mp4)
- [Previous planar mapping versus surface-aware motion](curvature-motion.gif) · [full-resolution comparison](curvature-motion.mp4)
- [Before / after, front](comparison.png)
- [Before / after, bank](comparison-bank.png)
- [Undersides](underside.png)
- [42 passing graphics/anatomy checks](tests-passed.json)
- [8 passing breakup PlayMode checks](breakup-passed.json)
- [Negative control: the previous planar shader fails all 4 surface-direction checks](planar-negative-control.json)
- [Earlier motion negative control: disabling translation/shear failed all 7 movement checks before the curvature refinement](motion-disabled-negative-control.json)
- [Negative control: treating Nightshade glass as a reflection fails the fixed-glass check](moving-glass-negative-control.json)
- [All 16 material/pass bindings compile without shader messages](shader-passes.json)

## Recovery provenance

The source atlas/layers are unchanged between the parent of paint update 4dbafabe9 (#1015) and base 22fbd8e1a. Recovery samples only the glass triangles using their existing UVs. Crimson's RGB artwork remains sRGB. Nightshade/Valis preserve the raw Shadow, Light and Ink channels, with strength, saturation and palette values retained in materials. Each texture's left half is the positive local-Z surface; its right half is the negative local-Z surface. Alpha records glass coverage. These standalone source drawings live outside generated hull Paint and Materials folders.

Nightshade's purple glass is fixed: its Shadow/Ink channels retain the stationary glass shading while only the segmented Light-channel glints move. Valis's triangular reflections are encoded in Shadow, so it retains full painted-reflection evaluation with its original strengths (Shadow 1.02, Light 0.20, Ink 1.50). The owner confirmed this glass/glint distinction in chat.

The extraction scripts are a one-time recovery record, not a second paint generator. Run them from scratch/canopy-reflections in the working checkout with Blender/Python and imported mesh dumps produced by the Unity inspection. source-inspection.json contains the original source fingerprints. reflection-bounds.json records the unskinned projection bounds; Valis's runtime material uses the baked skinned frame (1.35 scale), and its debris material variant overrides only its rebased projection.

Crimson's only source edit is the Canopy material assignment. Its source geometry/UV fingerprints match before the edit. All 22,854 imported surface corners match original position, normal and UV at 1e-5 precision after the material split. The added cluster-light variant corrects a separately reproduced High Fidelity lighting failure.

The curvature refinement replaces global translation, shear and width scaling with the change in reflected view direction at each surface normal, relative to its authored local-Z pose. The same bounded mapping drives the recovered artwork and Vanguard's procedural stroke construction. It preserves the drawings at the reference pose while allowing the existing curved/faceted normals to deform their movement. It is stylized reflection mapping, not a scene reflection or ray tracing. Vanguard's mapping intentionally changes in this refinement; the earlier two-pose pixel-equality result no longer describes the current shader.

The old/new comparison uses shader 374b0ef for the top row and 94da014a for the bottom row, with identical ship transforms, camera and lights. No geometry, palette, texture or material data changed in this refinement. Hull-shadow work remains deferred in #1027.

The subsequent integration check caught Nightshade's cockpit debris still using the old static glass. The cockpit surface has exactly the same local canopy bounds, so its material binding now shares the live canopy material; tail glass is unchanged. Valis's breakup continuity test now verifies its required fuselage canopy variant inherits the live material, while retaining exact identity checks for every other slot. All eight breakup PlayMode checks pass, including paint/geometry continuity, three Valis poses, fade/cleanup and revive. [Pre-fix local-coordinate inspection](debris-inspection.json) records why Nightshade can share its material while Valis requires a projection variant.

The targeted tests use URP-HighFidelity. They cover reflection motion on both authored sides, fixed Nightshade glass, Valis material strengths, prefab and debris projection bounds, damage flash/restoration and debris visibility. The new surface-direction test uses two symmetric shading normals with equal light/view response to isolate displacement from brightness. All four cases fail with near-zero displacement on the previous planar shader and pass on the new mapping. Movement is measured from reflection-on minus reflection-off images. These routed tests are local graphics proof, not the full merge suite.

The recovery and capture scripts refer to this session's agent-4 scratch paths; substitute your leased worktree path when reproducing them. The production maps and materials are the continuing authoring assets.
