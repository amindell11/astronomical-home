# Crimson evidence

One branch for Crimson's per-asset scratch and evidence, a folder per stage. Nothing
here merges to main. Consolidated 2026-10-03 under issue #869; earlier commits of the
branches folded in stay reachable, so commit-pinned links in merged PRs keep resolving.

| Stage | Where | Notes |
| --- | --- | --- |
| concept, model | tag `archive/task/crimson-drawn-wip` (contains `archive/task/crimson-drawn`) | Editable mesh drafts and the drawn-study history, based on main, so kept as tags rather than merged. |
| paint | tag `archive/task/crimson-ship` (`0abd44a5`) | The approved hand-painted Crimson as PR #725 pinned it: source, textures, orthographic set, previews. |
| paint (recipe) | `producers/codex-blender/crimson-texturing-20260928-010254/` | `texture_pass.py` is the original paint recipe. It opens `Before-texturing.blend` and `textures/painted-brush-source.png`, which are not on any branch. |
| integration | `producers/unity-integration/` | See below. |
| motion | `motion/breakup/` | Former `evidence/crimson-breakup` (PR #740 captures). |
| motion (authoring) | tag `archive/codex/crimson-breakup-authoring` (`9913320b`) | Breakup generator and assets as PR #740 pinned them, based on main. |

## producers/unity-integration/

Saved from the gitignored `results/visual-playable/` of the primary tree. These produced
`Assets/Visuals/Ships/Crimson/Crimson.asset` and the Crimson rig in PR #730 and existed
on no branch.

- `IntegrateIllustrated.cs`: builds the illustrated rigs; the only record of how
  Crimson's outline (contour) normals were built.
- `CrimsonConsolidationAuthoring.cs`: combines the painted pieces and contour shells
  into the single saved mesh.
- `CrimsonConsolidationRenderTests.cs`: renders split reference against the single mesh.
- `SaveCrimsonInertia.cs`: pins the authored inertia tensor on `Ship_2.prefab`.
- `BuildIllustratedPlayer*.cs`: player build helpers used for the playable check.
- `crimson-*.json`, `crimson-*.txt`: the consolidation and render-comparison results.

## producers/codex-blender/

Blender-side Python from Codex's local artifact archive (`crimson-*` folders), scripts
only. Hard-coded local paths; reference, not runnable tools.
