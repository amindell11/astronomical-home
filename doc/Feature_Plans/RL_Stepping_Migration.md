# RL Stepping Migration — retire runner-owned `EnvironmentStep`

> STATUS: living — **§"PR-0 — Finding" is why this doc survives its arc**: auto-step costs
> exactly one tick and an execution-order pin restores it, with the evidence quoted so the
> verdict stays auditable. Any future stepping work reads it instead of re-deriving it.
> The migration arc itself is CLOSED; its frozen PR-0/PR-1 decision briefs were trimmed
> 2026-08-06 (recover via `git show 3ca19bc7:doc/Feature_Plans/RL_Stepping_Migration.md`).

**Arc outcome (closed 2026-07-22).** All manual stepping is gone; the Academy auto-clock is the
sole stepper.

- **PR-0** — ordering spike, verdict RECOVERABLE (finding below; nothing merged).
- **PR-1** — #188: `EpisodeLoopDriver` onto the auto-clock, plus the one-line pin
  (`AICommander` → `[DefaultExecutionOrder(10)]`, after the ML-Agents stepper).
- **PR-2** — #197: `InferenceChooser` onto the auto-clock. Fork resolved *against*
  `DecisionRequester` — the manual K-tick counter stayed. No manual steppers remain.

Path A (in-process M-arenas) was the point of the arc; it shipped on top of this in #201.

**Date:** 2026-07-21 (drafted); closed 2026-07-22.
**Parent:** `Multi_Arena_Substrate.md` §"N-arena stepping model" (the already-blessed target:
Academy is the clock, per-arena driver collapses to reset-only); the Path A/B
throughput arc (memory `project_rl_training_throughput.md` §Path A — names the
manual global step as Path A's whole cost).
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

- **The env-step arc's PR-C fork 2** already recorded that a training agent + an
  `InferenceChooser` (or two `InferenceChooser`s) both stepping "break the pacing contract."
  Today that is documented-around, not fixed.
- **In-process M-arenas is impossible** with per-boundary manual stepping: M arenas each want
  their own decision cadence, but there is one global step. The throughput arc names
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

### PR-0 — ordering spike (the gate; no product change) — DONE, verdict RECOVERABLE

**In:** in one PlayMode harness path, flip to `AutomaticSteppingEnabled = true` + boundary-only
`RequestDecision()` with no manual `EnvironmentStep()`, and measure against the existing
invariants: (a) zero-latency — the boundary action governs the following physics interval, not a
tick later; (b) terminal-observation-before-reset holds; (c) the JSONL trajectory-equivalence /
determinism pins still pass, or a bounded, characterized shift is identified. Deliverable is a
written finding + the concrete recommended shape for PR-1 (pure deletion vs. `DecisionRequester`
vs. execution-order pinning), not merged product code.

**Out:** touching `InferenceChooser`; multi-arena wiring; deleting the manual path in `main`
(the spike is measured on a branch/throwaway path). No training run.

**Gate:** the finding decided PR-1's mechanism (fork P1-2, "recoverable").

### PR-1 — migrate `EpisodeLoopDriver` to auto-step — SHIPPED #188

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

### PR-2 — migrate `InferenceChooser` (the shipped in-game pilot) — SHIPPED #197

**In:** the in-game trained pilot stops owning the Academy clock — `RequestDecision()` on its
K-cadence (or a `DecisionRequester` on `LivePilotAgent`), read the action when it arrives, no
manual step. This is the change that **structurally kills the "two steppers collide" wart**: N
in-game inference pilots (or a training agent + a pilot) then all just `RequestDecision()` and the
Academy batches them, instead of fighting over `AutomaticSteppingEnabled`.

**Out:** obs/policy changes; re-minting checkpoints (stepping is orthogonal to the model).

**Fork — RESOLVED (ai-counsel, #197):** kept the **manual K-tick counter** on the existing tick;
`DecisionRequester` rejected. The in-game pilot tolerates the one-tick delay (it is not
determinism-pinned like training), so the idiomatic component bought nothing over the smaller diff.

### After the arc — Path A stepping model — SHIPPED #201

With no manual global step, `Multi_Arena_Substrate.md`'s N-arena model became reachable as written:
per-arena **reset-only `RLDriver`**, the Academy as sole clock batching all arenas' agents. That was
the throughput/teams payoff and its own arc; this migration was its precondition, not its whole.

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

## How it composed with neighboring work (settled)

- **Path B headless players (#185/#187):** orthogonal, as predicted — Path B was chosen
  *specifically to avoid* this refactor. They compose in Path A players under `--num-envs`.
- **PR-4 self-play (#184):** landed first, so PR-1 rebased onto its N-agent request loop and
  deleted the manual step. The self-play *training run* remains parked — on the `RL_SELFPLAY`
  launcher gap, not on anything stepping-related.

## Related

- `doc/Feature_Plans/Multi_Arena_Substrate.md` (the target N-arena stepping model)
- memory `project_rl_training_throughput.md` (Path A vs Path B; why Path B sidesteps this)
- memory `project_tactical_ai_direction.md` (env-step arc record — PR-C fork 2 two-steppers
  note, PR-4 self-play stepping; its brief was deleted 2026-08-06)
- memory `project_multi_arena_rethink`, `project_rl_training_throughput`, `project_tactical_ai_direction`

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
