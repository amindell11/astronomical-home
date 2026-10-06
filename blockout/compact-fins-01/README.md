# Nightshade — compact, sharper upper and lower fins

Source: [fcaa6930](https://github.com/amindell11/astronomical-home/commit/fcaa6930dae6b82248dbb129ab1e6cb8a972ce3b). **Round 02 is the current blockout candidate; owner approval pending.**

The owner approved the tail mounting direction, then identified the upper and lower fins as too soft and cute compared with the reference. During the first shape pass, they also asked for substantially smaller fins because the main top silhouette already worked without them. Their saved source was checkpointed as `e77f6110` before editing.

Round 01 sharpens the shoulders, straightens the swept edges and narrows the rear ends into clipped blade tips. Round 02 additionally reduces that planform to 62% in local X/Y around its central attachment, while retaining the existing thickness, live Mirrors and the owner's lower-fin transform. The lower fin's existing seam was reseated on its tilted surface. The full-size sharp pass is retained as intermediate evidence (`9060885a`), not the current candidate.

![Before and after](round-02/comparison-iso_front.jpg)
![Top comparison](round-02/comparison-top.jpg)
![Current side](round-02/review-side.jpg)

- [Current isometric](round-02/review-iso.jpg)
- [Underside](round-02/material/underside.png)
- [Isolated fin profile](round-02/fin-only/upper_top.png)
- [Orthographic review](round-02/ortho/ship_render.json)
- [Game scale](round-02/scale/scale.png)
- [Source check](round-02/check/ship_check.json)
- [Edit audit](round-02/edit-audit.json)
- [Preservation audit](round-02/fingerprint-delta.json)

`ship_check` reports one existing finding: the owner's unapplied scale on `Tail root mounting blocks`, `(0.756041, 0.754301, 0.490019)`. That part has the identical fingerprint it had in the owner checkpoint; no exemption or transform application was added. There are no new findings. This is a review candidate, not a passed stage.

Only the two swept-fin meshes and their existing seam strips changed. Their topology, object transforms and modifier stacks remain intact. Every other part, including the owner's resized mounting blocks and edited rear fork, is unchanged. Source SHA-256: `42741374e8c5bcf71be727098630a58ff5ca793a4703547ff0a2463a2c5f0088`.

The source and existing manifest are committed. Production legacy hull, ShipLegacyList, Unity integration, breakup-slot deferral and the owner's separate `Nightshade1.blend` remain untouched. No PR or detail-stage approval.

Appended to evidence parent `fefc5bfff5ae5953ffeccab47afbf87ac8d4cae3` without replacing earlier rounds.
