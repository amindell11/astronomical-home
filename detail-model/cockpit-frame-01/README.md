# Nightshade — canopy frame detail

Source: [2a7f4cf5](https://github.com/amindell11/astronomical-home/commit/2a7f4cf57787e1a0f9274c5a87c11c61d57d9116). **Detail-model candidate; owner review pending.**

The owner noted that the first detail pass left the cockpit under-detailed, clarifying that they meant the canopy frame. This round details the metal cradle around the approved glass pod: upper/lower glazing gaskets, segmented beveled retaining rails, fitted outer frame plating, and a stepped rear collar with latch recesses.

![Frame detail](round-01/review-frame.jpg)

![Before and after](round-01/comparison-frame.jpg)

<details><summary>Additional review views and validation</summary>

- [Cockpit side](round-01/closeups/cockpit_side.png)
- [Cockpit top](round-01/closeups/cockpit_top.png)
- [Whole ship](round-01/material/iso_front.png)
- [Underside](round-01/material/underside.png)
- [Review sheet](round-01/sheet/sheet.png)
- [Game scale](round-01/scale/scale.png)
- [Source comparison](round-01/compare/compare_top.png)
- [Source check](round-01/check/ship_check.json)
- [Edit audit](round-01/edit-audit.json)
- [Fingerprint delta](round-01/fingerprint-delta.json)

</details>

Blender previews only; no in-engine, flight, paint or completed detail-stage approval is claimed.

All previously existing part fingerprints are identical to `f12664a4`. The glass, hull, fins, prior detail parts, transforms, materials, source visibility and modifier stacks remain intact. Nine new frame parts are separate meshes with live Mirror and Solidify modifiers, in the hull role. The hidden earlier trim remains untouched in `ignore`.

`ship_check` passes with zero unresolved findings and the same nine active owner-approved scale exemptions. The manifest is unchanged. The source is saved in Object Mode.

Source SHA-256: `8d71a89ff25da69070d31d70547c1852981960bcfdc217a041ffc1ac03cd9b79`. Source and existing manifest are committed on the current task branch. Production legacy hull, ShipLegacyList, Unity integration, breakup-slot deferral and the owner's separate `Nightshade1.blend` are untouched. No PR.

Appended to evidence parent `e90330639e3d7b2f57d4d3c0bbcc39f0c3068517`, preserving previous rounds.
