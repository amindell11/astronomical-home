# Nightshade — tail root mounting blocks

Source: [564ad251](https://github.com/amindell11/astronomical-home/commit/564ad25120f0e7b229dfaa486c72c02e8d8385c8). **Blockout candidate; owner approval pending.**

The owner asked for chunky mechanical blocks connecting the straight tail forks to the central fuselage, showing the exposed gap in their Blender viewport. Their latest saved fork and lower-fin edits were checkpointed as `bb5ab4df` before this pass.

A mirrored pair of angular shoulder blocks now overlaps both the fuselage and fork roots. Each side has broad planar faces, chamfered edges, and a tapered aft end. The attachment is a separate editable part with a live X Mirror. All existing owner geometry, transforms, topology and modifiers are unchanged.

![Attachment close-up](round-01/review-mounts.jpg)
![Full ship](round-01/review-iso.jpg)

- [Top view of attachment](round-01/attachment/top.png)
- [Full side](round-01/material/side.png)
- [Rear isometric](round-01/material/iso_rear.png)
- [Orthographic review](round-01/ortho/ship_render.json)
- [Game scale](round-01/scale/scale.png)
- [Source check](round-01/check/ship_check.json)
- [Edit audit](round-01/edit-audit.json)
- [Preservation audit](round-01/fingerprint-delta.json)

`ship_check` passes with zero findings. One new part, `Tail root mounting blocks`, has 40 cage vertices and 44 faces before the live Mirror. The fingerprint comparison confirms zero changes to existing parts. Source SHA-256: `60b11551272740fe81e20314a258be2d9488df059666dec3deb4b349f3b4c5c0`.

The source and existing manifest are committed. The owner's separate `Nightshade1.blend`, production legacy mesh, ShipLegacyList, Unity integration and breakup-slot deferral are untouched. No PR or detail-stage approval.

Appended to evidence parent `3dd4bb32e1910f0efdc6e21f3cd8a6e38413be14` without replacing earlier rounds.
