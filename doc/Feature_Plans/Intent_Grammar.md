# Intent Grammar — policy-authored weighted relational objectives

> STATUS: RATIFIED design (2026-08-11); grammar + normalization contract FROZEN at Stage B
> (2026-08-13). Stage A verdicts: §Stage A verdict table (11 composes · 1 needs-ray-geometry
> · 1 red flag); Stage B rulings (VEL keep + θ-head, LANE slot, cover-take out of grammar,
> POS width amendment): §Stage B freeze. Next: Stage C — the schema-break window.

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
| VEL | instance | velocity direction | weight (non-negative — §Stage B freeze), direction θ | referent, frame |
| LANE (enters at Stage C — §Stage B freeze) | instance | position ray along referent's facing | signed weight | referent |
| FIELD | class | hazard repulsion | weight | — |
| FIRE | — | engage (Fire_Lane_Rework) | — | primary, secondary |

≈9 continuous + ~8 small discrete branches with LANE (vs today's 5+2). Legacy-equivalence: pin all
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

- **Intent gizmos** (native drawers on the intent's own components): referent rings, frame-resolved POS points riding their
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
  **DONE 2026-08-13 — §Stage B freeze.**
- **Stage C — the schema-break window.** Obs additions (missiles, K-hostile/asteroid slots,
  fire-lane geometry) + the action head above + retrain, Bench-1-gated per the seam plan's
  standing rule. The action head inherits the §Stage B freeze rulings: LANE slot, VEL θ-head
  with the conversion warm start, the POS width form. Candidate co-riders for the same window: #377 (asteroid lobes),
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
   **RULED at Stage B (2026-08-13): keep — the falsifier did not fire; form and
   consequences in §Stage B freeze.**
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

**Integration branch (user-directed 2026-08-12): Stage A lands on `mpc-trunk`, not main.**
The trunk is cut from main `85b7b5e8`; A1 (#399) merged into it. Every remaining Stage A
slice does the same: prepare the slot from `origin/mpc-trunk`, base the PR on `mpc-trunk`,
and merge via the pool gate with the explicit base (`merge <slot> origin/mpc-trunk`).
Readiness gates that read "A1 merged" verify content on `mpc-trunk`, not main. The trunk
lands on main as one gate-tested merge at Stage A close, alongside the verdict-table docs
commit. A1's bit-exactness evidence is unaffected (trunk tip = the main tip A1 was proven
against).
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
   in `results/mpc-rig/bingo/NOTES.md`, frozen into this doc at close. Intent gizmos are OUT of
   Stage A; their natural home is Stage C when live sentences flow in production.
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

## Stage A verdict table (frozen 2026-08-13)

The #395 bingo run, executed on `mpc-trunk` head `9bd2c9d7` (rig: all 13 rows + 3 VEL-zeroed
arms, seed 1234, 9/9 incl. the 16-variant same-seed replay proof; sessions: six rows via the
A3 lane, controller probe, canonical eval env). Verdicts are the user's rulings, made on the
trace/plot/session evidence; run detail lived in `results/mpc-rig/bingo/NOTES.md` (retained
with the trajectory panel; per-variant traces deleted per #303).

| # | Row | Evidence (one line) | Verdict |
|---|-----|---------------------|---------|
| 1 | orbit | +229° sweep holding 40±2 m with no POS ring; session 3× full-time draws, near-untouched | composes |
| 2 | kite | backs 55→115 m facing a pursuer; session survives 42–64 s unarmed vs a firing Aggressor | composes |
| 3 | cover-take | reaches the authored point (1.6 m) — but the author computed the cover projection; the single-referent session lane cannot say "cover" at all (2/3 out-of-bounds retreat) | **RED FLAG — intention-shaped** |
| 4 | fire-lane dodge | point-repel exits the lane in ~2 s but can only say "flee this point", never "stay just off the ray"; in session the off-nose point rides a tracking shooter's live nose | **needs new geometry (ray)** |
| 5 | missile-drag | flees a pursuing missile 389 m through the rock triplet at 2.0 m clearance | composes |
| 6 | herd-toward-asteroid | station-holds the herding post on a moving enemy (world-frozen offset θ — frame evidence below) | composes |
| 7 | mine-retreat | retreats 12→82 m from the mine while holding enemy facing at 7.1° mean | composes |
| 8 | shoot-the-rock | non-ship AIM referent binds; settles on the 18 m ring facing the rock | composes |
| 9 | two-hostile lanes | circulates hostile-1 while the repel term clears crossing hostile-2's sweeping lane | composes |
| 10 | wingman-hold | rides a moving ally's wing 0.7 m off station for 136 m | composes |
| 11 | minefield-transit | 90 m transit through 8 mines at 2.7 m clearance — required posWidth 60 vs asset 10 | composes |
| 12 | Dummy closeout | rig closes 55→7.1 m; session **14W/1L/0D, 29.4 s mean, zero timeouts** | composes |
| 13 | drift hold | armed-all-zero: zero motion, zero churn, solver live; session idles 120 s untouched | composes |

**Fork 1 (VEL) falsifier: does NOT fire.** All three movement rows degrade hard VEL-zeroed —
orbit produces no motion at all, kite is caught (range 55→0.1, churn 19.75 rev/s), missile-drag
never flees. Stage B's ruling input is an unambiguous keep.

**Red-flag disposition (cover-take).** Ruled intention-shaped: "take cover" is not reachable by
slot-geometry growth alone — the cover *projection* (enemy×occluder→point) is the intention.
Grammar consequence for Stage B: either a proximity/occlusion potential enters as a class-term
question (rule 3 review against the hazards cap) or the tactic is accepted as living outside
the movement grammar. Not silently patchable by authoring.

**Ray geometry (fire-lane dodge).** The lane is a ray; a point stand-in cannot normalize to
"just off the lane". Ray/line slot geometry is the structural growth Stage B should scope
(the schema table already reserves geometry as fixed-per-slot — this adds a geometry kind,
not a policy choice).

**Stage B normalization evidence (recorded, per the brief's blindsider).** Three POS-led rows
disagreed with the asset posWidth 10 (minefield-transit 60, cover-take 20, wingman-hold 5):
saturation err²/(err²+w²) goes gradient-flat past ~3w, so far-field reach and tight settle
cannot share one width. Candidate remains setpoint-relative normalization. Related note: the
closing-VEL term biases the closeout equilibrium inside the POS setpoint (7.1 m vs 12) —
composition arithmetic to keep in mind when authoring, not a defect.

**FIELD channel unexercised.** Threat steps 0.0% and applied-step obstacle cost 0 in every rig
row — rollout pruning kept applied paths clear, so FIELD's differential authority was never
tested by this card. A Stage B/C observation, not a Stage A failure: the card's obstacle rows
prove clearance behavior, not FIELD-weight sensitivity.

**Probe-2 bench-debt partial payment (as briefed).** Bench-1's production couple scored Dummy
6.5W/15, every non-win a timeout. The settled solver playing a clean hand sentence: 14W/1L/0D,
mean 29.4 s, zero timeouts (sole loss an out-of-bounds exit at full HP). The closeout deficit
is a property of what the policy asks for, not of the solver's ability to close. All six
session rows also ran at hull-scale churn (strict 2.6–3.5 rev/s; drift-hold 0.06) vs the
couple's 11.5 — the churn pathology does not reproduce under clean sentences.

## Stage B freeze (2026-08-13)

Rulings made by the user on the §Stage A verdict table evidence. The grammar's syntax axes
are unchanged — every amendment below is vocabulary or contract, as the closed-grammar claim
requires.

1. **VEL: KEEP — fork 1 closed.** The falsifier did not fire: all three movement rows
   collapse VEL-zeroed (orbit motionless, kite caught at 19.75 rev/s churn, missile-drag
   never flees). Form: the Stage C action head emits **weight + direction θ** (a sin/cos
   pair — radial/tangential with the magnitude divided out). Rationale: the hand-authored
   card used vector magnitude as a second authority dial (orbit tangential 8, missile-drag
   radial −12, slot weights pinned at 1) — a weight/magnitude confound that is admission
   rule 2's action-space degeneracy living inside a slot; normalizing makes the slot weight
   the sole authority channel, uniform with AIM/POS, and keeps rule 6 honest (one speed
   reference, equal weights mean comparable force). Consequences, accepted: a speed setpoint
   stops being sayable (the speed reference becomes a solver/character-axis constant; POS
   setpoint + weight trade-offs covered the card's station-keeping needs); bit-exact legacy
   equivalence dies at the action head (Stage C is a declared schema break); the warm start
   survives as the mechanical conversion direction = normalize(r, t), weight ≈ ‖(r, t)‖·w.
   Until Stage C the solver-side carrier stays radial/tangential as built. Footnote, valid
   under either form: VEL's weight is effectively non-negative — repel-from-θ is
   attract-to-θ+180°, so sign lives in the geometry; the schema table now says so.
2. **LANE: a ray-carrying instance slot enters the starter schema** (fire-lane dodge's
   verdict). Position-cost on a ray along the bound referent's facing, signed weight —
   repel = "stay off this referent's lane", attract = hold it. Vocabulary growth only: ray
   was already in the geometry axis; this names its carrier. Built at Stage C with the
   action head. The reserved hostile-fire class term is untouched — by the timescale rule
   it remains the ambient many-lane channel for teams content; LANE is the focused
   one-shooter dodge.
3. **Cover-take: OUT OF GRAMMAR.** The red flag stands as ruled — the cover projection
   (enemy×occluder→point) is the intention, and no slot-geometry growth reaches it. "Take
   cover" joins team coordination outside the movement grammar's jurisdiction. Named
   reopening path (the only one): an admission-rule-3 class-term review for a
   proximity/occlusion potential — a fourth class term, hence a design event under the
   frozen cap. Two-referent pair frames (which would make the projection a frame choice, and
   would also cure the world-frozen θ below) were considered at the freeze and not adopted.
4. **POS normalization: AMENDED on paper.** The fixed-width saturation err²/(err²+posWidth²)
   is gradient-flat past ~3·posWidth, so far-field reach and tight settle cannot share one
   width (minefield-transit required 60, wingman-hold 5, vs the asset's 10). The frozen
   rule-6 contract for POS: **the width scales with the authored geometry** —
   setpoint/offset-relative is the named candidate, and the exact form is Stage C design
   (the be-at-point case has setpoint 0, so pure setpoint-relative is insufficient as
   written). Implementation and the per-term contract test land with Stage C; the
   production asset stays untouched until then.

Recorded evidence carried forward, no grammar change: FIELD's differential authority went
unexercised by the card (0 threat steps everywhere) — Stage C owes it a direct test before
trusting FIELD weights; the Position-frame offset θ is world-frozen at authoring (herd row)
— staleness family, lives with the pair-frame non-adoption above.

Glossary rows landed with this freeze per §Forks 3: the "intent grammar" canonical row, the
"term" collision row, and sentence slots joining the "slot" collision row ("intent sentence"
landed with A1).
