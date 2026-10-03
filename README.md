# Valis evidence

One branch for Valis's per-asset scratch and evidence, a folder per stage. Nothing here
merges to main. Consolidated 2026-10-03 under issue #869 from `evidence/valis-geometry`
and `evidence/valis-wings`; their commits stay reachable from this branch, so
commit-pinned links in merged PRs (#843, #852, #865) keep resolving at the old paths.

| Stage | Folder | Came from |
| --- | --- | --- |
| concept | `concept/approved-views/` | `valis-geometry:history/concepts` (concept C, top view, turnaround; issue #796 names these as design authority) |
| concept | `concept/texture-direction/` | `valis-geometry:history/texture-direction` |
| model | `model/review/`, `model/user-shape-cleanup/`, `model/shoulder-repair/`, `model/cockpit-fit/`, `model/profile-refinement/`, `model/flat-shading-symmetry/`, `model/canopy-joins/` | same names under `valis-geometry:history/` |
| paint | `paint/layered-paint-prototype/`, `paint/2026-10-02-crimson-retry/` | same names under `valis-geometry:history/` |
| integration | `integration/baseline/`, `integration/helpers/` | `valis-geometry:history/integration-baseline`, `history/helpers` |
| motion | `motion/2026-10-02-wing-motion-and-profiles/` | same name under `valis-geometry:history/` |
| motion | `motion/wings-evidence/` | all of `evidence/valis-wings` (PR #852 captures) |
| producers | `producers/wing-motion/` | local files saved 2026-10-03, see below |

`legacy/valis-geometry-README.md` is the old branch README; its `history/...` paths map
through the table above.

## Where the builders are

- Skinned-hull builders `RebuildValis.cs`, `InspectSkin.cs`, `VerifyReimport.cs` and the
  Blender helpers: `motion/2026-10-02-wing-motion-and-profiles/valis-wing-motion/v02/`.
- Hull scale and recentring (`unity-build.json`, scale 0.228625789, centre y -0.335398436):
  same folder, and `integration/baseline/unity-build.json` for the first integration.
- Paint authoring tools and tests: tag `archive/codex/valis-paint-authoring-archive`
  (`2d433ca2`), based on main, so kept as a tag rather than merged.
- Breakup builder and its input `approved-export.json`: branch `evidence/valis-breakup`,
  `v01/authoring/` (not folded in here; it backs work in flight).

## producers/wing-motion/

Files from the primary tree's gitignored `results/valis-wing-motion/` that were on no
branch: `archive_valis.py` and `v02/publish_evidence.py` (the archive and evidence
publishers), the Unity brief, PR and issue drafts, test summaries, and two prefab
snapshots (`v02/Valis-local-before.prefab`, `v02/Valis-staged.prefab`).
