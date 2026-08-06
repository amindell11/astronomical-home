# RL Trainer Runtime Takeover

> STATUS: live arc — opened 2026-08-05; slices 0–2 COMPLETE (#259 tripwire ·
> wire-contract freeze · stage-1a wrapper #262); slice-3 brief FROZEN
> 2026-08-06 (§Slice-3 decision brief) — next = the rider bench PR, then the
> stage-1b build; stages 3+ are entry-gated, not committed.

*Seeded 2026-08-05 by a four-lane grounding review run in the arc-opening
session (ml-agents feature-usage inventory, C#↔Python wire-contract map,
prior-art path assessment, operational-substrate compatibility sweep — full
lane reports live in that session's transcript) plus the 2026-08-03 Pass-2
throughput rulings (`project_rl_training_throughput.md` §Stage 0/1/2 +
§Custom RL trainer runtime).*

## Vocabulary

**trainer runtime** — the Python process that owns environment scheduling,
PPO updates, checkpointing, and stats for a training run. Today that is stock
`mlagents-learn` (the **ml-agents runtime**); this arc replaces it stage by
stage with project-owned code in `training/rl/` (the **owned runtime**).
"Trainer" joins the glossary collision table with this landing — qualify it
everywhere ("trainer runtime", "trainer config", "custom-trainer plugin
seam"), and always in titles.

## Motivation — throughput-led (user ranking locked 2026-08-05)

The measured chain (all 2026-08-03; evidence in
`project_rl_training_throughput.md` and the living throughput plan §Stage
0M/1/2):

1. The K1-4 tree sustains **139.195 steps/s (13.920 decisions/s)** at
   N=6/M=1/T4 ⇒ ~7 h per 3.5M-step run.
2. Stage 1 profile: the ML-Agents decision path is **98.7% synchronous gRPC
   exchange wait** (4.833 ms/decision); all obs-build + serialization +
   action-apply is 0.063 ms (<1%). Obs trim is a dead lever.
3. Stage 2A/2B: topology and torch-thread levers are closed (N3/M2 −1.2%
   tie; T2 −7.6%; T1 −25.2%). Timer split: policy evaluation ~94 s + PPO
   update phases ~64.5 s ≈ 82% of bench wall time, mostly serial.
4. Purpose-built runtime estimate: **2–3× raw throughput** (~3 h per 3.5M
   run), practical ceiling ~400 steps/s set by env-side work.

Stage acceptance is therefore measured in **decisions/s and wall-clock per
run**. Two riders land where the owned layer touches them but never justify
scope on their own:

- **Seam inversions** (§Seam inversions): launcher/gate/dashboard re-derive
  runtime-owned facts in 11 places; the owned layer replaces them with
  producer-emitted contracts.
- **Stack freedom**: mlagents 1.1.0 pins Python ≤3.10.12, numpy <1.24,
  onnx ==1.15.0, torch effectively <2.9 (newer torch breaks its ONNX
  export). Freedom arrives only as far as ownership reaches.

Dead motivations, recorded so they aren't re-raised: hybrid-action support
(the "no mixed continuous+discrete" claim was refuted in source and by the
K1-0 smoke — K1-3's 5+2 spec runs on stock PPO), and trainer CPU saturation
(the 0.71-core probe is obsolete; the cost is exchange latency + serial
update phases, not cores).

## User decisions (locked 2026-08-05)

1. **Throughput-led.** Primary metric decisions/s + wall-clock; seam/stack
   wins ride along.
2. **Stage-gated takeover.** Stages 1–2 are committed now; every later stage
   opens only through its written entry gate. A full ml-agents reimpl is NOT
   the committed end state.
3. **Layered equivalence.** Cheap behavioral gates at every stage; the full
   paired-run gate concentrates at cutover; math gates arm exactly when a
   stage re-owns the corresponding math (§Equivalence gates — amended
   2026-08-05, user ruling).
4. **1a/1b split + cadence contract** (2026-08-05). Stage 1 splits into a
   zero-risk entry wrapper (1a) and the scheduler re-own (1b); the arc
   advances only in quiet-lane increments so it never blocks other feature
   threads (§Cadence contract). Wire-contract freeze stays full-depth (a
   depth trim was offered and not taken).

Standing assumptions (noted, not asked): the two runtimes coexist behind a
`run_parallel.py` runtime selector, with stock `mlagents-learn` remaining the
reference implementation until a stage's gates pass; the wire-contract freeze
(slice 1) and the #251 player-build tripwire (slice 0) land before stage 1
builds.

## The ladder

**Stage 1a — owned entry wrapper (committed; split ruled 2026-08-05).** A
project-owned entry that parses the CLI/YAML, writes the run manifest,
registers an ADDITIVE stats-writer (in-process, wrapper-scoped — amended
2026-08-05 from the `[mlagents.stats_writer]` entry point, which is
venv-global and would ride along into stock reference runs; tfevents keep
flowing, `plot_progress.py` stays on them) emitting `summaries.jsonl`,
refuses `--resume` + `--initialize-from` loudly, then delegates the entire loop to stock
`learn.main()` (wrapper mechanics proven by the 2026-08-03 torch-thread
experiment). The launcher grows its runtime selector here; dashboard + bench
repoint to manifest/summaries in the same slice. Lands the manifest- and
summary-side seam inversions with the loop untouched — semantic risk ≈
zero. Gates: smoke only (ghost-swap canary + one 4k parallel smoke).

**Stage 1b — ready-environment scheduler re-own (committed).** Replaces the
`learn.py` run loop and `SubprocessEnvManager` scheduling with project code.
Retains, as imported libraries at the pinned version: `mlagents_envs`
(communicator + side channels), and ml-agents' `TorchPolicy` /
`TorchPPOOptimizer` / buffer / `GhostTrainer` / curriculum-manager classes —
the update math is inherited, not ported. Cost stated honestly: this imports
ml-agents *internals*, so upgrading ml-agents gets harder; acceptable
because the toolchain is pinned anyway. Lands the remaining inversions
(explicit worker-index argv, atomic checkpoint publish + checkpoint
manifest). Gates: cheap tier (§Equivalence gates) — stage 1 alone never
becomes the production path.

**Stage 2 — cross-worker microbatch inference (committed).** Collect ready
workers inside a sub-millisecond window and run one vectorized policy forward
instead of sequential per-worker forwards. Estimated 190–240 steps/s. Gates:
cheap tier + the batched ≡ sequential identity proof; the committed scope's
single full paired run fires at this stage's cutover.

**Stage 3 — GPU PPO updates (entry-gated).** Entry gate: stages 1–2 shipped
and the update phase still ≥~25% of wall time. Requires a CUDA torch build
(isolated env or a relaxed pin — first stack-freedom bite). Gates: cheap tier
+ frozen-buffer CPU-vs-GPU tolerance + a re-armed full paired run at its own
cutover (§Equivalence gates, re-arm rule).

**Stage 4 — local actors + rollout barriers (entry-gated).** Inference moves
into Unity (Sentis) with weight sync at update boundaries — this changes the
wire usage itself (no per-decision exchange) and needs its own wire-contract
amendment plus a sample-efficiency guard. Not designed here.

**Stage 5 — PPO math re-ownership (entry-gated).** Only with an observed
algorithm need or a stack-ceiling bite. This is where the ~5–6k-line reimpl
surface lives (PPO/GAE, buffers, curriculum lane semantics incl. the global
reward-deque quirk, self-play/ELO, export) — and where the full math-gate
suite arms. Not committed.

## Equivalence gates (frozen)

**Cheap tier — every stage; hours, not nights:**

- *Paired throughput bench*: existing `bench_throughput.py` protocol — 24k
  steps, N=6/M=1, ≥2 quiet replicates per arm, same tree + config SHA-256,
  ml-agents runtime vs owned runtime. >5% replicate gap → third run, don't
  interpret unstable pairs. This is the stage's throughput claim.
- *Curriculum canary*: full production config, ~300k steps per arm — far
  enough to cross the first lesson advance (K1-4 crossed at ~222k). Assert
  the transition fires at a comparable step; overlay loss/entropy for gross
  divergence; check mechanical invariants (updates per N steps,
  checkpoint/summary cadence, one JSONL per worker).
- *Ghost-swap canary*: `ppo_ship_combat_selfplay_smoke.yaml` under the owned
  runtime — swap/team/ELO mechanics exercised in seconds.
- *Stage-specific sim-free proofs* — see the table.

**Cutover gate — once for the committed scope.** When the owned runtime
becomes the production path (post-stage-2): one full from-scratch run per
runtime, same tree/config/composition; final checkpoints judged with the
#244 machinery (K=5 gate replicates on canonical seeds, per-episode McNemar
paired A/B on shared seeds). Pass = within the measured noise floor
(replicate SD ≈ 1.2–2.5 on /75 totals). It certifies the combined
stage-1+2 delta at once. Preferred vehicle: piggyback the next needed
production retrain — run it on the owned runtime, so the stock reference arm
on the same tree is the only marginal run; flag at ledger-claim time that a
gate failure sends that retrain back to stock (the piggybacked thread
carries the schedule risk). Bisection if it fails: microbatch window = 1
reproduces sequential scheduling — an internal A/B knob separating stage 2's
contribution from stage 1's.

**Re-arm rule.** Every entry-gated stage that changes update math or
synchronization semantics (GPU updates, `threaded`-style overlap, local
actors, PPO re-ownership) re-arms the full paired-run gate at its own
cutover.

Why behavior-level while the math is inherited: the sim is not run-to-run
reproducible ([[project-eval-sim-nondeterminism]]; the golden-mask history),
so "same inputs → same weights" is untestable against a live sim. The gate's
paired-replicate statistics are the calibrated instrument for "same
strength".

**Math — arms when the corresponding math is re-owned (sim-free, so exact):**

- *Frozen-buffer regression*: one recorded trajectory buffer as a committed
  fixture; identical buffer + seeded weights through the pinned ml-agents
  optimizer and the owned one; losses and post-update weights match to float
  tolerance.
- *Synthetic known-value tests*: hand-built micro-batches with hand-computed
  GAE / clipped-surrogate / entropy expectations (the #244/#240 stdlib test
  shape, no Unity boot). Same pattern for any re-owned subsystem: synthetic
  reward stream → curriculum lesson transitions at the same steps, ELO
  updates, checkpoint pruning.
- *Export identity* (any stage that changes who writes the ONNX): export from
  identical weights via both paths; Sentis imports both; identical inference
  outputs on a fixed observation batch.

| Stage | Cheap tier | Sim-free proof | Full paired run |
|---|---|---|---|
| 1 entry + scheduler | bench + canaries | — | covered at cutover |
| 2 microbatch | bench + canaries | batched ≡ sequential | **cutover gate** (combined 1+2) |
| 3 GPU updates | bench + canaries | frozen-buffer CPU-vs-GPU tolerance | re-armed at its cutover |
| 4 local actors | bench + canaries + sample-efficiency guard | export identity | re-armed at its cutover |
| 5 PPO re-ownership | bench + canaries | full math suite | re-armed before cutover |

## Compatibility requirements (hard core)

From the operational-substrate sweep — the owned runtime must, from stage 1:

1. Write `<results_dir>/<run-id>/ShipCombat/ShipCombat-<step>.onnx` with
   decimal, resume-monotonic steps, complete-when-visible (owned runtime does
   write-then-rename — inversion V2).
2. Write the final `<results_dir>/<run-id>/ShipCombat.onnx` at the run root.
3. Write tfevents in the behavior dir carrying the six `plot_progress.py`
   scalar tags + the `Lesson` family (+ `Self-play/ELO` under self-play).
4. Resolve `checkpoint_settings.results_dir` relative to CWD and create the
   run dir at launch.
5. Accept `<config> --run-id --env --num-envs --base-port --no-graphics
   [--resume|--force] [--initialize-from] --env-args …` — config positional,
   `--env-args` forwarded verbatim to every worker alongside
   `--mlagents-port <base+k>` (contiguous from k=0; suffix contract and
   worker-seed decorrelation both hang off it).
6. Pass `RL_SMOKE` / `RL_SELFPLAY` / `RL_HYBRID_SCRIPTED_WORKERS` through to
   workers; keep workers in its own process tree (Windows `taskkill /T`
   reapability); exit 0 on success.
7. Emit the stdout grammar the drivers/dashboard parse: `Listening on port`,
   `… Step: N. Time Elapsed: T s. Mean Reward: R.[ ELO: E.]`, the
   `max_steps:` echo, and the lesson-advance line — until the structured
   summary stream (V5) lands AND both consumers are repointed in the same
   slice.
8. Export ONNX that `SentisModelParamLoader` accepts: opset 9,
   `version_number` 3, `memory_size` 0, `obs_{i}` inputs in
   alphabetical-by-sensor-name order, the continuous (+ discrete, post-K1)
   output triplets. Inherited for free while ml-agents' `ModelSerializer` is
   retained; pinned by an export-identity test the moment it isn't.
9. Keep enough checkpoints for post-run `eval_gate.py --once` backfill (the
   default eval policy) and honor `keep_checkpoints`/`checkpoint_interval`.
10. Keep `--initialize-from` / `--resume` working against
    `<run>/<behavior>/checkpoint.pt`, including the archived seed runs
    (`training/archive/ship_combat_500k/`), or migrate the seeds explicitly.

`RLTrainerConfigEditModeTests` pins the YAML *text* of every config in
`training/rl/` (cross-config hyperparameter identity, γ ↔ `RewardSpec`,
pacing-contract engine settings, env-param keys, curriculum band ⊇ eval
density, self-play/hybrid shape). The owned runtime keeps the YAML schema, so
the tests keep gating; if the schema ever changes, the invariants migrate —
they are the substance, the regexes the accident.

K1 note: the wire-contract doc freezes current main's schema (obs 26 + [64,7]
buffer, 6 continuous) with the K1-3 delta (obs 28, 5-continuous+2-discrete,
`rl-episode-v6`) as an addendum; whichever tree is production when stage 1
lands is the reference.

## Seam inversions (riders, land in the owned layer)

The substrate sweep found consumers re-deriving runtime-owned facts (wiring
rule 6 corollary violations). Stage 1 inverts the ones in its layer:

- **Run manifest** *(stage 1a)* — owned entry writes `{runId, behavior,
  resultsDir, startedAt, maxSteps, mode, configHash}` at launch; replaces:
  behavior name hardcoded in `eval_gate.py:310` (V3), results root hardcoded
  ×5 (V4), run-start inferred from dir ctime (V7), self-play inferred from
  ELO's presence in a log line (V6).
- **Structured summary stream** *(stage 1a)* — `summaries.jsonl` via the
  additive stats-writer plugin replaces stdout scraping by two different
  regexes (V5); human log stays human. Consumers (`dev/rl-status/server.py`,
  `bench_throughput.py`) repoint in the same slice; stdout markers stay
  emitted until then (compat item 7).
- **Fail loud on `--resume` + `--initialize-from`** *(stage 1a)* (V11) — the
  launcher's boundary guard stops being load-bearing.
- **Checkpoint manifest + atomic publish** *(stage 1b)* — write-then-rename;
  manifest line `{step, onnx, pt, completedAt}` after fsync; replaces: step
  regex'd from filenames (V1), completeness inferred from glob visibility
  (V2).
- **Explicit `--harness-worker-index k`** *(stage 1b)* in per-worker argv
  replaces the port-arithmetic derivation (V9); `TrainingHost` keeps the
  throw-on-mismatch as a cross-check.
- Kept as-is: `RLDriverContractEditModeTests`' cross-language pin (V10) — the
  direction is right; it extends to the owned launcher constants.

## Slices

- **Slice 0 — precursors.** (a) Land the 2026-08-03 throughput results — the
  living-plan update currently sitting uncommitted in codex worktree `89ee` —
  on main (rides this arc's docs landing). (b) #251 player-build tripwire:
  the merge gate never builds a player, the last break sat invisible 8 days,
  and this arc lives on the player path; a cheap scheduled/gate-adjacent
  player build lands before stage 1 builds (its own small PR; board card
  exists after this landing). (c) Board/ledger/memory bookkeeping.
- **Slice 1 — wire-contract freeze** (`RL_Trainer_Wire_Contract.md`,
  docs-only, brief-grade). Freezes every boundary surface with owner +
  stage-1 disposition (retained / owned / inverted-at-stage-N): comm version
  1.5.0 + two-call handshake, port grammar, side-channel framing + the
  engine-config values the pacing contract asserts, env-param names +
  sampler-valued lessons, obs ordering (alphabetical by sensor name),
  ActionSpec, ONNX tensor names/opset/version, results layout + checkpoint
  grammar, stdout markers, CLI surface, env-var pass-through. Seed: the
  contract-map lane report's freeze checklist (arc-opening session).
- **Slice 2 — stage 1a wrapper build** (light pr-prep; consumer repoints
  reviewed with it).
- **Slice 3 — stage 1b scheduler build** (pr-prep DONE 2026-08-06 —
  §Slice-3 decision brief; adversarial atomicity round run and disposed in
  the brief. Build order: rider bench PR first, then the 1b PR).
- **Slice 4 — stage 2 build** (pr-prep first).
- Later slices open only through their entry gates (§The ladder).

## Slice-3 decision brief — stage 1b, ready-environment scheduler re-own (FROZEN 2026-08-06)

> Prep session 2026-08-05/06; forks resolved with user; adversarial atomicity
> round (two consultants: fresh Fable subagent + codex CLI 0.144.1, identical
> Mode-B packet) disposed — see §Atomicity round below. Codex invocation
> (recorded for convention): `codex exec --sandbox read-only - < packet.md`,
> cwd = repo root. The implementing agent builds from this brief plus the
> plan; it re-decides nothing here.

### Scope

Replace the stock run loop — `learn.py`'s `run_training` flow,
`TrainerController` (297), the `EnvManager` base pump (157), and
`SubprocessEnvManager` (546) — with project code in
`training/rl/trainer_runtime/`, behind the existing
`python -m trainer_runtime.entry` CLI (surface unchanged; `entry.py`'s
`learn.run_cli(options)` delegation line is what dies). Retained as pinned
imports: `mlagents_envs` wholesale (`UnityEnvironment`, communicator, side
channels), `TrainerFactory` + trainer plugin registry (GhostTrainer wrap +
`GhostController` ride it), `AgentManager`/`AgentProcessor`/`AgentManagerQueue`,
`EnvironmentParameterManager` + `GlobalTrainingStatus`, `TorchModelSaver`
(subclassed for owned publish) + `ModelSerializer`, the stock stats writers +
the owned `JsonlStatsWriter`. Update math is inherited, not ported.

In-slice riders: atomic checkpoint publish + checkpoint manifest (V1/V2
inversion incl. consumer repoint), per-checkpoint training-status
persistence, per-worker `--harness-worker-index` emission (V9 producer half),
eval_gate behavior-name sourcing from the run manifest (V3 kill, with
fallback).

### Non-goals

- No throughput claim — stage 2 owns that; this slice's contract is
  equivalence.
- No update-math or curriculum-semantics changes (advance logic bug-for-bug).
- No editor-lane runtime selector (`run_training.py`/`run_smoke.py` stay
  stock).
- No C# changes — V9's consumer half (TrainingHost reads the flag,
  cross-checks port arithmetic, throws on mismatch) is a named follow-up
  micro-PR outside the quiet-lane cadence (wire contract §3 amendment
  records the split).
- No thread topology — stage-2 design input.
- Bench reference arm is NOT in this diff: rider PR (fork F) lands first.

### Locked forks

- **A. Worker topology — subprocess-per-env retained.** Owned scheduler
  reproduces stock's process-per-worker shape (worker fn, shared step queue,
  restart quotas ported 1:1). Why: the cheap-tier gates measure behavioral
  equivalence; identical topology means the canary compares only the
  re-owned scheduling logic. Threads re-evaluated as a stage-2 design input.
- **B. Ownership boundary — retain `AgentManager`.** Re-own learn-flow +
  TrainerController + pump + SubprocessEnvManager as one owned scheduler;
  retain `AgentManager`/`AgentProcessor` and `TrainerFactory` as imports.
  Why: trajectory assembly is correctness-dense (the `interrupted` flag
  drives PPO truncation bootstrapping — wire contract §4 load-bearing) and
  no cheap gate can prove an equivalent port; factory retention inherits the
  GhostTrainer wrap (`trainer_factory.py:121`) and MAX(min_lesson_length)
  buffer sizing (`trainer_factory.py:99`) for free.
- **C. Checkpoint publish + manifest — atomic publish, manifest + glob
  fallback, V3 killed.** (Publish ordering and seam split revised by the
  atomicity round — items 1/2.)
  - Split of responsibility: the owned saver (a `TorchModelSaver` subclass)
    stages and atomically renames the **interval artifacts only**
    (`<name>.tmp` in the same dir → fsync → `os.replace()`; order
    `ShipCombat-<step>.pt` → `ShipCombat-<step>.onnx`), stages
    `checkpoint.pt.tmp`, and signals "published step N" to the loop. The
    **owned loop runs the commit tail** after `trainer.advance()` returns —
    strictly ordered: ① manifest line append+flush → ② atomic
    `training_status.json` save (fork D) → ③ `checkpoint.pt` rename (resume
    pointer commits LAST). Running post-advance puts the tail after
    `ModelCheckpointManager.add_checkpoint`, so the persisted registry
    includes step N and reflects pruning. Pointer-last shrinks the
    inconsistency window from seconds (a multi-MB ONNX export sat inside
    it) to microseconds (two renames). `.tmp` names are invisible to the
    legacy glob and anchored regex. `copy_final_model` (run-root
    `ShipCombat.onnx`) atomicized the same way, after the final commit tail.
  - Manifest: `results/rl-training/<run-id>/checkpoint_manifest.jsonl`
    (sibling of `run_manifest.json`), one line per published checkpoint,
    camelCase `{step, onnx, pt, completedAt}`, paths relative to the run
    dir, append-only. Duplicate-step semantics: reader dedupes by step,
    **last line wins** (resume legs legitimately republish the boundary
    step with different weights; stock overwrites identically, we merely
    record it). Writer repairs a torn tail on resume-open (truncate any
    non-newline-terminated fragment before appending). Reader stays
    tolerant of an unterminated tail only. Name + reader in
    `trainer_runtime/contract.py`.
  - Consumers: `checkpoint_watch` prefers the manifest when present, falls
    back to today's glob+regex when absent (per-poll preference; step-dedup
    across both). In manifest mode it yields only steps whose `.onnx`
    exists at yield time (skip + log — pruning outlives lines by design;
    smoke configs run `keep_checkpoints: 2`, so this is not
    production-moot). Filename grammar stays frozen as compat. `eval_gate`
    sources behavior from `run_manifest.json` when present, hardcoded
    `"ShipCombat"` fallback otherwise. Accepted caveat: a resume-leg
    republish of step N rewrites bytes a prior `verdict.json` already
    judged; the gate never re-judges (stock-inherent overwrite, now
    documented).
  - Saver injection: swap onto `trainer.model_saver` (and
    `ghost.trainer.model_saver`) immediately after `factory.generate()` and
    BEFORE `create_policy` — ghost `create_policy` triggers the inner
    `add_policy` → `model_saver.register` + `initialize_or_load`. Ordering
    pinned by unit test.
- **D. Resume-state persistence — per-checkpoint, hooked in the loop.**
  (Seam relocated by the atomicity round, item 1: a saver-seam hook would
  persist a run-start ELO forever and a one-behind registry.) The loop's
  commit tail (fork C ②) saves `GlobalTrainingStatus` atomically
  (temp + `os.replace`) after every publish; for ghost-wrapped trainers it
  first mirrors `ghost.current_elo` into `GlobalTrainingStatus` (stock only
  does this in end-of-run `save_model` — `ghost/trainer.py:331`). Every
  status write — per-checkpoint AND at-exit — routes through the one atomic
  helper; stock's truncating `save_state` is never called (a phase-blind
  timeout kill during teardown would otherwise destroy the last good
  snapshot and brick `--resume` on an uncaught `JSONDecodeError`). Schema
  unchanged (stock-readable). Why: stock persists only in `learn.py`'s
  `finally`; our watchdog kills are `taskkill /F /T`, so lesson state,
  registry, and ELO were lost while weights resumed. With the commit tail,
  resume loads a (weights, lesson, ELO, registry) tuple consistent as-of
  the last completed tail, worst case one checkpoint interval stale.
- **E. V9 slicing — emit-only.** Owned env factory appends
  `--harness-worker-index k` to each worker's `additional_args` (Unity
  ignores unknown argv; `TrainingHost` port-arithmetic derivation + throw
  stays the sole authority). C# read + cross-check = follow-up micro-PR
  (wire contract §3 amendment).
- **F. Bench reference arm — rider PR, lands before the 1b build.**
  `bench_throughput.py` gains `--trainer-runtime` (threaded into the command
  and the row's provenance key) + a stock-arm reader that parses
  `(step, elapsed)` from the stock stdout summary lines in the existing
  `{run_id}-parallel-trainer.log` redirect (the pre-1a surface, resurrected
  for the stock arm only).

### Assumptions (code-cited)

1. **Package shape.** Owned loop extends the #262 package; suggested modules
   `run_loop.py` (controller port) + `env_scheduler.py` (worker/scheduling
   port) + `publish.py` (owned saver); `contract.py` gains the checkpoint
   manifest name + reader. Implementing agent free on file naming, not on
   seam placement.
2. **CLI + config parse unchanged** — `learn.parse_command_line` (which also
   runs `register_trainer_plugins`, populating the factory's type registry)
   and the `--resume`+`--initialize-from` refusal stay as in #262.
3. **Worker function parity** with `subprocess_env_manager.worker`
   (`:116-243`): same side-channel quartet (EngineConfigurationChannel fed
   the YAML `engine_settings` — the pacing-contract values; env-params;
   stats; analytics on worker 0 only), same `env_seed = seed + worker_id`
   (`learn.py:190`), same log-level propagation into workers (keeps
   `Listening on port` — emitted by retained `environment.py:223`), same
   STEP/RESET/CLOSE/ENV_EXITED protocol, plus exactly one addition: the
   per-worker index flag (fork E).
4. **Scheduling parity**: `_step` ready-poll (queue all non-waiting, poll
   until ≥1 response), restart quota/rate-limit machinery, restart → full
   reset, ported verbatim from `subprocess_env_manager.py:299-432`.
5. **Loop parity**: `start_learning` sequence ported — initial reset with
   current samplers, `log_current_lesson()`, `advance()` then
   `reset_env_if_ready` once per processed step-info, `_not_done_training`
   stop, `finally: _save_models`; np/torch seeded as
   `trainer_controller.py:67-68`; per-advance lesson-number stat.
6. **Curriculum quirk guarantee**: the global reward-deque clear ports
   verbatim from `trainer_controller.py:218-220` (ANY lane advance → clear
   ALL trainers' buffers); the MAX(min_lesson_length) window is inherited
   via the retained factory; ghost `should_reset()` → full reset +
   `end_trainer_episodes`; `elif updated → set_env_parameters` (no reset)
   branch kept. Pinned by stdlib unit tests driving the ported loop with a
   synthetic reward stream against the REAL retained
   `EnvironmentParameterManager` + `GlobalTrainingStatus` (advance step,
   clear, reset-vs-no-reset all asserted), plus the 300k canary observable.
7. **Stdout markers flow free**: `Listening on port` (retained worker code +
   log config), lesson line (retained
   `environment_parameter_manager.py:119-133`, matching the dashboard's
   anchor-free `LESSON_RE`), console summary (retained ConsoleWriter). Zero
   marker repoints in-slice; both structured consumers were repointed @1a;
   the `Listening on port` waiters are the stock-only editor lanes.
8. **Editor lanes stay stock**; the owned loop keeps `env_path=None` viable
   through retained `UnityEnvironment` but no editor-lane wiring lands.
9. **Run artifacts parity**: `configuration.yaml`, `run_logs/timers.json`,
   tfevents (stock writers still registered), `summaries.jsonl` (owned
   writer kept, resume-monotonic offset kept, plus the same torn-tail
   repair on resume-open as the checkpoint manifest — the latent
   append-after-torn-fragment defect exists in the @1a writer too and this
   slice owns that file), `run_logs/training_status.json` (per-checkpoint
   via the commit tail, fork D).
10. **Process contract**: exit 0 on success; env-failure exceptions
    re-raised after model save (port of `trainer_controller.py:180-200`
    semantics); workers remain children (taskkill /T reapability);
    `run_parallel.py` untouched (its `RLDriverContractEditModeTests` pin
    stands).
11. **`threaded: true` refused loudly at parse** — configs never set it,
    default false (`settings.py:639`); the owned loop is single-threaded and
    drops the trainer-thread machinery.
12. **Single-behavior constraint stays** (#262 `entry.py:58-62`); dynamic
    ghost behavior registration (`ShipCombat?team=1` appearing in step infos
    post-reset) is handled by the ported registration path, same brain_name
    → same trainer.
13. **Test shape**: stdlib + fakes at the process boundary (no Unity boot):
    scheduler tests with fake workers, publish-atomicity tests (kill points
    simulated between write stages), curriculum-invocation tests (#6),
    manifest round-trip + torn-tail + duplicate-step tests, consumer tests
    (manifest+fallback watch; eval_gate behavior sourcing).
14. **Crash model**: process-kill atomicity (`taskkill /F /T`), not
    power-loss durability — fsync-before-rename bounds the window; no
    directory fsync on Windows; NTFS same-volume `os.replace` is the
    atomicity primitive.
15. **Windows spawn safety**: worker fn top-level importable; cloudpickle
    for the env factory (stock parity).

### Gates (run at build time — each behind a base-port 5006 ledger claim)

- Paired 24k throughput bench, N=6/M=1, ≥2 quiet replicates per arm, owned vs
  stock on the same tree+config (rider F must be merged first).
- ~300k curriculum canary per arm: first lesson advance at a comparable step
  (K1-4 crossed ~222k), loss/entropy overlay, mechanical invariants (updates
  per N steps, checkpoint/summary cadence, one JSONL per worker).
- Ghost-swap smoke: `ppo_ship_combat_selfplay_smoke.yaml` under the owned
  runtime via `run_parallel --self-play --smoke`.
- Resume drill (new): (a) hard-kill a smoke mid-run, `--resume`, assert
  lesson/ELO/step continuity against the last completed commit tail;
  (b) resume a COMPLETED smoke run — exercises the max-step republish →
  duplicate manifest line → last-wins reader path; (c) publish-atomicity
  unit tests simulate kills at every stage boundary of the saver sequence
  and the commit tail.

### Atomicity round — dispositions (run 2026-08-06 pre-freeze)

10 raw findings from the two consultants, deduped to 7. "Cl" = Claude
consultant, "Cx" = codex.

| # | Item | Disposition | Where |
|---|---|---|---|
| 1 | Saver-seam status hook can't see ELO or the just-published registry entry (Cl-1 ≡ Cx-2, observed) | ACCEPT — commit tail relocated into the owned loop, post-advance; ELO mirrored explicitly | Fork D |
| 2 | Resume pointer committed seconds before publish record + status (Cx-1 ≡ Cl-4, observed) | ACCEPT — pointer-last commit tail (manifest → status → `checkpoint.pt`); window seconds → µs | Fork C |
| 3 | Duplicate manifest step-lines on resume republish; watcher would yield twice (Cl-2 ≡ Cx-4, observed) | ACCEPT — reader dedupes by step, last-wins; yield-once; stale-verdict caveat documented | Fork C + tests |
| 4 | Torn manifest tail + append-only resume ⇒ malformed committed record (Cx-3, hypothetical, mechanism-verified) | ACCEPT — writer repairs torn tail on resume-open; mirrored to `summaries.jsonl` (same latent defect, file owned by this slice) | Fork C + assumption 9 |
| 5 | At-exit `save_state` left on stock's truncating write bricks `--resume` under teardown-window kills (Cl-3, observed) | ACCEPT — every status write through the one atomic helper | Fork D |
| 6 | Manifest lines outlive pruned artifacts; smokes run `keep_checkpoints: 2` (Cl-5 + Cx grounding, observed) | ACCEPT — manifest-mode watch yields only existing artifacts (skip + log) | Fork C consumers |
| 7 | `summaries.jsonl` step sawtooth across resume rollback; `startedAt` describes the current leg (Cx-5, observed) | DOCUMENT — inherent to rollback resume, identical shape in stock tfevents; the wire-contract monotonicity clause is about checkpoint steps, which holds; benches never resume | Note in `contract.py` docstring |

Residual accepted windows (crash model: process kill, not power loss):
kill inside the saver's artifact stage → partial `.tmp` orphans only, next
resume republishes the step; kill inside the commit tail → at worst manifest
has step N while status/pointer are N−1 (µs-scale across two renames), resume
falls back to the pointer with last-wins republish semantics; kill between
`add_checkpoint` pruning and the tail's status save → registry one prune
behind the directory until the next tail (benign — pruning re-walks from the
registry).

### Follow-ups spawned by this brief

- C# micro-PR: TrainingHost reads `--harness-worker-index`, cross-checks the
  port-arithmetic derivation, throws on mismatch (+EditMode test); completes
  V9 per the wire contract §3 amendment.
- Rider PR (fork F): bench stock-arm enablement — lands BEFORE the 1b build.

## Cadence contract (locked 2026-08-05)

The arc advances only in quiet-lane increments: docs + Python + unit tests
(no Unity assemblies; subprocess-free test lanes). Per-increment machine
footprint: a minutes-scale bench claim plus at most one ~2h canary block on
base-port 5006, evening-schedulable via ledger claims. The arc originates NO
dedicated full runs while a piggyback vehicle is plausible — the cutover
gate rides the next needed production retrain (§Equivalence gates); only if
none materializes by stage-2 readiness does the arc schedule its own pair.
Worktree slots are held only for the duration of a build; arc code lives in
`training/rl/`, so C#-side feature threads see no file-conflict surface.

## Non-goals

- Owning the gRPC protocol or forking the Unity ml-agents package — the C#
  communicator is upstream code; `mlagents_envs` is the healthiest layer in
  the stack and is retained.
- Forking `mlagents.trainers` — upstream is actively maintained (4.0.3,
  2026-04-17); a fork diverges from a moving target and buys nothing without
  also relaxing the version pins.
- Eval-threshold recalibration (bundle v2 at the rules change owns it); gate
  and player-eval lane (#252) run unchanged throughout — they consume
  checkpoints, not the runtime.
- Self-play league redesign; curriculum semantics changes. While inherited,
  ml-agents behavior is retained bug-for-bug (global reward-deque semantics
  documented in the YAML comments included).
- The MPC/nav-field block and the combat rules-change design — separate live
  threads; coordinate via the ledger.

## Process rules (carried from the paydown close-out)

- Every persistence/resume PR routes through an adversarial review with an
  atomicity checklist — the owned runtime is made of crash-window shapes.
- Python test shape: stdlib known-value tests + fakes at the process
  boundary; no Unity boot in unit lanes (#244/#240 precedent).
- No merge with an undispositioned review thread; a one-line below-bar reply
  counts, silence does not.

## Coordination

- **base-port 5006 is single-occupancy across sessions** — every paired
  bench/run in this arc claims the port via the ledger first.
- K1-3 (#250) is parked pending user playtest; the schema/composition surface
  (`ShipAgentFactory`, obs/action consts) is shared — second merger adapts.
- Throughput Pass 2 stages 0–2 are complete; this arc is the successor to its
  "custom trainer runtime" deferral. The board card repoints to this doc's
  memory topic file; the Pass-2 plan stays the throughput evidence record.

## References

Memory: `project_rl_training_throughput.md` (Stage 0/1/2 + deferral),
`project_rl_infra_paydown.md` (§PASS CLOSED seeds), `rl_training_run_mechanics.md`
(runbook), `active_work_ledger.md`. Docs: `RL_Training_Throughput_Optimization.md`
(living evidence), `RL_Infra_Paydown_Pass.md` §Pass close-out,
`Anchored_Intent_Architecture.md` §Infrastructure (plugin-seam ruling — still
correct for its question). PRs: #252 (player eval), #251 (player build fix),
#244 (statistical eval), #219 (hybrid league).
