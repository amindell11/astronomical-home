# RL Trainer Runtime Takeover

> STATUS: live arc — opened 2026-08-05; slice 0(b) player-build tripwire landed
> (`PlayerBuildTripwireEditModeTests`); next = slice 1 wire-contract freeze;
> stages 3+ are entry-gated, not committed.

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
registers an ADDITIVE stats-writer plugin (`[mlagents.stats_writer]` — a
supported ml-agents seam; tfevents keep flowing, `plot_progress.py`
untouched) emitting `summaries.jsonl`, refuses `--resume` +
`--initialize-from` loudly, then delegates the entire loop to stock
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
- **Slice 3 — stage 1b scheduler build** (pr-prep first; adversarial
  atomicity round mandatory — checkpoint/manifest/resume writes are its
  whole surface).
- **Slice 4 — stage 2 build** (pr-prep first).
- Later slices open only through their entry gates (§The ladder).

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
