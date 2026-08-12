# Intent Grammar — policy-authored weighted relational objectives

> STATUS: RATIFIED design (2026-08-11) — all forks ruled (§Forks). Stage A briefed + carded
> (§Stage A decision brief, frozen 2026-08-12): the solver limit-cycle sequencing gate is
> SATISFIED (Probe 2 resolved, #389 merged with settled-loop pins green); slices on the
> tracker #391–#395.

The policy's action stops being a pre-blended movement answer and becomes an **intent sentence**:
a small set of weighted relational cost terms the MPC re-solves at
50 Hz with fresh referent state. Strategy holds for one decision (~200 ms); execution of the
strategy is re-derived every physics step. Today's anchored intent is the one-referent,
two-term corner case of this design.

Relation to standing docs: extends `Pilot_Decision_Seam.md` on the decision axis (objective
terms cross the seam; the character axis — regularization/safety in `MpcSettings` — is
untouched, and the `WeightOverride[]` deletion stands: that channel overrode character weights,
this one authors objective terms). Inherits `Fire_Lane_Rework.md`'s engage bools unchanged.
Subsumes and retires three earlier candidates: the anchor selector, the cost-shape lane, and
the terminal-cost prototype's ambition.

## Design principle

The 5 Hz policy cannot be reactive and must not be bypassed. Resolution: **the policy chooses
the feedback law, the solver runs it.** Two cliffs bound the design:

- **Prescriptive cliff**: terms that encode intentions (dodge, kite, take cover) make the
  policy a mixing board over authored tactics. Test: a term nameable with an intention verb is
  a composition, not a primitive — reject it.
- **Staleness cliff**: policy-authored world-frame quantities die as the world moves (the K=0
  lesson). Rule: **every policy-authored free parameter is expressed in a frame the solver
  keeps live at 50 Hz** (the K=1 anchoring lesson, generalized).

Creativity lives in composition: which referent each primitive binds, where in that referent's
frame the free parameters sit, and the weights trading primitives off. Open-ended sentences
over a closed grammar.

## The grammar (closed — syntax never grows, only vocabulary)

A term is a point in: **space** (position-cost | velocity-cost | facing-cost) ×
**sign** (the sign of its continuous weight — attract/repel is not a discrete choice) ×
**geometry** (point | ray | radius-setpoint; fixed per slot, not policy-chosen) ×
**frame/referent** (whose live frame the geometry lives in: position / facing / velocity frame
of a bound referent).

Two slot kinds, sorted by a **timescale rule** — entities the policy focuses on become
instance referents; populations it is ambiently aware of become class terms; anything living
below the decision period (dumb bullets) never reaches the policy at all:

- **Instance slots** bind one referent each, chosen per decision from the observation slots
  (you can only bind what you can see, by construction). Binding is **bind-then-hold**: the
  discrete action names an obs slot, the harness resolves it to entity identity at decision
  time (the ShipId pattern from the seam arc), and the solver holds that identity — obs-slot
  reshuffling never retargets a held intent.
- **Class slots** have no referent; the solver resolves the membership itself each solve
  (e.g., ObstacleField). They exist solely as **perception delegation** — the solver sees
  what the policy cannot (cardinality, precision) — never to encode intention.

**Referent invalidation (uniform, all entity types):** a held referent that despawns drops its
term's weight to zero until the next decision. Defined behavior, not an error.

## Term admission rules

1. **Geometry, not intention.** Fails the intention-verb test → out.
2. **Non-composable.** Expressible via existing primitives + referent/frame choice → out
   (redundancy is action-space degeneracy, not a convenience).
3. **Class terms are perception delegation only**, admitted when the policy demonstrably needs
   *differential* control over that class's weight. Per-type pricing that is not a tactical
   choice belongs on the character axis. Cap: exactly three class terms for the foreseeable
   roadmap (hazards, hostile-fire, incoming-threats); a fourth is a design event.
4. **Cheap in-rollout**: analytic, a few flops per candidate-step inside the Burst job.
5. **Obs-grounded**: a weight the policy cannot ground in observations is a noise channel; a
   term enters the menu in the same schema window as its observational support.
6. **Normalization contract**: each term documents its 0–1-ish per-step normalization so equal
   weights mean comparable force. Unit discipline is a known failure mode here (the 100×
   SmoothnessCost bug) — the contract is tested per term, not asserted.

## Starter schema (typed slots — the generic-slot endpoint is deferred)

| Slot | Kind | Type (fixed) | Continuous outputs | Discrete outputs |
|---|---|---|---|---|
| AIM | instance | facing | signed weight, angular offset φ | referent |
| POS | instance | position point | signed weight, offset (r, θ), setpoint | referent, frame |
| VEL | instance | velocity direction | signed weight, direction θ | referent, frame |
| FIELD | class | hazard repulsion | weight | — |
| FIRE | — | engage (Fire_Lane_Rework) | — | primary, secondary |

≈8 continuous + ~7 small discrete branches (vs today's 5+2). Legacy-equivalence: pin all
referents to *enemy* and frames to *position-frame* and the schema degenerates to
approximately today's anchored intent — the warm-start/curriculum point (pin early, release
late; scheduling depends on trainer-runtime maturity).

Growth story under roadmap content: teams → hostiles/allies join the referent vocabulary
(nearest-K obs slots) + a hostile-fire-lane class term; new obstacle types → join
ObstacleField under FIELD, splitting only past admission rule 3; missiles → instance referents
(above decision timescale) + incoming-threat class membership; mines → the easiest instance
referents in the game. The sentence structure is intended to survive all of it unchanged —
that claim is falsified on paper via the bingo card before anything is built. Out of
jurisdiction: team *coordination* (focus assignment, formations) — a layer above per-ship
intent; encoding it as terms is exactly what rule 1 rejects.

## Debug surfaces (the design's second product)

The blend happens outside the network, so every decision is a legible typed sentence:

- **IntentPainter** (painter system): referent rings, frame-resolved POS points riding their
  referents live, weights as thickness/alpha — the 5 Hz strategy layer and 50 Hz tracking
  layer visually distinct in-world.
- **Sentence probe** (`SessionProbes`): per-decision CSV → weight-vs-threat correlations,
  referent-switch rates, weight-entropy saturation checks.
- **Rig replay**: logged sentence streams replay open-loop against the deterministic solver
  rig; per-term cost curves along the rollout arrive pre-diagnosed (a stall shows *which*
  term trapped it).

Caveat: sentence-level ≠ strategy-level interpretability, and the policy may learn degenerate
dialects (opposed terms fighting to a net force). Signed weights reduce the temptation;
weight-entropy probes surface it; mild action regularization is the lever if legibility decays.

## Staging

- **Stage A — policy-free, on the solver rig + scripted archetype sessions.** Implement the
  starter terms + normalization contract; hand-author weight vectors; run **tactic bingo**:
  for each candidate tactic, write it as a sentence and watch it. Outcomes per row: composes
  (grammar wins) / needs a new frame or geometry (grammar grows structurally — cheap) / needs
  an intention-shaped term (red flag — real gap, or the tactic lives outside movement/fire).
  Zero schema risk, zero retrain. Bingo card: orbit · kite · cover-take · fire-lane dodge ·
  missile-drag through rocks · herd-toward-asteroid · mine-retreat · shoot-the-rock ·
  two-hostile lane avoidance (teams row) · wingman-relative hold (ally row) · minefield
  transit (mines row) · Dummy closeout (the retune pass's live success criterion — finish a
  stationary target, no timeout) · drift hold ("nothing matters" sentence, all weights ≈ 0 —
  exercises the unpinned weight head). Protocol note: the movement rows (orbit, kite,
  missile-drag) run VEL-zeroed vs VEL-live — fork 1's falsifier. A brawl row (zero-range
  hold) was considered and REJECTED: zero-range dominance is a rules artifact the combat-rules
  work is designing out, not a target tactic.
  Optional expressiveness capstone: express the roster archetypes as static weight presets
  (replacing ArchetypeLaws with presets would be a separate bench-gated env shift — the
  roster is a trained interface; not this arc's spine).
- **Stage B — freeze.** Grammar + contract frozen in this doc; the ratified coin's glossary
  rows land (with the "term" collision row and the "slot" row extension — §Forks 3); fork 1's
  VEL falsifier converted into a final keep-or-drop ruling on the bingo evidence.
- **Stage C — the schema-break window.** Obs additions (missiles, K-hostile/asteroid slots,
  fire-lane geometry) + the action head above + retrain, Bench-1-gated per the seam plan's
  standing rule. Candidate co-riders for the same window: #377 (asteroid lobes),
  event-triggered decisions (only if trainer-runtime owns time-aware discounting by then).

## Forks

Settled in the design discussion (2026-08-11): typed slots before generic slots (exploration
tarpit otherwise; bingo card decides the slot roster) · attract/repel as weight sign, not a
discrete branch · geometry fixed per slot · bind-then-hold referent semantics · FIELD stays
a single hazard class until rule 3 forces a split.

Ruled at review (2026-08-11, same day):

1. **VEL: provisional keep; final ruling at Stage B on bingo evidence.** Clean break kills
   legacy-equivalence (today's schema includes the velocity reference — the sole warm-start
   hedge for the Stage C break), but VEL's executor changes under the solver fix, so ruling
   its long-term fate now is premature. Falsifier on the card: if no movement row degrades
   VEL-zeroed, VEL drops at Stage C and the legacy warm start is abandoned knowingly.
2. **Weight head: bounded raw signed weights, no pinned term — the character axis is the
   numéraire.** Softmax is positive-only, so signed weights (already settled as the
   attract/repel mechanism) would force a sign channel or paired terms back in; sum-to-1
   makes drift unsayable; rule 6's normalization contract already delivers comparable
   scales. Pinning a term would make its weight unlearnable — the fixed character-axis
   scale is ruler enough.
3. **Coin RATIFIED: "intent sentence" / "intent grammar".** Extends the anchored-intent
   lineage; the metaphor system (vocabulary/syntax/composition/dialects) is load-bearing.
   Glossary rows enter at Stage B per convention, together with a new "term" collision row
   (objectives' activation term vs intent/cost term) and instance/class slots joining the
   "slot" collision row.
4. **Class-term cap = exactly 3** (hazards, hostile-fire, incoming-threats); a fourth is a
   design event — a rule-3 review, not a bigger cap. Bingo card as amended in §Staging
   (+Dummy closeout, +drift hold, −brawl, VEL-zeroed protocol note).

## Stage A decision brief (frozen 2026-08-12)

Slice breakdown lives on the tracker (the tracker owns it; this doc carries the decisions):
umbrella #391 · A1 #392 (sentence carrier + terms) · A2 #393 (rig generalization) · A3 #394
(session brain + lane) · #395 (bingo run, an investigation not a PR). Sequencing:
A1 → (A2 ∥ A3) → #395 → closing docs commit landing the verdict table here. Entry gate:
Probe 2's convergence gates passed (#389, settled-loop pins); the owed roster bench is not a
Stage A dependency (policy-free), and #395's Dummy-closeout session row partially pays it.
Vocabulary in this brief: "term" = intent/cost term (never the objectives system's activation
term); "sentence slot" = a typed sentence position (never a worktree slot).

Forks, as ruled:

1. **Carrier: unify now (U).** The legacy anchored channel becomes the degenerate sentence at
   the evaluation layer — one cost path; legacy-equivalence becomes a tested property. The
   parallel additive block was rejected as the coordinator-bypass shape wiring rule #6
   prohibits, deferring a known reconciliation into the Stage C schema-break window.
   Bit-exactness (same-seed rig trace diff, base commit vs branch) is A1's merge evidence;
   any exactness break = stop and reclassify (env-shift question), never threshold
   negotiation. Sessions stay single-referent + FIELD; multi-referent resolution is deferred
   (the rig resolves synthetically; the commander's one-anchor seam is untouched).
2. **Term shapes.** VEL keeps radial/tangential (forced by bit-exactness under U; the schema
   table's direction-θ form is a Stage C action-head question, ruled with fork 1 at Stage B).
   POS = a point at polar offset (r, θ) in the referent's chosen frame; cost grows with
   (distance-to-point − setpoint)², so setpoint 0 = be-at-point and r₀ = hold-ring
   (radius-setpoint geometry as a continuous parameter); normalized by saturation
   err²/(err² + posWidth²) with `posWidth` on the character axis (`facingWidth` precedent);
   rides the terminal ramp. FIELD's policy weight scales the TurnAwayCost branch only;
   `collisionPenalty` stays character-axis and un-zeroable (drift-hold = no hazard shaping,
   still no suicide channel). Known consequence, accepted: FIELD-over-TurnAwayCost makes
   negative-weight hazard *attraction* semantically dead — if cover-take reads "needs new
   geometry" (a proximity potential), that is the card working, not a defect. AIM generalizes
   the existing Aim()/FacingCost with signed weight × offset; maps bit-exactly.
3. **Substrate: rig-first for all 13 rows; zero paper-only.** A rig referent is a kinematics
   stream, so roadmap content (missiles, mines, allies, second hostiles) costs nothing to
   stand up. Six rows get archetype-session confirmation (orbit, kite, cover-take, fire-lane
   dodge, Dummy closeout, drift hold); multi-referent and non-ship-referent rows are rig-only.
   Obstacles enter the rig through the production ConvertObstacles path (the conversion is the
   producer's contract) with a real threat classifier for the sampler; referent motion =
   closed-form scripted laws (stimulus, not opponent AI); sentences are authored in the
   scenario rows.
4. **Artifacts.** Rows are code-authored `[Explicit]` cases (MPC_RIG_EMIT precedent — no merge-
   gate load); the permanent suite gains only the per-term normalization-contract tests and
   the equivalence pins. Each row emits RigResult reads (recorded, not asserted), a per-tick
   trace extended with per-term cost-breakdown columns (the "which term trapped it"
   diagnostic), and a plot from one committed script (the rig CSVs previously had no reader).
   Verdict = a human ruling per row (composes / needs new frame or geometry / red flag), live
   in `results/mpc-rig/bingo/NOTES.md`, frozen into this doc at close. IntentPainter is OUT of
   Stage A; its natural home is Stage C when live sentences flow in production.
5. **Slicing** as carded above; A3 may fold into A2 at build time if it stays small.

Blindsider resolutions:

- **Absent sentence ≠ all-zero sentence.** The legacy mapping sets FIELD authority = 1
  (today's character-ceiling shaping) with POS at 0; drift-hold *explicitly* authors all
  weights ≈ 0 — that distinction is the row's point. `NavObjective.IsIdle` generalizes to "no
  armed sentence slot" in A1 (POS-only/FIELD-only objectives must solve, not reset). Hand
  vectors stay conventionally in [−1, 1]; learned bounds are Stage C's question.
- **Referent kinematics:** enemy-bound slots keep the rolled prediction stream (bit-exactness);
  synthetic referents are per-slot (pos, vel, yaw) snapshots linearly extrapolated in-rollout —
  the existing fallback path, ≤3 distinct referents by construction. Rolled streams per
  synthetic referent rejected as fidelity theater.
- **posWidth per row:** rig rows clone MpcSettings in-memory (never the asset file); the asset
  gets one default for sessions; persistent per-row disagreement is recorded Stage B evidence
  (candidate: setpoint-relative normalization), not silently tuned around.

Assumptions (user-ratified): new terms as `Cost/Terms/` files extending the fixed Burst menu;
new inputs ride CostInput, `Cost.Evaluate`'s signature stays stable; weights are bounded raw
signed per §Forks 2 and **ceiling-relative** (they multiply wFacing/wVelTrack/wObstacle; a new
wPos ceiling is chosen for comparability — the rule-6 contract governs each term's 0–1
envelope, ceilings stay the numéraire); zero/absent sentence ⇒ bit-identical current behavior;
the production MpcSettings asset and every roster/trained surface stay untouched; hand-authored
vectors live only in scenario/brain code; FIRE is inherited unchanged (Fire_Lane_Rework owns
that lane in a parallel session); carrier structs follow the standing Burst hygiene
(Sequential layout, fixed size); golden traces are emitted at the base commit and diffed in
the PR, never committed; bingo artifacts under `results/mpc-rig/bingo/` with #303 deletion;
the session lane mirrors the open-loop lane pattern; coinages get inline first-use definitions
now and glossary rows at Stage B (earlier only if A1 code makes one load-bearing).
