# Nightshade — first mechanical detail candidate

Source: [f12664a4](https://github.com/amindell11/astronomical-home/commit/f12664a4d3c6fbc1e599c0df0f904d2aa58aba88). **Detail-model stage; owner review pending.**

The owner approved the medium-detail proportions at `124a71cf` and asked to continue with detail modeling. That approval is recorded in [arc #868](https://github.com/amindell11/astronomical-home/issues/868) and [approval.json](approval.json). The current straight fork pair is retained. The pipeline geometry lock remains reserved for an approved flight check.

## Visual evidence

The approved form, with shallow angular armor plates, panel joints, wing cooling louvers, cockpit latches, service covers and an aft exhaust insert. These are editable geometric parts, not paint textures.

![Detail candidate](round-01/review-iso.jpg)

![Approved source and detail candidate](round-01/comparison-iso.jpg)

![Close-ups](round-01/review-details.jpg)

<details><summary>Turnaround, game scale and validation</summary>

![Review sheet](round-01/sheet/sheet.png)

![Game scale](round-01/scale/scale.png)

- [Top](round-01/material/top.png)
- [Side](round-01/material/side.png)
- [Rear isometric](round-01/material/iso_rear.png)
- [Underside](round-01/material/underside.png)
- [Tail detail](round-01/closeups/tail.png)
- [Sanctioned source comparison](round-01/compare/compare_top.png)
- [Source check](round-01/check/ship_check.json)
- [Edit audit](round-01/edit-audit.json)
- [Fingerprint preservation](round-01/fingerprint-delta.json)

</details>

Blender previews only; no in-engine, flight, paint or completed detail-stage approval is claimed.

## Preservation and validation

Every original mesh retains its exact fingerprint: stored vertices, topology, transforms, material assignments and live modifiers. New detail parts have their own live Mirrors. The existing duplicate editing origin was parented under the common identity origin without changing world transforms. Its visible fin is now in the hull role. The hidden older fin and three hidden trim parts remain editable and hidden, in `ignore`, so export matches the owner's visible shape.

`ship_check` passes with zero unresolved findings and 9 active scale exemptions explicitly approved by the owner. The manifest retains all approved scale exemptions, including the hidden trim. No transforms or modifiers were applied. Original source visibility is preserved. No original parts were deleted or regenerated.

This candidate keeps the straight forks rigid, with no moving assemblies proposed. `Engine.Main` marks the new exhaust insert. Weapon socket placement and flight integration remain to be resolved before a flight-check candidate; this first detail round does not change the production loadout or mounts.

Source SHA-256: `5a6c38fb5e3472ef6ac7d4738bf68cfc4d490fd2a1e4201d7bf4045da15df2bf`. Source and manifest are committed on the existing task branch. Production legacy hull, ShipLegacyList, Unity integration, breakup-slot deferral and the owner's separate `Nightshade1.blend` are untouched. No PR.

Appended to evidence parent `724e44eddb8b0bfe69ebcbedf4437420ef3a36ab`; previous blockout evidence and pins are preserved.
