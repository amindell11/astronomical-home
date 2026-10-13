# Bulwark — edge hinges

[Editable Blender file](Bulwark.blend) · [Review](index.html)

The owner requested a hinge at the flap edge so deployment can be adjusted. This revision starts from the owner's newest save, including reshaped/repositioned flaps and smaller engine housings. Compact interleaved hinge barrels and short brackets attach at the forward root edges. Both flap pairs keep their current geometry and world-space poses.

## Deployment controls

In the `Flap hinges` collection, select `Upper flap deployment` or `Lower flap deployment`. Change the Item panel's **X rotation**, or rotate around the control's local X axis. Both mirrored flaps move together. **X = 0°** restores the owner's pose. The review illustrates upper **−12°** and lower **+12°** opening relative to that pose; the saved file remains at zero.

The flap meshes remain `10 Tail fins` and `10 Tail fins.001`, with their editable vertices and existing live modifiers. `11 Upper hinge brackets` and `13 Lower hinge brackets` stay attached to the ship; `12 Upper hinge leaves` and `14 Lower hinge leaves` rotate with their flap pair. New hinge geometry is separately editable and mirrored. No owner modifiers were applied. This is an editable modeling mechanism, not a Unity animation/export setup.

## Preservation and validation

- Owner checkpoint: `82dcdda7cf250d766c6e05800e697ec7ce488823`.
- Saved owner SHA256: `f93b5fb2cd35fcadffdff999ca590cd3152dd9476aa73e1f882cf61915acb81a`.
- Candidate SHA256: `0fe0ee9690d0f3e16791d6398fc8bcbe168641847f2c501881a9d10326eaf3a3`.
- Only the two flap parent relationships changed among existing objects; their mesh data are untouched. Maximum world-space vertex deviation at the saved pose: `8.43e-07` units.
- All other existing fingerprints match the owner's save exactly.
- Motion checks verify rigid flap movement, fixed brackets, mirrored cage symmetry, and exact return to the zero pose.
- New hinge meshes have no nonmanifold edges or zero-area faces and have positive signed volumes.
- Source checks report 61 findings before and 60 after. Reparenting the lower pair resolves its origin finding. No new findings or exemptions; the remaining findings are inherited, so overall source validation is not clean.
- Studio, orthographic/eight-view sheet, game-scale, comparison, and deployment preview renders completed without changing source hashes.

The owner save remains available at `owner/Bulwark.blend` in the evidence branch. Medium-detail shape review continues; tracked source is unchanged.
