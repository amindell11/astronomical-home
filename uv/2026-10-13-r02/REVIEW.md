# Bulwark — cockpit seam beneath the rim

The cockpit cut now follows its hidden open underside, beneath the surround. The visible glazing is one continuous chart, including across the live mirror's centerline. Every other part's UVs match the reviewed r01 layout exactly. Geometry and materials remain locked to approved r08.

![Continuous canopy checker](https://media.githubusercontent.com/media/amindell11/astronomical-home/e0d5c568f45131910c7cf2d6286df5ab0d58e73c/uv/2026-10-13-r02/checker-canopy.png)

![Whole ship checker](https://media.githubusercontent.com/media/amindell11/astronomical-home/e0d5c568f45131910c7cf2d6286df5ab0d58e73c/uv/2026-10-13-r02/checker-hero.png)

## Layout and checks

The canopy's two former charts are replaced by a continuous chart straddling the atlas center. Its UV mirror reflects across that chart's centerline; the other parts retain the r01 half-atlas arrangement. The vacant spaces fit the canopy at the same density without repacking any other part. Chart colors identify parts, not paint colors.

![Updated UV atlas](https://media.githubusercontent.com/media/amindell11/astronomical-home/e0d5c568f45131910c7cf2d6286df5ab0d58e73c/uv/2026-10-13-r02/UV-guide.png)

- [UV density: pass](uv-after/ship_uv.json). Max/min **1.000001409**, below the 2.0 limit.
- [Geometry lock: pass](lock-after/ship_lock.json). All 66 stored object geometry fingerprints and material fingerprints match r08. Separate parts, live geometry modifiers, hinges and zero deployment pose remain intact.
- [Cockpit-only change audit](check-delta.json): only `06 Cockpit dome` has a different UV fingerprint from r01. Every other UV fingerprint is identical.
- [Canopy seam check](canopy-seam-check.json): no interior UV seams on the smoothed canopy within `1e-6` UV units. Its only chart boundary is the open underside perimeter. Evaluated overlap selection is clear after coalescing repeated shared UV calculations within that tolerance; the maximum raw shared-coordinate difference is under 0.0003 texel at 2048.
- [Native authored-chart overlap check](uv-overlap.json): zero selected loops across all 20 visual parts.
- [Full source-check delta](check-delta.json): the same 60 inherited findings, with none added and no exemptions. Full source validation still fails for those existing findings.
- [Checker capture audit](checker-report.json): the saved source remained unchanged. Source SHA256 `2790e4dfce08191ccf35443e46b2bacfecf70b630b2babdf6ca7c44cbbfe9e54`.

## Owner ruling

The owner accepted the reviewed layout except for the cockpit seam, then selected “Hidden underside (Recommended)” on 2026-10-13. This revision implements that location in commit `9a422d65d0684fbc426bac0b8d181bc417a444ce`. [Scope ruling](https://github.com/amindell11/astronomical-home/issues/963#issuecomment-6156835596).

The earlier instruction “Lock geometry and fix UV’s” explicitly advanced geometry lock before the normal flight check; no flight or handling approval is claimed. [Previous UV repair and lock review](../2026-10-13-r01/REVIEW.md). [Approved plum concept and actual Unity fleet comparison](../../paint/2026-10-13-r01/REVIEW.md). Production paint follows review of this corrected cockpit chart.

[Editable Blender source](Bulwark.blend) · [Canopy repair audit](canopy-audit.json) · [Lock](lock.json) · [Ship settings](ship.json) · [PR #989](https://github.com/amindell11/astronomical-home/pull/989)
