# scripts/

Index only. Each script's contract (exit codes, machine channel, state files) lives in its own `--help` / comment-help; review law for changing any of it is `doc/agents/script-contracts.md`.

## Pool & worktrees

| Script | Purpose | Entry | Contract |
|---|---|---|---|
| `agent_worktree_pool.sh` | Slot locks, task branches, PR open, gated squash-merge, merge journal. | `./scripts/agent_worktree_pool.sh <verb>` | `--help`; section map at top of file |
| `worktree_dashboard.sh` | Read-only view of every slot: lock, branch, PR, ahead/behind, merge phase. | `./scripts/worktree_dashboard.sh [--watch]` | header comment |
| `remote_gate.sh` | Ship a branch to the remote lane box over SSH and run the Unity gate there. | `./scripts/remote_gate.sh [branch]` | header comment |
| `install_hooks.sh` | Point `core.hooksPath` at `.githooks/`. | `./scripts/install_hooks.sh` | header comment |

## Unity access & tests

| Script | Purpose | Entry | Contract |
|---|---|---|---|
| `unity_access.ps1` | Per-project owner lease + machine-wide boot lane + FIFO queue for shared Unity editors. | `-Action <verb> -Lease <id> ... [-Json]` | comment-help; section map at top of file |
| `unity_access_client.ps1` | The sanctioned way to call the coordinator from PowerShell. | dot-source, `Invoke-UnityAccessCoordinator` | comment-help |
| `unity_test_agent.ps1` | Run the Unity test suite (cold batch or `-Routed` warm editor) and write the summary the pool reads. | `-Mode <m> -ScopeType <s> ...` | comment-help; section map at top of file |
| `unity_test_scope_lib.ps1` | Scope map reading, Auto-scope resolution, filter-to-name conversion. | dot-source | function comments |
| `unity_test_scopes.json` | The scope map: module path globs and authored test filters. | data | read by the scope lib |
| `unity_doctor.ps1` | Preflight: live editor, project owner, domain-reload trap, BurstCache. | `[-Json] [-FailOnWarn]` | comment-help |

## Capture

| Script | Purpose | Entry | Contract |
|---|---|---|---|
| `capture/assemble.py` | Assemble capture frame dumps into mp4/gif. | `python scripts/capture/assemble.py <frame-dir>` | module docstring / `--help` |

## Hygiene & ratchets

| Script | Purpose | Entry | Contract |
|---|---|---|---|
| `resharper_ratchet.ps1` | Unity-aware ReSharper findings; blocks only on PR-changed lines. | `-BaseRef <ref> [-Audit]` | param block; `run-resharper` in the pool |
| `resharper-unity.DotSettings` | Inspection profile the ratchet runs with. | data | read by the ratchet |
| `sync_unity_solution.ps1` | Regenerate the `.sln` through a batch Unity (the ratchet's RunBatch child). | `-ConfigPath <json>` | called by the ratchet |
| `inert_diff.ps1` | Verdict: is a C# delta comment/whitespace-only? | `-OldPath <f> -NewPath <f>` | header comment (3-value exit) |

## lib/ (dot-sourced primitives)

| File | Owns |
|---|---|
| `lib/repo_root.ps1` | `Get-RepoRoot`: worktree root via git, never `..` counting. |
| `lib/unity_editor.ps1` | `Resolve-UnityEditorPath`: the editor matching `ProjectVersion.txt`. |
| `lib/process_tree.ps1` | `Stop-ProcessTree`: kill a Unity and its children. |
| `lib/unity_churn.ps1` | `Test-UnityAnalyticsChurnOnly`: classify tracked changes as the known analytics-define flip. |

## tests/

`scripts/tests/test_*.sh` and `test_*.ps1`, run by `./scripts/agent_worktree_pool.sh run-script-tests` and by the merge gate whenever the landing diff touches `scripts/**`. Hermetic: state stays in a temp dir; every machine root is injected.
