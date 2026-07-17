# RL ML-Agents Agent — PR-3 Decision Brief & Implementation Plan

> STATUS: living — RL agent design reference (obs/action/reward contract); runbook in training/rl/README.md

**Date:** 2026-07-15 (scoped via pr-prep with the user; Codex adversarial review folded)
**Parent:** `Tactical_AI_Audit_And_Roadmap.md` §3′/§4′ PR-3. Builds on PR-1 #122
(velocity-reference interface), PR-2a #132 (maneuver-oracle gate), PR-2b #138
(episode/reward/reset layer, `RL_Episode_Reward_Layer.md`).
**Status:** Frozen brief — ready for implementation hand-off.

> **One-line intent.** Wire an ML-Agents PPO policy into the existing seams — sensor from
> `ObservationExtractor`, actions (velocity reference + fire gate + boost) into the
> PR-1 intent interface, reward/lifecycle riding PR-2b's `EpisodeRunner` — plus the
> training/eval process around it. Arc gate: beat the scripted baseline on held-out seeds;
> PR merge gate: infrastructure complete + demonstrable learning signal.

---

## Scope

**In:** ML-Agents `Agent` subclass + `AgentChooser` + sensor flattening in
`Game.RLHarness.Editor`; manual Academy stepping driven by `EpisodeRunner`'s decision
boundary; training scene + `TrainingHost`; composition hoist (`SpawnPair`/`ResetPair`
test code → harness); boost command seam (`NavigationIntent` field → Navigator OR into
drive command); `Brain.InstallChooser` internal seam; truncation change to
`PayDecision`/telescoping test + JSONL schema bump; Python training home `training/rl/`
(pinned env spec, PPO YAML, runbook); held-out-seed eval via `InferenceOnly` +
`DeterministicInference`; committed tiny ONNX fixture; locked-pacing contract for all RL
runs; deletion of the 2025 training residue.

**Out (non-goals):** shipping the learned policy in-game (guardrail: scripted AI stays
shipped until a learned agent beats it); self-play/league (PR-4); asteroid-field episodes
+ obstacle observation tokens (one deferred package — see Deferred); learned firing
discipline beyond the bare gate; standalone training build; attention/BufferSensor
encoders; `NavigationIntent` rename (deferred, board).

## Fork resolutions (with why)

1. **Clock ownership — runner-owned manual stepping.** `Academy.AutomaticSteppingEnabled
   = false`; at each `EpisodeRunner` K=10 boundary the integration does
   `AddReward(boundaryResult)` → `RequestDecision()` → `Academy.EnvironmentStep()`.
   *Why:* one clock, single-sourced from `RewardSpec.decisionIntervalSteps` — reward and
   decision boundaries coincide by construction, preserving PR-2b's tested ownership.
   The idiomatic `DecisionRequester` alternative runs a second, global K-counter whose
   phase drifts from the runner's per-episode counter after any episode whose length
   isn't a multiple of K.
2. **Assembly & hosting — extend `Game.RLHarness.Editor` + in-editor training scene.**
   Add the `Unity.ML-Agents` reference to the existing asmdef; a dedicated training
   scene hosts a `TrainingHost` MonoBehaviour that composes the pair and drives the
   loop; `mlagents-learn` attaches to editor play mode. *Why:* one home for all RL
   code; `Game.Core` never references ML-Agents; a new assembly pre-builds structure
   for post-gate shipping needs. Standalone build deferred until throughput bites
   (asmdef already includes `WindowsStandalone64`; its `UNITY_INCLUDE_TESTS` constraint
   must be revisited then — noted, no work now).
3. **Observation v1 — minimal fixed self+target vector, time-blind (~23 floats).**
   Flattened `SelfToken` + `TargetToken` + `hasTarget` + the two geometric envelope bits
   (`inMyEnvelope`/`inEnemyEnvelope`) + **ego-frame arena-center vector** (2 floats,
   scaled by `arenaRadius` — border shaping and the out-of-bounds loss are functions of
   arena-center distance; PR-2b invariant 2 requires reward-relevant state be
   observable) + **primary-weapon readiness** (1 float — lasers gate on heat, so fire
   outcomes are otherwise hidden-state). Distances/positions normalized by
   `RewardSpec.arenaRadius`, velocities by `MaxSpeed`. No threat/obstacle slots (always
   empty in the lasers-only empty-space scenario); no episode-progress scalar.
   *Why:* no dead channels; the token-list contract makes K-slot flattening additive
   later.
4. **Timeout is truncation, not termination.** Timeout → `Agent.EpisodeInterrupted()`
   (value bootstraps past the cutoff); kills/out-of-bounds → outcome `AddReward` then
   `EndEpisode()`. On truncation the shaping step does NOT force terminal Φ=0. *Why:*
   the 120 s draw rule is a harness artifact, not game semantics; a time-observing
   policy would learn to farm the draw clock and carry a train/deploy observation
   mismatch. JSONL schema bumps to `rl-episode-v2` with an `endKind` field
   (terminal/truncation) since truncation rows now carry non-zero final Φ; telescoping
   test gains the truncation case.
5. **Action space — 4 continuous, threshold-gated discrete semantics.** `[vx, vy, fire,
   boost]` ∈ [−1,1] (explicit clamp). Velocity pair = ego-frame reference scaled by
   `MaxSpeed`, converted to a world-plane vector **once at the decision boundary** (the
   cached WORLD vector is held for the interval; re-rotating per tick with live yaw
   would feed heading back into the reference). Fire = `a₃ > 0` → `intent.enableFiring`
   (`Gunsight.ShouldFire` still gates on top). Boost = `a₄ > 0`, **one-shot
   spend-now-if-ready at the boundary tick** (commanded while on cooldown = no-op;
   availability is observed, so the policy can learn timing — sharper credit than a
   held window that fires mid-interval on cooldown expiry). *Why not hybrid:* verified
   2026-07-15 against ml-agents source at package 4.0.3 — `ActionSpec` *constructs*
   mixed spaces but `CheckAllContinuousOrDiscrete()` still throws in the trainer
   communicator path (`GrpcExtensions.cs:178`) and inference path (`TensorApplier`);
   the earlier "hybrid works in 4.0" memory note was ctor-signature-only and is
   corrected. Continuous thresholds are the supported encoding; the capacity/chatter
   cost is accepted (Gunsight masks fire chatter; spend-if-ready masks boost chatter).
   *Why boost at all:* a 3 s-cooldown impulse is a beyond-horizon resource the 1.5 s
   MPC cannot value (today it samples boost stochastically at 0.15/step); reserve/spend
   is the learner's job; obs already carries availability + cooldown; the model already
   simulates it.
6. **Boost ownership — policy-owned in `VelocityReference` mode.** Boost sampling
   zeroed **via the same agent-local `MpcSettings` clone that carries `wVelTrack=50`,
   installed before navigator init** (the solver captures its settings reference at
   construction — mutating the shared asset would alter the baseline; swapping after
   construction would not take). Legacy/scripted modes untouched. Accepted: the
   ranger/oracle tracker loses opportunistic boost → re-run the PR-2b characterization
   to refresh the floor. Revisit when asteroid-field episodes land (MPC boost also
   serves collision escape there). The MPC rollout does not pre-plan the policy-forced
   impulse; the 50 Hz replan adapts next tick — accepted approximation.
7. **Training/eval process.** New `training/rl/` at repo root: env spec pinned to
   **Python 3.10.12 exactly** (release-pair constraint is `>=3.10.1,<=3.10.12`) with
   exact `mlagents`/`mlagents-envs`/Torch versions + a resolved lock captured at env
   setup; PPO trainer YAML with **pinned initial hyperparameters** (γ=0.99,
   `time_horizon` 64 decisions, GAE λ 0.95, lr 3e-4, network 2×256, batch/buffer
   1024/10240 — starting points, tunable with reporting) and **pinned
   `engine_settings`** chosen to satisfy the pacing contract (decision 8 note below);
   runbook. γ invariant: PR-2b's "trainer must read `RewardSpec.gamma`" is deliberately
   weakened to **equality pinned by a repo test** that parses the YAML and asserts
   `gamma == RewardSpec` default (0.99) — loud on drift, no generator machinery for one
   value. Env setup includes a **trainer-connected smoke**: a short `mlagents-learn`
   run asserting the 4-continuous action shape end-to-end, terminal-vs-interrupted
   flags, and the pacing assertion. Turning existing `RewardSpec` knobs (timeout, λ)
   against the stalemate is in-scope and reported; *adding* reward terms (per-step time
   cost) needs a check-in first. The 2025 residue (`results/CommanderCurriculum_v2/`,
   `scripts/scale_training.sh` + sibling training scripts) is deleted — it actively
   misleads.
8. **Merge gate vs arc gate.** The PR merges on: all tests green, Python-free
   `HeuristicOnly` integration test proving the Agent loop, eval harness pinned by the
   ONNX fixture, and a demonstrable training signal (improving reward curve + eval
   checkpoint run). The **arc gate** ("beat baseline on held-out seeds") gets a frozen
   protocol now: 20 pinned held-out seeds (disjoint from training seeds, constants in
   the harness), Wilson 95% lower bound on win-rate > 50% (draws count as non-wins;
   PR-2b floor: win-rate > 5% with fewer timeouts), checkpoint selected on
   training-seed eval BEFORE the held-out set is opened, and any `RewardSpec` knob
   change resets the protocol (held-out seeds are never a tuning set).
9. **Sequencing.** Branch from `main` now; do not stack on or wait for the user-held
   recorder PR #151. Known single-file overlap (`RLEpisodePlayModeTests.cs`);
   second-merger-adapts.

## Step & reset ordering contract (Codex P1-4/P1-5 — load-bearing, not implementer's choice)

- `TrainingHost` drives the loop from a coroutine on `WaitForFixedUpdate` — the SAME
  phase the PlayMode tests use — so the boundary runs after the physics step and after
  all `FixedUpdate`s (commander at −40 included).
- Boundary sequence at end of step t: capture snapshot → `runner.Tick()` pays reward →
  `AddReward` → `RequestDecision()` → `Academy.EnvironmentStep()` (CollectObservations
  reads the boundary snapshot; OnActionReceived caches the new action synchronously) →
  the action takes effect from step t+1 through t+K. **Zero decision latency** — observe
  s_t, act on [t, t+K): standard MDP semantics, no previous-action observation needed.
- Episode start: after `ResetPair` + `ProjectileFlush` + `runner.Begin()`, the first
  decision is **primed immediately** (RequestDecision + EnvironmentStep before the first
  simulated step) so no steps run under a default action.
- Terminal sequence: outcome `AddReward` → `EndEpisode()`/`EpisodeInterrupted()`
  **before** pair reset (the terminal observation must reflect the end state, and both
  calls send it synchronously) → then reset → `Begin()` → prime. `OnEpisodeBegin` stays
  a no-op (assert-only); the host is the single reset owner.
- `EpisodeRunner` exposes one explicit **boundary result** per paid decision: dense,
  shaping (envelope/border), outcome reward, and end kind (none/terminal/truncation) —
  the `Finish`-computed outcome must ride the terminal boundary result, not a separate
  channel.

## Pacing contract (Codex P1-6)

All RL runs (training, eval, characterization) share one sim semantics: **every rendered
frame advances exactly one fixed step**, so Update-driven systems (Scout scanning, shield
regen) tick once per fixed step at any speed. The trainer's engine-config side channel
defaults (`time_scale: 20`, `capture_frame_rate: 60`) VIOLATE this at our 50 Hz fixed
step — the YAML pins explicit `engine_settings` (values fixed empirically at env setup
to satisfy the contract), and `TrainingHost` carries a **runtime assertion** that fails
loud if the resolved `Time` settings break frame≙fixed-step. Cost: training wall-clock
is frame-rate-bound (~5–10× real time in-editor, vsync off) instead of timescale-bound.

## Assumptions (user-reviewed; Codex-corrected where noted)

- `AgentChooser : IIntentChooser` (not `IStateChooser`), plain class, installed before
  `AICommander.Initialize` so `Brain.BuildStates` skips state-profile init
  (RangerChooser/ManeuverChooser precedent).
- Reflection install promoted to `internal Brain.InstallChooser(...)` +
  `InternalsVisibleTo(Game.RLHarness.Editor)`; both existing reflection sites migrate.
- **The harness injects the opponent reference** (`AgentChooser.Configure(opponent)` —
  the Ranger precedent): `Scout.nearbyShipRadius` is 30 m but spawns run 25–60 m, so
  `EnemyTracker` acquisition would leave the agent target-blind (and the observation
  empty) beyond 30 m. The target token and `intent.target` build from the injected
  ship's live kinematics; the PR-2b floor was measured with the same injected-target
  privilege, so the comparison stands. The baseline keeps production sensing.
- Intent rebuilt every `Brain.Decide` (50 Hz): cached **world-plane** velocity vector +
  cached fire gate + fresh aim/target snapshot from the injected ship.
- `wVelTrack = 50` clone-override on the agent's `MpcSettings` (PR-2b precedent; carries
  the boost-prob zeroing too, decision 6).
- Scenario unchanged from PR-2b characterization: Ship2 pair, agent on `TestPilotMPC`
  host commander, baseline `UtilityPilot.prefab`, lasers-only via `Reequip`, empty
  space, `EpisodePoses.Derive`, `RespawnShip` pair-reset + `ProjectileFlush`.
- `Agent.MaxStep = 0`; `EpisodeRules` stays the only termination owner.
- Existing `SeedScope` streams (101/202); no new Unity-side RNG stream. Eval sets
  `BehaviorParameters.DeterministicInference = true` (it defaults FALSE — InferenceOnly
  alone is stochastic) + pinned inference seed/device, pinned by a test.
- Fresh `BehaviorName` (not legacy `RLPilot`); `Default` type for training,
  `InferenceOnly` for eval.
- Sensor reads envelope bits from the same `CombatSnapshot` the runner captured at the
  boundary (one extraction moment, one read-only LOS cache); `Gunsight.Evaluate` is
  forbidden from observation code (observer effect — PR-2b Codex catch).
- `Agent.Heuristic()` inverse-maps Ranger-style logic into raw actions — what makes the
  Python-free integration test drive the full loop. (The trainer-connected paths it
  cannot cover are exercised by the env-setup trainer smoke, decision 7.)
- Tests: EditMode units (obs flattening, action mapping incl. thresholds, YAML-γ) +
  PlayMode `HeuristicOnly` full-episode integration; `-ScopeType Auto` iteration;
  whole-file comment ratchet on touched files.

## Blindsider resolutions (pre-Codex)

- Locked pacing for all RL runs — superseded by the fuller Pacing contract above.
- Committed ONNX fixture: one tiny throwaway PPO checkpoint (~1–2 MB, LFS, under
  `Assets/`) pins the `InferenceOnly` eval path in tests; real checkpoints live
  untracked under `results/rl-training/`.
- `PayDecision` distinguishes true-terminal (force Φ=0) from truncation (keep Φ) —
  folded into decision 4's schema bump.

## Deferred (board-carded where noted)

- **Asteroid-field episodes + nearest-K obstacle observation tokens** — one package,
  the next environment step, before/with PR-4 (self-play hours in empty space compound
  the transfer gap; the real game fights in fields). Obs and env change together
  (PR-2b invariant-2 discipline).
- **`NavigationIntent` rename** (board) — with `enableFiring` + boost aboard it is the
  full act-intent, not just navigation; rename when the dust settles.
- Threat-token channel (missiles) — with the weapon-variety PR that makes it non-empty.
- Per-step time cost / per-shot cost reward terms — only if training misbehaves, with
  check-in.
- Standalone headless training build (+ the asmdef `UNITY_INCLUDE_TESTS` constraint
  revisit) — iff in-editor throughput bites.
- Shipping inference / in-game learned chooser — post-arc-gate, own design.
- Hybrid action space — re-check if a future ml-agents release removes the
  communicator/inference `CheckAllContinuousOrDiscrete` rejection.

---

# Full training run — frozen decision brief (2026-07-15, pr-prep)

The arc-gate run itself: long PPO training → checkpoint selection on training
seeds → one sealed held-out eval → findings. Mostly operational; the code diff
is small and enabling. Scoped with the user; ready for implementation hand-off.

## Scope

**In:** `heatPct` observation (self heat only, via `IHeatReadout.HeatPct`; obs
23→24, readiness bit stays) + re-minted smoke ONNX fixture (`run_smoke.py`) +
obs-size/flattening test updates; training-seed eval entry point (parameterize
seeds + checkpoint path on `CheckpointEvaluator`/`TrainingBootstrap` —
`RunHeldOutEval` hardcodes the held-out list today); `keep_checkpoints` bump so
selection covers the whole run; asserting `run_training.py` (armed batch editor
+ start-flag, long timeouts, `--resume`-aware, kills the editor when the
trainer exits); pilot run ≈200k steps (wall-clock forecast + learning-signal
check) → full 2M run; checkpoint selection on training-seed eval BEFORE the
held-out set opens; one held-out eval per candidate protocol; findings +
runbook updates; best checkpoint committed via LFS.

**Out (check-in first):** env changes (heat relax/removal, shield-regen tuning)
— the heat-free ablation was considered and REJECTED for this PR; new reward
terms (per-step time cost); multi-pair/throughput harness work (only if the
pilot forecasts >24 h for 2M); PR-4 self-play; shipping inference.

## Fork resolutions (with why)

1. **Staging — pilot then full run, batch-mode, pooled slot.** ~200k-step pilot
   measures real steps/sec (training is frame-rate-bound under the pacing
   contract; 2M decisions = 20M fixed steps) and confirms learning signal at
   real arena scale (the smoke's tiny-arena config proved plumbing, not
   learning). Batch editor via the armed bootstrap; runs execute from a pooled
   slot so the primary project's Unity lane stays free.
2. **Heat — mechanic stays; visibility fixed instead.** The policy cannot
   overheat (`Lasers.ShouldFire` gates on `WouldOverheatOnNextShot`), so what it
   lacks is heat *visibility*: the readiness bit (`CanFire` = cooldown +
   `!Overheated`) is nearly constant-1 and the 0–100 gauge is hidden —
   under-delivering decision 3's stated intent for that channel. Fix: append
   continuous self `heatPct`. Asset math (damage 20/shot, 4-shot cold burst,
   sustained ~0.75 shots/s ≈ 15 DPS-if-hitting vs shield regen 20/s **with a
   5 s damage-interrupt delay**) says hit *rate*, not heat, is the binding
   constraint — one landed hit per 5 s freezes regen; ~13 s of pressure kills
   inside the 120 s clock. Heat-free ablation (cooldown-limited 5 shots/s =
   100 DPS) rejected by the user for this PR: symmetric env change, weakens the
   arc-gate claim, resets the protocol.
3. **Knob policy — existing knobs only, reported.** RewardSpec fields (λ,
   timeoutDecisions, separation) + PPO YAML hypers are in-scope, each turn
   reported with before/after evidence; any RewardSpec change re-measures the
   characterization floor and resets the protocol (held-out stays sealed). New
   reward terms and env changes check in first.
4. **Merge semantics — PR merges on findings, gate or not.** The arc gate stays
   the ARC's gate; a failed Wilson gate yields a follow-up run, not a stalled
   branch. Gate math for honesty: 20 seeds × 5 eps = 100 episodes; Wilson 95%
   LB > 50% needs ~60/100 wins (scripted-ranger floor: 4W/0L/16D).
5. **Best checkpoint committed via LFS** (like the smoke fixture): the eval
   result stays reproducible from the repo and PR-4 gets a frozen opponent.

## Assumptions (user-reviewed)

- Pooled slot agent-1/agent-2 (agent-5 reserved for the recorder handoff),
  ledger row per protocol; unity-access coordination for every editor boot.
- Pilot wall-clock forecast >24 h for 2M ⇒ check in before throughput work.
- Training runSeed stays 1 (`EvalProtocol.TrainingRunSeed`); episode variety
  via per-episode pose derivation. Run-id hygiene: distinct `--run-id` per run.
- Monitoring: TensorBoard + episode JSONL; curve reported at checkpoint
  intervals. Torch stays CPU (2×256 MLP; the env is the bottleneck).
- JSONL schema unchanged (rows embed RewardSpec, not observations).
- Characterization floor is scripted-vs-scripted → unaffected by the obs
  change; only RewardSpec/env changes re-measure it.
- Operational: long runs peg CPU for hours — start times coordinated with the
  user; concurrent merge-gate suites on the same machine risk flaking
  timing-sensitive PlayMode tests (#129 caveat).

## Pilot findings & arc outcome (2026-07-15, run id `ship_combat_pilot`)

**Result: the learned policy decisively beats the AttackAggressive baseline in
the prototype arena; the user accepted the pilot as sufficient and closed the
arc without the full 2M run or the formal held-out gate.**

- **Learning signal:** mean reward −0.205 → +1.93 over 200k decisions,
  monotone, σ 1.37 → 0.11; plateau ≈1.9 from ~step 120k. Throughput 56.8
  decisions/s under the pacing contract (2M forecast ≈9.8 h, never spent).
- **Outcomes:** 2035 W / 159 L / **0 draws** across 2,194 training episodes —
  every episode ended in a kill, vs the scripted ranger floor of 4W/0L/16D.
  The pre-registered stalemate levers (heat/regen/timeout) were never needed.
- **2M run:** launched then ABORTED at user instruction before any training
  step ran (curve had converged by 120k; prototype arena doesn't justify 10 h
  of polish). Config + `--resume` remain if ever wanted.
- **Formal gate: waived for the prototype** (user decision). Basis instead:
  pilot curve + outcome mix, a recorded InferenceOnly episode, and a live
  in-editor eval session the user watched. If a defensible beat-the-baseline
  claim is ever needed, run the rigor ladder on FRESH seeds (this decision
  deliberately spends none of 1001–1020, which remain sealed).
- **Deterministic-inference check (live eval, 2026-07-16):** `InferenceOnly` +
  `DeterministicInference` vs the baseline on seed 7: **7/7 wins across two
  in-editor eval runs (5/5 clean-run artifact:
  `results/rl-eval/20260715-234947-custom-summary.json`, winRate 1.0, Wilson
  LB95 0.566; plus 2/2 in a partial run)** — all terminal kills, agent hull
  untouched, kill times 39–101 s. The earlier seed-1 recorded draw reads as an
  outlier, not a systematic stochastic→argmax gap.
- **Committed artifact:** final checkpoint `ShipCombat-199974.onnx` lands via
  LFS as `Assets/Tests/Fixtures/ShipCombat-pilot.onnx` (frozen opponent seed
  for PR-4 self-play; reproducible eval). Raw run artifacts stay untracked
  under `results/rl-training/ship_combat_pilot/`.
- **Next (per pivot):** asteroid-field episodes + obstacle observation tokens
  (the environment step, board-carded), then PR-4 self-play — training hours
  buy something there, not here.
