# Bulwark — straight tail edges

[Editable Blender file](Bulwark.blend) · [Before/after review](index.html)

The previous transition pinched the boom into the narrow front of each engine housing, producing a curved waist in the top view. This revision carries straight longitudinal edges from the boom roots through widened housing shoulders. The seam remains between two independently editable parts.

Only `04 Tail booms` and the forward shoulder of `05 Tail tips` change. Their vertices move laterally; the side-profile coordinates remain. The engine tilt, rear housings, engine cavities, cockpit seating, circular side insets, and the owner's latest armor edits remain. All 15 visible parts are separate, with live modifiers and editing groups retained. Viewport overlays are on.

## Checks

- Latest owner checkpoint: `9eff188d81390e80de3fc0202a0bde35796c9791`.
- Saved owner SHA256: `29da5d3819d1c5e916a8e2627ede6ae674436aa0be042fb23cc9aa08fb9737d5`, unchanged.
- Candidate SHA256: `29459999061552c660fd5dfe702e3043d0c14a9fb5e898746c0bdb42713a8b34`.
- Fingerprints confirm only the two named meshes changed; no objects added or removed.
- The seven top-view longitudinal edges are straight to within 1.15e-07 units. No engine geometry moved.
- Both modified parts evaluate without boundary or nonmanifold edges.
- Source checks: 52 inherited findings before, 51 after, no new rule/part findings. Triangulating the changed housing cap removes its nonplanar n-gon finding. Remaining inherited transform, hierarchy, mirror-plane, and hidden-geometry findings remain unexempted.
- Studio, orthographic, eight-view, game-scale, and before/after render sets completed. Source hashes remain unchanged by rendering.

This is an editable shape review; the owner decides whether to promote it. It has not replaced tracked source or advanced the art stage.
