# Bulwark — concept paint correction

The bone nose coat now crosses the physical nose-block seam onto the adjoining armor, ending in an aggressive angled rear point. The small bridge between the two aft armor plates is charcoal. The exposed frame and engine housings stay charcoal beneath the plum plates, with diagonal bone fin wedges and a sharper nose stripe.

## Approved concept reference

![Approved plum concept](../2026-10-13-r01/Bulwark-paint-concept.webp)

## Revised native shader views

These are Unity renders of the saved model and masks. The gray-background top, side and quarter views make the paint easier to compare with the concept. They retain the locked geometry and approved UVs; the concept is the paint reference, not a replacement geometry record.

![Revised quarter view](https://media.githubusercontent.com/media/amindell11/astronomical-home/f689dff93a46f613a548d82ff7e62144bcf24357/paint/2026-10-13-r04/lineup/Bulwark-concept-aligned-quarter.png)

![Nose coat crosses the seam](https://media.githubusercontent.com/media/amindell11/astronomical-home/f689dff93a46f613a548d82ff7e62144bcf24357/paint/2026-10-13-r04/lineup/Bulwark-concept-aligned-top.png)

![Charcoal structure beneath the armor](https://media.githubusercontent.com/media/amindell11/astronomical-home/f689dff93a46f613a548d82ff7e62144bcf24357/paint/2026-10-13-r04/lineup/Bulwark-side-quarter.png)

## What changed and why

The first pass assigned too much supporting structure the plum coat and used broad cuts across the nose and fins. Those choices flattened the concept's layered armor and angular paint design. The revised frame, housings, bridge block and plate undersides use the distinct charcoal base coat. Bone coverage is projected continuously across the nose and forward armor rather than stopping at an object boundary. The fin cuts follow their outer-panel angles, leaving plum wedges at the roots. Warm shadow tint keeps the bone faces from turning blue-gray under the game shader.

The four 2048 grayscale layers remain recolourable: [Shadow](layers/Shadow.png), [Light](layers/Light.png), [Ink](layers/Ink.png) and [BaseRegion](layers/BaseRegion.png). BaseRegion carries shaded bone-coat opacity over a plum substrate, including chips and painted-plane variation. Colour stays in the material nodes and [palette settings](palettes.json); no RGB colour atlas or shader change is included. [Paint settings](paint-settings.json) record the bindings and strengths.

## Current game fleet

Crimson, Vanguard, Nightshade and Valis use their current prefab meshes and materials, freshly rendered in Unity. Cargo is excluded. The fleet views use shared cameras and lighting at equal display size for comparison; they do not claim relative gameplay sizes.

![Revised paint beside the current fleet](https://media.githubusercontent.com/media/amindell11/astronomical-home/f689dff93a46f613a548d82ff7e62144bcf24357/paint/2026-10-13-r04/lineup/Bulwark-fleet-quarter.png)

![At 96 pixels per ship](https://media.githubusercontent.com/media/amindell11/astronomical-home/f689dff93a46f613a548d82ff7e62144bcf24357/paint/2026-10-13-r04/lineup/Bulwark-fleet-96px.png)

## Validation and status

- [Geometry lock](lock-after/ship_lock.json): pass. All geometry fingerprints match approved r08; separate parts, live modifiers and hinge controls remain intact.
- [UV density](uv-after/ship_uv.json): pass, max/min **1.000032** against 2.0. Every UV fingerprint matches the approved cockpit-seam revision.
- [Paint validation](paint-validation.json): masks are grayscale at 2048; geometry and UVs are unchanged. Unity scene state, selection and existing materials stayed unchanged after capture.
- [Source check](check-after/ship_check.json): the same 60 inherited findings, none added or exempted. Full source validation still fails for those existing findings.
- [Source audit](paint-source-audit.json): SHA256 `350363e5d79e771c95c519080aaa2cb6c474769a74a0690ada83b92fb8d2a6a1`. Source and masks are committed in `d7061055838015ffd7e9d936da179880e6ce5d2b`. The preceding frame/angle correction is preserved in `b4dfd2a3451506be84136aedcc0eb458785aeb26`.

This revised paint is awaiting owner review. Captures use temporary evaluated meshes and materials through the current Unity shader. No gameplay prefab integration, flight approval or merge is claimed.

[Editable Blender source](Bulwark.blend) · [Editing notes](README.md) · [Previous mask pass](../2026-10-13-r02/REVIEW.md) · [PR #989](https://github.com/amindell11/astronomical-home/pull/989) · [Nose-crossing and bridge-block scope](https://github.com/amindell11/astronomical-home/issues/963#issuecomment-6157586151)
