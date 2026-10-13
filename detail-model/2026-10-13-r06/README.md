# Bulwark — separate tail fins

[Editable Blender file](Bulwark.blend) · [Review](index.html)

The owner considers the medium-detail model nearly dialed in and requested the missing swept tail pieces from the in-scene reference as a new part to adjust.

`10 Tail fins` is a new independent mesh under the `Tail fins` editing group and collection. Its top footprint follows the bone-colored aft tips on the packed turnaround. A thin, rising profile leaves the engine openings clear. It has a 12-vertex cage, live Mirror across `SymmetryOrigin`, and a small live Bevel. Edit the right-hand fin to change the mirrored pair; object transforms adjust it independently of the engine housings. Gray materials and viewport overlays remain.

All existing meshes, parent transforms, modifiers, and reference objects match the owner's latest save. The new mesh and its editing parent are the only additions. The source now has 15 visible mesh objects.

## Checks

- Owner checkpoint: `44c6a0791767235e6140804d3ca74631b9138ea8`.
- Saved owner SHA256: `72544cec74fb246faec56a6e1af73dd4e004e4355a9ab0fca284404b110fab6e`.
- Candidate SHA256: `67d7f875b6b6b8248a6eee410a837f6d75f740f362f146bbfd25a5e2146d2d14`.
- Fingerprints confirm every existing object is unchanged.
- Source checks: 52 inherited findings before and 52 after; none added by the fins. No exemptions added.
- Evaluated fins have no boundary or nonmanifold edges.
- Studio, orthographic, eight-view, game-scale, and before/after render sets completed. Rendering left source hashes unchanged.

This is a separately editable addition for owner adjustment. Tracked source is unchanged; no art-stage advance.
