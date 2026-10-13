# Bulwark — locked geometry and repaired UVs

The approved r08 shape is locked. `PaintUV` now covers all 20 visual parts at matching texel density. The lower-hull projection fold was split into separate charts; Blender's overlap selection reports no overlaps in the authored charts. The plum palette remains the approved paint direction.

![Checker view](https://media.githubusercontent.com/media/amindell11/astronomical-home/d9e672f4f797f4d731b71958310768ba075fe1fe/uv/2026-10-13-r01/checker-hero.png)

## UV layout

2048 × 2048 atlas. Authored charts occupy the left half; live mirrors offset their copies into the right half. This keeps both sides independently paintable. Large armor plates retain continuous charts, with seams at part boundaries and changes in surface direction. Hardware has separate charts. The colors below identify parts, not the approved paint scheme.

![UV layout](https://media.githubusercontent.com/media/amindell11/astronomical-home/d9e672f4f797f4d731b71958310768ba075fe1fe/uv/2026-10-13-r01/UV-guide.png)

![Top checker view](https://media.githubusercontent.com/media/amindell11/astronomical-home/d9e672f4f797f4d731b71958310768ba075fe1fe/uv/2026-10-13-r01/checker-top.png)

![Side checker view](https://media.githubusercontent.com/media/amindell11/astronomical-home/d9e672f4f797f4d731b71958310768ba075fe1fe/uv/2026-10-13-r01/checker-side.png)

## Checks

- [UV density: pass](uv-after/ship_uv.json). Max/min is **1.000001409**, below the 2.0 limit. The previous layout had eight parts without usable UV area and a ratio of 33.612594.
- [Geometry lock: pass](lock-after/ship_lock.json). All 66 stored object geometry fingerprints match the r08 checkpoint; materials also match. Separate parts, live geometry modifiers, and the hinge rig remain intact. Both deployment controls retain their zero pose.
- [Authored UV overlap check](uv-overlap.json): zero selected loops across all 20 visual parts. This checks the source charts; modifier-generated bevel and thickness surfaces inherit their UVs.
- [Source validation delta](check-delta.json): the same 60 inherited findings, with none added and no exemptions. The full source check remains a failure for those existing scale, origin, mirror-plane, n-gon and role findings.
- [Repair audit](uv-audit.json) and [checker capture audit](checker-report.json) refer to the saved source SHA256 `44ddcb64c81d22cdea4c875c9c66c56b50281d53929b3a5573f4f3d6c9cdf9a7`.

## Owner ruling and next stage

On 2026-10-13 the owner instructed: “Lock geometry and fix UV’s”. The lock was created before the UV edit, checkpointed in `4d2e0dd19`, and verified afterward. This explicitly advances geometry lock ahead of the normal flight-check stage; flight or handling approval is not claimed. UV repair is committed in `bc706397d38000c82ec86bf57b08fb0b76d27201`.

The layout is ready for owner review before production paint. [Approved plum concept and current in-game fleet comparison](../../paint/2026-10-13-r01/REVIEW.md). No production masks or Unity integration are included in this UV revision.

[Editable Blender source](Bulwark.blend) · [Lock](lock.json) · [Ship settings](ship.json) · [PR #989](https://github.com/amindell11/astronomical-home/pull/989) · [Scope and owner ruling](https://github.com/amindell11/astronomical-home/issues/963#issuecomment-6156520021)
