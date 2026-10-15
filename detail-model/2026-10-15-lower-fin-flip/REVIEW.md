# Lower fin orientation

The owner requested the lower fins face outward/downward, with their angled painted sides on the outside.

The existing lower-fin mesh was reflected across its mounting plane, which contains the root attachment line. Face winding was reversed to retain outward normals. The live Mirror and Bevel modifiers, object transform, deployment control, fixed brackets, and moving hinge leaves were retained.

## Verification

- Only `Lower flaps - Fins` changes in saved-source per-part fingerprints.
- Painted broad-face world normal Z components are -0.872 to -0.891: all face downward.
- Root attachment midpoint error is under 0.0000003 units; maximum pairwise vertex-distance error is under 0.0000004 units. The fin shape and hinge attachment are preserved within floating-point tolerance.
- Paint UVs stay attached to the same face corners and vertices despite reversed winding; a fresh reload independently verifies the association.
- All live image pixels are preserved. Recent BaseRegion brush strokes were saved and packed so the current owner paint survives reopening.
- The owner's active main-deck edit mesh is unchanged. All other parts retain their geometry, UV, and material fingerprints.
- Source validation retains the exact same 51 inherited findings. The older geometry lock remains superseded by owner edits.

![Downward-facing painted surfaces](underside.png)

![Side profile and hinge seating](side.png)

Blender renders, not in-game validation.
