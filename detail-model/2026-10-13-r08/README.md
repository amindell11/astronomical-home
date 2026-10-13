# Bulwark — edge hinges

[Editable Blender file](Bulwark.blend) · [Review](index.html)

The owner requested a hinge at the flap edge so deployment can be adjusted. This revision starts from the owner's newest save, including reshaped/repositioned flaps and smaller engine housings. Compact interleaved hinge barrels and short brackets attach at the forward root edges. Both flap pairs keep their current geometry and world-space poses.

## Deployment controls

In the `Flap hinges` collection, select `Upper flap deployment` or `Lower flap deployment`. Change the Item panel's **X rotation**, or rotate around the control's local X axis. Both mirrored flaps move together. **X = 0°** restores the owner's pose. The review illustrates upper **−12°** and lower **+12°** opening relative to that pose; the saved file remains at zero.

The flap meshes remain `10 Tail fins` and `10 Tail fins.001`, with their editable vertices and existing live modifiers. `11 Upper hinge brackets` and `13 Lower hinge brackets` stay attached to the ship; `12 Upper hinge leaves` and `14 Lower hinge leaves` rotate with their flap pair. New hinge geometry is separately editable and mirrored. No owner modifiers were applied. This is an editable modeling mechanism, not a Unity animation/export setup.

## Preservation and validation

- Owner checkpoint: `ebc22a5431ebf13e808df45aed1337237f89eb61`.
- Saved owner SHA256: `362ebab5128a2e3b1db1c038d2bdbf444aaf28a3626d9693d220369a8825019e`.
- Candidate SHA256: `62bbb20bfe5a96cac53e790237d35a3159864101bcd9459d0028e78ad0711674`.
- Only the two flap parent relationships changed among existing objects; their mesh data are untouched. Maximum world-space vertex deviation at the saved pose: `8.43e-07` units.
- All other existing fingerprints match the owner's save exactly.
- Motion checks verify rigid flap movement, fixed brackets, mirrored cage symmetry, and exact return to the zero pose.
- New hinge meshes have no nonmanifold edges or zero-area faces and have positive signed volumes.
- Source checks report 60 findings before and 59 after. Reparenting the lower pair resolves its origin finding. No new findings or exemptions; the remaining findings are inherited, so overall source validation is not clean.
- Studio, orthographic/eight-view sheet, game-scale, comparison, and deployment preview renders completed without changing source hashes.

The owner save remains available at `owner/Bulwark.blend` in the evidence branch. Medium-detail shape review continues; tracked source is unchanged.
