# evidence/asset-arch-studies — slice 6b (#921) of the asset-architecture arc (#868)

- `acceptance_report.md`: mechanical checks 1–9, computed from git trees (base `517b6226` against the PR head) plus the editor-side reports below.
- `manifest.tsv`: every file the slice touches. Each row is moved (from, to, GUID), deleted (path, GUID, folders included), art (source, `art/` path, GUID) or evidence (source, `<branch>:<path>`, GUID).
- `plan.json`: the GUID-graph plan, with the moves and their outside referrers, and the removals.
- `artmap.json`: the `art/` copies with source blob ids. `evidence.json`: the evidence commits and their entries.
- `snap_before.txt`, `snap_after.txt`, `targets.txt`: check 4. These are renderer, collider and particle-renderer snapshots of the 24 prefabs and scenes that reach a moved GUID, taken before any change and after the moves, renames and forced reimport.
- `shaders_before.txt`, `shaders_after_rename.txt`, `shaders_after.txt`: check 5. Each shader's name, `Shader.Find` round trip, errors and pass list, taken before the rename, after it, and after the post-delete recompile.
- `recheck_report.json`, `deletes.txt`: the zero-referrer re-check run right before the `DeleteAsset` batch (95 GUIDs, 0 referrers outside the delete set), and the batch it fed.
- `moves.tsv`: the `MoveAsset` batch.
- `test_summary_full.json`, `fulltests.log`: check 9.
- `tools/`: the scripts that produced all of the above. `Slice6bTools.cs` ran in the live editor through `unity command run_script`.
