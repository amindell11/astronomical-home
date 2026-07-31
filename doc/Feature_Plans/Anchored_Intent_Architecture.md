# Anchored Intent Architecture — policy↔MPC facing & velocity interface

**STATUS: DESIGN EXPLORATION (2026-07-29). No implementation, no PR slices yet.**
Consumed by the combat rules-change retrain bundle (see the 2026-07-28 rules-change
handoff memory). Origin: the facing-thrash investigation, live-eyeballed with the
Policy debug gizmos (PR #222). Empirical appendix below is PENDING the gizmo
observations.

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
machinery, not per-entity intent dims** — `wObstacle` already composes 64 anchored
avoidance terms per rollout step without the policy naming any of them.

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

## Priors (the shortlist worth reading, one line each on what transfers)

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
  Cheap diagnostic kept: run the FROZEN checkpoint at interval 2–5 — smooth
  commands ⇒ the obs→action map is smooth and churn was bearing-sampling; jitter
  ⇒ intrinsic dither.
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

1. **Hybrid ActionSpec claim REFUTED in source (2026-07-29); e2e smoke pending.**
   Both council legs independently verified it: the pinned trainer's
   `action_model.py` builds Gaussian + MultiCategorical heads whenever both
   sizes are nonzero, and the communicator's capability check
   (`GrpcExtensions.cs:175`) only restricts when the trainer lacks hybrid
   support. The `AgentActions.cs:22` doc comment is stale and is actively
   shaping the action space (fire/boost as threshold-gated continuous). Ladder
   step 1 (smoke) confirms the full train→export→Sentis path; the comment dies
   with it and fire/boost move to a discrete branch at the K=1 retrain.
2. Weight-0 priors: velocity-aligned facing / momentum velocity — confirm shapes.
3. Velocity authority dim: DEFERRED until an observed failure pulls it —
   aim-vs-mobility knob symmetry is not evidence (`facingWeight` earned its
   place via observed nose-drift; this one hasn't).
4. Care-vector discipline: every exposed gain owes an answer to "what situation
   makes its optimal value differ from 1?" — a gain nothing rewards will pin at
   saturation (the facingWeight lesson, pending confirmation below). Now the
   ENTRY BAR for the demoted care-vector rung, not just a review question.
5. Action-domain fork (see OPEN FORK section): resolve at the endpoint entry
   gate, alongside the actor-population cap.

## Empirical appendix (PENDING — Policy gizmo session, PR #222)

- [ ] `facingWeight` label during a brawl: pinned ≥ ~0.9 (authority channel
      unused ⇒ ranked-alternative 2 confirmed dead) or genuinely modulating?
- [ ] Churn number at ~20 u vs ~3 u: does churn scale as 1/range?
- [ ] Pause/step through one 200 ms hold window: nose slews monotonically toward
      the frozen command (healthy saturated tracker) or hunts/overshoots (MPC
      chatter — would elevate the smoothness-sweep priority)?
