# Bulwark — fitted interfaces

[Editable Blender file](Bulwark.blend) · [Before/after review](index.html)

This candidate starts from the owner's latest saved edits, checkpoint 9d17e34f53414637ac43c3f96d69cf3fac5d9458. The existing tail housings, engines, and armor remain as saved.

## Requested changes

- Tail booms extend and reshape to meet the tail housings' front cross-section. The housing profile carries forward through a short transition, with a narrow separation seam. The tips stay in place.
- Side insets have concentric circular rings in their actual face plane, correcting the oval profile and irregular back face.
- Cockpit seating follows the hull. The original teardrop planform and live subdivision remain; a thin, separately editable surround follows the canopy edge and hull.

Only three existing objects changed: `04 Tail booms`, `06 Cockpit dome`, and `07 Side insets`. `06 Cockpit surround` is new. All other object fingerprints match the saved owner scene. There are 15 visible, separate mesh objects, with original editing groups, transforms, and live modifiers retained. Viewport overlays are on. Gray materials remain.

## Checks and limits

- Saved owner SHA256: e218ca1f4c0e13ce4ef7b3e22de0c24d45c4f89a519d725363ea40a41f7fd505 (unchanged).
- Candidate SHA256: 9f0afcb1206d45c53da73bd418a243c4ee96dc702faf664ef355951e318f9842.
- Source checks: 52 inherited findings before and 52 after, with no new rule/part findings. Findings concern inherited transforms, hierarchy, mirror planes, and existing geometry; no exemptions were added.
- Modified closed parts and the new surround evaluate without boundary or nonmanifold edges. The original canopy's open underside remains intentional.
- Side-ring maximum radial error: 2.07e-07 units.
- Studio, close-up, orthographic, eight-view, and game-scale renders completed. Before/after views use the same cameras.

This is an editable shape-review candidate. It has not replaced the tracked source or advanced to UVs, paint, flight check, or Unity integration.
