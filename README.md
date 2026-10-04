# evidence/asset-arch-vendor — slice 6a (#917) of the asset-architecture arc (#868)

- `manifest.tsv` — every pack file: kept (from → to, GUID) or out (GUID).
- `acceptance_report.md` — mechanical checks 1, 2, 3, 6, 7 computed from git trees (base `4b7622fe` vs PR head).
- `equivalence_report.txt`, `snap_before.txt`, `snap_after.txt`, `targets.txt` — check 4: renderer/collider/particle-renderer snapshot of the 40 prefabs and scenes reaching a moved GUID, before any change and after the move + forced reimport.
- `pivot_proof.txt` — check 5: `[AsteroidPivot]` lines and post-import volume centroids of the 10 `Shapes/Models` FBX.
- `recheck_report.json` — the zero-referrer re-check run immediately before the `DeleteAsset` batch (601 GUIDs, 0 referrers outside the delete set).
- `editscene_removed_placements.txt` — the three raw placements deleted from `EditScene`.
- `test_summary_full.json` — check 8: `run-tests agent-1 -Mode Both -ScopeType Workspace` (971 passed, 2 skipped, 0 failed).
- `graph_before.json`, `plan.json` — the GUID graph keep sets and the move/delete plan.
- `tools/` — the scripts that produced all of the above (`Slice6aTools.cs` ran in the live editor via `unity command run_script`).
