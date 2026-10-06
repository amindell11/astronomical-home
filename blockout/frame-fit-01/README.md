# Nightshade — lowered frame fit and rounded nose

Source: [73ca2f1d](https://github.com/amindell11/astronomical-home/commit/73ca2f1d8a71944fab14a1bc70fa53dcd6e5673d). **Current candidate: round 02. Owner approval pending.**

The owner lowered and reshaped the hull frame to expose more side glass, then asked for the surrounding hull geometry and rim pieces to follow it. They also requested a rounder front instead of the pointed hull nose. While away, they explicitly authorized exiting Edit Mode and saving on their behalf. Their saved frame, lower-fin and other edits were checkpointed as `c1c0a7bd` before changes.

The cockpit aperture follows the glass at the owner's lowered boundary heights. The front frame is broader and rounder, with two additional support loops through the existing nose surfaces. The nearby rail surfaces were redistributed to remove the previous folded/extended surfaces. The remaining upper seat, inner seal, and lower bezel are fitted to the new opening; the lower bezel cross-section was reduced in round 02.

## Visual evidence

Owner checkpoint compared with the fitted hull. Both show the owner's saved visibility state.

![Frame comparison](round-02/comparison-iso.jpg)

Top view shows the rounder nose and closer aperture fit.

![Top comparison](round-02/comparison-top.jpg)

Side glass remains exposed above the lower frame.

![Side](round-02/review-side.jpg)

Remaining trim shown only for fit inspection; these pieces remain hidden in the source, as the owner left them.

![Rim fit](round-02/review-rims.jpg)

<details><summary>Additional current views and checks</summary>

- [Whole ship](round-02/material/iso_front.png)
- [Underside](round-02/material/underside.png)
- [Orthographic top](round-02/ortho/top.png)
- [Orthographic side](round-02/ortho/side.png)
- [Game scale](round-02/scale/scale.png)
- [Opening with glass hidden](round-02/cockpit/seat_without_glass.png)
- [Rims from the side](round-02/cockpit/rim_fit_side.png)
- [Source check](round-02/check/ship_check.json)
- [Hull edit audit](round-01/edit-audit.json)
- [Rim edit audit](round-02/edit-audit.json)
- [Preservation audit](round-02/fingerprint-delta.json)

</details>

Blender blockout previews only; no detail-stage or in-engine approval is claimed.

## Validation and preservation

`ship_check` reports the same 13 findings as the owner checkpoint: unapplied object scales, the duplicate origin/fin's parentage, and that fin's missing visual role. No new findings or exemptions. The stage has not passed validation.

Only the main hull and three existing rim/seat meshes changed. All glass, fins, other objects, source visibility, object transforms and modifier stacks are retained. The original upper fin stays hidden and the owner's visible duplicate is included in the review captures. The owner-deleted upper metal bezel was not recreated. Hull vertices aft of the cockpit are unchanged; the two new nose loops add 34 vertices and 32 faces. Mirror modifiers remain live.

Source SHA-256: `3e834f815d8f6227f60fc91669c8cd634d266a16cdf5004fd3ed4bab0edbc1c0`. The source and existing manifest are committed. Legacy production hull, ShipLegacyList, Unity integration, breakup-slot deferral and the separate owner file `Nightshade1.blend` remain untouched. No PR.

Appended to evidence parent `3f379a35e05a4665f03dc8e740559774c8e7f1f5` without replacing earlier rounds.
