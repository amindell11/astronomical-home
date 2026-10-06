# Independent Nightshade blockout

Slice 9 of arc #868; original hull brief #796. Awaiting owner approval of proportions.

Built in fresh pool slot agent-1, lease nightshade-blockout, starting at concept-only commit `12c372e11ceb0b134045966c8859392bb8af45f0`. No previous blockout, builder or render was used. The legacy mesh was not needed.

## Candidate

![Side and isometric review](round-03/nightshade-review.jpg)
![Orthographic and game-scale review](round-03/nightshade-ortho-scale.jpg)

The owner prioritizes side and isometric views, then top, with front only supporting. The compact bowed pair remains symmetrical across X, with the strongest upward bend toward the rear. Differences between drawings were resolved as perspective separation of that same pair, rather than vertically asymmetric prongs. Smooth transitions retain a longitudinal blade ridge. Flat color regions distinguish major forms only.

## Editable source and rounds

Source branch: `task/nightshade-blockout`. Only `art/ships/nightshade/Nightshade.blend` and `ship.json` were added after the concept-only starting point.

- Round 01: `c910eb8d`, initial volume checkpoint.
- Round 02: `d3687314`, connected wing roots and softer volumes.
- Round 03: `e01648512918179cff9cd0127ef36ff299fec986`, broader outer blades, later tail rise, sculpted cross-sections and an aligned packed side reference.

The sanctioned `ship_new` scaffold supplies the root, role collections, reference planes, cameras and review preset. All eight mesh parts retain live X Mirror modifiers anchored to SymmetryOrigin. Top, full turnaround, and a side crop are packed into the source. Per-ship Blender authoring scripts are preserved here as scratch, outside the asset tree.

## Validation

`ship_check`: pass, zero findings and zero exemptions. `ship_render --set ortho` and `--set scale`: pass. All three reports agree on source SHA-256 `a192119d8e04df6404170a7c90f46b134a1fb932a2cc249a9d9886a9cede2d4d`. Blender 5.1.2.

The volume views use the shared review renderer with studio lighting for readable thickness; the orthographic and game-scale sets use the unchanged sanctioned flat preset. These are Blender blockout previews, not in-engine proof. No export, Unity integration, legacy-hull edits, ShipLegacyList edits, breakup-slot deferral edits, detail work or PR.