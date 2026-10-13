# Bulwark — fitted flap roots

[Editable Blender file](Bulwark.blend) · [Review](index.html)

The owner repositioned the upper pair and added a lower pair, treating the new tails as armored flaps. This revision fits separate tapered mounting shoes between their forward roots and the existing boom/engine armor. Their aft ends remain free and the exhaust openings stay clear.

## Editing

- `10 Tail fins` remains the owner's upper pair.
- `10 Tail fins.001` remains the owner's lower pair.
- `11 Upper flap mounts` and `12 Lower flap mounts` are the new independent meshes in `Flap mounts`.
- Both mount meshes have a live Mirror and small Bevel. Tab into Edit Mode to reshape the right-hand cage; both sides update.
- The mounts are independent from the flaps, so repositioning a flap does not move its mounting shoe automatically.
- Gray materials and viewport overlays remain enabled. All existing vertex coordinates, transforms, parents, modifiers, references, and other fingerprinted data match the owner's save. The lower pair was linked to `role.hull` for review/export bookkeeping.

## Validation

Owner checkpoint: `9b7cb3ed6f5ed1834a1ebfc0ad14d04b7cc03514`.

Saved owner SHA256: `afbc3c140f3716d3d50e11e7e3dd04080c750a443a0cba7b1656c555f48dec1d`.

Candidate SHA256: `1477dbfe5d96727133c6ae2aa2bc8b18f6bc012bf7e1833f3d244077da7d4b3b`.

Per-part fingerprints confirm all existing objects are unchanged. The only additions are two mount meshes and their editing parent. The evaluated mounts are closed and have outward normals, no nonmanifold edges, and no zero-area faces.

Source checks retain 59 inherited findings from 60 before the change. Linking the lower pair to `role.hull` resolves its missing-role finding; no new findings or exemptions were added. This is not an overall clean source-validation result. Owner transforms and modifiers remain editable.

Studio, orthographic, eight-view, game-scale, and before/after renders completed without changing source hashes. The comparison includes all visible meshes in both versions, including the owner's lower flaps before their role was assigned. The difference views isolate the added mounts.

Medium-detail shape review continues. The tracked source is unchanged.
