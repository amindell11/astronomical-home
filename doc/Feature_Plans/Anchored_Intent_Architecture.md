# Anchored Intent Architecture — policy↔MPC facing & velocity interface

**STATUS: K=1 ARC PLAN FROZEN 2026-07-31 (§"K=1 arc plan" below).** Slices
K1-0…K1-4, forks user-ruled, seams code-grounded (three-way seam map,
2026-07-31 session). Each slice still gets a short pr-prep before building;
re-deciding the frozen rulings is out of bounds. The endpoint sections remain
design exploration behind their entry gate.
Consumed by the combat rules-change retrain bundle (see the 2026-07-28 rules-change
handoff memory). Origin: the facing-thrash investigation, live-eyeballed with the
Policy debug gizmos (PR #222).

**Infra council (2026-07-29):** two-model consult (Fable + GPT-5.6 Sol, both
xhigh effort, repo-grounded) on the long-term-policy infrastructure, followed by
a source check of the pinned mlagents 1.1.0 plugin seam. Folded in below:
trainer path RESOLVED (custom trainer plugin), fixed-K bridge DELETED,
sequencing revised, contracts + risk ladder + priors added. One fork remains
OPEN (action domain).

## Problem

The policy commands facing as a world-plane angle and velocity as a world-plane
vector, zero-order-held for 10 fixed steps (5 Hz). Both channels pre-flatten
*frame-relative* intents ("aim at the enemy", "orbit the enemy") into world
coordinates at decision time, and the flattened form goes stale as the world moves:

- Measured command churn 48°/decision vs a 36°/decision yaw-slew budget; tracking
  error (50.6°) ≈ churn — the MPC is a stale-reference servo, not an aim tracker.
- Staleness is distinct from the yaw-rate wall: inside ~3 u the required track rate
  (v/r) exceeds 180°/s and NO interface fixes aim there; in the ~3–8+ u annulus the
  ship could track but the stale ZOH reference stops it.
- Velocity has the same disease with milder symptoms (regulation not convergence;
  plant inertia low-passes churn; no precision envelope) — orbits decay into
  spirals, dodges land off-axis.
- Pre-#221 the fast aim loop existed: `Cost.EvalContext.Create` resolves
  `InterceptYaw` per rollout step when `projectileSpeed` is set. Manual aim made it
  dormant by design; the churn is the policy trying to be a 50 Hz servo through a
  5 Hz straw.
- Today `|fx,fy| → 0` means the nose *drifts* (facing cost vanishes, nothing else
  steers yaw): the policy cannot delegate aim even when it wants to.

## Design: anchored intents

Intent = frame reference + relation + authority. The MPC re-resolves anchors every
rollout step (the machinery `InterceptYaw` already uses); the policy changes its
mind at 5 Hz; staleness dies by construction.

```
FacingIntent   = (anchor, offsetRad, weight)
VelocityIntent = (anchor, radial, tangential, weight)     // polar in anchor frame
anchor ∈ { Enemy(intercept), Entity(ref), Velocity, World, None }
```

- "Face away while evading" = constant `offset π` in the enemy frame — zero churn.
- Weight 0 becomes honest delegation: facing falls back to a velocity-aligned prior
  (thrust-efficient pose), velocity to current-course/momentum. Abstention =
  competence, not drift.
- `NavigationIntent.target` already carries the enemy as a live ref (kinematics +
  source transform) — the seam speaks "entity"; only the facing/velocity channels
  flatten to angles/vectors today.

**Existence proofs (nothing here is hypothetical):**
- `InterceptYaw` (dormant, was production pre-#221) = the Enemy-anchor facing
  resolver.
- The scripted archetype roster IS the enemy-polar velocity vocabulary: Aggressor =
  radial regulation (desiredRange 10), Orbiter = tangential-at-range, Kiter =
  range-band + tangential, Evader = radial-away. Constant commands in the anchored
  frame; churning commands in world frame.
- `facingWeight` (#219) = the first authority/gain channel, shipped.

**Frame-feedback scar (do not re-litigate):** `ToWorldVelocity` converts ego→world
once per decision because re-rotating per tick feeds live yaw back into the
reference. Enemy-anchored frames avoid the self-yaw loop (frame depends on
positions, not own yaw); the bearing feedback they do introduce is the desired
closed loop — it is what makes an orbit an orbit.

## Scaling: standing vs focal, and the per-entity endpoint

Multiplicity (teams, multiple enemies, projectiles) scales through **class
machinery, not per-entity intent dims** — `wObstacle` already composes up to 96
anchored avoidance rows per rollout step (64-slot scan → 128 merged with ship
contacts → 96 solver rows after multi-sphere expansion; max-composed, worst wins)
without the policy naming any of them.

- **Standing classes** (MPC cost, always on, resolved at 50 Hz over N entities):
  threat avoidance (asteroids; projectiles are fast obstacles — `TurnAwayCost` is
  already velocity-gated), leash/bounds, later formation/teammate spacing.
- **Focal intent** (policy, 5 Hz, fixed-dim): a *care vector* of gains over the
  standing classes, plus one/two focal anchored intents. The initial care vector
  exposes weights the MPC already has (`wVelTrack`, `wFacing`, `wObstacle`, leash)
  — zero new class engineering; `facingWeight` proved the mechanism.

**The per-entity endpoint (the design's mature form):** the policy emits
`{radial, tangential, weight}` for EVERY token in the observation buffer — a
mini cost function handed to the MPC per hold window. Properties:
- Continuous selection kills the discrete pointer-head problem (targeting = weight
  mass on a token). PPO-native in principle.
- Collapses per-class cost engineering into one general relation vocabulary
  (caveat: some behaviors keep engineered shape — avoidance wants collision-course
  gating, not a radial spring).
- The class design is exactly this design with pooling; K=1 focal anchor is this
  design with one token. They nest.

**Costs of the endpoint, ranked (revised by the infra council):**
1. Not expressible through stock ML-Agents' action path — `ActionSpec` is flat
   fixed-size; `BufferSensor` is input-only. RESOLVED: the custom-trainer-plugin
   seam reaches the action distribution (see Infrastructure below); the cost is
   one custom Actor, not a trainer rewrite.
2. The load-bearing property is **permutation equivariance**, not dimensionality
   — the sample space is the same 3 dims per token either way. One shared
   per-token head computes distribution parameters from token *content*, so slot
   identity is meaningless by construction: no canonical slot assignment, ever,
   and a gradient from any entity trains the one shared skill. Per-slot heads
   (the former "fixed-K bridge") are strictly dominated — deleted from this
   plan. Equivariance does NOT remove the exploration burden of many
   simultaneously *valid* tokens; that is the open action-domain fork below.
3. Endpoint sample efficiency needs output priors (weights default 0, policy
   learns deviations — residual policy learning, see Priors) — which
   reintroduces standing behavior as the *default*, through the floor, not the
   interface. Radial/tangential means also init at zero: ignored-but-wild
   outputs destabilize optimization even when their weight is zero.

**Sequencing (revised by the council; each rung expressible before the next is
funded):**
1. **Anchored K=1 arc — seam + encoder in ONE arc, one retrain.** The former
   rungs 1+2 merged: the seam list had no consumer or test surface until K=1
   lands, so landing it separately was dead capacity. Stabilize the
   anchored-term *semantics* now (frame, relation, weight, sign, normalization);
   do NOT pre-build the list container. Phrase the K=1 head in the endpoint
   vocabulary — `{radial, tangential, weight}` in the anchor frame — so the
   endpoint is an encoder/trainer swap, not an action-semantics change (each
   semantics change is a retrain; never buy two). Fire/boost move to a discrete
   branch at this retrain (hybrid spec confirmed — open item 1).
2. **Care vector — DEMOTED off the critical path** to an optional ablation. A
   gain is admitted only with an observed situation where its optimal value
   differs from 1 (open item 4's discipline is now the entry bar). It is not
   infrastructure for the endpoint.
3. **Per-entity endpoint — funded only through a written entry gate:** (a) an
   observed multi-entity failure the K=1 policy cannot express (e.g. target
   selection among 2+ enemies demonstrably picked wrong), AND (b) the
   actionable-actor population cap resolved from gameplay design (see the
   action-domain fork). Seam and MPC unchanged — encoder + trainer-plugin
   upgrade, not redesign.

At every rung the World anchor survives as the raw-command bypass: if the game
outgrows the vocabulary, the prior degrades into direct control instead of
preventing it.

## Infrastructure: custom trainer plugin (RESOLVED 2026-07-29)

The council split on the trainer path — sidecar cleanrl-style PPO over
`mlagents_envs` (Fable) vs. ML-Agents' official custom-trainer plugin seam
(Sol). Resolved by reading the pinned source (mlagents 1.1.0 in
`training/rl/.venv`): **the plugin seam reaches the action distribution
cleanly; the plugin path wins.** Evidence:

- Plugins register whole `Trainer` classes via the `mlagents.trainer_type`
  entry point (`plugins/trainer_type.py`). `create_policy()` is a trainer
  method and `TorchPolicy` takes `actor_cls` as a ctor parameter — the actor
  owns the entire network and action distribution; nothing upstream knows what
  distribution it uses.
- The `Actor` contract is five methods (`update_normalization`,
  `get_action_and_stats`, `get_stats`, `forward`, `memory_size`). A
  `SimpleActor` subclass swapping `network_body` + `action_model` for the
  entity encoder + shared per-token masked head satisfies all of it. The stock
  `TorchPPOOptimizer` touches the actor only through `get_stats` + generic
  buffer keys — a custom optimizer may not be needed at all.
- **Export is free.** `ModelSerializer` exports `policy.actor.forward` with
  names/shapes derived from `ActionSpec`. Declare the spec as continuous
  `3·Amax + k` (or hybrid with fire/boost discrete) and the trainer, exporter,
  and Unity loader are consistent end-to-end by construction — the entire
  eval/gate/capture estate works unchanged. No hand-built export.
- **Self-play is free.** `trainer_factory.py:121` wraps ANY trainer in
  `GhostTrainer` when self-play settings are present.
- Static shapes only: actor input `[batch, Amax, features]` + validity mask,
  action output flat `[batch, 3·Amax + k]`, batch the sole dynamic axis.
  Dynamic-length action tensors are out of the design (Sentis optimization +
  loader contract both prefer fixed; the fixed cap matches the existing
  BufferSensor padding convention already in production).
- Quirk, load-bearing for masking: `trust_region_policy_loss` computes
  **per-dimension** PPO ratios (elementwise `exp(new−old)`, advantage
  broadcast), not the textbook joint ratio. Padded rows that write identical
  log-probs at sample time and evaluate time get ratio exactly 1 → zero
  gradient, cleanly inert. That invariant — padded rows emit identical
  log-probs in both paths — is a contract to pin with a test.
- Mechanics: registration needs a tiny local pip package (`pip install -e`
  into the venv) exposing the entry point; it lives in `training/rl/` like the
  rest of the harness.

Total custom surface: entry-point stub + thin `PPOTrainer` subclass (~30
lines) + one custom Actor (~200–300 lines, mostly the copied encoder). GAE,
rollout, checkpointing, stats, curriculum, self-play all inherited at the
pinned version. The rejected sidecar would have re-owned all of those plus a
hand-maintained ONNX contract; its residual advantage (full loss ownership,
e.g. CAPS in ~10 lines) does not outweigh that, and CAPS can ride a thin
optimizer subclass if an observed post-anchor dither ever pulls it in.

## Contracts the endpoint cannot retrofit

**Row ↔ entity binding is owned by the observation producer, from the K=1 arc
onward.** The 5 Hz decision snapshot assigns weight to token rows; the MPC
re-resolves at 50 Hz across a hold window in which the scan set churns. The
producer emits one trusted batch per decision — padded token rows + validity
mask + the corresponding live entity refs — and output row *i* applies to the
entity bound to input row *i* for that hold window. Never reconstructed
downstream by sort order, nearest-neighbor, or re-scan (`CLAUDE.md` wiring
rule 6 corollary: the producer owns the location/format its consumers read).
Anchored terms bind to entity identity (`NavigationIntent.target` already
carries a live ref — same semantics), never to token index; otherwise weights
silently migrate between asteroids mid-hold — a failure no trainer metric
surfaces. Token identity is otherwise a three-way cross-language contract
(C# token order → ONNX layout → action unflatten → MPC term binding); one
owner (C# constants) + a pinning test in the
`RLDriverContractEditModeTests` pattern.

Because the head is permutation-equivariant, rows may reorder freely between
decisions — canonical slots and stable cross-decision entity IDs stay
unnecessary unless per-entity recurrence is ever introduced.

## OPEN FORK — action domain: every buffer token vs. authored actor set

The one council disagreement that survives:

- **Every-token (the original endpoint framing, Fable):** emit
  `{radial, tangential, weight}` for all 64 observation-buffer tokens;
  equivariance + zero-weight priors bound *effective* exploration to the few
  tokens the encoder lights up.
- **Authored actor set (Sol, two observed mechanisms):** (a) the 64-token
  buffer contains *asteroids* — the focal enemy lives in the fixed observation
  vector (`AgentObservations`), so "every token" taken literally actionizes
  obstacles while excluding the target; (b) masking fixes slot identity and
  parameter count, not PPO's exploration burden — 64 valid rows is still 192
  sampled continuous dims per decision. Proposal: a small actionable-actor
  set (ships, focal objectives; `Amax` derived from an authored gameplay
  population limit, NOT defaulted to the obstacle cap), with hazards staying
  standing MPC costs — the standing/focal split above, taken at its word.

Leaning: the actor-set reading is the plan's own scaling section applied
consistently, and Fable's red-flag 3 (PPO ratio/entropy statistics varying
with live token count) wounds the every-token variant from the other side.
Decision deferred to the endpoint entry gate, where the population cap must be
resolved anyway; the K=1 arc is identical under both readings.

## Risk-retirement ladder (cheapest first, each retires the next unknown)

1. **Hybrid-ActionSpec smoke** — tiny hybrid behavior, short train → export →
   Sentis load. Source already refutes the rejection claim (open item 1);
   expect PASS in hours. Kill the stale comment with it.
2. **Fixed-cap export smoke** — toy per-token actor at small `Amax`, exported
   through the stock serializer, loaded via the `ComposeInferenceOnly` fixture
   path. Verify torch→ONNX→Sentis parity for zero/one/max valid rows; padded
   rows contribute zero; permuting input rows permutes output rows. Retires
   the whole inference lane before any trainer work exists.
3. **K=1 mechanical rebase, no learning** — drive anchored commands from the
   scripted archetypes; measure churn + tracking error in the trackable
   annulus under 5 Hz decisions / 50 Hz resolution. The behavioral hypothesis
   test.
4. **K=1 stock-PPO retrain against the gate roster** — the funding gate for
   everything after. No care vector, no self-play changes.
5. **Supervised per-token head probe** — teacher targets from the scripted
   archetypes with randomized row order/count/padding; validates set
   semantics, masks, binding, and export before PPO owns all failures.
6. **Plugin-trainer parity run on the K=1 action space** — never debug a new
   trainer and a new action parameterization simultaneously.
7. **Small-N per-entity PPO, then self-play last** — one focal actor plus a
   distractor first; self-play last because a new action representation plus a
   moving opponent makes failures unattributable. Budget one diagnostic here:
   per-token-count PPO ratio/entropy statistics (Fable red-flag 3 — a
   hypothetical; one measurement, no preemptive fix).

## K=1 arc plan (FROZEN 2026-07-31)

Operationalizes ladder rungs 1, 3, 4 (2, 5, 6, 7 are endpoint-side). Grounded in
the 2026-07-31 three-way seam map (command path, MPC cost, obs/action surface).

**User rulings (2026-07-31):**
- **Build first; both lanes open with short smokes.** K=1 and the rules change
  each start as short smoke runs; retrain bundling is decided at launch time by
  what is actually ready (counsel item 9's one-bundled-retrain is the preference
  if both are).
- **Schema riders ride the K=1 break:** enemy primary resource obs
  (lockout/ready + heatPct — exact channels in K1-3's pr-prep; under the full
  lockout rule `ready` is NOT derivable from `heatPct` alone) plus the locked
  self-channel swap. No projectile tokens — that is a bigger break, out.
- **Checkpoint fate: replaces `ShipCombat-699941` on gate pass** — atomic
  branch merge per the locked rules-change merge strategy.
- **Roster stays legacy.** Scripted archetypes keep world-vRef at 5 Hz; anchored
  drive is probe-only until after the run. This preserves the yardstick: same
  rules + same roster ⇒ K=1 candidate gate scores read directly against the
  post-lockout 699941 baseline (unlike a rules change, the 75-point scale keeps
  its meaning across this break).
- **Facing action = `(ox, oy)`:** offset angle around the intercept anchor via
  atan2, magnitude = authority weight — the #219 pattern, gizmo-validated.
- **Weight-0 prior mechanism: deferred to K1-1's pr-prep** (blend-target vs
  dual-term, decided with a cost-shape sketch in hand; note `wMomentum` exists
  unused as a candidate velocity prior).
- **Hybrid fallback: proceed continuous.** If K1-0 fails, fire/boost stay
  threshold-gated continuous — the discrete branch is a rider, not the cargo.

### The K=1 interface

Action space: **5 continuous + 2 discrete branches** (fire, boost; pending K1-0):
- **Facing `(ox, oy)`** — angle = offset around `InterceptYaw` in the enemy
  frame, magnitude = weight. `(0,+1)` = aim at intercept; `(0,−1)` = face away.
- **Velocity `(vr, vt, vw)`** — radial/tangential speeds in the enemy frame,
  normalized to maxSpeed, plus an explicit weight. Sign pin (glossary-bound at
  K1-1): `vr > 0` closes along `+losHat`; `vt > 0` is CCW.

Anchor is **fixed Enemy(intercept)** — no anchor-selection dims.
`{radial, tangential}` spans every direction, so the enemy-polar frame is a
complete basis at K=1; escaping the frame is what weight→0 is for. This keeps
the arc identical under both action-domain readings, as the fork requires.

MPC changes: `CostInput` gains anchored mode + facing offset + `(vr, vt)`;
`facingTarget = InterceptYaw(step) + offset`, replacing today's silent
intercept-overrides-`facingRad` precedence (which dies by construction);
anchored `vRef(step)` resolved per rollout step from the rolled ship pos and
`enemyStates[step]` — the first step-varying reference besides facing;
`MpcWeight.VelTrack` added (not overridable today); the policy path re-enables
enemy state + `projectileSpeed`, waking the dormant enemy rollout.

### Slices

| Slice | Lands | Content / gate |
|---|---|---|
| **K1-0 hybrid smoke** | scratch; comment fix on main | Ladder 1. Short hybrid-spec train → export → Sentis load through `ComposeInferenceOnly`. Kills the stale `AgentActions.cs:22` claim AND `RL_MLAgents_Agent.md`'s doubly stale 4-continuous section. |
| **K1-1 anchored seam + MPC** | **main** (additive, dormant in production) | Intent anchored fields, `CostInput` terms, per-step resolution, weight-0 priors (mechanism per its pr-prep), `MpcWeight.VelTrack`, EditMode cost tests pinning frame/relation/weight/sign/normalization semantics. |
| **K1-2 mechanical rebase probe** | **main** (probe lane; roster untouched) | Ladder 3. Archetype laws driven through the anchored channel in a harness probe config; facing probe (harness slice D) measures churn + tracking in the trackable annulus. **Go/no-go on the behavioral hypothesis.** |
| **K1-3 schema break** | **long-lived branch** | New ActionSpec, `AgentActions` rewrite, obs riders, compose-site consolidation (one shared helper for `ShipAgentFactory`/`InferenceChooser` — today two comment-synced sites), golden-test updates, smoke-fixture regen, training smoke. |
| **K1-4 the run** | branch → atomic merge | Ladder 4: stock-PPO vs the unchanged gate roster, no care vector, no self-play changes. Staged checkpoint; merge replaces 699941 on pass, on explicit user call. |

### Baseline protocol

- Re-baseline 699941 on post-lockout main, **≥4 reps** (±4/75 per-run floor),
  before reading any K=1 score.
- The branch cannot load 699941 (schema); comparison is gate-score vs
  gate-score, valid because rules and roster are unchanged.
- The atomic-merge checklist must hand-stage the gameplay copy: staging into
  `Assets/Settings/AI/Models/` is a **manual copy bound only by a prefab
  GUID** — no script owns it. Scripting it during K1-4 is in-scope (wiring
  rule 6 corollary: producer owns the location consumers read).

### K1-1 decision brief — anchored-seam (FROZEN 2026-07-31)

Prepped via pr-prep; user-ruled. The implementing agent builds from this without
re-deciding. Scope: the K1-1 row of the slice table — intent anchored fields,
`CostInput` anchored terms, per-step resolution, weight-0 priors,
`MpcWeight.VelTrack`, EditMode cost tests. Lands on main, additive, dormant in
production. Non-goals: no action decode (K1-3), no archetype driving (K1-2), no
probe, no `AgentChooser` change, no settings-asset value changes (dormancy =
zeros in `MpcSettings_AgentPilot`).

**Fork rulings:**

1. **Weight-0 prior mechanism: DUAL-TERM.** Per channel: anchored term scaled by
   its authority weight (the shipped #219 pattern) plus an always-on low-weight
   prior. Velocity prior = the existing dormant `MomentumCost`/`wMomentum`
   (verbatim — direction-only cosine vs `initialVel`, speed-gated; stays in its
   ramped state-regularizer home). Facing prior = new
   `FacingCost(yaw − velocityAlignedYaw)` term with new settings field
   `wFacingPrior` (default 0), speed-gated like `MomentumCost`. *Why:* blend-target
   makes w=0 a full-stiffness command to the prior and mid-w a full-force pull to
   an invented midpoint, and it would replace #219's shipped, gizmo-validated
   w-scales-cost semantics; dual-term extends what already ships.
2. **Anchored-weight plumbing: `CostInput` fields, ceiling pattern.** Weights ride
   as data in the anchored block; effective weight = settings ceiling × intent
   weight (`wFacing × fw`, `wVelTrack × vw`). *Why:* the shared `facingOverride`
   array aliasing must not widen (2026-07-31 corrections); `CostInput` exists for
   signature-stable input extension. `MpcWeight.VelTrack` + switch arm still land
   (settings-level tuning arm — a different consumer).
3. **Anchored velocity term: PER-STEP, un-ramped.** The anchored vRef replaces the
   reference inside the existing `VelocityTrackCost` slot (mode-switched, never
   both references at once). *Why:* a step-varying reference is still regulation,
   not reaching; keeps `wVelTrack = 50`'s tuned meaning and the 699941 baseline
   comparison valid. Accepted asymmetry: the velocity *prior* (`MomentumCost`)
   stays ramped — its shipped home; moving it is out of scope.

**Blindsider rulings:**

- **B1 — both `facingRad` and anchored facing set: THROW at `ApplyIntent`.** No
  real non-error case exists (grep-verified: only `AgentChooser` sets `hasFacing`,
  never `aimAtTarget`); explicit user pull. This also covers the old
  `hasFacing`+`aimAtTarget` combo, which today resolves by silent precedence.
- **B2 — anchored intent with `hasTarget == false`: COLLAPSE to prior-only.**
  User-reclassified as a legitimate domain state (future target acquisition will
  produce target-less windows), so the collapse is boundary handling, not a
  guard: anchored terms drop, config-gated priors keep steering, navigator stays
  armed (coasting delegation).
- **B3 — resolved vRef may exceed maxSpeed: NO CLAMP.** Best-effort tracking is
  the honest semantics; normalized cost may exceed 1.
- **B4 — near-zero range: ε-gate to velocity match.** Below ε both polar
  components drop and vRef = `enemyVel`, mirroring `InterceptYaw`'s ε-gate;
  facing already falls to bearing. Stateless.

**Assumptions (user-reviewed):**

- **Relative reading of the enemy frame:**
  `vRef(step) = enemyVel(step) + vr·losHat(step) + vt·tangentHat(step)` — the
  "vr > 0 closes" pin is only unconditionally true relative, and
  Orbiter-vs-moving-enemy requires it. `tangentHat = (losHat.y, −losHat.x)`
  (vt > 0 = CCW around the enemy, consistent with the `fwd = (−sin, cos)` yaw
  convention; positive facing offset likewise CCW from intercept).
- Legacy `aimAtTarget` maps to anchored offset-0 weight-1 at `ApplyIntent` —
  bit-exact (`InterceptYaw + 0f`); the silent precedence at `Cost.cs`
  `EvalContext.Create` is deleted.
- Per-step resolution reuses `EvalContext.Create`'s existing enemy projection
  (`enemyStates[step]` track + linear-extrapolation fallback both already
  handled; rolled ship pos `s.pos` already in hand).
- Navigator feeds enemy state for anchored intents regardless of `aimAtTarget`;
  hitscan (`projectileSpeed ≤ 0`) anchors facing to pure bearing.
- Intent units: vr/vt in m/s (matching `velocityReference`); weights [0,1];
  [−1,1]×maxSpeed normalization is K1-3's decode contract. No clamps in cost
  math — decode clamps (`ToFacingWeight` precedent).
- The anchored block rides as ONE nested struct through `MpcInputs` → `Solve` →
  `CostInput`; the editor `EvaluateBreakdown`/`BuildCostInput` path gets the
  same block, and `CostBreakdown` gains the anchored/prior fields so the mirror
  cannot drift.
- Arming: an anchored-velocity intent arms the navigator (`ShouldIdle`) without
  a legacy `velocityReference`; the legacy reference is unused in anchored
  velocity mode.
- `EvalContext` gains `[StructLayout(Sequential)]` (preventive Burst hygiene).
- Priors are config-gated (`weight > 0`), mode-independent — `wMomentum`'s
  shipped shape; dormancy holds via asset zeros. No speculative
  `MpcWeight.FacingPrior` member (deliberate omission; probe configs set the
  settings field).
- Shared `facingOverride` array untouched; anchored intents leave
  `weightOverrides` empty (test-pinned) so ceiling × weight never double-scales.
- Tests: new `MpcAnchoredIntentEditModeTests.cs` (`Category("MPC")`, headless,
  `MpcVelocityReferenceEditModeTests` pattern). Pins: offset-0 ≡ intercept;
  offset/vt CCW signs; vr closes; per-step re-resolution; weight-0 → prior-only;
  B1 throw; B2 collapse; B4 ε-gate; dormancy (default anchored block ⇒ legacy
  cost path unchanged).
- Glossary same-PR: anchor collision row gains the anchored-intent sense;
  entries for *anchored intent* + the sign pins; add *terminal ramp*.
- Sequencing: no ledger/file overlap with harness slices D/F. The teacher
  fire-gate boundary fix remains a hard gate before K1-4's run, not this PR's
  cargo.

### K1-2 decision brief — mechanical velocity rebase probe (FROZEN 2026-07-31)

Descriptive label: `velrebase`. Prepped via pr-prep; user-approved. The
implementing agent builds from this without re-deciding. Lands on **main**,
additive, probe lane only; production roster untouched.

**Scope:** re-express the scripted archetype *velocity* laws through the K1-1
anchored velocity channel in a measurement-only harness composition, and measure
velocity-command churn + velocity/position tracking against a paired legacy
drive of the same laws. Ladder rung 3 — the go/no-go on the anchoring hypothesis
before the K1-3/K1-4 retrain spend.

**Reframing that scopes the whole slice:** K1-1 already routes archetype
*facing* through the anchored channel (`aimAtTarget` → anchored offset-0 at
`ApplyIntent`), behaviorally matching the pre-K1-1 silent-intercept path. So
K1-2 changes **nothing** about archetype facing; it activates the untested half
of the seam — **anchored velocity** (`anchored.hasVelocity`, no production caller
today). The instrument therefore centers on *velocity*-command churn and
velocity/position tracking, not facing reversals (which read identically across
arms — that identity is a sanity check, not the signal).

**Non-goals:** no policy/ONNX change; no `AgentChooser`/`InferenceChooser`
change; no production `BuildIntent` behavior change (legacy world-vRef path stays
byte-identical); no roster/`OpponentRoster` production-path change; no MPC cost
change (K1-1 seam consumed as-is); no facing metrics as the primary signal; not
K1-3 (action decode) or K1-4 (the run).

**Fork rulings:**

1. **Measurement design — open-loop clean-pairing composition (Fork A).** New
   harness composition: measured ship = archetype-under-test (legacy or anchored
   drive), enemy = a deterministic non-reactive mover (new minimal fixed-path
   chooser — constant-velocity drift or scripted circle; `DummyChooser` is
   zero-velocity and gives radial archetypes no moving bearing). Both arms share
   the same seed ⇒ identical enemy path + identical Evader juke sequence ⇒ clean
   paired delta on the single changed variable (drive frame). *Why not measure
   the existing opponent slot:* the policy agent reacts to the opponent, so
   changing its drive diverges the arms and breaks pairing. New `SessionSpec`
   surface to install an archetype on the measured slot; a tiny command-readout
   on the archetype chooser (last emitted command + monotonic decision count)
   supplies the probe (`IPolicyReadout` lives on `AgentChooser`, not archetypes).
2. **New velocity probe, not a facing-probe reuse (Fork A cont.).** Register a new
   probe (working name `velrebase`) reusing the facing probe's proven machinery
   (decision-counter watch, ceil-rank percentile pooling, JSONL + `-probe.json`
   sidecar). It measures velocity churn from **actual ship kinematics**
   (velocity-heading reversals/s, mean |lateral accel|) + **per-step tracking
   error** against the intended reference, binned by range (3–8 u annulus vs
   < 3 u). *Why kinematics:* legacy (world-vector) and anchored (polar) commands
   live in different spaces and are not directly comparable, and resolving the
   polar at the 5 Hz boundary would falsely re-introduce churn — the point is
   50 Hz resolution. Kinematics are drive-agnostic and honest.
3. **Re-expression mechanism — shared-core extraction (Fork B).** Extract each
   law's LOS-frame polar core to a method returning native
   `(radialSpeed, tangentialSpeed)`; the legacy path packs it to a world
   `velocityReference` byte-identically (production untouched), an anchored-emit
   path packs the same numbers into `AnchoredIntent`. *Why over parallel
   choosers:* the law stays single-sourced (wiring philosophy); parallel choosers
   would fork the law into two copies for a smaller production-file blast radius —
   principle wins.
4. **Scope — Orbiter, HoldRange (Aggressor+Kiter, one class), Evader (Fork B).**
   Dummy excluded (zero-velocity, no law to rebase).
5. **Border dropped on both arms; law math verbatim (Fork C).**
   `BorderTangentSteer` is a world-frame post-process with no anchored equivalent;
   keeping it would confound only the legacy arm. Dropped on both (arena sized so
   it never triggers); production roster keeps `Steered`. Law math kept verbatim
   including Orbiter's centripetal feed-forward — churn is a smooth function of
   range and does not churn, so centripetal barely moves the result, and dropping
   it changes two variables at once. Anchored-arm overshoot (if any) is a finding
   to note, not a confound to pre-remove.
6. **Comparison — paired delta primary; mirror as loose context (Fork D).**
   Per-archetype legacy-vs-anchored delta, same seed / same enemy path, binned by
   range; d2.0 (obstacles cancel in the pairing under identical fields), seeds
   2001–2005 ×3. The 699941 facing baseline is loose context, not a gate
   (different quantity). **PASS:** velocity churn → ~0 for constant-relation
   archetypes in the anchored arm; annulus (3–8 u) tracking error drops toward the
   actuation floor; no improvement required inside 3 u (yaw wall); facing metrics
   unchanged across arms. **FAIL:** churn not reduced, or annulus tracking
   degraded (MPC cannot stably resolve the polar command).

**Blindsider rulings:**

- **Churn metric is kinematics-based + per-step tracking error** (see Fork ruling
  2) — never a delta in the command's native space.
- **Readout decision counter bumps at 5 Hz** — increment inside the
  `tickCounter % RecomputeIntervalTicks == 0` recompute branch, not every
  `Decide`, so the counter matches the decision cadence.
- **`wVelTrack = 50` clone replicated on the measured slot.** `OpponentRoster`
  clones `MpcSettings` with `ScriptedWVelTrack = 50f` so scripted archetypes drive
  the velocity interface tightly; the open-loop composition applies the same on the
  measured ship, both arms, or the velocity-tracking weight differs across arms.
- **One-facing-source throw not tripped.** Aiming archetypes set `aimAtTarget`
  (→ anchored facing offset-0) *and* the probe sets `anchored.hasVelocity`;
  velocity is not a facing source, so the `ApplyIntent` count stays 1 —
  velocity + intercept-aim coexist. Evader sets no facing (nose delegates to the
  `wFacingPrior = 0` prior; Evader's signal is velocity, not nose).
- **Enemy needs a mover.** `DummyChooser` is zero-velocity; a new minimal
  fixed-path chooser (non-reactive) is the trackable target. Optional
  stationary-enemy secondary condition if churn-alone isolation is wanted later
  (removes the anchored arm's automatic enemy-velocity lead).

**Assumptions (user-approved batch):**

- Lands on main, additive; production roster + `BuildIntent` legacy paths
  byte-identical; new probe integrates into the `RL_HARNESS_PROBES` registry +
  paren grammar (`SessionProbes.Factories`, known-keys validated).
- Output under `results/rl-eval/` via `EpisodeJsonl.NewRunPath` +
  `HarnessSessionHost.ProbePath` (per-episode `-{name}.jsonl` +
  `-{name}-probe.json` sidecar), same convention as facing/contact.
- Reps mirror the baseline protocol: seeds 2001–2005 ×3, d2.0.
- Anchored velocity reference is enemy-relative
  (`enemyVel + vr·losHat + vt·tangentHat`, K1-1 `AnchoredVelocityRef`); the
  anchored arm's enemy-velocity lead is kept as legitimate anchored semantics.
- Glossary: register `velrebase` (probe) and the K1-2 sense of *mechanical
  rebase* at brief-freeze if not already covered by the K1-1 anchored entries.

**Sequencing / in-flight collision:** agent-4's live `combat-telemetry` slice
adds a `combat` probe touching the same `ParseProbes` default / `SessionProbes`
registry / lane-smoke / README hunks the new velocity probe touches. K1-2 build
rebases over `combat-telemetry` once it lands (or coordinates the registry hunk);
flag in the ledger claim. No overlap with K1-1 (#243, merged) or harness slices
D/F (merged). Teacher fire-gate boundary fix remains a hard gate before K1-4, not
this PR's cargo. Clear `src/Asteroids3D/Library/BurstCache/` before testing.

### Code-grounding corrections (2026-07-31 seam maps)

- `EvalContext.Create` already runs per rollout step per candidate (~128×17 ≈
  2.2k calls/solve) with a step-indexed enemy rollout (`enemyStates`, propagated
  once per solve via `Model.Step`, constant-control assumption). Per-step
  anchored resolution is existing machinery; the change is *what* resolves.
- `wVelTrack` is absent from the `MpcWeight` override enum — the velocity
  weight channel is gated on adding it.
- Enemy-anchored `vRef(step)` is the first horizon-varying reference besides
  facing; `velocityReference` today is a horizon constant.
- `EnemyTarget` is a per-tick value snapshot, not a live ref; liveness comes
  from `AgentChooser` re-reading `opponent.Kinematics` every `Decide`. The
  archetypes cache the whole intent (incl. the snapshot) at 5 Hz while the
  policy path refreshes per tick — two staleness contracts behind one struct;
  K1-1 should not add a third.
- The shared mutable `facingOverride` array is aliased into every intent
  (`Navigator.SetWeightOverrides` stores the reference) — extending the
  override set at K1-1 must not widen that aliasing hazard.
- The real obs contract is the golden 26-float vector in
  `RLAgentEditModeTests.Fill_LaysOutTheCombatChannels`, not
  `RLDriverContractEditModeTests` (which pins Python log-suffix templates —
  the *pattern* to copy for cross-language pins, not the obs pin itself).
- No YAML change for the schema break — shapes ride the gRPC handshake; the
  only Python-side edit would be `trainer_type` if/when the plugin trainer
  lands (endpoint-side, not K=1).

- **ML-Agents' own attention stack** (`torch_entities/attention.py`:
  `EntityEmbedding` + `ResidualSelfAttention`) — copy verbatim; its operators
  are proven ONNX-exportable and Burst/Sentis-executable *by this project's
  production checkpoint*. One change: tap the per-token embeddings BEFORE the
  pooling that stock ML-Agents applies for its flat head.
- **Entity Gym / RogueNet** (entity-neural-network) — the only OSS precedent
  for per-entity *actions* under PPO; copy the action-schema thinking, masked
  log-prob/entropy bookkeeping, and permutation tests. Zero dependency — the
  framework is effectively unmaintained.
- **AlphaStar / OpenAI Five** — validate attention-over-entities as the
  selection mechanism; weight-mass-on-a-token is the continuous relaxation of
  their pointer heads. Leagues, LSTMs, cluster scale: anti-transferable.
- **Residual policy learning** (Silver 2018 / Johannink 2019) — the
  weights-default-zero prior by name: competent controller as the floor,
  policy learns deviations; justifies bias-init and explains why early
  training looks like MPC-default + perturbations. The residual here is a
  cost contribution, not a motor-action delta.
- **Differentiable MPC** (Amos 2018) — the boundary NOT to cross: same shape
  (learned component emits cost parameters), but backprop-through-the-solver
  is unnecessary — PPO already treats the MPC as plant dynamics. Cited to
  preempt the scope creep.
- **Neural motion planner lineage** (Zeng 2019) — closest published analogue
  of the whole design: network emits a cost volume, classical planner
  consumes it, at different rates. Existence proof + vocabulary only.
- **CleanRL** (Huang, JMLR 2022) — reference PPO implementation for auditing
  the algorithm's details; no longer the trainer skeleton now that the plugin
  path is resolved.
- **CAPS** (Mysore 2021) — the principled anti-dither loss; gated on an
  observed post-anchor smoothness failure; rides a thin optimizer subclass if
  pulled.

## Alternatives considered

- **Raise decision rate** (interval < 10): fixes staleness only, never the yaw
  wall; taxes trainer load linearly (≈ ship count); γ/shaping retune. The anchored
  design makes 5 Hz sufficient — intent changes slowly, geometry is the MPC's.
  (The interval-2–5 frozen-checkpoint diagnostic once kept here is RETIRED:
  the 2026-07-30 gizmo session showed churn scaling with 1/range — bearing-
  sampling, not intrinsic dither. See appendix.)
- **First-order hold** (emit facing rate, +1 dim): fixes staleness, fully learned,
  subsumed by anchoring (an anchored frame IS the correct first-order model).
- **MPC smoothness regularization**: complementary, not competing.
  `MpcSettings_AgentPilot` ships `wSmoothness{Thrust,Strafe,Yaw} = 0` — the
  tracker has zero control-delta damping and `SmoothnessCost` anchors solve-to-
  solve. Sweep under the frozen checkpoint (wFacing-sweep protocol) before any
  retrain-into-it. Risk if retrained-into blind: policies learn to overdrive
  low-passed plants.
- **CAPS-style policy-loss regularization**: the principled anti-dither tool
  (loss, not reward — a prior over the function class, not a task change).
  Under the resolved plugin path it is a thin optimizer subclass, no longer
  deep surgery; still gated on an observed post-anchor smoothness failure.
- **Reward smoothness**: rejected — prescriptive, and the wFacing×15 sweep showed
  this system answers "please behave" pressure by misbehaving differently
  (reversals/s 2.70→3.69).

## Open items

1. **RESOLVED — K1-0 smoke PASSED 2026-07-31.** The hybrid rejection claim was
   refuted in source (2026-07-29: `action_model.py` builds Gaussian +
   MultiCategorical heads whenever both sizes are nonzero; the communicator
   check at `GrpcExtensions.cs:175` only restricts when the trainer lacks
   hybrid support) and the e2e smoke confirmed the full train→export→Sentis
   path: trainer accepted 4-continuous + 2×2-discrete, ONNX exported dual
   action heads, checkpoint drove a 600-decision episode via
   `ComposeInferenceOnly` (artifacts:
   `results/rl-eval/k1-0-hybrid-smoke-2026-07-31/`). The stale
   `AgentActions.cs:22` clause died with it; fire/boost move to discrete
   branches at K1-3. The on-failure fallback ruling is moot.
2. Weight-0 priors: velocity-aligned facing / momentum velocity — confirm
   shapes. **Ruled 2026-07-31: mechanism (blend-target vs dual-term) decided in
   K1-1's pr-prep with a cost-shape sketch in hand.**
3. Velocity authority dim: DEFERRED until an observed failure pulls it —
   aim-vs-mobility knob symmetry is not evidence (`facingWeight` earned its
   place via observed nose-drift; this one hasn't).
4. Care-vector discipline: every exposed gain owes an answer to "what situation
   makes its optimal value differ from 1?" Now the ENTRY BAR for the demoted
   care-vector rung, not just a review question. (Gizmo session 2026-07-30:
   facingWeight itself passes the bar — it modulates in-brawl rather than
   pinning at saturation; see appendix.)
5. Action-domain fork (see OPEN FORK section): resolve at the endpoint entry
   gate, alongside the actor-population cap.

## Empirical appendix (OBSERVED 2026-07-30 — Policy gizmo session, PR #222)

- [x] `facingWeight` during a brawl: **genuinely modulates** — the authority
      channel is used, not pinned. The delegation mechanism is live and
      learnable; the "gain nothing rewards pins at saturation" failure did NOT
      materialize for facingWeight. The care-vector demotion stands on the
      council's grounds (not endpoint infrastructure; per-gain entry bar), not
      on a saturation observation.
- [x] Churn vs range: **churn decreases as range increases** — consistent with
      1/range bearing-rate scaling. The staleness mechanism is CONFIRMED: churn
      is the policy re-sampling a moving bearing at 5 Hz, not intrinsic dither.
      Anchoring attacks the right disease.
- [x] Hold-window step-through: **nose is slow to react to a new facing order,
      then slews monotonically** — a healthy saturated tracker rate-limited by
      the yaw-slew budget (36°/decision vs 48°/decision commands), no
      intra-window hunting. No MPC chatter: the smoothness sweep stays
      complementary/deprioritized, not a pre-retrain blocker. The reaction lag
      is the slew wall itself, which anchoring removes by moving tracking into
      the MPC's 50 Hz loop.
