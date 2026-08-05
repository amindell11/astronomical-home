# RL Infrastructure Paydown Pass

> STATUS: **PASS CLOSED 2026-08-04** — PR-1 #223 · PR-2 #224 · harness-lane arc COMPLETE 2026-07-31 (`RL_Harness_Lane_Unification.md`: A #231 / move #236 / C #238 / D #239 / F #240 / B #246; E closed unbuilt) · PR-4 #244 · post-arc hygiene #249 · **PR-5 SHIPPED #252** (`3635cc65`, 2026-08-03). Bench-hardening HELD item superseded by Throughput Pass 2 (`b2b8a8d1`). Retrospective + record corrections: §Pass close-out.

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

## PR-3 — GREW INTO ITS OWN ARC; design FROZEN 2026-07-29

Scoping showed this is an arc, not a PR. All forks resolved with the user;
authority is now **`RL_Harness_Lane_Unification.md`** (slices A–F: substrate +
eval migration → capture/painters · slot 2 · probe clients in parallel →
bench client → Python surface). Headlines: one host + typed SessionSpec;
composition + RunBlock primitives with clients as protocol coroutines; probe
interface/registry (facing probe = first client, resolves the ledger BLOCKED
row); painter/canvas contract for live+capture markup; `record.flag` deleted;
ram bench split into contact probe + regression client (client later CLOSED
unbuilt — arc doc decision 6 closure note); eval summary schema
v2 (`opponents` rename + schema id + provenance). Playtest / profiler /
throughput lanes designed-for, not built. Do not re-decide here.

## PR-4 — statistical eval layer

> STATUS: **SHIPPED #244** (`959ab4f3`, 2026-07-31; landing-tree gate 671/673).
> Built exactly to the brief below plus one review round (write-ahead held-out
> registry, bundle seed authority, resumable banking events — disposition table
> on the PR). Wilson bounds are computed and quoted but deliberately NOT the
> decision cutoff — bound-based verdicts wait for bundle v2 (threshold
> recalibration at the rules change). Brief retained as the design record.

**Scope.** Verdict machinery that consumes measured uncertainty: replicate
protocol + interval verdicts in the gate loop, auto-arming, a banking CLI,
paired A/B checkpoint comparison, calibration bundle as versioned config.
All Python; C# untouched. **Non-goals:** threshold *recalibration* (locked:
waits for the rules change — bundle v2 is authored then); tournament/rating
machinery (deferred, carded); sim-determinism root-cause (separate BUGS card);
any C# schema or `EvalProtocol` change.

### Calibration evidence (prep-time variance experiment, 2026-07-30)

34 replicate gate-shape evals on current main (`results/rl-eval/variance-2026-07-29/`):

| Checkpoint | n | mean | SD | range | historic single draw |
|---|---|---|---|---|---|
| `699941` (banked) | 10 | 71.0 | 2.0 | 69–75 | 74 |
| `1500020` (final) | 8 | 70.1 | 1.2 | 68–72 | 67 |
| `399945` (outlier) | 8 | 61.1 | 2.5 | 57–65 | 58 |
| `699941` jobs-off | 5 | 73.0 | 1.0 | 72–74 | — |
| `699941` alt seed sets | 4×1 | 68.5 | ~2.9 obs | 65–72 | — |

Findings the design rests on: run-jitter SD ≈ 1.2–2.5 on totals (±4/75 folklore
≈ 2σ, now measured); the banked-vs-final "74 vs 67" gap is noise (true means
71.0 vs 70.1) while the 400k dip is real (>4σ) — replication separates exactly
these; per-cell noise brushes the ALERT threshold on healthy policies (an
Evader 11 observed from a ~13.5-true policy); seed-set effect SD ≈ 2 —
comparable to jitter, larger at cell level (an Orbiter 8 from a never-below-11
policy); jobs-off mode is tighter (SD 1.0) but *shifted* (+1.9) — execution
mode is part of the measurement. Caveat: historic draws ran on the Phase-B
tree; historic-vs-today gaps confound code drift with draw luck — only
within-experiment spread cleanly measures jitter.

### Forks (resolved, with why)

1. **Variance experiment → ran at prep time.** Fork 2's architecture depended
   on the magnitude; it also delivered slice A's acceptance calibration.
2. **Replicate/verdict architecture → adaptive confirmation.** `K_watch=1`; a
   degraded read (cell ≤ threshold or total < min, Wilson bounds finally read)
   triggers +2 confirmation replicates of the same checkpoint and a pooled
   re-verdict (cells n=45); ALERT means *confirmed* degraded; STOP stays the
   two-consecutive-confirmed streak. No formal alpha-spending — confirmation +
   streak IS the sequential rule; the gate reports to a human. Banking:
   `K_bank=5` on canonical seeds (SE≈0.9), paired A/B vs the incumbent
   (per-episode McNemar on shared seeds + mean-diff on replicate totals — the
   instrument that catches 74-vs-67 as noise), one held-out draw
   (interval-reported; answers generalization, never canonical thresholds).
   Rejected: fixed K=3 (3× watch cost for rarely-needed confirmation), sliding
   windows (confound policy drift with noise; smear real single-checkpoint dips).
3. **Gate semantics → erosion detector with auto-arm.** Report-only until a
   checkpoint first passes the healthy predicate, then armed; armed state +
   arming step in `verdict.json`; `--from-step` stays as escape hatch. Closes
   the `--min-step` board card (operator-knob shape rejected: misuse = the
   observed false-STOP failure mode). Curriculum-aware arming rejected
   (re-parses trainer-owned state).
4. **Seed regime → calibration bundle.** `{seed set, eps/seed, thresholds,
   arming predicate, K_watch/K_confirm/K_bank, executionMode}` versioned as one
   config; v1 = today's values, `executionMode: parallel`. Watch stays
   2001–2005×3 (pairing anchor); rotation rejected (destroys paired-test
   power); held-out 1001–1020 unseals ONLY at banking, one draw per event
   (verified never drawn to date). Jobs-off recorded as candidate v2 change at
   the rules-change break — never mixed into the parallel-mode series.
5. **Offline ELO → pairwise only; tournament deferred.** Head-to-head A-vs-B is
   slice C config + the same two-proportion verdict code; round-robin + rating
   fit carded, gated on its first real consumer (post-rules-change candidate
   selection).
6. **Math home → all Python.** Cross-run inference needs multiple summaries;
   C# sees one run. C# per-run Wilson stays informational.
7. **Replicate orchestration → Python loop, separate boots, `step-N/rep-k/`
   dirs.** Cross-boot replicates are what the historical series measures (Burst
   compile timing is a live jitter suspect); in-session replicates would
   under-measure. Replay contract becomes rep-aware (blindsider 4).
8. **Sample identity → one run dir = one sample**, enforced and surfaced by an
   evidence-provenance block in `verdict.json`. No C# id field.
9. **Resume window → documented convention** (`--from-step` after resume, in
   gate docstring + README). Mechanism ungrounded in code; below the fix-ladder
   entry bar.

### Assumptions (locked)

1. Stats are stdlib-Python (Wilson, pooled intervals, exact McNemar via
   binomial); no scipy/numpy.
2. Python recomputes intervals for pooled data; C# `wilsonLowerBound95` unchanged.
3. `EvalProtocol` constants untouched (arc approved assumption 3).
4. Builds on slice A's `opponents[]`/schema-id summaries and slice F's
   extracted watch/launcher library; verdict rules land in the post-F verdict
   module.
5. Bundle v1 is a config file in `training/rl/`; every verdict artifact records
   the bundle id that judged it. The `seeds × eps = 15` CLI hardcode moves into
   bundle validation.
6. `verdict.json` schema evolves freely (armed state, confirmation, provenance)
   — no programmatic readers exist.
7. Existing gate CLI flags keep working; new behavior is additive.
8. No blended aggregate; bench margins stay deterministic and out of scope.
9. Paired A/B reads per-episode outcomes from the episode JSONL (rows carry
   seed/episodeIndex/opponent), pairing on (seed, opponent, episodeIndex).

### Blindsider resolutions

1. **Pooled Wilson used as-is under seed clustering**, caveat documented —
   thresholds are calibrated on the same clustered structure, so the mild
   interval understatement is absorbed by calibration.
2. **Banking = separate small CLI** on the slice-F launcher library, not an
   `eval_gate` mode.
3. **Held-out draws require an explicit flag AND append to a usage registry
   file** — exposure of the sealed set stays auditable.
4. **Replay rule:** verdict re-derived from all reps present in a step dir;
   missing confirmation reps run only if the pooled state is
   degraded-unconfirmed. Deterministic and resumable.

### Coordination

Touches `eval_gate.py`/`test_eval_gate.py` and adds the bundle + banking CLI +
stats module — all post-slice-F files; zero overlap with slices B–E. "Replicate"
enters `doc/Glossary.md` (one full protocol re-execution, fresh boot, identical
inputs; differs only by mechanical sim nondeterminism — NOT a new seed draw, NOT
a cross-tree re-eval). The variance dataset and the four golden baselines
(`results/rl-eval/golden-main-d61b31cc/`) are the calibration provenance.

## PR-5 — player-build eval lane (`player-eval`)

> STATUS: design FROZEN 2026-07-31 (pr-prep session; feasibility consult:
> codex `gpt-5.6-sol` xhigh, verdict folded — disposition in the prep chat).
> The brief below is the authority; the implementing agent re-decides nothing.

Move checkpoint evals off mid-run editors onto the #187 player path
(`RLTraining.exe` precedent) — the structural fix for the editor-beside-fleet
fragility class (observed casualties: editor OOM silent death during the
variance experiment, mid-eval lease TTL expiry ×2, boot-lane dir vanish, run 1
killed by a second Unity session). Interim policy already in force
(run-mechanics runbook): mid-run evals OFF by default while prototyping; the
eval gate runs as post-run backfill (`--once`), which #244's verdict machinery
supports natively.

**Feasibility gate — resolved.** A player cannot parse `.onnx` at runtime
(`ONNXModelConverter` is editor-only; Unity staff confirm runtime serialization
is unsupported), and ML-Agents ingests ONLY a `ModelAsset` — no public
`Model`→`ModelAsset` exists and runtime `ModelLoader.Load` returns `Model`. So
the editor stays as a short per-checkpoint conversion tollbooth; what moves to
the player is the sim (all of the wall-clock). Transport = AssetBundle: the
convert step builds a genuine `ModelAsset` the supported way and the player
loads it via `AssetBundle.LoadFromFile` — ML-Agents and the packages stay
untouched.

**Scope.** A dedicated headless eval player (new scene + build method + exe at
`build/rl-harness/`); an editor convert step (import candidate — and opponent,
when the opponent is a checkpoint — then build one single-session AssetBundle
into the caller-named out dir); the typed `ModelAsset` composition seam;
`eval_lane.py --exec player` orchestration (leased convert → lease-free player
sim → summary read-back); player exit paths. Editor eval unchanged as the
reference protocol and the verdict-bearing gate. **Non-goals:** gate/banking
rewiring onto player mode (waits for bundle v2 at the rules change); capture /
OpenLoop lanes in the player; any threshold or calibration change; an exe
staleness oracle.

### Forks (resolved, with why)

1. **Measurement-mode rollout → opt-in tool now; gate stays editor.** Player
   scores are a NEW uncalibrated `executionMode` with an expected shift (the
   jobs-off precedent: +1.9, tighter SD); recalibration is locked to bundle v2.
   The runbook's interim policy already shields the fleet, so capability lands
   now without calibration debt; player becomes the calibrated default at v2.
2. **Exe topology → dedicated eval scene + build method + exe** (rejected: an
   `RLTraining.exe` boot-mode switch). Training boot (arm-and-wait for the
   trainer handshake) and eval boot (parse-run-exit) differ genuinely; a shared
   entry adds a mode-flip failure surface to every training fleet worker.
3. **Composition seam → typed `ModelAsset`, resolved at each boot boundary**
   (editor: `AssetDatabase` in `TrainingBootstrap`; player:
   `AssetBundle.LoadAsset` in the new bootstrap). `ShipAgentFactory.LoadModel`
   and its `#if UNITY_EDITOR` are deleted; composition signatures take
   `ModelAsset`. Rung 1: player-side `AssetDatabase` use becomes
   unrepresentable. Bundle contract: one bundle per session, candidate +
   optional opponent under fixed names; mirror = same asset both sides.

### Assumptions (locked)

1. Convert step = batch editor `-executeMethod`, an editor sibling of
   `RLTrainingPlayerBuild` reusing `TrainingBootstrap.Import`; explicit
   `AssetBundleBuild[]` (no persistent bundle tags enter the project);
   `StandaloneWindows64`.
2. Convert runs through `unity_access.run_batch` (coordinator rule 6); the
   player launch takes NO unity-access lease (players aren't shared editors —
   `run_parallel.py` precedent).
3. Player exit: `Application.Quit(0/1)` mirroring both editor
   `EditorApplication.Exit` sites in `HarnessSessionHost`.
4. `HarnessAssets` reaches the player scene-serialized (`TrainingHost`
   precedent).
5. Capture + OpenLoop lanes stay editor-only (record⇒graphics throws headless;
   OpenLoop has no player demand).
6. Summary provenance logs the source stem, not the asset path (Python keys on
   the stem).
7. Test strategy: EditMode coverage for the new parse grammar + boundary
   throws; e2e proof = convert + player run on the committed smoke-fixture
   `.onnx` (explicit path) producing a schema-v2 summary + exit 0, plus one
   real-checkpoint run reported in the PR body.

### Blindsider resolutions

1. **Player-mode env grammar:** new `RL_HARNESS_BUNDLE` (bundle path) alongside
   `RL_HARNESS_ONNX` (kept for stem/tag/provenance). `RL_HARNESS_BUNDLE` set in
   an editor session throws at parse — retired-names rigor, no silent
   context-dependent meaning shift.
2. **Smoke default dies in player mode:** `--exec player` requires an explicit
   `--onnx`; the parse throws on incomplete bundle vars. (The editor smoke
   default is a test convenience, not a lane feature.)
3. **Missing exe fails loud** naming the rebuild command; NO staleness
   detection (`run_parallel.py` precedent — freshness is the operator's).

### Coordination

Touches RLHarness hosts/compositions + `eval_lane.py`. The `ShipAgentFactory`
signature surface is shared with the in-flight K1 arc — second merger adapts.
Vocab: player execution is NOT a new `SessionLane`; the glossary entry (rides
the implementation PR) qualifies "player eval lane" as the eval lane under
player `executionMode`.

## Held

- Bench hardening + adjudicating throughput run — SUPERSEDED: throughput
  reopened as Pass 2 scoped to the K1 schema break (`b2b8a8d1`); Stage 0 ran
  2026-08-03 (M 161.185 vs K 139.195 steps/s). Pass 2's own scoping owns
  benching now; the held discussion is moot.
- Combat-sector validation of the shipped policy + #222 gizmos: separate
  existing thread (agent-1), not part of this pass.

## Pass close-out (2026-08-04)

Two-lane retrospective at completion (PR-trail review of #231/#236/#238/#239/
#240/#244/#246/#249/#251/#252 against the frozen briefs; artifact verification
of every posted result). Full reports live in the coordinating session's
transcript; verdicts and corrections here.

**Verdict.** Contract fidelity was high across all 11 PRs — zero silent
deviations; every departure was flagged in the PR body or user-ruled as an
amendment. The pass's strongest habit: calibrating the instrument before
trusting it (slice A discovered the sim nondeterminism via its own baselines
and amended to the deterministic mask; #239 reported its falsified facing
acceptance rather than rescuing it; #244 ran the variance experiment before
designing verdicts on top of it).

**Record corrections (from artifact recount; conclusions unchanged):**
variance experiment = **35** evals, not 34; jobs-off shift **+2.0**, not +1.9;
golden row-identity range **18–24**/75, not 18–21. Two unrecorded nuances:
(a) the banked arm's best replicate (75/75) came from the one run that failed
at teardown after writing its summary — excluding it moves the banked mean
71.0 → 69.4; (b) held-out seeds 1001–1002 are exercised routinely by the
test-eval fixture lane (smoke model only, so no selection leak), meaning
"sealed" holds at the candidate/banking layer but that exposure is invisible
to `heldout_draws.jsonl`.

**Still-standing defects — the two undispositioned codex rounds (#246/#240):**
`RLCapturePlayModeTests` missing its domain category (AGENTS.md violation);
unsanitized checkpoint stem can reach `CaptureRecorder.Validate` and abort a
capture; `eval_lane.py` manual out-dir uses second-precision timestamps
(collision window). Process finding behind them: #246 merged 17 minutes after
a 2×P1 review with no disposition — the sole break in an otherwise universal
disposition-table norm, at peak merge parallelism. Fix is procedural: no merge
with an undispositioned review thread; a one-line below-bar reply counts,
silence does not.

**Operational notes:** `eval_bank.py` has never banked a real run (BANK_ROOT
never created) — first real use is its e2e. Capture producers still leak raw
frames (K1-4 left 945 PNGs beside finished mp4s); the retention board card
stays open — the pass only bought the one-time 2.4 → 1.4 GB cleanup.
Recurring review classes worth making structural: runtime component lookups
(×3 in one week) and structure-ratchet placement (×3) — both always caught by
the adversarial lens, never in-house.

**Seeds for the next pass — RL trainer Python reimpl (new thread):**
1. Freeze the C#↔Python wire contract (obs/action schema, checkpoint/ONNX
   format, handshake grammar, JSONL/summary schemas) as its own brief-grade
   doc before porting — the arc's freeze→build→flag loop, applied to the
   boundary the trainer lives on.
2. Define trainer-equivalence gates BEFORE porting: fixed-seed update math on
   synthetic batches vs the ml-agents reference, gradient/loss traces on a
   frozen buffer. The golden-mask lesson: byte-identity against a live sim
   fails; know which layer is gateable first.
3. Fund #251's player-build tripwire first — the merge gate never builds a
   player (the identical asmdef break sat invisible 8 days) and the trainer
   arc lives entirely on that blind side, consuming the player exe, env
   grammar, and JSONL schemas.
4. Route persistence/resume PRs through the adversarial lens with an
   atomicity checklist — crash-window/authority shapes (#244's write-ahead
   registry, resume addressability) were codex's uniquely-caught class, and a
   trainer is made of them (checkpoint writes, registries, partial batches).
5. Python test shape to carry forward: #244's stdlib known-value tests +
   fake lane launcher, #240's subprocess-free env-composition tests — pure
   functions plus fakes at the process boundary, no Unity boot.
6. Existing anchors: ml-agents 3.0 has no mixed continuous+discrete actions
   (`Tactical_AI_Audit_And_Roadmap.md`; K1-3's 5+2 ActionSpec rides the
   custom-trainer-plugin seam, `Anchored_Intent_Architecture.md`
   §Infrastructure); Throughput Pass 2 owns the perf motive; the run-mechanics
   runbook, `eval_bundle_v1.json`, and the player-eval lane are the
   operational substrate the new trainer must slot into.

## References

Memory: `handoff_2026-07-27_stage3_results.md`, `rl_training_run_mechanics.md`,
`project_code_bloat_audit_2026-07-18.md`, `active_work_ledger.md`.
PR record: #171–#222 (arc), #220 (eval gate), #218 (dashboard), #201/#197
(stepping/arenas), #154/#196 (capture).
