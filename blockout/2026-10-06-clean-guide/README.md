# Nightshade — clean remodel from the native guide

Current candidate: [source commit `7892d87b`](https://github.com/amindell11/astronomical-home/commit/7892d87ba1d2f6c2c3a08c4b3ae483fb8e66ea5c).
Arc #868, slice 9; original brief #796. **Owner shape approval is pending. No PR or integration.**

The owner requested a medium-detail first shape review after rejecting the coarse blockouts. The latest clarification is decisive: the legacy model is a hard dimensional guide, not geometry or UVs to reuse. The direct-import study was a misunderstanding and is superseded.

The current source rebuilds the pitched hull, main wing blades, upper/lower small fins, canopy, and armor as new editable cages. Legacy cross-sections and surface measurements guide native proportions. The compact fork pair is newly authored from the approved side/isometric direction. No legacy faces, UVs, or texture remain in the deliverable. UV authoring and final paint are later stages.

## Review pictures

![Side](round-02/review-side.jpg)
![Isometric](round-02/review-iso.jpg)
![Game scale](round-02/review-game-scale.jpg)

- [Top and bottom](round-02/review-top-bottom.jpg)
- [Clay view](round-02/review-clay.jpg)
- [Orthographic and eight-view turnaround sheet](round-02/sheet/sheet.png)
- [Exact orthographic side](round-02/material/side_orthographic.png)
- [Committed cage comparison](round-02/compare/compare_top.png)

## Evidence and scope

- `ship_check`: pass, zero findings and exemptions; [report](round-02/check/ship_check.json).
- Source SHA-256: `88cec980374e004bf8134e71bede43dfa584bb7b4f24cf2c14a57427cba127df`.
- 21 mesh parts; 2,310 authored vertices, 2,184 authored faces including 2,005 quads. Every part keeps a live X Mirror using SymmetryOrigin.
- [Topology inventory](round-02/topology.json) records zero legacy faces, UVs, or textures copied.
- [Detail fingerprint delta](round-02/detail-fingerprint-delta.json): seven additions, no existing cage changed or removed.
- Approved concept images and side crop remain packed as reference planes; the scene uses the sanctioned scaffold and roles.
- Only `art/ships/nightshade/Nightshade.blend` and `ship.json` differ from the requested concept-only base. Production legacy hull, ShipLegacyList, Unity integration, and the breakup deferral are unchanged.

The evidence remains under `blockout/` as requested, although this owner-directed first shape review includes medium detail. Material colors are provisional review colors. The ship-art process is not complete.

## This chat's intermediate studies

- `round-01`: new clean cages before armor joints, shield insets, and engine collar; committed as `cff8001d`.
- `round-02`: current candidate with those narrow detail additions.
- `superseded-medium`: this chat's earlier independently modeled interpretation, abandoned in favor of a closer native guide.
- `superseded-import`: direct legacy reuse study, explicitly superseded by the owner's clarification.
- `guide`: original-mesh cross-sections used only as dimension references.
- `scripts`: this chat's scratch construction and review recipes. The legacy-base recipe documents the independent fork cage's construction; its imported body is not part of the current deliverable.

No prior agent blockout, script, or render was read or reused. Existing evidence history was retained by appending this folder to parent `578f01f5a56730fd13140f8eb903db820c5c88ba`.
