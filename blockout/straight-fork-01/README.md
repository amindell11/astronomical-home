# Nightshade — restored taper and original straight fork

Source: [c0891943](https://github.com/amindell11/astronomical-home/commit/c089194321925e3362b977b2d67f048b1d1fc9bc). **Blockout candidate; owner approval pending.**

The owner asked to redo the gradual central fuselage taper after accidentally undoing it, and to match the original mesh's straight rear fork. Two open Blender sessions disagreed; the owner explicitly chose the saved file. That saved source was checkpointed as `6859548d` before editing.

The gradual taper is restored through the existing fuselage, with its spine armor and collar following. The existing rear wing cage was subdivided and fitted to the original mesh's native outline and upper/lower surfaces. It now has the original broad shoulders, straight parallel ends, length and restrained rise. No legacy faces or UVs were imported. The hidden root begins at Y=0.16 within its attachment; exposed outline samples fit within 0.0012 model units. No reference stretching.

![Side](round-01/review-side.jpg)
![Isometric](round-01/review-iso.jpg)
![Top](round-01/review-top.jpg)
![Original fork outline versus fitted cage](round-01/fork-reference-overlay.jpg)

- [True side orthographic](round-01/material/side_orthographic.png)
- [Rear isometric](round-01/material/iso_rear.png)
- [Game scale](round-01/scale/scale.png)
- [Orthographic report](round-01/ortho/ship_render.json)
- [Source check](round-01/check/ship_check.json)
- [Edit audit](round-01/edit-audit.json)
- [Preservation audit](round-01/fingerprint-delta.json)

`ship_check` passes with **zero findings**. Exactly four intended parts changed: hull, aft spine armor, aft collar and rear wing pair. All other saved owner parts have identical fingerprints. No objects were replaced, added or removed, and all modifier stacks remain live. The rear pair remains the same mesh datablock, with its cage subdivided from 156 to 732 vertices; the other three parts retain their topology.

Source SHA-256: `f5213ebf09bee20dd172f6c2a7b9131404d9ea698023fd4b4154f6098830fb39`. The manifest remains committed without added exemptions. The owner's separate `Nightshade1.blend` is untouched. Production legacy hull, ShipLegacyList, Unity integration and the breakup-slot deferral are unchanged. No PR or detail-stage approval.

Appended to evidence parent `239ad2ea5a6e37b6b0177e91f65411e697771acf` without replacing earlier rounds.
