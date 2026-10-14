# Bulwark — one wrapping rear stripe

The separate rear marks now follow one angular bone stripe across the rear top plates, upper armor shoulders and outer side plates. The same model-space path determines every section, so the band aligns across the separate UV charts and continues over the plate edges. Physical armor gaps and the recessed charcoal frame remain visible.

## Native shader views

![Coherent rear stripe from above](https://media.githubusercontent.com/media/amindell11/astronomical-home/6e4212b75053842f92149c5067c28b1f3c0f2cb5/paint/2026-10-13-r07/lineup/Bulwark-concept-aligned-top.png)

![Stripe wraps from top to sides](https://media.githubusercontent.com/media/amindell11/astronomical-home/6e4212b75053842f92149c5067c28b1f3c0f2cb5/paint/2026-10-13-r07/lineup/Bulwark-concept-aligned-quarter.png)

![Side wrap](https://media.githubusercontent.com/media/amindell11/astronomical-home/6e4212b75053842f92149c5067c28b1f3c0f2cb5/paint/2026-10-13-r07/lineup/Bulwark-side-quarter.png)

The band keeps an angular turn at the shoulders and a consistent projected width. The nose coat and its dark seam, fin wedges, pewter side canisters and blue cockpit glazing retain their previous finish.

## Current fleet and distance view

![96 pixels per ship](https://media.githubusercontent.com/media/amindell11/astronomical-home/6e4212b75053842f92149c5067c28b1f3c0f2cb5/paint/2026-10-13-r07/lineup/Bulwark-fleet-96px.png)

![Current game fleet](https://media.githubusercontent.com/media/amindell11/astronomical-home/6e4212b75053842f92149c5067c28b1f3c0f2cb5/paint/2026-10-13-r07/lineup/Bulwark-fleet-quarter.png)

Crimson, Vanguard, Nightshade and Valis use their current game meshes and materials. Cargo is excluded. Shared lighting and cameras at equal display size support color/readability review; these are not relative gameplay sizes.

## Editing and checks

[BaseRegion](layers/BaseRegion.png) remains an editable grayscale mask. Bone stays a separate palette color. The EdgeCoat material lets the stripe cover charcoal armor thickness edges while preserving charcoal outside the band. Shadow, Light and Ink are unchanged. No UV or mesh edits were needed.

- [Geometry lock](lock-after/ship_lock.json): pass; every geometry fingerprint is unchanged.
- [UV density](uv-after/ship_uv.json): pass, max/min 1.000032 against 2.0; every UV fingerprint matches the approved layout.
- [Paint validation](paint-validation.json): native capture left Unity scene state, selection and existing materials unchanged.
- [Source check](check-after/ship_check.json): the same 60 inherited findings; none added or exempted. Full source validation still fails on those findings.
- [Projection audit](mask-audit.json) and [source audit](paint-source-audit.json): source SHA256 `4f6821a05da8c2e23895ef5b2b53085694170c34521ddedfc2691a9a18d184f1`, commit `17cadab9bf7dc58385e98090bb6916b9b0fac5d4`.

The wrap is awaiting owner review. Captures use temporary evaluated meshes/materials through the current game shader; gameplay integration, flight approval and merge remain separate.

[Editable Blender source](Bulwark.blend) · [Editing notes](README.md) · [Previous metal/paint review](../2026-10-13-r06/REVIEW.md) · [PR #989](https://github.com/amindell11/astronomical-home/pull/989) · [Scope](https://github.com/amindell11/astronomical-home/issues/963#issuecomment-6158456999)
