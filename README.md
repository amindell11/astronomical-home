# Vanguard evidence

One branch for Vanguard's per-asset scratch and evidence, a folder per stage. Nothing
here merges to main. Consolidated 2026-10-03 under issue #869 from
`evidence/vanguard-retexture` and `evidence/vanguard-breakup`; their commits stay
reachable from this branch, so commit-pinned links in merged PRs (#814, #821) keep
resolving at the old paths.

| Stage | Where | Notes |
| --- | --- | --- |
| concept, model | tag `archive/codex/art-preview-scenes` (`28730a31`) | Drawn-study history and preview scenes, based on main, so kept as a tag rather than merged. |
| model | `model/source-reorganization-2026-09-09/` | The abandoned September reorganization: `vanguard.blend` (the renamed `starship_scratch_model_3.blend` working copy), its README, `textures/`, `guides/`, `reference/`. History only: `drawn-study/VanguardPainted.blend` on main is the one editable Vanguard source. |
| paint | `paint/retexture/` | All of `evidence/vanguard-retexture`: `history/` (decisions and captures) and `pipeline/` (paint scripts, `VanguardConsolidation.cs`, inputs). |
| integration | `paint/retexture/pipeline/VanguardConsolidation.cs`, `paint/retexture/history/consolidation/`, `history/native/` | The Unity-side merge helper and its checks. |
| motion | `motion/breakup/` | `evidence/vanguard-breakup:results/vanguard-breakup/evidence` (PR #821 captures). |
| motion (authoring) | tag `archive/codex/vanguard-breakup-authoring` (`7cbef8f9`) | Breakup generator, validation and capture helpers as PR #821 pinned them. |
| paint (experiments) | `paint/experiments/` | Dated livery and texture experiments, 2026-07-26 to 2026-09-23. |
| producers | `producers/drawn-study/` | `author-vanguard-wear.py` and the `inspect-vanguard*.py` probes, saved from pool slot 4's gitignored `results/`. `export_study.py` and `build_structure.py`, the drawn study's Blender-to-Unity export, retired from `art/ships/vanguard/drawn-study/` by #921. |
| studies | `studies/drawn-art/` | The Unity side of the drawn-art study, removed from main by #921: scenes, prefabs, materials, meshes, backdrop plates and capture tools, `.meta` included. Its README has the revive recipe. |

`paint/experiments/` holds the dated experiment folders that sat untracked under
`art/ships/vanguard/experiments/` in the primary tree (pre-livery checkpoint, AI livery
review variants, image-generation proof, texture concept and MVP, packed-texture
snapshots, `vanguard_uv_work.blend`), without their `.blend1` backups (those are in this branch's history at `662b3cd4`).

## Recolorable redo

[2026-10-15 detail-model review](detail-model/2026-10-15/README.md): hybrid modeled details and later grayscale masks, scoped in #1016. Source validation passes; owner detail-model approval is pending.

[2026-10-15 paint UV layout](uv/2026-10-15/README.md): approved detail model, owner-waived flight check, geometry lock verified, density ratio 1.000002. Layout approval pending.

[2026-10-15 palette/region concept and vent-clearance correction](paint/2026-10-15/README.md): geometry/UV approvals recorded on #1016; owner-approved fit correction applied and rechecked. Palette/region approval pending.

- [Recovered livery regions](paint/2026-10-15/region-correction/README.md): original production placement beside the corrected flat-region proposal; supersedes the rejected whole-part assignments.

- [Production artwork preservation](paint/2026-10-18/preservation/README.md): original detail retained, baked broad lighting removed, and canonical brush treatment shown separately; replaces the rejected region redraw.

- [Authored neutral region masks and selected line treatment](paint/2026-10-18/authored-regions/README.md): recovered artwork, source correction for extra hatch outlines, canonical bakes and export; Unity review deferred by owner.

- [Painted light and shadow restoration](paint/2026-10-18/painted-layers/README.md): restores the procedure's brushing and light/shadow layers after the owner approved the softer lines; Unity review remains deferred.
