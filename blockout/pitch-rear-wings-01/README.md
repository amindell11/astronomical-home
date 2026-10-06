# Nightshade — forward pitch and new rear wings

Source: [a90891ff](https://github.com/amindell11/astronomical-home/commit/a90891ffa1d2a49e433f77e6afd2fd8172264816). **Blockout; owner approval pending.**

The owner requested a stronger front taper and a few degrees of forward pitch across the upper fuselage, beefier center connections for the swept fins while retaining thin outer blades, and a fresh rear pair after deleting the tendrils. The new rear pieces should read as wings, guided by the original native mesh and the approved Nightshade three-quarter concept.

## Changes

- Upper fuselage pitched forward five degrees about X. The deformation blends through the lower hull and aft body; the existing canopy, frames and armor follow it. The front has a lower, sharper side profile.
- Small-fin roots extend farther into the center and gain section depth; their outer sections remain thin. The paired underside follows the same construction.
- A newly authored mirrored rear wing pair replaces the empty area left by the owner's deletion. It uses broad flat faces, angular shoulders, edge bevels, pointed ends and a low upward sweep. No prior tail geometry, legacy faces or UVs were reused.
- Owner-edited main wings and their geometry are unchanged.

## Review

![Side](round-01/review-side.jpg)
![Isometric](round-01/review-iso.jpg)
![Approved Nightshade reference comparison](round-01/nightshade-concept-comparison.jpg)

- [Clay](round-01/review-clay.jpg)
- [Top](round-01/review-top.jpg)
- [Orthographic and turnaround sheet](round-01/sheet/sheet.png)
- [Game scale](round-01/scale/scale.png)
- [Owner-source comparison: side](round-01/compare/compare_side.png)
- [Owner-source comparison: top](round-01/compare/compare_top.png)

## Source preservation and validation

The owner confirmed saving. Their latest file, including the tail deletion, was committed as `ad77982b` before editing. The comparison uses that saved checkpoint.

`ship_check` passes with no findings or exemptions: [report](round-01/check/ship_check.json). Source SHA-256: `98f63ec49a9535cd70b7b5bd68c4aabb3c1affd1651ee492bb6741830946a1cd`.

[Edit audit](round-01/edit-audit.json) and [fingerprint delta](round-01/fingerprint-delta.json): 20 intended existing parts changed, one new rear wing pair, no removed parts and no unclaimed changes. All existing topology and modifier stacks are retained. Mirrors remain live. The deleted tendril object remains absent. No modifiers were applied and no existing parts were merged or regenerated.

Production legacy mesh, ShipLegacyList, Unity integration and the existing breakup-slot deferral remain unchanged. No PR. Materials are provisional. Evidence appended to `be46d93dfa8a289ce466b904189eff2986c2ad08` without replacing earlier rounds.
