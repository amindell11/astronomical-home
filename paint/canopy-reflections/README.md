# Canopy reflections — issue #1017

Unity editor renders of Crimson, Nightshade, Valis and Vanguard, in that order. The close-up sequence holds light and camera orientation fixed while each ship turns and banks; it is an asset render, not a gameplay recording. It does not claim owner art approval or a complete combat/death-playthrough.

- [Shared motion](canopy-motion.gif) · [full-resolution MP4](canopy-motion.mp4)
- [Before / after, front](comparison.png)
- [Before / after, bank](comparison-bank.png)
- [Undersides](underside.png)
- [34 passing graphics/anatomy checks](tests-passed.json)
- [Negative control: disabling translation and shear fails all 7 movement checks](motion-disabled-negative-control.json)

## Recovery provenance

The source atlas/layers are unchanged between the parent of paint update 4dbafabe9 (#1015) and base 22fbd8e1a. Recovery samples only the glass triangles using their existing UVs. Crimson's RGB artwork remains sRGB. Nightshade/Valis preserve the raw Shadow, Light and Ink channels, with strength, saturation and palette values retained in materials. Each texture's left half is the positive local-Z surface; its right half is the negative local-Z surface. Alpha records glass coverage. These standalone source drawings live outside generated hull Paint and Materials folders.

The extraction scripts are a one-time recovery record, not a second paint generator. Run them from scratch/canopy-reflections in the working checkout with Blender/Python and imported mesh dumps produced by the Unity inspection. source-inspection.json contains the original source fingerprints. reflection-bounds.json records the unskinned projection bounds; Valis's runtime material uses the baked skinned frame (1.35 scale), and its debris material variant overrides only its rebased projection.

Crimson's only source edit is the Canopy material assignment. Its source geometry/UV fingerprints match before the edit. All 22,854 imported surface corners match original position, normal and UV at 1e-5 precision after the material split. Vanguard has zero changed pixels in both full-lineup before/after poses.

The recovery and capture scripts refer to this session's agent-4 scratch paths; substitute your leased worktree path when reproducing them. The production maps and materials are the continuing authoring assets.
