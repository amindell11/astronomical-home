# RL Infrastructure Paydown Pass

> STATUS: live arc — PR-1 (hygiene) + PR-2 (drivers/coordinator) building in parallel; PR-3 (eval/capture host unification) in pr-prep; PR-4 (statistical eval) designs after PR-3 freezes; bench-hardening item HELD pending user discussion

*Draft • 2026-07-28 • seeded by a four-lane parallel review (run history + results artifacts, code audit, PR trail #130–#222, board/deferral sweep) run in the coordinating session on 2026-07-28.*

Motivation: before the next training push (which waits on the combat rules-change
design, `handoff_2026-07-28_rules_change_design.md`), pay down the infra debt the
last four training arcs accumulated, and build the extensible capture + evaluation
substrate the rules-change telemetry lane will land on.

## Findings (condensed; each lane's full report lives in the coordinating session)

1. **Eval statistics cannot support the decisions riding on them.** The ±4/75
   noise floor is an n=2 estimate; Phase B's 14-checkpoint gate series spans 16
   points (58–74); no checkpoint has ever been evaluated more than twice, so eval
   variance and policy variance are confounded. The banked production checkpoint
   (`ShipCombat-699941`, shipped via #221) rests on one unreplicated 75-episode
   draw. Wilson bounds are computed in summary JSON but nothing reads them. ELO
   demonstrated a treadmill three separate times. The scripted roster is
   calibrated to the 20 u laser envelope — gate scores will not be comparable
   across the pending rules change.
2. **Eval and capture are the same machinery written twice, with near-zero
   extensibility.** `CheckpointEvaluator.Run` hardwires archetype list,
   composition, probe, and summary; each new eval/probe type costs a bespoke
   bootstrap + child-script + Python-wrapper triple (the facing probe died of
   this — its patch rotted and its bootstrap was never committed). Capture's RL
   entry point lives inside `RLEpisodePlayModeTests.cs` (~lines 400–644), cannot
   ride the coordinator batch lane, and rejects mirror opponents — both recent
   arcs hand-authored scratch scenarios to film self-play. `EvalCandidate.onnx`
   is a single import slot, so policy-vs-policy eval (offline ELO, ram bench) is
   inexpressible.
3. **Driver duplication + coordinator lease gaps, with observed failures.**
   `default_unity_exe` ×4, ~70-line boot skeleton ×3 across `training/rl/`
   drivers. `RunBatch` acquires a pid-less owner lease that TTL-expires mid-eval
   (observed twice on Phase A) and holds the machine-wide boot lane for the
   child's entire run (codex P1 on #220). `StartEditor -EditorArgs` replaces
   `-projectPath` verbatim (cost 2 builds + 1 crash). The #181
   coordinator-boot e2e (`run_smoke.py`) was never run.
4. **Throughput record is self-contradictory** — plan doc says trainer 0.71
   cores (not saturated); ledger says trainer-saturated with workers at 0.59
   after the attention obs. Bench instrument's documented ~4% repeatability vs
   observed inability to resolve <20%. **HELD** — user wants a discussion before
   any bench work is scheduled (may not be necessary).
5. **Approved-but-unexecuted bloat + at-risk scratch.** Batch E oracle-gate
   retirement approved (~480 lines). `rl_eval.ps1` (the manual eval driver used
   for every checkpoint scan) exists only in the agent-3 worktree. Ram-friction
   bench untracked in `dev/ram-bench-harness/` despite calling itself permanent.
   Confirmed dead: `scratch/eval_child.ps1`, `scratch/facing_probe_child.ps1`,
   both `plot_progress` forks (hardcoded RUN_DIRs), `dev/null/` (inert git-lfs
   hooks from a transient `core.hooksPath` accident). Config drift: hybrid ran
   `lambd 0.95` while stage-ii shipped 0.98 in the main config; pilot/smoke lack
   `beta_schedule`; README three versions stale.
6. **No artifact home or retention policy.** 2.4 GB in `results/`, ~1.27 GB raw
   PNG frames beside finished mp4s; 29 run dirs incl. debris marked deletable
   since 07-24; sticky `record.flag` footgun; ~7-step manual launch ritual.

## User decisions (locked 2026-07-28)

- **λ:** past runs not relitigated; **future runs use `lambd: 0.98`**. Pin the
  hyperparameter block cross-config via `RLTrainerConfigEditModeTests`.
- **Ram bench:** becomes part of the redesigned eval/capture system (PR-3);
  parked under `training/archive/` meanwhile so it can't be lost.
- **Objectives doc (+88 uncommitted lines):** commit with a one-line status
  correction (PR-5 built then SHELVED as #156; brief retained as retry seed).
- **Stepping/Path A:** confirmed DONE (#188/#197/#201) — the "open" entries in
  the parking lot / throughput topic file are stale records; delete them.
- **Bench hardening / adjudicating throughput run:** HELD for discussion.
- **Gate threshold recalibration:** deferred until the rules change lands; PR-4
  builds machinery only.

## PR-1 — hygiene & rescue sweep (slot agent-2)

Scope (bounds the diff):

- **Config:** `ppo_ship_combat_hybrid.yaml` → `lambd: 0.98`; add
  `beta_schedule` to pilot/smoke or annotate why absent; fix the pilot header
  comment (claims "identical to ppo_ship_combat.yaml" — false on three axes);
  extend `RLTrainerConfigEditModeTests` to pin the hyperparameter block across
  all six YAMLs.
- **C# deletions:** batch E oracle-gate retirement (`RLHarness/OracleTypes.cs`,
  `ManeuverChooser.cs`, `DummyTarget` aim path, `ManeuverOraclePlayModeTests`);
  `VfxEnabled` (vestigial post-#210). One rung: deletion; no replacement guards.
- **Scratch promotion/deletion:** promote `plot_progress.py` into
  `training/rl/` with a `--run-dir` argument (delete `_run2` fork); promote
  `rl_eval.ps1` from `D:/amind/git/agent-3/scratch/` into `training/rl/`;
  delete dead `scratch/eval_child.ps1` + `scratch/facing_probe_child.ps1`;
  delete `dev/null/`; park `dev/ram-bench-harness/` under
  `training/archive/ram-bench-harness/` (absorbed properly in PR-3);
  `archetype_summary.py` schema docstring v3→v5 if kept, else delete.
- **Docs:** commit the +92 working-tree lines of
  `RL_Training_Throughput_Optimization.md` (finished results documentation);
  commit `Objectives_Encounters_Sector_Rethink.md` with the status correction
  above. NOTE: both diffs live uncommitted in the PRIMARY tree — replicate them
  into the worktree (copy the files), do not re-derive.
- **Record cleanup (not repo code):** delete stale board cards (brain-state
  menu #161, merge-gate retry #128, tactical-AI goal-policy card, 500k
  policy-vs-Evader footage card) and stale parking-lot/topic entries
  (stepping PR-2, Path A); delete the two ✅ ledger rows past their charter.
- **Results retention (primary tree, gitignored, DESTRUCTIVE — enumerate and
  confirm with the user in-session before deleting):** raw PNG frame dirs whose
  mp4 exists; run dirs marked deletable 07-24 (`bisect_noinit`,
  `ship_combat_smoke`, `manualaim2/4/5`).

Out of scope: README rewrite (PR-2 owns it — describes the drivers PR-2
reshapes); anything touching the drivers or coordinator; eval/capture C#
beyond the deletions named.

## PR-2 — driver consolidation + coordinator lease hardening (slot agent-3)

Scope:

- Extract `training/rl/driver_common.py`: `default_unity_exe` (keep eval_gate's
  ProjectVersion.txt-derived shape), `wait_for`, `log_contains`, marker
  constants, `config_run_id`/`config_has_self_play`. All five drivers import it.
- Collapse `run_smoke_selfplay.py` into flags on `run_smoke.py` (its unique
  value is editor-lane self-play composition proof — keep that assert).
- `scripts/unity_access.ps1`: (a) pid-back or renew the `RunBatch` owner lease
  once the child exists (fixes the observed mid-eval TTL expiry); (b) release
  the machine-wide boot lane after startup rather than holding it for the
  child's whole run (the #220 codex P1 — preserve the opaque-child contract);
  (c) make `-EditorArgs` compose with, not replace, `-projectPath`.
  `scripts/tests/test_unity_access.ps1` extends to cover (a)–(c).
- Rewrite `training/rl/README.md` (driver-first workflow, 3.5M/36 checkpoints,
  `rl-episode-v5`, coordinator boot path — the raw `Unity.exe` invocation #181
  retired dies).
- **Proof:** actually run the #181 coordinator-boot e2e (`run_smoke.py`
  end-to-end through the coordinator) — the standing unrun follow-up.
- JSONL suffix contract: pin `run_parallel.py:log_suffixes` ↔
  `TrainingHost.ComposeSuffix` (`-w{k}-a{j}`) with a cross-language test or
  shared constant — consumer currently re-derives a producer-owned format.

Out of scope: eval_gate verdict logic, phase-aware thresholds (PR-4);
any C# harness restructuring (PR-3).

## PR-3 — eval/capture host unification (pr-prep first; build after freeze)

Goals: one parameterized batch host lane replacing the per-type
bootstrap+child+wrapper triple; capture promoted out of the test file; probes
become pluggable instead of patch-fossils. Known forks for the pr-prep:

- Shape of the parameterized entry (opponent-source: archetype roster | second
  checkpoint; recording on/off; probe selection) vs separate hosts.
- Second ONNX import slot design (enables policy-vs-policy: offline ELO, ram
  bench, mirror eval).
- `CheckpointEvaluator` split line: episode loop vs archetype summary.
- CaptureHost boundary: shared composition with eval; `record.flag` lifecycle
  (self-cleaning); mirror lane first-class.
- Probe lane: rebuild the blocked facing probe (ledger row) as the first
  client; carries rock-shooting probe + heat-telemetry read (parking lot).
- Ram bench absorption (user decision above).
- eval_gate watch-loop vs verdict-rule extraction (boundary with PR-4).

## PR-4 — statistical eval layer (design after PR-3 freezes)

N-replicate eval protocol (separate eval vs policy variance empirically, once);
interval reporting in summaries/verdicts (read the Wilson bounds already
computed); phase-aware gate arming (no more false STOP on fresh curricula).
Threshold recalibration explicitly deferred to the rules change.

## Held

- Bench hardening + adjudicating throughput run — user discussion first.
- Combat-sector validation of the shipped policy + #222 gizmos: separate
  existing thread (agent-1), not part of this pass.

## References

Memory: `handoff_2026-07-27_stage3_results.md`, `rl_training_run_mechanics.md`,
`project_code_bloat_audit_2026-07-18.md`, `active_work_ledger.md`.
PR record: #171–#222 (arc), #220 (eval gate), #218 (dashboard), #201/#197
(stepping/arenas), #154/#196 (capture).
