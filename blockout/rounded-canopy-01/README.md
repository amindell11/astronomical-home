# Nightshade — rounded canopy front

Source: [124a71cf](https://github.com/amindell11/astronomical-home/commit/124a71cf4c050b2232d6f19b815233980fa9fa97). **Current candidate: round 03. Owner approval pending.**

The owner approved the outer nose shape and requested a rounder canopy front to follow it. The upper and lower glass noses are broader and slightly shorter, retaining their longitudinal taper and the owner's side-window exposure. Existing canopy vertices were reshaped in place. The opening's inner edge, adjacent rail faces, and remaining hidden rim pieces were fitted to the new glass. The approved outer nose silhouette is unchanged.

## Review

![Top comparison](round-03/comparison-top.jpg)

![Isometric comparison](round-03/comparison-iso.jpg)

- [Cockpit side](round-03/cockpit/side.png)
- [Whole ship isometric](round-03/material/iso_front.png)
- [Underside](round-03/material/underside.png)
- [Orthographic top](round-03/ortho/top.png)
- [Orthographic side](round-03/ortho/side.png)
- [Game scale](round-03/scale/scale96_top.png)
- [Remaining rims, visible only for inspection](round-03/cockpit/rim_fit_iso.png)
- [Hull opening without glass](round-03/cockpit/seat_without_glass.png)
- [Source check](round-03/check/ship_check.json)
- [Preservation audit](round-03/fingerprint-delta.json)

Round 01 reshaped the glass; round 02 fitted the inner opening and remaining trims; round 03 blended that opening adjustment through adjacent existing rail faces.

## Validation and preservation

`ship_check` reports the same 13 pre-existing findings: unapplied scales, the duplicate origin/fin's parentage, and that fin's missing visual role. No new findings or exemptions. Validation has not passed; these are blockout review renders, with no detail-stage or in-engine approval claimed.

Only the two glass meshes, the cockpit's inner hull edge/adjacent faces, and three existing rim/seat meshes changed. Mesh topology, object transforms, live Mirror modifiers, source visibility and all other parts are retained. The glass aft of Y=0.707 is unchanged. All fins, the owner's visible duplicate fin and deleted bezel state are preserved. The approved outer nose and hull aft of the cockpit are unchanged.

Source SHA-256: `5e83c4f83b490e0eeb14af05ae94f2c4dc3ca2790c3c97ff9972d0f0eed947d7`. The source and existing manifest are committed on the task branch. Legacy production hull, ShipLegacyList, Unity integration, breakup-slot deferral and the owner's separate `Nightshade1.blend` remain untouched. No PR.

Appended to evidence parent `6b0b8d458a6d76524b3d2256a750ae5d449a2285` without replacing earlier rounds.
