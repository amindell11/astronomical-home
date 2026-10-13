# Bulwark — tail proportions, round 7

Gray blockout only. Proportions still under owner review.

The owner's saved body/front/cockpit/armor work is checkpointed at fbb80869. Only the main tail booms, tail housings and nested engines were edited. Other part fingerprints are unchanged. Alternate tail objects remain present and hidden, as authorized.

The rear connections are wider, engines are larger, and the housings keep their swept top silhouette with a calmer side profile. The first deeper housing study (d54cdd6c) was rejected as too thick from the side. This candidate reduces that tail height by 35%, preserving the approved-for-this-pass top outline (maximum XY coordinate change below 0.000001).

Source commit: e6013c91911fc5d8467fa80ceb0e6546dc5f42d9.
Source SHA256: c8db1ba7cdd63e2ed0482c51db9f202ebfa701b56e284cbf7b5fad97cb2a0537.

## Review

[Side](side-aligned.png) · [Top](top-aligned.png) · [Rear quarter](rear-quarter.png) · [Interactive comparison](index.html)

## Validation and limits

- Studio, orthographic and 32/48/64/96-pixel views rendered from evaluation copies; source hash unchanged.
- Review renders explicitly include all visible owner-authored meshes. Some owner-added objects have not yet been assigned pipeline roles, so the stock role-only renderer would omit them.
- 35 source-check findings remain, all present in the owner's saved checkpoint: scales, hierarchy/role assignments, non-planar polygons and an inset normal/mirror issue. No new findings. No transform or modifier application, merging, or cleanup of untouched owner parts.
- Tail surfaces have recalculated normals; non-planar end caps in the edited pieces were triangulated in place.
- Mirrors remain live. No Unity exports or integration; no detail stage.
- Size-S quality subagent skipped, source-only art.

[Source check](ship_check.json) · [Before check](owner-ship-check.json) · [Delta](validation-delta.json) · [Preservation audit](edit-audit.json)
