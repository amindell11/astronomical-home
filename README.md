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
