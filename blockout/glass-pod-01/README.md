# Nightshade — tapered glass pod in an open metal cradle

Source: [262eee9f](https://github.com/amindell11/astronomical-home/commit/262eee9ff4a5068220c7c0ff6db8e78097589291). **Round 02 is the current blockout candidate; owner approval pending.**

The owner described one elongated glass pod seated through a hole in the hull, with stronger separation from its gray metal cradle. The cockpit concept is authoritative; the legacy mesh was not used. Their saved source was checkpointed as `6610e439` before editing.

Round 01 established a through-opening with an interior wall, glass extending above and below the cradle, and a distinct metal rim. The owner found the oval too round and asked to keep the earlier tapered shape. Round 02 restores the narrow forward tip and wider rear shoulders from the owner's pre-edit canopy while retaining the separate glass volume and real opening. Glass remains two existing editable halves that meet at the concealed equator; parts were not merged.

## Visual evidence

Current tapered pod, alongside the same view with glass hidden to expose the opening.

![Current cockpit and open cradle](round-02/review-cockpit.jpg)

Current ship, including the owner's visible edited upper fin.

![Current ship](round-02/review-ship.jpg)

Forward taper in side profile.

![Current side profile](round-02/review-side.jpg)

<details><summary>Additional current views and checks</summary>

- [Orthographic top](round-02/ortho/top.png)
- [Orthographic side](round-02/ortho/side.png)
- [Front](round-02/ortho/front.png)
- [Rear](round-02/ortho/back.png)
- [Underside](round-02/material/underside.png)
- [Game scale](round-02/scale/scale.png)
- [Source check](round-02/check/ship_check.json)
- [Edit audit](round-02/edit-audit.json)
- [Preservation audit](round-02/fingerprint-delta.json)

</details>

These are Blender blockout previews, not in-engine or final glass materials.

## Fin visibility correction

The first review setup rendered role members regardless of viewport hiding, which showed the owner's hidden original `Upper small swept fins` and omitted the visible edited duplicate `Upper small swept fins.001` outside the role collections. This was a capture error, not an older source or changed fin mesh. The corrected round-02 review setup follows saved visibility and includes that existing duplicate. No source collection assignments were changed. All fin fingerprints match the owner checkpoint.

`before/` and `round-01/` retain the initial captures with that incorrect fin selection. `round-01-inclusive/` is an intermediate capture that included the duplicate but still showed the original. They are not current review images. The render script in `scripts/` is the final corrected version.

## Validation and scope

`ship_check` has the same 13 existing findings as the owner checkpoint: unapplied object scales, the duplicate origin/fin's parentage, and the duplicate fin's missing visual role. No new findings, no exemptions added, and no modifiers applied. This is a review candidate, not a passed stage.

Only seven existing cockpit/hull meshes changed. The hull opening replaced 64 surface faces with 16 interior wall faces; only the cockpit boundary moved. Existing objects and mesh datablocks, object transforms, modifier stacks, live Mirrors, and every unclaimed part are retained. The local topology was then preserved during the taper correction, with face normals recalculated. Source SHA-256: `6dfdfeadd4eb89adba626cb2c539506b2435155eedfa501385fc1c45ff341b70`.

The source and existing manifest are committed. Production legacy hull, ShipLegacyList, Unity integration, breakup-slot deferral and the owner's separate `Nightshade1.blend` remain untouched. No PR or detail-stage approval.

Appended to evidence parent `d0bd494f8d1127d8acb9ad5dbd1470ba72b56b27` without replacing earlier rounds.
