# Vanguard evidence

One branch for Vanguard's per-asset scratch and evidence, a folder per stage. Nothing
here merges to main. Consolidated 2026-10-03 under issue #869 from
`evidence/vanguard-retexture` and `evidence/vanguard-breakup`; their commits stay
reachable from this branch, so commit-pinned links in merged PRs (#814, #821) keep
resolving at the old paths.

| Stage | Where | Notes |
| --- | --- | --- |
| concept, model | tag `archive/codex/art-preview-scenes` (`28730a31`) | Drawn-study history and preview scenes, based on main, so kept as a tag rather than merged. |
| paint | `paint/retexture/` | All of `evidence/vanguard-retexture`: `history/` (decisions and captures) and `pipeline/` (paint scripts, `VanguardConsolidation.cs`, inputs). |
| integration | `paint/retexture/pipeline/VanguardConsolidation.cs`, `paint/retexture/history/consolidation/`, `history/native/` | The Unity-side merge helper and its checks. |
| motion | `motion/breakup/` | `evidence/vanguard-breakup:results/vanguard-breakup/evidence` (PR #821 captures). |
| motion (authoring) | tag `archive/codex/vanguard-breakup-authoring` (`7cbef8f9`) | Breakup generator, validation and capture helpers as PR #821 pinned them. |
| producers | `producers/drawn-study/` | `author-vanguard-wear.py` and the `inspect-vanguard*.py` probes, saved from pool slot 4's gitignored `results/`. |

Not here: the dated experiment folders that sat untracked under
`art/ships/vanguard/experiments/` in the primary tree. They were lost on 2026-10-03
before they could be copied; see issue #869.
