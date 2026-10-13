# Bulwark — first plum paint masks

The approved plum direction is now on the locked model and approved UVs as separate recolourable paint layers. These are native Unity shader renders, not generated paintovers. Geometry and UVs are unchanged from the approved cockpit-seam revision.

![Plum paint through the game shader](https://media.githubusercontent.com/media/amindell11/astronomical-home/c1442075e55a922754233c7085d1a42779c92cc4/paint/2026-10-13-r02/lineup/Bulwark-painted-large-quarter.png)

## Current fleet

Crimson, Vanguard, Nightshade and Valis use their current prefab meshes and materials, freshly rendered in the live Unity Editor. Bulwark uses the exact evaluated source with its new masks. Cargo is excluded. Shared cameras, lighting and equal display size support colour comparison; these are not relative gameplay-size claims.

![Native fleet quarter view](https://media.githubusercontent.com/media/amindell11/astronomical-home/c1442075e55a922754233c7085d1a42779c92cc4/paint/2026-10-13-r02/lineup/Bulwark-fleet-quarter.png)

![Native fleet top view](https://media.githubusercontent.com/media/amindell11/astronomical-home/c1442075e55a922754233c7085d1a42779c92cc4/paint/2026-10-13-r02/lineup/Bulwark-fleet-top.png)

![At 96 pixels per ship](https://media.githubusercontent.com/media/amindell11/astronomical-home/c1442075e55a922754233c7085d1a42779c92cc4/paint/2026-10-13-r02/lineup/Bulwark-fleet-96px.png)

## Paint and editing

Plum armor, a warm bone ram face and flap tips, charcoal structure, and slate glazing reproduce the approved colour direction. A plum stripe wraps the nose. Painted planes, restrained panel ink, edge wear and a continuous canopy reflection give the source its first detailed mask pass.

[Shadow](layers/Shadow.png), [Light](layers/Light.png) and [Ink](layers/Ink.png) are independent 2048 × 2048 grayscale images. [BaseRegion](layers/BaseRegion.png) selects the nose stripe and flap-tip alternate coats. Colour is held separately in the base materials and [palette settings](palettes.json), not baked into an RGB atlas. The existing game shader's alternate-colour blend supplies those regions, with emission strength zero. [Paint settings](paint-settings.json) record bindings and strengths.

The [Blender source](Bulwark.blend) contains packed masks and editable colour nodes. External layers are included for hand painting; reload and repack them after external edits. Separate parts, live modifiers and both hinge controls remain editable.

## Validation and approval status

- [Paint validation](paint-validation.json): geometry and UV fingerprints match the approved UV source. All four masks are grayscale at 2048. Unity scene state, selection and existing materials are unchanged after capture; no scene or production asset was saved.
- [Geometry lock](lock-after/ship_lock.json) and [UV density](uv-after/ship_uv.json): pass. Density max/min remains **1.000001409**, below 2.0.
- [Source check](check-after/ship_check.json): retains the same 60 inherited findings, with none added or exempted. Full source validation remains a failure for those existing findings.
- [Source audit](paint-source-audit.json): source SHA256 `96a9e782c7dc5cb59eb24ce8f84248474246b4065a2c6bc76f5ef36a2b400a50`. Paint source and layers are committed in `0a10673539f1c2233217f7de53588916d79ba202`.
- The owner approved plum, then approved the corrected UV layout on 2026-10-13. This mask pass is awaiting review.

Bulwark has no gameplay chassis prefab yet. These captures use temporary meshes and materials through Unity's current `Astronomical/Drawn/Surface` shader; gameplay integration and flight check are still separate. The [paint-stage workflow](https://github.com/amindell11/astronomical-home/blob/main/.claude/skills/ship-art-pipeline/stages/6-paint.md) requires approval of the concept and then the masks in game before advancing. No in-game mask approval or flight approval is claimed here.

[Approved concept and original fleet comparison](../2026-10-13-r01/REVIEW.md) · [Approved UVs and cockpit seam](../../uv/2026-10-13-r02/REVIEW.md) · [PR #989](https://github.com/amindell11/astronomical-home/pull/989) · [Owner UV approval and paint scope](https://github.com/amindell11/astronomical-home/issues/963#issuecomment-6157011863)
