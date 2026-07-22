# RL Stepping Migration — retire runner-owned `EnvironmentStep`

> STATUS: live arc — stepping-migration record (drafted 2026-07-21). Design agreed at the arc level;
> PR-0 is a gating spike whose result reshapes PR-1/PR-2. Per-PR briefs get pr-prep'd
> when reached. Decoupled from — and orthogonal to — the headless-player throughput PR.

**Date:** 2026-07-21.
**Parent:** `Multi_Arena_Substrate.md` §"N-arena stepping model" (the already-blessed target:
Academy is the clock, per-arena driver collapses to reset-only); `RL_Training_Throughput.md`
§"Path A" (names the manual global step as Path A's whole cost).
**Driver:** the manual `Academy.EnvironmentStep()` is process-global, so it can have exactly
one owner — which blocks in-process M-arenas (Path A) and forces the "two steppers collide"
workarounds. Migration is architectural-correctness work the user wants regardless of
throughput, and it is the on-ramp Path A / teams (`SimpleMultiAgentGroup`/CTDE) require.
Driving memory: `project_multi_arena_rethink`, `project_rl_training_throughput`,
`project_tactical_ai_direction`.

> **One-line intent.** Stop disabling ML-Agents' automatic clock and calling
> `EnvironmentStep()` by hand; keep the boundary-driven `RequestDecision()` cadence; let the
> Academy auto-step every FixedUpdate and batch every arena's agents into one inference call —
> which is exactly what in-process multi-arena and teams need.

## The problem

Two places turn off automatic stepping and drive the Academy by hand:

1. **`EpisodeLoopDriver`** (training + eval harness) — `Academy.Instance.AutomaticSteppingEnabled
   = false` (`EpisodeLoopDriver.cs:29`); `RequestDecision()` + `EnvironmentStep()` to prime the
   first decision (`:49-50`) and at each paid boundary (`:72-73`), on a `WaitForFixedUpdate`
   coroutine.
2. **`InferenceChooser`** (the *shipped in-game* trained pilot) — same disable
   (`InferenceChooser.cs:119`); `RequestDecision()` + `EnvironmentStep()` on the AI decision
   tick (`:61-64`), every `ShipCombatPolicy.DecisionIntervalSteps` (=10) fixed steps.

`EnvironmentStep()` is **process-global**: one call steps every agent in the process and blocks
on one batched inference round-trip. So whoever calls it owns the whole-process clock, and there
can be only one owner. Direct consequences:

- **`RL_Env_Step_Package.md` PR-C fork 2** already records that a training agent + an
  `InferenceChooser` (or two `InferenceChooser`s) both stepping "break the pacing contract."
  Today that is documented-around, not fixed.
- **In-process M-arenas is impossible** with per-boundary manual stepping: M arenas each want
  their own decision cadence, but there is one global step. `RL_Training_Throughput.md` names
  this as Path A's entire cost; `Multi_Arena_Substrate.md:257` calls the manual loop "the
  *interim* stepper… before the Academy exists." The Academy exists now — the interim window
  closed, and the scaffolding quietly became load-bearing in two places.

Not a mistake for single-arena bring-up (it bought provable reward/decision/reset ordering
during the phase where that was the whole game, and produced the pilot + 2M curriculum run) —
but genuinely against the framework grain, and the wrong shape for the multi-arena/teams goal.

## Core technical insight — the two concerns are separable

The manual stepper conflates two independent things:

- **`RequestDecision()`** — *which* fixed steps an agent decides on (the every-K-steps boundary
  cadence, owned by `EpisodeRunner.Tick` via `decisionIntervalSteps`).
- **`EnvironmentStep()`** — *when the whole process ticks* (once per FixedUpdate under the
  Academy's automatic stepper).

The migration keeps the former and deletes the latter: leave `AutomaticSteppingEnabled = true`,
keep the boundary-driven `RequestDecision()`, let the Academy auto-step on FixedUpdate. On
non-boundary ticks the agent simply doesn't request → no inference for it → the `AgentChooser`
holds its last action (today's between-boundary behavior, unchanged). Under auto-stepping, every
agent across every arena that requested a decision on a given FixedUpdate batches into one
inference call — that batching is the multi-arena throughput win.

Favorable ground truth for both consumers:

- Both are already **FixedUpdate-driven**. The in-game pilot ticks on `AICommander.FixedUpdate →
  Brain.Decide(context, dt)` (`AICommander.cs:96-104`, `Time.fixedDeltaTime`), the same phase the
  ML-Agents auto-stepper runs on. So auto-step and the chooser coexist on one clock; only the
  request-vs-step *ordering within* that clock needs pinning.
- `ShipAgent` is already reset-externally: `MaxStep = 0`, `OnEpisodeBegin` a deliberate no-op
  (`ShipAgent.cs:97`), reset owned by the hosting loop. Nothing in the agent assumes it owns the
  clock — the ownership lives entirely in the two steppers above.
- No `DecisionRequester` exists anywhere today; the boundary cadence is hand-rolled. `DecisionRequester`
  is the idiomatic auto-step cadence primitive and is an option for PR-2 (see forks).

## The one open question (what PR-0 exists to answer)

Today's loop guarantees **zero decision latency**: the observation captured at boundary step *t*
immediately produces (via the synchronous `EnvironmentStep`) the action that governs physics over
[*t*, *t+K*), and on episode end `EndEpisode` runs *before* the pair-reset so the terminal
observation reflects the end state. Under automatic stepping, the Academy's FixedUpdate stepper
and Unity's physics integration are ordered by **script execution order** (`Brain` is
`[DefaultExecutionOrder(-70)]`; the ML-Agents stepper has its own order). Whether an action
requested at a boundary lands *before* the physics tick it is meant to govern — i.e. whether the
zero-latency contract survives — is the thing the spike measures. Everything downstream is shaped
by the answer: a clean survival makes PR-1/PR-2 near-deletions; a one-tick shift means either
accepting bounded latency (fine in-game, must be re-pinned in the determinism tests) or a
`DecisionRequester` + execution-order approach.

## PR slicing

### PR-0 — ordering spike (the gate; no product change)

**In:** in one PlayMode harness path, flip to `AutomaticSteppingEnabled = true` + boundary-only
`RequestDecision()` with no manual `EnvironmentStep()`, and measure against the existing
invariants: (a) zero-latency — the boundary action governs the following physics interval, not a
tick later; (b) terminal-observation-before-reset holds; (c) the JSONL trajectory-equivalence /
determinism pins still pass, or a bounded, characterized shift is identified. Deliverable is a
written finding + the concrete recommended shape for PR-1 (pure deletion vs. `DecisionRequester`
vs. execution-order pinning), not merged product code.

**Out:** touching `InferenceChooser`; multi-arena wiring; deleting the manual path in `main`
(the spike is measured on a branch/throwaway path). No training run.

**Gate:** the finding decides PR-1's mechanism. Do not scope PR-1 until this lands.

### PR-1 — migrate `EpisodeLoopDriver` to auto-step

**In:** drop `AutomaticSteppingEnabled = false` and both manual `EnvironmentStep()` calls; keep
the priming + boundary `RequestDecision()`; whatever ordering primitive PR-0 blessed; re-green the
determinism + JSONL-equivalence suites. `EpisodeRunner` (host-agnostic boundary clock) is
untouched — it never stepped.

**Out:** `InferenceChooser` (PR-2); any arena-count change (Path A is a later, separate arc that
this unblocks).

**Note — PR-4 self-play composes cleanly:** PR-4's "`RequestDecision` on all agents, then one
`EnvironmentStep`" (N agents, *one* arena) becomes "`RequestDecision` on all agents; the Academy
auto-steps" — strictly simpler, still one arena. This migration does not conflict with in-flight
PR-4; it removes the manual-step half of PR-4's generalization.

### PR-2 — migrate `InferenceChooser` (the shipped in-game pilot)

**In:** the in-game trained pilot stops owning the Academy clock — `RequestDecision()` on its
K-cadence (or a `DecisionRequester` on `LivePilotAgent`), read the action when it arrives, no
manual step. This is the change that **structurally kills the "two steppers collide" wart**: N
in-game inference pilots (or a training agent + a pilot) then all just `RequestDecision()` and the
Academy batches them, instead of fighting over `AutomaticSteppingEnabled`.

**Out:** obs/policy changes; re-minting checkpoints (stepping is orthogonal to the model).

**Fork to resolve at pr-prep:** manual `RequestDecision` on the existing tick (smallest diff,
mirrors today's `ticksUntilDecision`) vs. a `DecisionRequester` component (idiomatic, moves the
cadence out of the chooser). PR-0's latency finding informs whether the in-game pilot can accept
a one-tick action delay (almost certainly yes — it is not determinism-pinned like training).

### After the arc — Path A stepping model (separate, now unblocked)

With no manual global step, `Multi_Arena_Substrate.md`'s N-arena model is reachable as written:
per-arena **reset-only `RLDriver`**, the Academy as sole clock batching all arenas' agents. That
is the throughput/teams payoff and its own arc (needs per-arena seeding + the reset-driver seam);
this migration is its precondition, not its whole.

## Risks / watch-items

- **The ordering contract is the whole risk** — concentrated in PR-0 by design. If auto-step
  can't reproduce zero-latency and a bounded shift is unacceptable to the determinism pins, the
  fallback is execution-order pinning (order the ML-Agents stepper vs. `Brain`/physics
  explicitly), which is knowable but is the expensive branch.
- **Determinism-test churn:** the JSONL trajectory-equivalence fixtures may shift by the
  characterized amount; re-mint deliberately (the PR-B "re-mint BEFORE PlayMode" lesson applies).
- **In-game regression surface:** PR-2 touches shipped AI. Guard with the existing
  `InferencePilotPlayModeTests` decision-cadence assertion (`~1 decision / DecisionIntervalSteps`)
  plus a live-fire smoke.

## Interaction with in-flight work

- **Headless-player throughput PR (Path B):** orthogonal. Path B was chosen *specifically to
  avoid* this refactor (each player runs the unchanged single-arena scene); this arc neither
  accelerates nor blocks it. They compose later (in-process-M-arena players under `--num-envs`).
- **PR-4 self-play (unpushed, agent-1):** compatible — see PR-1 note. If PR-4 lands first, PR-1
  rebases onto its N-agent request loop and deletes the manual step; if this lands first, PR-4
  inherits the auto-step loop.
- **Unity contention:** PR-0/1/2 need PlayMode runs + unity-access coordination; sequence behind
  the live editor on agent-1, never race it.

## Hand-off

Start at PR-0 — it is cheap, non-contended-in-scope-once-scheduled, and gates everything. When its
finding lands, pr-prep PR-1 against that recommendation, build via the agent-worktree-pr-loop from
`main`. PR-2 follows PR-1. Path A stepping model is a later arc seeded by this one.

## Related

- `doc/Feature_Plans/Multi_Arena_Substrate.md` (the target N-arena stepping model)
- `doc/Feature_Plans/RL_Training_Throughput.md` (Path A vs Path B; why Path B sidesteps this)
- `doc/Feature_Plans/RL_Env_Step_Package.md` (PR-C fork 2 two-steppers note; PR-4 self-play stepping)
- memory `project_multi_arena_rethink`, `project_rl_training_throughput`, `project_tactical_ai_direction`

---

# PR-0 — Decision brief (frozen 2026-07-21, pr-prep)

> A **gating spike**, not a product change. Deliverable = a written finding appended here that
> shapes PR-1; no code merges. Branches from current `main` (carries #185 `HarnessAssets`).

## The question, mechanically reduced

Does automatic Academy stepping preserve the two invariants the manual stepper guarantees —
**(1) zero decision latency** (the action decided at boundary step *t* governs physics over
[*t*, *t+K*)) and **(2) terminal-observation-before-reset** — and if not, is the gap recoverable?
The mechanism reduces to **one lever**: the execution order of ML-Agents'
`AcademyFixedUpdateStepper` (calls `EnvironmentStep` in its own `FixedUpdate` under auto-stepping)
relative to `Brain`/`AICommander` (`[DefaultExecutionOrder(-70)]`, reads the agent action and
applies velocity that same `FixedUpdate`). Stepper before `Brain` → fresh action lands same tick
(zero-latency preserved); stepper after → `Brain` reads stale action → one-tick latency.

## Scope

**In:** a throwaway PlayMode measurement on a branch. An A/B trajectory diff: the deterministic
Heuristic agent driven through the episode loop under (arm 1) today's manual stepping and (arm 2)
automatic stepping, same seed/spec, per-fixed-step 10-channel kinematics recorded and diffed at
1e-3 — the measured window **spans at least one episode boundary** so both invariants are
exercised. If the default auto-step arm diverges, a **conditional execution-order probe**: pin the
stepper before `Brain` (or move `Brain`'s order), re-run the same diff, and report whether the
shift is recoverable. Finding written here: default behavior + (if divergent) recoverable-with-a-
one-line-order-pin vs. structural (order control insufficient → PR-1 is the `DecisionRequester`/
bounded-latency branch).

**Out (non-goals):** any merged product change to `EpisodeLoopDriver`/`InferenceChooser` (measured
on a throwaway variant/branch only); the in-game `InferenceChooser` path (PR-2 — its 3–5-per-40
cadence bar tolerates one-tick latency; it is real-time AI, not determinism-pinned); the actual
migration (PR-1); the remote-gRPC path (reasoned-equivalent, see assumptions — not re-run);
`DecisionRequester` adoption (a PR-1 option this spike may *recommend*, not build).

## Fork resolutions (with why)

1. **Measurement instrument = A/B trajectory diff (primary), direct action-timing probe
   (attribution on divergence); lean-on-existing-tests REJECTED.** *Why:* no existing test can
   catch the target regression — `RLAgentPlayModeTests`' reward-equivalence + decision-count asserts
   still pass under a one-tick shift (reward still telescopes, decisions still count), and
   `RLEpisodePlayModeTests.TrajectoryEquivalence_*` never drives the agent/Academy (it tests *reset*
   determinism with a scripted RangerChooser). So "flip and re-run the suite" goes green through the
   exact defect — a trap to name in PR-1's context. The A/B diff is the honest behavioral-equivalence
   proof and directly renders a latency shift as position drift; the timing probe (record the
   fixed-step where a boundary's action first moves the rigidbody; assert *t→t+1* both modes)
   disambiguates a real one-tick shift from a spurious Burst-ordering artifact if the diff diverges.
2. **Spike breadth = measure + de-risk (conditional execution-order probe).** *Why:* the point of a
   gating spike is to hand PR-1 a *closed* question. "There's a one-tick shift" without "…and it's a
   one-line order pin vs. structural" returns PR-1 to exploration — defeating the gate. The probe is
   bounded and throwaway (toggle one order value, re-run the Fork-1 instrument) and *conditional*
   (never fires if the default is already zero-latency), so it costs nothing in the clean case and
   buys the decisive finding in the divergent one.

## Assumptions (user-reviewed)

- **Vehicle = Heuristic policy** through the episode loop — deterministic, Python-free
  (`ShipAgent.Heuristic` is pure `RangerChooser.HoldRangeVelocity`), no gRPC nondeterminism. The
  ordering question is policy-source-independent, so Heuristic faithfully proxies the trained path.
- **Reuse the determinism harness:** `PacingContract` locked pacing (`timeScale=1`,
  `captureDeltaTime=fixedDeltaTime`) + the `Record`/`Sample` 10-channel capture at 1e-3, as
  `RLEpisodePlayModeTests.TrajectoryEquivalence_*` already does.
- **Controlled pre-combat window** (separation ~18–24) so float chaos (combat + CEM near-ties)
  doesn't swamp a one-tick signal — plus a short `timeoutDecisions` so a terminal/truncation lands
  inside the recorded window (boundary-spanning, per the blindsider).
- **Auto-step arm runs on a throwaway driver variant**, never a `manualStep` flag threaded through
  the shipped `EpisodeLoopDriver` — nothing merges from this spike.
- **Remote-gRPC path is reasoned-equivalent, not re-run:** under a remote policy `EnvironmentStep`
  blocks on the round-trip within the stepper's `FixedUpdate`, before physics — sim-step ordering is
  identical; the pacing contract makes the block span wall-clock, not sim time.
- **PlayMode, headless, `-ScopeType Auto`, worktree loop; branch from current `main`.** Fixture
  restores `AutomaticSteppingEnabled = true` in TearDown (`RLAgentPlayModeTests:69-70` pattern).
- **Coordinate Unity via unity-access; do not race the agent-1 editor** (PR-4 #184 in review there).
- Findings appended to this doc under PR-0; no board card (the spike closes within PR-1's prep).

## Blindsider resolutions

- **Measured window spans a reset boundary** (folded in): one short-timeout window that crosses an
  episode boundary and keeps recording into the next episode's opening steps, so the single diff
  exercises *both* mid-episode zero-latency and terminal-obs-before-reset (the manual path's
  `EndEpisode`-before-`pair.Reset` ordering). Not two separate windows.
- **Instrument-validity guards (baked in regardless):** (a) the auto arm must **assert it actually
  auto-stepped** (academy step count / `DecisionsReceived` advances) before its trajectory is
  trusted — a mid-session `AutomaticSteppingEnabled` flip that silently fails to engage the stepper
  would otherwise yield a garbage-but-green diff; (b) if the two arms can't share one process cleanly
  (stepper re-creation on the mid-session flag flip), fall back to two separate `-runTests`
  invocations rather than back-to-back arms in one method.

---

# PR-0 — Finding (2026-07-21)

> **Verdict: RECOVERABLE.** Default automatic stepping breaks the zero-latency contract by exactly
> one tick; pinning the Academy step *before* `Brain` restores it. Not clean, not structural.
> → PR-1 takes fork **P1-2 "recoverable"**: pure deletion of manual stepping **+ a one-line
> execution-order pin** (order our `AICommander`/`Brain` after the ML-Agents stepper). Do **not**
> escalate to the `DecisionRequester`/bounded-latency branch — order control demonstrably restores
> the invariant.
>
> **Provenance note.** The spike harness + CSVs were produced by the agent-2 spike agent
> (16:20–16:25); that agent's session ended before it wrote this section. This finding is
> reconstructed from its raw artifacts (`agent-2/results/pr0-stepping/{Manual,AutoDefault,EarlyStep}.csv`,
> uncommitted throwaway) by the orchestrating session, matching the independent CSV recon the handoff
> anticipated. Evidence is quoted below so the verdict is auditable, not asserted.

## What was run

Three arms of the same deterministic Heuristic episode loop (`PacingContract` pacing, seed = episode
index, pre-combat separation 18–24, decision interval K = 10), each recording 10-channel kinematics
per fixed step over **2 episodes × 100 steps = 200 rows**, boundaries aligned every 10 steps in all
arms. Arms:

- **Manual** — today's runner-owned synchronous stepping (the reference).
- **AutoDefault** — `AutomaticSteppingEnabled = true`, boundary-only `RequestDecision()`, no manual step.
- **EarlyStep** — recovery probe: `EnvironmentStep()` every FixedUpdate at execution order −75 (ahead
  of `Brain`@−70), emulating "stepper before Brain."

Diff at 1e-3 against Manual, with per-cell exact-match and one-tick-lag analysis.

## Evidence

**AutoDefault breaks zero-latency (one-tick lag).** First bit mismatch at **ep0 step 1**
(`ax` 6.6769857 → 6.677342; `avx` −0.019637 → **0**). The boundary action decided at step *t* does
not move the rigidbody until *t+1*: `AutoDefault[t] ≈ Manual[t-1]` holds cleanly for the first
several steps (e.g. Manual step1 `avx` = −0.019637 == AutoDefault step2; step2 −0.072432 ==
AutoDefault step3; …) before chaotic bloom (max abs diff 357 in `yaw` by ep1). This is the exact
`stepper-after-Brain → Brain reads stale action` failure the brief predicted.

**EarlyStep restores zero-latency.** **Bit-identical to Manual (diff = 0.0) through ep0 step 15**,
*including across the step-9 decision boundary* — the fresh boundary action lands the same tick, as
the manual path guarantees. A small residual seeds at **ep0 step 16** (max abs diff 1.6e-2) and grows
chaotically thereafter (max 6.9 in `yaw`, ep1). The residual begins **mid-decision-interval** (not at
a boundary or reset), so it is a second-order effect, not a latency shift.

**Instrument validity.** The isolated `Arm_AutoDefault` run **passed** both guards — the Academy
actually auto-stepped (`academyStepDelta` > rows/2) and `DecisionsReceived == decisions paid == 20` —
so the one-tick lag is a true behavioral result, not a stepper-never-engaged artifact. The CSV on
disk is from that isolated pass.

**Process caveat (blindsider confirmed).** The back-to-back 3-arm run **failed** `Arm_AutoDefault`
("delivered a different decision count than the runner paid — Expected 20 But was 10"): the
mid-process `AutomaticSteppingEnabled` flip did not cleanly re-engage the stepper after the prior
arms. The blindsider's fallback is mandatory — **run each arm in its own `-runTests` invocation**;
the trustworthy AutoDefault CSV was produced that way.

## Recommendation for PR-1

1. **Mechanism = fork P1-2 "recoverable":** delete the ctor `AutomaticSteppingEnabled = false` and
   both `EnvironmentStep()` calls in `EpisodeLoopDriver`; keep both `RequestDecision()` calls; add a
   **one-line execution-order pin ordering our `AICommander`/`Brain` after the ML-Agents
   `AcademyFixedUpdateStepper`** (the stepper is ML-Agents-owned — move *our* order, don't touch
   theirs; the probe demonstrated the contract holds when the step precedes `Brain`).
2. **Permanent guard = integer step-index timing-invariant property test** (boundary action first
   moves the rigidbody at t+1; terminal observation precedes reset) — **not** a committed float
   golden, per the frozen PR-1 fork P1-1.
3. **Close/characterize the residual before re-greening.** The EarlyStep probe is a crude emulation
   (unconditional every-FixedUpdate step); prime suspect for the step-16 drift is auto-step's
   every-frame stepping vs. Manual's boundary-only stepping introducing a second-order difference. In
   PR-1: verify the production order-pin drives the residual to zero against the timing property test.
   **If it proves irreducible**, it is bounded float drift — re-mint the determinism fixtures
   deliberately (the "re-mint BEFORE PlayMode" lesson) rather than fighting it. This does **not**
   reopen the structural branch: same-tick action application is restored either way.

**Do not** adopt `DecisionRequester` / accept bounded latency for PR-1 — that structural branch is
unwarranted; order control restores the invariant.

---

# PR-1 — Decision brief (frozen 2026-07-21, pr-prep)

> **Two gates (hard):** (1) PR-0's finding selects the mechanism (fork P1-2); (2) lands **after
> #184** (PR-4) — it migrates the *post-#184* N-agent driver. Ground against post-#184 `main`.

## Post-#184 ground truth

`EpisodeLoopDriver` already generalizes to N agents (verified from the #184 head): prime and each
non-terminal boundary do `agent.RequestDecision(); opponentAgent?.RequestDecision();
Academy.Instance.EnvironmentStep();`; the ctor sets `AutomaticSteppingEnabled = false`. So the
migration is mechanically a **~3-line deletion** (ctor flag + the two `EnvironmentStep()` calls),
identical for the single-agent and self-play paths — both keep their `RequestDecision()` calls and
simply stop owning the step. PR-1's real content is the mechanism (gated), the permanent guard, and
the test/self-play re-green — not the deletion.

## Scope

**In:** remove runner-owned manual stepping from `EpisodeLoopDriver` (ctor
`AutomaticSteppingEnabled = false` + both `EnvironmentStep()` calls); keep the boundary/prime
`RequestDecision()` calls; apply the PR-0-selected mechanism (fork P1-2); land a **permanent
timing-invariant property test** (the guard); re-green the harness suites + the self_play trainer
smoke under auto-step.

**Out (non-goals):** `InferenceChooser` / the in-game pilot (PR-2 — it still forces
`AutomaticSteppingEnabled=false` after PR-1; interim two-steppers tolerated, see guard); any
arena-count / Path-A `RLDriver` work; obs/reward/policy changes; re-minting checkpoints (migration is
behavior-identical in the clean/recoverable branches; the structural branch halts before any re-mint).

## Fork resolutions (with why)

1. **Permanent guard = direct timing-invariant property assert** (boundary action first moves the
   rigidbody at t+1; terminal observation precedes reset) — **NOT a committed golden trajectory.**
   *Why:* after migration there is no second mode to A/B against, and this repo deliberately avoids
   committed float goldens — the existing `RLEpisodePlayModeTests.TrajectoryEquivalence_*` compare two
   *live* same-process runs precisely because Burst rounds differently across managed/Burst and version
   bumps. An integer step-index property assert is version-robust and encodes the actual contract; it
   is correctly scoped because the stepping change touches *when* the step fires, never observation or
   action *values* (broader trajectory fidelity stays covered by the untouched reset-equivalence tests).
   A naive agent will reach for a golden — steer away explicitly.
2. **Migration mechanism = keyed on PR-0's finding (pre-ratified tree):** *clean* → pure deletion;
   *recoverable* → deletion + a one-line execution-order pin on **our** component (`AICommander`/`Brain`
   ordered after the ML-Agents stepper; exact value from PR-0's probe — the stepper is ML-Agents-owned,
   so we move our order, not theirs), still behavior-identical; *structural* → **STOP and report** for
   joint re-scope (do NOT silently become the `DecisionRequester`/bounded-latency PR — that branch
   changes trajectories and reopens the determinism pins). *Why:* two small branches ship
   behavior-identical; only the structural branch balloons, and pre-authorizing a big mechanism swap
   inside a gated spike's downstream is the one thing worth halting for.

## Assumptions (user-reviewed)

- Migration is identical for single-agent and self-play (post-#184 driver confirms — both drop the
  shared `EnvironmentStep`, both keep `RequestDecision`).
- PR-1 grounds against and lands after #184; if #184 is unmerged when PR-1 starts, rebase onto it.
  Sequencing: #185 ✓ → #184 → PR-1.
- Re-green targets: `RLAgentPlayModeTests` (reward-equivalence, decision-count), the
  `CheckpointEvaluator` eval path (InferenceOnly, deterministic), and the self_play PlayMode two-agent
  loop test. The `TrajectoryEquivalence_*`/telescoping pins and `InferencePilotPlayModeTests` don't
  drive the harness stepper → unaffected.
- Clean/recoverable branches need no determinism re-mint (behavior-identical).
- PlayMode headless, `-ScopeType Auto`, worktree loop, unity-access coordination.

## Blindsider resolutions

- **Self_play trainer smoke is in the merge gate.** #184 proved self_play only under *manual*
  stepping; PR-1 flips that loop. The single-agent suite can't cover the trainer-side self_play path
  (ELO, snapshot swap, two team_ids over gRPC), so PR-1 re-runs `run_smoke_selfplay.py` under auto-step
  as a gate (needs venv + trainer + unity-access — an explicit heavier gate, accepted).
- **Harness auto-step tests set `AutomaticSteppingEnabled = true` in SetUp** (baked in): after PR-1,
  `InferenceChooser` still forces it *false* until PR-2, so in a full-suite run an inference-pilot test
  could leave it false and silently prevent the harness auto-step from engaging (garbage-green). Do not
  rely on the ML-Agents default or on the driver setting it.
