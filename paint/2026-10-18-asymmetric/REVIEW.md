# Bulwark asymmetric brushwork

Source commit `b1c3cc274217212f14dd5ce2777f4ae08494a247`. Uses Vanguard's verified signed-space painter at `a21a67317` (held/vanguard-redo), with 30 degree sweeps, brush contrast 3.2 and source-scaled brush frequency 0.13. Other paint strengths are retained from Bulwark's existing finish.

The owner explicitly authorized separating only overlapping armor UVs. Front and side armor had overlapping charts, including opposite-side overlap. Only their PaintUV coordinates changed: per-face planar charts at target 56 pixels per source unit were placed in unused atlas space, retaining the existing mirrored half-atlas offset. No geometry, modifier, transform, material assignment or other UV layout changed. Both parts use solid charcoal, so their remap leaves the base coat untouched. All BaseRegion pixel hashes match before/after, including the approved livery hash ef2cec3d4b04a27d02ac74c82fc6edb6a25d2c42829e43c56af8348eba5a3656.

The new baseline matches the new UV fingerprint. Cockpit authored deltas and all unaffected ink pixels were carried forward. Normal rebake passes and retains those edits exactly; whole generated-mask hashes did not match the first generation, so the evidence does not claim whole-mask byte identity. `rebake-verification.json` records the exact checks. The saved source freshly reloads to the same geometry/material fingerprints and texture pixel hashes.

The independent-side check now passes, as do geometry-lock verification and UV density (1.626336:1 against a 2:1 maximum). No exceptions were added. Paint now varies across the ship while the approved base pattern remains symmetric. No wear or chips were added; visible brushwork comes from the shared paint tile.

Compare previous-top.png with final-top.png and final-front.png; game-scale-top.png shows the small view. These are Blender paint previews, not Unity approval. Issue #1018 still tracks carrying the base coat beneath shader layers in Unity. Flight approval, gameplay integration, in-game lineup approval and breakup remain pending. No Unity tests were run and no merge was performed.

Reproduction: pinned shared tool source; separate_uvs.py and armor-uvs.json record the authorized UV repair; retain_edits.py transfers paint differences to the new baseline; apply.py changes only those UVs and three active mask images in the canonical source. verification.json, uv-check/, lock-check/ and rebake-report.json record validation. Earlier refusal evidence stays in this stage's Git history.
