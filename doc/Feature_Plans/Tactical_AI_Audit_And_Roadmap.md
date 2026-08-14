> **HISTORICAL (2026-07-22):** the goal-mode/tactical MPC path and utility brain this document
> audits were deleted by the rip-out arc (#203/#204/PR-3) — the shipped AI stack is now
> policy/velocity choosers over the MPC feasibility tracker. Kept as the design record of how
> the stack got here; §1–2's seam analysis remains accurate, the systems in §1's DECIDE column
> and the nav-layer cost inventory no longer exist.

# Tactical AI — System Audit & Roadmap

> STATUS: living — tactical-AI roadmap; arcs check in here

**Date:** 2026-07-08 (direction decided 2026-07-09; **pivoted 2026-07-11** — see the v2 banner below)
**Status:** Direction **pivoted 2026-07-11** via grill — committed to a *learned goal-policy + MPC-as-tracker* (§3′/§4′). The §3/§4 learn-V plan is demoted to a conditional later upgrade. PR-S1a (#106) in review; PR-S2 (#107) merged.
**Goal:** With AI navigation in a good place, use it as a baseline to build a more
sophisticated **tactical** layer on top — moving toward fluid, dynamic combat
behavior. This doc audits the current system (§1–2) and records the committed
direction (§3): **not** a hand-authored tactical layer, but a commitment to deep
learning — learn a tactical **value function**, keep the MPC as the controller, and
make the environment RL-native. §4 is the dependency-ordered PR sequence.

> **Direction changed after the audit.** §3 originally sketched a hand-authored
> tactical roadmap (world-model expansion → movement verbs → coordination). A
> grill on the broad direction (2026-07-09) rejected that: the prior hybrid
> attempt produced a knob-farm that didn't fit RL *because it hybridized at the
> behavior layer* (utility curves, cost weights = authored decision logic). The
> committed direction below hybridizes at a different axis — **learned decisions +
> a classical controller** — which is how AlphaDogfight / GT Sophy / CTDE-MARL
> actually shipped. The audit (§1–2) is unchanged and still the ground truth.

> **Direction pivoted again (2026-07-11).** A grill on PR-S3 reopened the §3 bet and
> replaced it. The near-term direction is now a **learned goal-policy that outputs a
> kinematic reference the MPC tracks** (§3′), not a learned value function the MPC plans
> against. §3/§4 below are kept as **superseded** history; §3′/§4′ are the live plan. The
> audit (§1–2) still stands. An independent Codex consult (gpt-5.6, 2026-07-11) reached the
> same recommendation unprompted.

> **Terminal learned-cost experiment shelved (2026-08-13).** The later-upgrade
> experiment—an executed-return model scored on MPC candidate terminal states—reached
> transition capture, the terminal-candidate inference seam, and a trained baseline
> (#364–#366; PRs #373, #354, #383). It is preserved on `archive/rl-value` and is not
> queued for `main`. Shadow scoring, authority-readiness evaluation, and the behavioral
> A/B (#367–#369) remain unbuilt; their issues stay closed until the experiment resumes.

---

## 3′. Committed direction v2 (2026-07-11) — learned goal-policy + MPC-as-tracker

**The pivot.** The §3 bet (learn V; MPC plans greedily against it) is demoted to a
*conditional later upgrade*. The near-term direction:

- **Learner** — an ML-Agents PPO policy (the installed-but-unused `com.unity.ml-agents`
  3.0.0) that runs once per decision tick (~0.2–0.4 s) and outputs a **desired planar
  velocity reference + a fire gate**. It slots into the existing `Brain`/`IIntentChooser`
  seam (`AI/Brain.cs`, `AI/Strategy/IIntentChooser.cs`) — the produced intent *is* the velocity
  reference, not a mode+weights bundle.
- **Controller** — the MPC is reshaped into a **feasibility tracker**: track the commanded
  velocity, keep collision-avoidance, keep intercept-facing as pure aiming geometry
  (`Cost.InterceptYaw`). The authored tactical cost block —
  `FacingCost`(as-tactic)/`ExposureCost`/`TangentialVelocityCost`/`MissDistanceCost`/`LosCost`
  (`AI/Navigation/MPC/Cost.cs`) — is **gated off in the new mode, not deleted**: the legacy
  modes keep it so the shipped AI is untouched and *becomes the scripted baseline opponent*.
- **Reward** — unchanged from §3.3 (sparse ±1, dense HP-differential, potential-based
  firing-envelope shaping Φ), computed at decision boundaries.
- **Training** — vs the frozen scripted baseline first (clean "beat the utility AI on
  held-out seeds" signal); self-play snapshots later.

**Why the pivot (root cause).** The §3 bet *forced* a large bespoke-infra investment: V must
be evaluated **in-process, hundreds of times per tick inside the CEM rollout** (§3.4), so V
cannot be a Python-side network — it must be a C#/Sentis artifact hot-reloaded each
value-iteration round, with a custom trainer, a returns logger, and ONNX hot-reload
plumbing. None of that exists, and it buys nothing until "the game feels different" once. The
two things §3 actually wanted from V — (a) no cold-start-from-random and (b) training
stability — are **preserved by the goal-policy**: a random *velocity goal* still yields
competent collision-free motion (the MPC executes it), and PPO over a low-dim reference into
a stabilizing controller is about as well-behaved as policy-gradient gets. Codex concurred
unprompted: *"Build B first… Option A is a premature heavy investment… it spends substantial
engineering effort on a custom training system before establishing that learning produces
better combat at all."*

**The deeper reason the old intent had to change — tactics live in the MPC cost.** The
current tactical behavior *is* the ~5 hand-authored cost terms in `Cost.Evaluate`
(intercept-facing, arc-exposure, tangential juke, miss-distance, LOS) with tuned weights. The
pre-pivot intent was `GoalMode + desiredRange + weight *multipliers*` — so any policy over it
could only **re-weight a fixed library of authored tactics**, never invent a maneuver that
isn't already a cost term. That ceiling *is* the knob-farm §3 was fleeing, relocated from
utility curves to cost weights. Cutting the learned/classical boundary at a **kinematic
velocity reference** (the "③" cut) removes the ceiling: a 2D velocity command spans strafe,
any-radius orbit, threat-relative evasive breaks, and cover approach by construction, and the
*reward* — not authored cost terms — teaches "deny their arc / be hard to hit / juke." This
also closes A's one residual advantage (ranking arbitrary feasible trajectories), since a
velocity+fire interface is not restrictive.

**When A/learn-V comes back.** Only if diagnostics show the binding limit is the MPC's ~1.5 s
horizon (not the interface, observation, reward, or game mechanics) — Codex's "oracle-goal
test" (below) is the discriminator. Likely-dominant middle form if it does: learn Q(s,g),
sample K candidate goals, rank with Q, MPC executes the winner — lookahead without a network
in the hot Burst loop.

## 4′. PR sequence v2 (2026-07-11)

Supersedes §4's Phase 0/1. Guardrail unchanged: the current utility+MPC AI stays the shipped
AI (gated) until a learned agent beats it.

- **PR-0 · Cost regroup + objective-aware idle gate** *(refactor, lands first).* Reorganize
  `Cost.Evaluate` into `Feasibility + Aim + Objective(goalMode-dispatch) + Tactical(toggle)` so
  the velocity tracker is a *composition*, not the scripted controller with its tactical terms
  zeroed out (the post-RL end-state is **two cost identities sharing one solver** — scripted
  baseline vs velocity tracker). Behavior-preserving for the scripted path (tuned assets + baked
  tolerances pin it) **plus one targeted fix**: the Navigator idle gate goes per-objective-kind,
  so MaintainRange/Flee stop mis-using waypoint-arrival semantics. Detail:
  `MPC_Velocity_Reference_Mode.md`.
- **PR-1 · MPC velocity-reference mode + tracking validation** *(classical, no ML).* Add a
  gated `VelocityReference` goal mode: the objective dispatch tracks a commanded **world-plane**
  velocity (`VelocityTrackCost`), keeps collision-avoidance + intercept-facing, tactical block
  off via `tacticalEnabled` (legacy modes byte-identical — **no weight-zeroing**). The reference
  enters through `ActIntent`/`ApplyIntent` (the seam the learner drives) plus a low-level
  `Navigator.SetVelocityReference`. Validate the **tracking fidelity** (solver converges to
  `v_ref`, incl. the off-axis strafe-authority case) in EditMode on `Model.Step`, plus a
  PlayMode smoke. **The closed-loop maneuver oracle (orbit/break/range) + CMA-ES move to PR-2**
  (built on the runner as a go/no-go gate *before* reward/ML) — see split rationale in
  `MPC_Velocity_Reference_Mode.md`. **Keystone: the interface both the oracle and the learner
  drive is built and exercised here.**
- **PR-2 · Episode / reward / reset layer** *(the architecture-neutral survivor of the old
  PR-S3).* Per-agent reward §3.3 at decision boundaries; engagement episode boundaries +
  reset; headless episode runner (mirror `ChaseBenchmarkModule` + PlayMode driver +
  `Time.timeScale`).
- **PR-3 · ML-Agents `Agent`.** Sensor = the merged `ObservationExtractor` (#107); action =
  velocity reference + fire gate + boost (4 continuous, threshold-gated — 4.0.3 still rejects
  hybrid specs in the trainer path) → PR-1's interface; reward/lifecycle = `AddReward`/
  `EndEpisode`/`EpisodeInterrupted` riding PR-2b's `EpisodeRunner`. Train vs frozen baseline.
  → **Gate: beats the scripted baseline on held-out seeds** (Wilson 95% lower-CI win-rate
  > 50%, no nav/collision regressions). Frozen decision brief + full design:
  `RL_MLAgents_Agent.md`.
- **PR-4 · Self-play snapshots + eval league** (frozen baseline anchored as anti-forgetting).
- **Later / conditional:** learned firing discipline; teams (CTDE); learn-V or
  Q(s,g)-ranking *iff* the horizon is proven the binding limit; native-sim port iff
  throughput (old Gate 1) bites.

**Old Phase-0 statics work:** PR-S1a determinism (#106, in review) and the multi-arena
rethink (`project_multi_arena_rethink`) remain valid substrate but **do not block** PR-1/PR-2
— the velocity interface, reward, and a single-arena episode runner are arena-count-independent.

**ML-Agents 3.0 gotchas to honor (Codex consult).** Pin the matching Python trainer
toolchain (Sentis 2.1); continuous actions arrive normalized `[-1,1]` (clamp/transform
explicitly); ~~the 3.0 trainer has no mixed continuous+discrete action support~~
(**overturned — see below**); `MaxStep` counts *decisions*
not physics ticks; PPO (not SAC) for self-play; reset both competitors atomically and award
terminal reward before `EndEpisode`; accumulate per-physics-step damage into the
decision-boundary reward; terminal potential = 0 so Φ telescopes.

> **OVERTURNED 2026-07-31 — do not honor the mixed-action gotcha.** It read as
> "keep firing scripted or make the fire-gate a continuous threshold"; both are wrong
> now. The communicator check (`GrpcExtensions.cs:175`) only restricts when the trainer
> *lacks* hybrid support, and the K1-0 smoke confirmed the full hybrid
> train→export→Sentis path end-to-end (`results/rl-eval/k1-0-hybrid-smoke-2026-07-31/`).
> Production ships the anchored **5 continuous + 2 discrete** schema — fire and boost are
> discrete branches, not thresholds. Detail: `RL_MLAgents_Agent.md` §hybrid, memory
> `project_anchored_k1_arc.md`. Left struck rather than deleted so the reversal is
> visible to anyone who read the original.

---

## 1. The system as it stands

> **⚠ §1–2 were written 2026-07-09 and describe the PRE-#204 system.** The utility
> brain layer they audit — `UtilityChooser`, `Sampler`, `UtilityBuilder`, `AIState`,
> `GoalRunner`, `StateProfile` — was **deleted by #204** (`10b3849a`, rip-out arc PR-2).
> `AI/Strategy/` now holds only `IIntentChooser`, `ActIntent`, `IPolicyReadout`, and every
> `IIntentChooser` implementation is an RLHarness chooser (policy or scripted archetype).
>
> **What still holds:** the three-stage pipeline, the `IIntentChooser` / `ActIntent` seam,
> and §1's nav-layer description. **What is historical:** everything about utility scoring,
> state profiles, and §2's decision-shape ceilings — those ceilings were demolished, not
> merely identified. Citations into the deleted layer are left in place as history.
> §3′/§4′ (above) are the live direction and are unaffected.

The AI is a clean **three-stage per-ship pipeline**, driven from
`AICommander.FixedUpdate` (`AI/AICommander.cs:80-94`):

```
PERCEIVE                    DECIDE                     ACTUATE
Scout → AIContext →         Brain → IIntentChooser →   Navigator (MPC) → Pilot
SituationAssessment         (UtilityChooser)           Gunner → weapons
   (snapshot)          →    ActIntent      →    (intent applied)
```

Per tick (`AICommander.cs:80-94`):
1. `context.UpdateAssessment()` — rebuild world model + `SituationAssessment` snapshot.
2. `intent = Brain.Decide(context, dt)` — policy chooses an action.
3. `Navigator.ApplyIntent(intent)` / `Gunner.ApplyIntent(intent)` — push to actuators.
4. `control.Pilot.Drive(Navigator.ComputeCommand())` — MPC → motion.
5. `Gunner.Fire()` — per-slot firing.

Execution order is pinned so caches are fresh: `NavFieldService -90`, `Scout -80`,
`Brain -70`, `Navigator -60`, `Gunner -50`, `AICommander -40`.

### The three load-bearing seams (all genuinely well-built)
- **`SituationAssessment`** (`AI/Context/SituationAssessment.cs`) — an immutable,
  mostly-normalized ~18-scalar snapshot rebuilt each tick. Effectively an
  observation vector already; consumed purely by utility factors
  (`Sampler.cs:87`).
- **`IIntentChooser.Decide(ctx, dt) → ActIntent`** (`AI/Strategy/IIntentChooser.cs`)
  — the policy is a plain `[Serializable]` object, **not** a MonoBehaviour, swapped
  via `[SerializeReference]` on `Brain` (`AI/Brain.cs:24`). Built explicitly as the
  RL slot-in point; the commander and actuators are untouched by a policy swap.
- **`ActIntent` + `Navigator.ApplyIntent`** (`AI/Strategy/ActIntent.cs`,
  `AI/Navigator.cs:219`) — a declarative action struct applied idempotently
  ("result depends only on the intent, never on prior state or call order,"
  `Navigator.cs:213-217`).

### The nav layer (the baseline we like)
A sampling-based CEM/MPPI MPC (`AI/Navigation/MPC/`): physically faithful dynamics
(`Model.Step`, drag + bank-coupled yaw + boost), time-correlated noise, warm-start,
and a topology-aware **terminal cost-to-go field** (Burst Dijkstra, in time-to-go
units, `AI/Navigation/Field/NavField.cs`). Its cost function already contains real
tactical ingredients (`AI/Navigation/MPC/Cost.cs`): intercept-lead facing, range-band
holding, enemy-arc exposure avoidance, tangential-velocity juking, LOS preservation,
miss-distance evasion. **Steerable, not just replaceable.**

### States are data, not code
One `AIState` shell; every behavior is a `StateProfile` ScriptableObject
(`Assets/Settings/AI/StateProfiles/*.asset`) bundling a goal strategy, tactical
flags, sparse MPC weight overrides, and utility factors (`AI/Strategy/StateProfile.cs`).
Scoring is a weighted **geometric mean** of multiplier factors (`Sampler`,
`UtilityBuilder`) with a solid anti-chatter stack: dwell floor + fading stickiness
+ exponential smoothing + transition margin.

**Bottom line on strengths:** the *architecture* is in good shape. The RL seam
exists, the observation snapshot exists, the action is declarative, states are
authorable data, and the MPC is a reusable low-level controller.

---

## 2. Where the ceiling is

Three convergent limitations cap tactical sophistication — **none of them in the
nav layer.** They live in the world model, the intent vocabulary, and the decision
shape. Plus a structural coordination gap.

### 2.1 The world model is flat, memoryless, and single-target *(deepest limiter)*
The AI knows **exactly one enemy** — the geometrically nearest contact
(`EnemyTracker` + `ShipScanner.NearestEnemy`, `AI/Context/EnemyTracker.cs:80-86`).
Everything downstream inherits that tunnel vision:
- **No threat ranking.** Target = nearest by distance. `EnemyFacingThreat` /
  `Outnumbered` are *computed* but never influence *which* enemy is engaged.
- **No memory.** `SituationAssessment` / `EnemyTracker` recomputed from scratch each
  tick. No last-known-position, no track history, no belief when LOS breaks — an
  enemy that juke-breaks the sense sphere is simply dropped. Feints/baiting/"search
  where I last saw them" are impossible.
- **No multi-contact geometry.** 2nd..Nth enemies exist only as scalar *counts*
  (`ContactSummary.cs`). Can't flank, focus-fire, or avoid crossfire.
- **Blind to own resources.** `IShipStatus` exposes no energy, no ammo, no
  per-weapon readiness. `IWeaponContext.IsReady` exists but the Gunner never
  consults it. Can't reason "low energy / out of missiles → disengage."
- **No incoming-fire perception.** `IncomingMissile` is a hardcoded `false` stub
  (`EnemyTracker.cs:38`) — the whole missile-evasion path is dead code.
- **Two disjoint targeting systems.** The missile `LockOnSensor`
  (`Combat/Targeting/LockOnSensor.cs`) picks by *cone angle*; the gun target picks by
  *nearest distance*. They can point at different ships; nothing reconciles them; the
  Brain never reads the lock state.

### 2.2 The action vocabulary is narrow
- **Three movement verbs total:** `Waypoint`, `MaintainRange`, `Flee`
  (`AI/Navigation/Types.cs:6-11`). No first-class orbit, strafe-at-range,
  intercept-point, cover-seek, or break-off — *even though the MPC cost function
  already has the machinery for most of them.* `MaintainRange` holds a scalar radius
  with no angular hook, so "circle the target" only emerges weakly and can't be
  commanded.
- **Firing is one global boolean.** The Gunner fires *all* slots whenever each
  self-gates (`AI/Gunner.cs:43-54`); no weapon selection, trigger discipline, ammo
  conservation, or hold-fire-until-lock at the AI level. Worse, `enableFiring` rides
  on the *movement* `StateProfile` — firing policy is a byproduct of which motion
  state won, not an independent tactical decision.
- **Cover is sensed but never used.** `HasNearbyCover` is a scoring input only;
  there's no "get behind that asteroid" movement goal.

### 2.3 The decision shape fights fluidity, and variants explode
- **Hard one-state-per-tick argmax** (`Sampler.cs:49-52`). Fluid behavior
  (strafe + retreat + fire simultaneously) can't be expressed as "one active state."
  Blending is only available as stochastic *switching* (softmax) — jitter, not blend.
- **Behavior nuance becomes whole new states.** Attack/AttackAggressive/AttackEvasive/
  AttackFast are separate assets duplicating goal + factors + weight overrides. No
  parametric or compositional layer — you can't say "Attack, but flank" without a new
  asset.
- **Strategic layer reaches into MPC internals.** `weightOverrides` reference cost
  weights by integer index, brittly coupling strategic profiles to the navigation
  cost enum's ordering.

### 2.4 No coordination
Every `Brain` decides in complete isolation. No blackboard, no shared target, no role
assignment, no formation; ally awareness is a scalar `Outnumbered` count. (`difficulty`
is declared on `AICommander` but never read — curriculum/skill scaling isn't wired.)

---

## 3. ~~Committed direction — learn a tactical value function~~ (SUPERSEDED 2026-07-11 → §3′)

> **Superseded by §3′.** Kept as history + as the definition of the *conditional later
> upgrade*. The reward design (§3.3) and observation contract (§3.4) below **carry forward
> unchanged** into the v2 plan; only the "MPC plans greedily against a learned V" training
> architecture is demoted.

**The bet:** commit to deep learning as the destination. Learn the *decisions*;
keep the *control* classical. Concretely: the MPC stays the controller, and the one
thing we learn (for now) is a **tactical value function** V(s) that feeds the MPC's
existing terminal cost-to-go slot. No authored decision knobs, no policy-gradient
network yet.

### 3.1 Why not the hand-authored roadmap
The prior hybrid attempt ended in a knob-farm that "didn't fit RL" — because it
hybridized at the **behavior layer** (utility curves, cost weights, state profiles),
i.e. it hand-built the policy. The fix is to hybridize on a *different axis*:
**learned decisions + a classical controller**. That's how the comparable systems
shipped — AlphaDogfight (1v1 deep-RL dogfighting beat an F-16 pilot in sim), GT Sophy
(deep RL beating top human racers, tactical overtaking), and CTDE-MARL (MAPPO/QMIX)
for decentralized coordination. It also *dissolves* the two hard questions instead of
answering them by hand (see 3.5, 3.6).

### 3.2 The architecture (hierarchical, "MPC is the policy")
- **Controller stays the MPC.** Keeping it is the *correct* RL architecture, not a
  hedge — robotics/racing RL learns references/values a controller executes, rarely
  raw torques. The MPC already has a **value-function-as-terminal-cost slot**
  (`wTerminal` × cost-to-go field, `AI/Navigation/Field/NavField.cs`).
- **Learn only V** = expected future combat advantage. Feed it in as
  `terminal_cost = −wTerminal · V(x_terminal)`. The 1.5 s horizon does
  *policy improvement*; V supplies the long-horizon credit the horizon can't see.
- **Training loop = value iteration with MPC as the improvement operator**
  (MuZero-shaped: model-based lookahead as the improver, a learned V as the critic).
  Loop: MPC-greedy-w.r.t.-V_k self-plays → compute value targets (MC / n-step / λ) →
  regress V_{k+1} → MPC now plans against a better V → iterate. **No actor network,
  no policy-gradient instability.** Exploration comes free from the CEM sampling
  noise + self-play diversity. A learned policy net enters *later* and only for a
  concrete reason (cheaper deploy-time inference via distillation, sub-horizon
  reactions, or learned team comms).

### 3.3 The reward (V = combat advantage by construction)
Per-agent (never a global team score), so it extends to teams unchanged:
- **Spine (sparse):** `+1` enemy destroyed, `−1` self destroyed (normalized).
- **Core dense term:** `+λ_d·(enemy HP lost) − λ_t·(my HP lost)` per step, normalized
  by max HP. V over health-differential *is* an advantage function — "expected future
  net damage swing."
- **Positional shaping (potential-based, policy-invariant — Ng et al. 1999):**
  `Φ(s) = k₁·[enemy in my firing envelope] − k₂·[I'm in enemy's firing envelope]`,
  reward `+= γΦ(s') − Φ(s)`. Gives the *feel* of "seek my shot / deny theirs" with no
  farmable exploit.
- Optional regularizers only if it misbehaves: small per-step time cost, small
  per-shot cost. Skip control-smoothness — the MPC owns it.
- **Train in representative asteroid fields**, not empty space: V then *absorbs*
  topology (partially subsuming the Dijkstra nav-field) and means both "how do I get
  there" and "do I want to be there."

### 3.4 The observation (token-list contract)
- **Egocentric, target-relative** — my kinematics + resources, primary target
  relative kinematics/facing/health.
- **First-class threat-track channel:** in-flight dangerous objects (missiles now,
  mines later) as tracked kinematic entities, separate from the enemy ship. *This is
  the mechanism* that makes per-weapon evasion emergent (3.5) — omit it and the agent
  can only avoid launch positions, never dodge a live missile.
- **Encoding = a list of typed entity tokens** (self / target / threat-track /
  obstacle-lobe). Pool with **nearest-K slots + a coarse local obstacle grid** for v1
  (feeds a plain MLP; permutation handled by distance-sort). **Attention is the
  destination, not the start** — the token-list contract makes it a drop-in swap
  (only the net's first layer + inference graph change; obs extraction, reward, loop,
  env untouched). Switch when: teams/many-entities, or K-slot rank-swap jitter hurts,
  or V is distilled to once-per-tick inference. Rationale: 1v1 has small entity
  counts (attention's variable-N strength unused), a plain MLP keeps pipe-validation
  confound-free, and **V is evaluated hundreds of times/tick inside the MPC** so
  cheap inference matters early.
- **Scope bound: learning is movement-only.** Firing stays rule-based
  (`Gunner`/`Gunsight`); V shapes movement to build firing geometry, deny theirs, and
  dodge threat tracks. Learned trigger discipline is a separable later addition.

### 3.5 How this answers "react to different weapons" (the original Q1)
Per-weapon evasion is **not authored** — it's what a threat-track channel + a
health-differential reward *produce*. A missile and a concussion mine are both just
"threat track with kinematics"; the net learns different responses because their
kinematics differ (missile closes/homes → late hard break, strip on an asteroid; mine
sits with a trigger radius → route around early). Evasion keys on threat **geometry**,
not weapon **identity** — the reframe only pays off *because* we learn it instead of
tabulating it.
(Current roster: `Lasers`/`Rippers`/`ChargeLasers` → `Laser`; `Railguns` hitscan;
`Missiles` guided + `IDamageable`/shootable. No mines yet — hypothetical.)

### 3.6 Coordination / decentralized wingman (the original Q2)
Deferred to Phase 2, but the design makes it cheap: a **per-agent tactical-V is
already the decentralized substrate.** Each agent runs its own V on its own local
view = inherently decentralized. Teams later = **target assignment** ("who do I
engage") + a light coordination signal layered on top of the *same* V, trained CTDE.
Wingman and enemy become **one artifact** (same policy, different observation + team
flag). The discipline that makes it compose: per-agent reward from day one, and the
V's state is "me vs primary target + generic threat slots," never "the one enemy that
exists."

### 3.7 Scope: single ship first
**1v1 self-play now, team-ready by construction, teams as an explicit later phase.**
Combat advantage is defined against one opponent; 1v1 self-play is the cleanest
signal and the standard path (AlphaDogfight was 1v1). Stacking CTDE on an unproven
pipe is how you get "nothing converges and you can't tell which layer broke."

### 3.8 Training environment & opponents
- **Unity-first** (reuses real game physics, no train/deploy divergence). `Model.Step`
  is already an isolated discrete model, so a **headless native sim stays on the
  shelf** as a fast-follow if throughput bites — a model you'll have already validated.
- **Bootstrap opponent = the current MPC+utility AI** (competent, stationary → dodges
  cold-start, gives a free eval metric: *win-rate vs baseline*). Cold-start was never
  really a risk: even with V₀ = current nav field, the MPC *already flies competently*
  — we improve up from today's behavior, not from a random network.
- **Then a checkpoint league** (sample past checkpoints; keep the scripted AI anchored
  in the pool as anti-forgetting + interop guarantee). Symmetric 1v1 → both sides
  **share one V** (parameter sharing).
- **Frozen, versioned combat sandbox** per training run (canonical ship + current
  weapons + representative field). Game content evolves; the sandbox is a snapshot.
  Don't learn against a moving target.

---

## 4. ~~PR sequence (dependency-ordered, with go/no-go gates)~~ (SUPERSEDED 2026-07-11 → §4′)

> **Superseded by §4′.** The Phase-0 statics/determinism findings remain valid (PR-S1a in
> review as #106; multi-arena as its own rethink), but the Phase-0→1 ordering below assumed
> the learn-V architecture. See §4′ for the live sequence.

**Guardrail:** the current utility+MPC system **stays the shipped AI through all of
Phase 0–1**. The learned V ships only after Gate 4. Feature-flag the terminal source
(Dijkstra field vs learned-V bake) so it's A/B-able and instantly reversible — nothing
here degrades the game while the bet is proven.

### Phase 0 — Substrate (the leap, made concrete)
> **Reshaped 2026-07-09 (grill + user call).** The original "PR-S1 · retire the three
> statics" was split. **PR-S1a** (determinism) is arena-independent. The
> multi-arena work is bigger than one PR and its **root decision — the arena isolation
> mechanism — ripples widely**, so it's promoted to a **dedicated design rethink**
> (memory `project_multi_arena_rethink`; board: High Dev Pool) instead of piecemeal
> per-static conversion. Ground-truth finding: only `ObstacleFields.Active` is a real
> AI-correctness blocker (2 consumers, both AI); `NavFieldService` is keyed-by-transform
> (data-safe across arenas) and `GamePlane` is contingent on the isolation mechanism —
> both deferrable. The minimal fix (obstacle field → the existing per-session
> `EnvironmentService`, injected like `IShipRegistry` via `WireShipDependencies`) is
> **rethink-proof** and is the likely first PR *out of* that design.

- **PR-S1a · Determinism prerequisites *(arena-independent)*.** Seed the RNG
  (`Sampler.cs:183`, `GoalRunner.cs:133-135`, `BurstSolver` frame-seed) via per-ship
  injection; make the hysteresis/combat timers dt-driven (`EnemyTracker`,
  `UtilityChooser.cs:60`, vs `Time.time`).
- **PR-S1b · Multi-arena substrate *(design rethink first)*.** Statics → per-session
  ownership + the isolation mechanism (separate `PhysicsScene` vs spatial offset vs
  process-per-arena) + headless N-arena stepping. See memory
  `project_multi_arena_rethink`.
  → **Gate 1 (throughput):** acceptable steps/sec across many arenas? If no, the
  native-sim decision moves forward *now*, before anything is built on top.
- **PR-S2 · Observation contract + logger.** The egocentric target-relative
  token-list obs (self / target / threat-tracks / obstacle-lobes). Log
  `(obs, action, reward-components, next-obs, terminal)` from live play — doubles as
  the RL data pipe and an analysis stream.
- **PR-S3 · Reward + episode API.** Per-agent reward (3.3); engagement episode
  boundaries + reset; headless episode runner.

### Phase 1 — First learned V (1v1)
- **PR-V1 · Self-play harness.** MPC(V) vs current AI (bootstrap) in the frozen
  sandbox; records episodes; tracks win-rate vs baseline.
- **PR-V2 · Offline V-fit.** Train the nearest-K MLP value regressor on logged
  returns / TD targets.
  → **Gate 2 (learnability):** does V predict advantage on held-out states?
- **PR-V3 · Wire V into the MPC terminal.** Bake V to the engagement-keyed grid (same
  runtime shape as today's field), re-tune `wTerminal`. *First moment the game feels
  different.*
  → **Gate 3 (no-regress):** MPC(learned-V) must not regress nav and should visibly
  improve positioning vs MPC(Dijkstra-field).
- **PR-V4 · Close the loop.** Iterate MPC(V_k) → collect → refit V_{k+1}; graduate to
  league self-play; target-network + replay for stability.
  → **Gate 4 (success):** iterated V beats the scripted baseline win-rate.

### Phase 2 — Graduation (later, each separable)
Attention encoder swap · learned firing / trigger discipline · **teams** (target
assignment + decentralized CTDE coordination on the *same* per-agent V — the wingman)
· native sim port if Gate 1 bit.

**Load-bearing caveat:** Gate 1 gates the whole phase — if Unity can't produce the
steps, everything downstream stalls. That's why it's PR-S1, not deferred cleanup.

---

## 5. One-line summary

**(v2, 2026-07-11)** The seams are right; the tactics were hiding in the MPC *cost*. Stop
hand-building the policy **and** stop planning to hot-reload a learned V into the CEM inner
loop — instead **learn a goal-policy that emits a kinematic velocity reference and let the MPC
track it** (MPC demoted to a feasibility controller, its authored tactical terms gated off and
kept only as the scripted baseline). ML-Agents PPO vs the frozen baseline first; learn-V
returns only if the MPC horizon is proven the binding limit. Per-weapon evasion and
coordination still *emerge* from observation + reward + training, not authoring.

> ~~**(v1, superseded)** The seams are right; stop hand-building the policy. The MPC is a
> controller worth keeping and already has a value-function slot — so commit to DL by
> *learning the tactical value function* (MPC stays the policy)…~~

---

## Appendix — key files

- **Owner/tick:** `AI/AICommander.cs`
- **Perceive:** `AI/Scout.cs`; `AI/Context/{AIContext,EnemyTracker,EnemyTarget,SituationAssessment}.cs`;
  `AI/Scanning/{ShipScanner,ContactSummary,ObstacleScanner}.cs`
- **Decide:** `AI/Brain.cs`, `AI/Strategy/{IIntentChooser,ActIntent,IPolicyReadout}.cs`;
  implementations in `RLHarness/Runtime/{AgentChooser,InferenceChooser}.cs` and
  `RLHarness/Opponents/*`.
  *(Pre-#204 this line read `AI/Strategy/{UtilityChooser,Sampler,UtilityBuilder,AIState,GoalRunner,StateProfile}.cs`
  + `Assets/Settings/AI/StateProfiles/*.asset` — that whole layer was deleted.)*
- **Actuate (nav):** `AI/Navigator.cs`; `AI/Strategy/ActIntent.cs`; `AI/Navigation/Types.cs`;
  `AI/Navigation/MPC/{Mpc,BurstSolver,Cost,Model}.cs`; `AI/Navigation/Field/*`
- **Actuate (guns):** `AI/Gunner.cs`; `Combat/Weapons/{Gunsight,WeaponBase,ChargeLasers,Railguns,Missiles}.cs`;
  `Combat/Targeting/{LockOnSensor,TargetLock,TargetingMath}.cs`
- **Wiring/world state:** `Ships/Ship.cs`, `Ships/Command/Types.cs` (`IShipStatus`/`IWeaponContext`),
  `Game/Services/Units/UnitService.cs`, `Ships/Registry/{IShipRegistry,ShipRegistry}.cs`,
  `AI/Scanning/IObstacleField.cs` (reached via `ArenaContext.ObstacleField`; the old
  `ObstacleFields.Active` static was hard-cut in the multi-arena arc),
  `AI/Navigation/Field/NavFieldService.cs`,
  `GamePlane`, `Game/MainGameManager.cs`

Related prior docs: `Chase_Navigation_Trade_Study.md`, `Chase_Nav_Synthesis_Summary.md`,
`Flee_Terminal_Cost.md`, `RL_Implementation_Plan.md`, `Behavior_Upgrades.md`.
