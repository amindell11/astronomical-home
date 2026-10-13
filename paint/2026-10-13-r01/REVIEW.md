# Bulwark paint-direction review

Current Unity ship materials beside Bulwark's proposed warm aubergine / bone colors. Existing ships are fresh native Unity renders, with their current prefab meshes and material references. Cargo is excluded at the owner's direction.

## Fleet colors

![Current ship colors with Bulwark's temporary palette](https://raw.githubusercontent.com/amindell11/astronomical-home/0c1d4d8c53c9609b8347440853467c0a6067d5d8/paint/2026-10-13-r01/Bulwark-fleet-top.webp)

![Quarter view](https://raw.githubusercontent.com/amindell11/astronomical-home/0c1d4d8c53c9609b8347440853467c0a6067d5d8/paint/2026-10-13-r01/Bulwark-fleet-quarter.webp)

![At 96 pixels per ship](https://raw.githubusercontent.com/amindell11/astronomical-home/0c1d4d8c53c9609b8347440853467c0a6067d5d8/paint/2026-10-13-r01/Bulwark-fleet-96px.webp)

All views use shared camera settings and neutral preview lighting. Equal display size supports palette comparison; this is not a relative gameplay-size claim.

## Approved palette

Owner approved plum on 2026-10-13: 'Proceed with plum then'. This records palette approval; production prerequisites below remain outstanding.

Warm aubergine gives Bulwark a distinct body color beside Crimson's red, Vanguard's orange, Nightshade's current blue, and Valis's jade. Bone emphasizes the broad ram face. Charcoal structure and muted slate glass support its heavy, utilitarian character.

Bulwark in the native lineup has temporary base regions only. The more developed image below proposes shading, linework and nose-band placement; it is a generated paintover of the exact r08 render references, not a production texture or a geometry record.

![Paint concept](https://raw.githubusercontent.com/amindell11/astronomical-home/0c1d4d8c53c9609b8347440853467c0a6067d5d8/paint/2026-10-13-r01/Bulwark-paint-concept.webp)

## Source and limits

The owner-approved r08 model is unchanged: SHA256 `0fe0ee9690d0f3e16791d6398fc8bcbe168641847f2c501881a9d10326eaf3a3`. All separate parts, live mirrors, hinges and zero deployment poses are preserved. The checkpoint remains on evidence commit `c260d17849c962210a5e8ff8626b5d44afa0f294`, under `detail-model/2026-10-13-r08/`.

The lineup is a paint concept review. No production masks, prefab integration, gameplay changes or merge are included. Flight check has not been established, the geometry lock is missing, and the read-only UV check fails: eight parts have no PaintUV area; the existing density ratio is 33.613 against a maximum of 2.0. Production masks therefore remain pending flight/geometry-lock and UV work. Source validation retains the 60 inherited findings recorded in the prior detail review.

Unity scene state and selection are unchanged after capture; no source material became dirty. No scenes or production assets were saved. Review images were resized and arranged without recoloring. PNG-to-WebP copies are lossless.

[Lineup provenance](lineup-provenance.json) · [Generated concept provenance](concept-provenance.json) · [Paintover prompt](Bulwark-paint-concept.prompt.txt)




[UV readiness report](https://github.com/amindell11/astronomical-home/blob/0c1d4d8c53c9609b8347440853467c0a6067d5d8/paint/2026-10-13-r01/uv-readiness/ship_uv.json) · [Geometry-lock verification](https://github.com/amindell11/astronomical-home/blob/0c1d4d8c53c9609b8347440853467c0a6067d5d8/paint/2026-10-13-r01/lock-readiness/ship_lock.json)
