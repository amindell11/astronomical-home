# Weapon strategy integration & balance phase — council principles

**Status: PLANNING ARTIFACT (2026-07-22).** Output of a four-seat adversarial design
council (2× codex gpt-5.6, 2× fable, two rounds: position + cross-rebuttal) with
value charters: RL learnability/training economics (codex-A), engineering minimalism
(codex-B), game-design depth/player feel (fable-A), balance rigor/exploit-hunting
(fable-B). Convened after a code recon of the firing path, ship-stat surfaces, and RL
interface. **Implementation starts after the 1v1 lasers-only self-play run lands** —
weapons change the obs vector, so nothing here carries a checkpoint-compatibility
constraint. Balance customer: **game-first; the RL/eval stack is the measurement
instrument** (Combat Depth tension web is the target).

Scope: the six existing weapons + firing interface; real chassis archetypes; the
deferred weapon catalog (flares, Sparrows, mines, system-disable, damage typing) as
things the principles must anticipate. Keystones/passives out of scope but must not
be painted into a corner.

---

## A. Consensus principles (unanimous or 4/4 post-rebuttal)

1. **The trigger is a decision, not a permission.** The acting policy decides the
   firing instant; no subsystem may veto or substitute for it. The legacy path
   (`NavigationIntent.enableFiring` → Gunner envelope auto-fire via
   `Gunsight.Evaluate`/`ShouldFire`) dies as a firing *authority*. Everything that
   makes weapon families distinct — burst discipline, charge release, shot economy,
   hold-through-lock — is a timing decision on a trigger.

2. **One actuator seam, every producer.** Player, learned agent, and scripted
   teachers all submit per-slot `WeaponCommand {held, pressed}` through
   `WeaponsController`; each weapon keeps interpreting its own semantics
   (auto/semi/charge). No pilot gets a private fire path. Teachers may keep
   envelope-based *decision logic* ("fire while envelope-valid") but express it as
   trigger commands through the same seam. Replacement ends in deletion — Gunner
   firing authority, `enableFiring`, and envelope-driven charge release removed in
   the same arc, before mixed-weapon training. *(Condition from fable-B, adopted:
   pinned eval baselines were minted against auto-fire teachers; the teacher
   migration must verify behavioral equivalence — or re-mint baselines — before the
   legacy path is deleted, so the stage-one A/B comparison stays valid.)*

3. **Action surface: all-continuous, per-slot triggers, selection is emergent.**
   `[vx, vy, boost, trigger_slot0, trigger_slot1]` — each trigger float
   threshold-maps to `held`; `pressed` is derived at the actuator boundary on the
   rising edge. No discrete weapon-select action (dodges the verified ML-Agents
   4.0.3 hybrid-spec rejection); "selection" = firing neither, either, or both.
   No pulse channel, no selector service, no mount router.

4. **Aim service informs; it never fires.** Intercept/ballistics math survives as
   one shared fire-control service feeding shot placement, the player's gunsight
   HUD, scripted teacher decisions, and *observations* (alignment/envelope state) —
   for everyone. `ShouldFire`-style geometry may not decide *when* for any pilot.

5. **No weapon enters the environment half-observable.** Each equipped weapon
   arrives with slot-local state sufficient to predict its next meaningful
   transition: family identity, readiness, one normalized resource gauge
   (heat/ammo/charge — family-specific meaning). Any capability causing delayed or
   remote damage arrives with perception-faithful telegraphy: incoming-lock and
   inbound-missile threat tokens are a **co-requisite of missiles**, not a
   follow-up. (The `EnemyTracker.IncomingMissile => false` stub is the fossil of the
   correct 2026-07-17 full-deferral; partial integration repeats the diagnosed
   mistake.)

6. **Observation schema: uniform slot-block grammar, two instances, zero padding.**
   Two ordered mount blocks sharing one fixed per-slot layout (the k-asteroid token
   precedent), weapon-identity channel included. No reserved keystone fields, no
   dead channels, no speculative capacity — dead channels are variance-noise, and
   slot-index hardcodes (the `primaryWeaponReady`/primary-heat template) are the bug
   pattern the grammar exists to kill. When variable-N mounts or keystones become
   real: deliberate schema version break + retrain, designed from real semantics.
   Schema version is recorded in eval metadata so every baseline is provably
   schema-tagged.

7. **Rewards judge results, not weapon doctrine.** No reward for conserving ammo,
   "correct" charge timing, using both slots, or firing discipline — those are eval
   metrics, never reward terms. Weapon economics live in mechanics; doctrine leaking
   into reward manufactures metronome play and widens the hacking surface. Agent
   imperfection under pressure (overheat, panic release, running dry) is content and
   a readable player exploit window — no action-space training wheels.

8. **Symmetry of tells in kind (obs ↔ HUD).** No state the agent reads may lack a
   player-readable counterpart, and no player tell may be missing from the agent's
   world — in kind, not channel-for-channel (presentation may aggregate; the obs
   encode the underlying causal state, not HUD widgets).

9. **Staging arc — one seam per stage, each stage retrain-gated by per-archetype
   eval:**
   - **(i)** Trigger-ownership swap, lasers-only, + the minimum coherent two-slot
     obs blocks (not obs-unchanged: shipping two-slot actions against primary-only
     obs is a knowingly aliased interface). A/B against the self-play baseline —
     the trigger-owning agent must match or beat auto-fire on the existing
     scorecard. Isolates the interface change from content.
   - **(ii)** Second weapon family live — ammo scarcity creates real arbitration
     pressure (hold-fire must be learned, not assumed).
   - **(iii)** Missiles + threat channel + first counterplay (cover-breaks-lock
     verified, or flares) as **one package** — a threat never ships before its tell
     and its answer.
   - **(iv)** Chassis archetypes enter the self token (normalized mass/thrust/
     shield class) and the curriculum; the matchup matrix gains a chassis axis.
   - Each stage extends the eval harness in the same change, and legacy-path
     deletion completes before mixed-weapon training.

10. **Chassis archetypes are real stat blocks, measured and legible.** Ship the
    paper roster (glass interceptor / tank-brawler / baseline) from the existing
    module pool + chassis mass/hull deltas; enemies draw from the same roster.
    Differentiation must move measured axes (TTK distribution, range-band
    occupancy, tempo) — symmetric stat shuffles and invisible ±8% deltas are both
    refused. Delta *size* is set by measurement (the point where the counter stops
    working), legibility second — "big numbers by courage" lost to "sized by the
    matrix" in rebuttal.

## B. Balancing program (consensus)

1. **The dominant-strategy audit becomes a machine-checked matchup matrix.** Built
   on `CheckpointEvaluator`: rows = build×strategy candidates (frozen checkpoints +
   scripted archetypes across loadout/chassis), columns = named counters, cells =
   per-matchup Wilson lower bounds + behavioral metrics over pinned seeds. **Ship
   criterion: every row has a measured punisher cell** ("red build" = a row with no
   counter — blocks the pass). Blended aggregates are banned from every report; no
   scalar headline number exists (fable-B withdrew its own dominance score as a
   banned blend). A 65/35 cell is healthy if the 35 side owns a different cell —
   winrate-flattening is explicitly not the goal.

2. **Variance discipline: paired counterfactual seed-swap trials.** Identical
   pinned seeds, sides/loadouts swapped, one stat family changed at a time —
   separates equipment advantage from spawn geometry and policy variance almost
   free. Adopted by all four seats; build it into the matrix runner.

3. **Frozen-policy stat sweeps are screening only, never acceptance.** A frozen
   policy plays the game it was trained in; a stat change that shifts optimal
   behavior invalidates its readout. Sweeps at the owning asset filter candidates;
   they certify nothing.

4. **Short-budget best-response probes are the acceptance test for finalists.**
   Fine-tune (~500k steps, the proven-informative budget) a best-response from the
   strongest checkpoint against the frozen population under candidate stats; if it
   collapses the matrix onto one build, the build is dominant. Reserved for
   finalists and suspected equilibria — not every candidate.

5. **No automated parameter search (CMA-ES-style). Unanimous.** It requires a
   scalar fitness; any scalar over the matchup matrix is the banned blend, and the
   operational loop exceeds a solo budget.

6. **Exploit bounties become permanent regression tests — once observed.** Every
   degenerate strategy discovered (by training, eval, or hand) gets a pinned repro
   in the gauntlet forever. Per the fix-ladder evidence bar: no pre-instrumented
   speculative exploit taxonomy; detectors are added when a corner is observed or
   when a stage makes it newly reachable (e.g. hoard/camp detectors land with the
   ammo stage).

7. **Balance on the tension knobs first, damage numbers last.** The tunable surface
   is the cost axes — heat commit depth, overheat penalty, regen delay/window, ammo
   scarcity, charge/lock times. Every change names which tension-web edge it
   strengthens and prices its buff with a nerf on a *different measured axis*
   (measured exchange rates from eval data, not spreadsheet DPS). Every stat change
   ships with a before/after delta table over pinned seeds vs. the frozen reference
   population.

8. **Weapon identity has a falsifiable bar: behavioral-signature separation.**
   Per-family signatures from the existing scorecard (trigger cadence, range-band
   occupancy, commit-window length). If two weapons' trained signatures are
   interchangeable, one is redundant regardless of winrate — homogenization is
   detected, not debated.

9. **Counterplay regression suite.** Scripted evals asserting each web edge
   mechanically exists (cover breaks lock, overheat window punishable, regen denies
   chip, missiles force spend-or-flee) so a balance pass cannot silently sever an
   edge.

## C. Contested — needs a user decision

1. **F4 — reward terms in the weapons era.** Unanimous: no doctrine terms, and the
   sparse ±1 outcome spine stays. Split on the two dense terms:
   - *Per-decision time cost*: fable seats defend it as the shipped root-cause fix
     for an observed degenerate Nash (2M passivity; 500k retrain confirmed no
     passivity, no suicide-rushing) — removing it on a hypothetical inverts the
     fix-ladder entry bar. codex seats predict it taxes patient charge/lock play
     and prefer match-rule stalemate handling.
   - *Pool differential*: three seats (both codex + fable-A) flag it distorts
     scarce-ammo economics — it pays transient chip (including shield chip that
     regen erases, double-counting the regen tension) while ammo weapons pay cost
     now for differential later. fable-B wants any change detector-gated.
   - **Council-weighted recommendation:** keep sparse + time cost; run a small
     reward-term ablation across laser/charge/ammo scenarios during stage (ii);
     shrink or anneal the pool differential when ammo/charge families enter, with
     hoard/camp detectors watching for passivity relapse. No weapon-specific
     compensating rewards under any outcome.

2. **Human-performability band in the matrix (novel, raised in rebuttal).**
   fable-A: a "punished by" cell only counts if the punisher is human-performable
   in kind — an agent-only 5 Hz micro counter certifies a game nobody plays; wants
   skill-bounded probes (noised / decision-rate-limited policies as human proxies).
   fable-B's upper-bound framing ("what the optimizer can't dominate is safe") was
   directly attacked as false in both directions. Cheap partial: the agent already
   decides at 5 Hz, and scripted archetypes are human-shaped; a noised-policy probe
   row is a modest matrix extension. Decide whether it enters the v1 matrix or the
   backlog.

3. **Overkill loss on shield break (pre-decision for the whole balance pass).**
   Observed rule: damage on the shield-breaking hit is silently discarded — a
   hidden tax on burst/alpha weapons (Railgun 45 vs Bulwark 90) and a sequencing
   exploit (break with cheap hit, land the big hit on hull). Unanimous red flag:
   decide bleed-through vs. explicit absorb-with-tell **deliberately, with a delta
   table, before** balancing on top of it.

## D. Red flags the refactor must not carry forward (all observed)

- **Charge-release inversion**: envelope exit reads as trigger release
  (`ChargeTime.HandleTrigger`), wasting held charge — the ownership bug the new
  seam exists to kill; no envelope-gated release semantics may be ported.
- **`EnemyTracker.IncomingMissile => false`**: missiles before a threat channel =
  unlearnable damage-noise + untelegraphed threat.
- **Primary-slot hardcodes** in obs (`primaryWeaponReady`, primary heat) — the
  template the slot-block grammar must eliminate, not copy.
- **Missiles' `ShouldFire` asymmetry** (held lock fires regardless of geometry) —
  dies with the auto-fire path.

## E. What this unlocks / adjacencies

- Trigger-seam unification accelerates the carded **goal-mode/tactical MPC ripout**
  (removes the Gunner/`enableFiring` dependency holding it alive).
- The slot-block grammar + tells-in-kind principle answer Ships.md's "pin the
  slot/keystone contract before freezing the obs space" warning without building
  abilities: the contract is *a slot contributes one token in the uniform grammar;
  a new slot kind is a schema version break*.
- Deferred catalog fit: flares = the missile-stage counterplay candidate; Sparrows
  wait for flares (counter-to-counter ordering); damage typing (DamageInfo) is the
  natural vehicle for the overkill/bleed-through decision in §C3.

---

*Council artifacts (position papers + rebuttals) were session-scratchpad files;
this document is the durable synthesis. Provenance: recon 2026-07-22 (weapons/
firing path, ship stats, RL interface — three parallel sweeps), then 2 rounds ×
4 seats.*

---

## Stage (i)′ — manual-aim lasers PR (pr-prep decision brief, frozen 2026-07-23)

**Supersedes stage (i) scope for the current build** (user ruling). Deviation from
principle A4 is deliberate: the agent's aim assist is removed entirely — aim math
survives as *observation only* (lead vector), never as shot placement. Trigger AND
facing belong to the policy. Driver: asteroid destruction must be a usable verb
(500k self-play plateau post-mortem; Gunsight LOS veto made blocking rocks
unshootable). Recon ground truth (2026-07-23): `WeaponCommand`/per-slot actuator
already production; shots travel exactly along ship facing (`WeaponBase.Fire` →
`firePoint.up`, no spread); MPC already has an unused commanded-heading seam
(`MpcInputs.facingRad` → `facingTarget`, outranked today by the intercept override
at `Cost.cs:41`); `SetFacingOverride` exists unused; `aimAtTarget=false` today =
free yaw.

**Scope:** lasers-only. Agent path only: policy owns trigger + facing. Non-goals:
NO two-slot obs/actions, NO Gunner/`enableFiring` deletion (teachers + production
legacy keep it; deletion completes later in the arc per A2), NO teacher migration
(scorecard instrument unchanged), NO charge/missile/grenade changes.

**Locked decisions (forks):**
1. **Facing action = 2 ego-frame direction channels** appended to the existing 4
   (`[vx,vy,fire,boost,fx,fy]`), chooser converts ego→world once per decision
   (velocity-reference precedent) → `NavigationIntent.facingRad` →
   `Navigator.SetFacingOverride`; agent chooser stops feeding aim-purpose
   `projectileSpeed`/enemy-yaw so the intercept override goes dormant (not fought).
2. **Aim obs = +2 ego-frame intercept-lead direction channels** (computed via the
   firing-side `Gunner.AimPoint` static, already used by `CombatSnapshot` — one
   lead truth); envelope bit ch18 retained (it already means "nose in cone + range
   + LOS" and flips with facing). **+ per-rock `healthPct`** (obstacle token 6→7
   floats) — chip-to-pop must be representable for a memoryless policy.
   **Obs total: 26 + 8×7 = 82.**
3. **Gate reframed, fundamentals not parity**: scorecard stays the instrument;
   pass = Dummy ~100%, Aggressor cell does not collapse vs the seed baseline
   (`results/rl-eval/pause-eval-500k-seed-499985-summary.json`), no degenerate
   behavior flags. Parity with the aimbot baseline is aspirational, not a bar.
   Rock-interaction emergence = tracked behavioral metric.
4. **Retrain from scratch** (schema+action break; warm start impossible): proven
   500k teacher-curriculum recipe; **density sampler replaces the density
   curriculum lane** (uniform ~0.5–2.5 from step 0; lethality/field-on lessons
   unchanged) — strict recipe-cloning for A/B died with the interface change.
5. **Production legacy shim (user-ruled):** frozen copy of the old obs-fill + a
   legacy aim/fire mode in the chooser, used only by the production inference path
   with the old 72-obs/4-action checkpoint. One clearly-marked deletable unit;
   dies when the new checkpoint ships.

**Blindsider resolutions / assumptions:** `NavigationIntent` gains
`manualFire`+`primaryHeld`+`facingRad`; AICommander pushes
`WeaponCommand{held, pressed=rising-edge}` to the existing `WeaponActuator` for
manual-fire intents, else legacy Gunner path; per-slot `prevHeld` lives on the
commander (PlayerCommander precedent) and resets in the `ResetState()` cascade
(trajectory-equivalence pins). `aimAtTarget=false` is safe (obstacle exclusion
keys on `hasTarget`). Lead obs zeroed with no target. Heuristic: faces enemy
bearing, fire=1. `EpisodeResult.SchemaId` → `rl-episode-v4` + obs size recorded.
Re-mint `ShipCombat-smoke.onnx` at 82/6 BEFORE any PlayMode run (stale-fixture
hang). `wFacing` (1.0 vs wVelTrack 5.0) is the expected first tuning lever, asset
value not code. Credit assignment for fragment damage needs NO work — the
delta-sampled pool differential is source-blind (telescoping tests pin it).

**Test strategy:** headless. `RLAgentEditModeTests` channel map rewritten to the
82 layout; `AgentActions` 6-action mapping tests; new manual-fire test (intent →
actuator command, rising edge, reset clears `prevHeld`); teacher/Gunner tests
unchanged (path untouched).

**Carried to stage (ii)** (codex P2 on #211, rebutted-as-speculative for lasers-only):
when charge weapons enter the manual path, `AICommander` must send one `held=false`
release on leaving manual fire (PlayerCommander.OnDisable precedent) — without it a
charge weapon never sees trigger-up and the press edge goes stale.

---

## Stage (ii) — attention + potential PR (pr-prep decision brief, frozen 2026-07-24)

**The RL learning-signal upgrade aimed at the pursuit hole (Evader 0/15).** Entity-attention
perception + a continuous pursuit potential + a λ bump, as ONE retrain-gated change. This is
the "attention / potential" thread of the settled stage (ii) design; the **pin-exploit fix +
ram benchmark is a SEPARATE PR** (stage (ii) mechanics — its own before/after benchmark, no
retrain). Recon ground truth (2026-07-24): BufferSensor is first-class in the installed
`com.unity.ml-agents 4.0.3` (`Runtime/Sensors/BufferSensor.cs`), the runtime advertises
`VariableLengthObservation = true` by default (`UnityRLCapabilities.cs:38,62`), and the
inference path names all obs generically `obs_{i}` (`SentisModelParamLoader.cs:21-28`,
`TensorNames.cs:13`) — so BufferSensor inference through `com.unity.ai.inference 2.6.1` is a
supported-but-locally-unexercised path, NOT stale versioning or an architecture gap.

**Scope:** (1) obstacle tokens move from the flat k=8 `VectorSensor` block to a
`BufferSensorComponent` (entity attention) on the training/eval `ShipAgent`; (2) Φ_env
de-flattened to a continuous pursuit ramp; (3) GAE `lambd` 0.95→0.98. Non-goals: pin-fix
mechanics (separate PR), second weapon family, missiles/threat, the clearance fan, production
checkpoint swap / legacy-shim deletion, and the ~4h retrain run itself (separate spend).

**Locked decisions (forks):**
1. **Spike-first, then bundle** (F1). BufferSensor is supported, so risk is ordinary
   integration, not viability. The obs/BufferSensor wiring is built and validated by an
   export→load→run smoke **through the real eval path** (`ShipAgentFactory.LoadModel` +
   InferenceEngine Burst) BEFORE the reward/λ/test work — a viability failure is caught in
   minutes, not after a wasted retrain. Attention + potential + λ then ride ONE from-scratch
   retrain. (Splitting into two retrains rejected — the package evidence made the isolation
   cost unjustified.)
2. **Obstacles-only BufferSensor** (F2). The buffer replaces only the asteroid tokens
   (source = `Scout.AsteroidScan`, already asteroid-only); the opponent keeps its rich flat
   block (facing/shield/health + fire-control obs ch 9–25). *Why:* matches PR-B's identical
   fork + principle A6 (all-live tokens, no dead channels); the opponent is a singleton in
   1v1 — a variable-length buffer for one entity is over-engineering, and homogenizing it
   would regress opponent modeling for a teams payoff gated stages away. Wired so a future
   ships-buffer is *additive* (a second BufferSensor), not a rewrite.
3. **Φ_env de-flatten, Φ-only** (F3). The binary k₁·[inMyEnvelope] term becomes a continuous
   **linear** closing ramp on `distanceToTarget`, saturating (clamped flat) inside
   `fireDistance = 20` and zero beyond `arenaRadius = 120`; **k₂ (in-enemy's-envelope) stays
   binary** — a symmetric de-flatten would add a repulsion gradient opposing pursuit and
   recreate mutual standoff. Rides the existing telescoping `Step(φ′,φ,γ,terminal)`; γ stays
   0.99 (pinned). *Soundness:* shaping is near-policy-invariant — it densifies the closing
   signal (credit/exploration), it cannot flip a "draws-are-free" passivity optimum. The bet
   is that Evader 0/15 is a credit failure (binary Φ gave zero gradient until in-envelope),
   and the Evader is catchable (speed-matched, border-steered, bounded arena). **Caveat held:
   a persisting 0/15 with the ramp in is the diagnostic to reach for a small draw-cost — not
   more shaping.** Draws stay free (honors #178's deliberate timeCost-not-draw-penalty).
4. **Scan cap/radius is data-driven** (F4a, user rule). Measure asteroid occupancy in the
   scan box across the training density band (0.5–2.5): if it saturates >8, raise the buffer
   cap and keep the 2s box; if the box under-fills 8, widen `Scout.obstacleLookaheadTime`.
   The cap is baked into the ONNX at export → sized to cover **max-density (2.5)** occupancy,
   measured **before** the viability spike. **Clearance fan deferred** (F4b) — entity
   attention is the single perception change this PR, for clean attribution.

**Assumptions (code-grounded):**
- **Ramp weight tuned UP from 0.1** — `envelopeK1` is repurposed as the ramp's saturation
  magnitude and is the **first tuning lever** (stage-(i)′ `wFacing` pattern); spread over
  distance, 0.1 gives only ~0.06 closing reward from a 60u spawn — too weak vs the ±1 spine.
- **Nearest-first sort dropped** (attention is permutation-invariant); only nearest-N
  *selection* survives, for cap truncation. No zero-pad sentinel — the buffer is
  variable-length, the attention mask handles absence.
- **Per-channel normalization relocates with the tokens** into the buffer append (unit-scale
  inputs for the attention encoder; `normalize:false` doesn't touch it).
- **Φ_pursuit gated on `hasTarget`** (0 with no target, mirroring lead-obs zeroing);
  saturation radius single-sourced from the weapon via `CombatSnapshot` (no reach-into-weapon
  at reward time, no duplicated `20`).
- **Obs split:** flat `VectorSensor` 82→26 (ego/combat only); asteroids to the BufferSensor
  (maxEntities from the occupancy measure, 7-float token). `ShipAgentFactory.Compose` sets
  size 26 + adds the `BufferSensorComponent`; production `LivePilotAgent`/`InferenceChooser`
  legacy 72/6 path untouched. `EpisodeResult.SchemaId` → `rl-episode-v5`, obs shape recorded.
- **λ 0.95→0.98** rider, touches no pin. Reward = C# edit to `PotentialShaping.EnvelopePhi`
  (+ `CombatSnapshot.distanceToTarget`/fire-range); `EpisodeRunner.PayDecision` unchanged; no SO.
- **Smoke fixture re-mint** at the (26 + buffer) shape before any PlayMode (stale-fixture
  hang), via `run_smoke.py` through unity-access. **EditMode pins rewritten**
  (`RLAgentEditModeTests`): flat→26 + BufferSensor token/selection/cap tests + continuous-ramp
  potential tests (monotone, saturates@20, telescopes). Headless; `-ScopeType Auto`.
- **Sequencing:** buildable now in parallel with the pin-fix PR (disjoint files — RL C#/yaml
  vs prefab/physics); no merge-ordering constraint. **HARD PRECONDITION: the stage-(ii)
  retrain MUST NOT precede the ram-pin-fix landing** (see blindsider below) — the composed
  retrain trains new physics + new obs + new reward together. Action space unchanged (6 actions).

**Blindsider pass:** one genuine interaction surfaced (below); the rest resolved to the
code-grounded assumptions above (ramp weight, sort-drop, cap-baked-at-export, normalization
relocation, hasTarget-gate, gate-sequencing).
- **Pursuit ramp × the ram-pin exploit.** Rewarding *closing* risks amplifying the observed
  flank-ram/pin strategy. Resolved on three legs: (1) the ramp **saturates flat at 20u** — as
  a potential, closing 20→10 pays `(γ−1)k ≈ −0.01k`, a faint push *out*, so shaping's
  close-range verdict is "stop at the envelope and fire," never dive to contact; k₂ binary
  adds a close-range penalty. (2) Ramming is a **mechanics** phenomenon (source-blind pool
  differential credits collision damage; the pin makes it safe) — it exists today without the
  ramp and is the ram-pin-fix PR's job, not shaping's. (3) A stronger inward pull could
  amplify ramming **only if contact is still lucrative when the ramp trains** → the HARD
  sequencing precondition above (retrain after the pin-fix, when contact is de-clawed and the
  rewarded terminal is "reach 20u → kill with lasers"). Tripwire: the eval scorecard's
  `aggrHPlost`/engagement metrics; residual ram tendency = a lethality-tuning follow-up, not a
  shaping change.
- Deferred flag: the eventual production-swap PR inherits a BufferSensor wiring requirement
  (not just an obs-size bump) when it retires the shim.

**Gate stack (before retrain spend):** occupancy measure → build obs/BufferSensor →
export→load→run viability smoke (real eval path) → finish reward/λ/tests → 50k Sentis smoke.

**Hand-off:** build via agent-worktree-pr-loop from `main`; occupancy measurement is build
step 1 (sets cap/radius). Retrain gated on this PR + the pin-fix PR landing.

---

## Ram-pin fix + ram benchmark PR (pr-prep decision brief, frozen 2026-07-24)

**Observed exploit (RL-discovered):** a flank-ram yaw-locks the victim — the rammer's
hull holds the victim's heading fixed, jailing both its guns (can't re-aim) and mobility
while thrust rides facing. Cause: convex-hull `MeshCollider` contact + `BouncyShip`
friction 0.6 (thrust-rides-facing coupling). **Classification:** observed degenerate
strategy under the balance program (principles B6/B7 — measured fix + permanent repro),
NOT a fix-ladder programmer error; no "guard vs structural" framing applies.

**Scope:** kill the ship-ship yaw-lock via a friction change, gated by a before/after
ram benchmark (metrics + clips). **Non-goals:** NO retrain (stage ii owns it), NO
obs/action/weapon changes, NO collider-shape/layer/collision-matrix change this PR (see
fork 1 — deferred, benchmark-gated).

**Recon ground truth (2026-07-24):**
- Ship has ONE collider — a convex `MeshCollider` on the `Mesh` child of
  `Assets/Prefabs/Ships/Ship_1.prefab` (@186), doing double duty: **physics body AND
  laser hitbox**. Rigidbody + `DamageController` (`IDamageable`) on the root; both
  root & Mesh on layer 7 (Ship).
- Lasers are moving **trigger projectiles** → `OnTriggerEnter` → `GetComponentInParent
  <IDamageable>` (`ProjectileBase.cs:70-79`); no raycast, no LayerMask. Self-hit guard =
  `attachedRigidbody == Shooter.Body`. A separate hitbox routes damage correctly iff it
  sits under the same root and shares the root rigidbody.
- Collision matrix (`DynamicsManager.asset`) is all-on. Ship↔ship & laser↔ship both
  physics-driven, no matrix filtering.
- `ShipRadius = DeriveShipRadius()` = union-bounds of ALL child colliders × 0.5
  (`Ship.cs:199-208`; triggers count) → feeds spawn separation / aim envelope / obstacle
  exclusion. Keeping the hull keeps `ShipRadius` stable.
- Friction knob: `Assets/Visuals/Ships/Shared/BouncyShip.physicMaterial` (guid
  `ca2dde0d…`) — DynamicFriction/StaticFriction 0.6, Bounciness 0.3, combine 0 (Average).

**Locked decisions (forks):**
1. **Friction-first, benchmark-gated (keep the hull for everything).** Change ONLY
   friction 0.6→0; hull stays for asteroids + lasers + ship-ship. Rationale: user
   requires narrow **asteroid** threading (hull-shaped) — a sphere-for-everything would
   regress it, and two of the three interaction classes (laser, asteroid) want the hull.
   The yaw-*lock* is friction-driven (0 tangential friction transmits push, not spin, so
   the victim can rotate out even while touched; convex hulls can't geometrically
   interlock). Supersedes the earlier "circle collision model" ruling in light of the
   asteroid requirement. **Deferred escalation:** if the benchmark shows residual pinning,
   a follow-up does the layer-split dual collider (hull on its own layer vs Asteroid+
   Projectile; a sphere on a ship-body layer vs ship-body only, 0 friction; needs 2 new
   layers + collision-matrix surgery). The benchmark IS the gate for that spend (B7).
2. **Benchmark = committed permanent regression test (B6), single-run A/B.** Forked from
   the headless `OpponentArchetypePlayModeTests.Sweep` pattern; opt-in flag/env-gated
   (out of the merge-gate hot path, re-runnable). Each scenario runs at friction 0.6 AND
   0 in one run via a runtime `PhysicsMaterial` instance assigned to **each** ship's
   collider per condition (never mutating the shared asset; both ships must be set —
   `FrictionCombine=Average`). Same pinned seeds → paired delta (B2). Permanent repro of
   "0.6 pins / 0 doesn't," independent of the asset's later value.
   - **Pass criterion:** assert a **delta direction with margin** — friction-0
     victim-yaw-rate-in-contact exceeds friction-0.6's by a conservative margin (fix
     demonstrably frees the victim) and/or flank-ram TTK lengthens. Margin pinned from
     the after-fix run. Robust to physics variance; catches a future friction re-raise.

**Scenarios & metrics:**
- (a) **flank-ram:** `Rammer` (full-speed toward target, `aimAtTarget+enableFiring` —
  must fire or TTK is undefined) vs `StationaryFireVictim` (zero velocity, aims + fires,
  no flee). (b) **mutual head-on:** both charge + fire (TTK variant; `mutualKill` tracked).
- Per condition: TTK = `EpisodeResult.simSeconds`; **victim yaw-rate-in-contact** = new
  `Σ|opponent.Kinematics.yawRate|` (deg/s) accumulated while `Range() ≤ ΣshipRadii + ε`
  (contact **proxy** — no real contact flag; relative-delta only; fallback = benchmark-
  only `OnCollisionStay` flag on the spawned victim if noisy); **victim shots** =
  `ArchetypeGateProbe.shotsFired`; + start/end HP pools. Measures the guns/aim jail, NOT
  the mobility jail (deliberate — victim doesn't flee, per spec).

**Assumptions / blindsider resolutions:**
- Edit `BouncyShip.physicMaterial` DynamicFriction & StaticFriction 0.6→0; keep Bounciness
  0.3, combine modes unchanged.
- New choosers `Rammer` + `StationaryFireVictim` implement `IIntentChooser` in the **test
  assembly** (benchmark-only; NOT added to the `OpponentArchetype` enum/roster → never
  enter training). Aim/fire block copied from `HoldRangeFireChooser`
  (`Opponents/HoldRangeFireChooser.cs`).
- Deterministic head-on/flank poses set explicitly, bypassing the random-facing
  `EpisodePoses.Derive`; each condition runs from a fresh `EpisodePair.Reset` with the
  same seed. Spawn/arena rig reuses `OpponentArchetypePlayModeTests.SetUp`.
- Clips = one-off PR deliverable (NOT CI): a `CaptureScenario` (RequiresGraphics) reusing
  the choosers via `SpawnCombatShip` + `Brain.InstallChooser`, recorded at friction 0.6
  and 0, both-POV; assembled offline via `scripts/capture/assemble.py`, linked in the PR.
- Verify at build: the benchmark's spawned ships actually carry `BouncyShip.physicMaterial`
  + the hull (A/B must exercise real physics); nothing besides ship colliders references
  that material.
- Consequence accepted: the live 500k enemy's ram behavior may soften until stage-ii
  retrain — that behavior IS the exploit. RLAgent/eval/teacher tests untouched.

**Test strategy:** metrics test headless + opt-in gated; capture scenario RequiresGraphics.
Build order inside the PR: author choosers + benchmark → measure friction-0.6 baseline →
apply friction→0 fix → measure after + set the pass-margin → record clips both conditions.

### Benchmark redesign — mechanism corrected by first measurement (2026-07-25)

The scripted stationary-victim benchmark ran and **falsified the brief's yaw-lock
mechanism** (the gate working as designed, pre-fix):
- `yawRate` is real angular velocity. Friction 0.6 SPINS the victim *more* (121 vs
  101 °/s) — the ram couples into torque and tumbles the victim; it does NOT freeze
  heading. Raw |yawRate| conflates "spun by collision" with "rotating to re-aim" → wrong
  pass proxy.
- Against a *stationary* victim the clean separators inverted the exploit story: friction-0
  made the rammer MORE lethal (parks + kills in 10 s, victim 100 % HP lost), friction-0.6
  protected the sitting target (chaotic bounce knocks the rammer off aim; TTK 26 s, victim
  survives). The scripted stationary victim cannot reproduce the real exploit, which jails a
  *fleeing* opponent (a **mobility** jail).

**Pivot (user-ruled 2026-07-25): reproduce the exploit with the real exploiter — mirror
self-play of the trained ramming policy, not a hand-scripted rammer.**
- **Rammer = victim = the trained self-play checkpoint** `ShipCombat-999950` (selfplay2
  best-on-record; the policy that rams). Mirror self-play = the exact condition the exploit
  emerged in. Locate the `.onnx` (`results/rl-training/ship_combat_selfplay2/…` or agent-3
  tree); run via the eval/inference path (`CheckpointEvaluator` / `RL_EVAL_ONNX` through the
  Unity-access coordinator), NOT a scripted-chooser PlayMode sweep.
- **B3 honesty bound:** a FROZEN policy is *screening*, not acceptance — this proves the
  **mechanic** (does the same learned ramming behavior still pin?), NOT that ramming is
  balanced post-fix (that is a stage-ii retrain / best-response question, out of scope).
- **Primary metric = sustained contact duration** (longest single contact episode / fraction
  of episode in contact). Pass = friction-0 **collapses** sustained contact vs 0.6, margin
  pinned from measured numbers. Secondary: relative angular velocity in contact (tumble),
  TTK distribution, mutual-kill rate, HP exchange. Raw victim-yaw-rate DROPPED.
- **Scenarios:** mirror self-play at two spawn geometries — flank approach + head-on
  (subsumes the head-on TTK variant). Runtime friction A/B on both ships (0.6 vs 0, same
  seeds, asset untouched).
- **Self-validation guard:** confirm mirror @0.6 actually reproduces sustained ram-contact
  BEFORE the full A/B; if the checkpoint doesn't ram, STOP (wrong checkpoint / pin not
  reproducing), don't measure a non-ramming policy.
- **Superseded:** scripted `Rammer`/`StationaryFireVictim` choosers + stationary/head-on
  scripted scenarios — remove per scope conservation unless a piece is genuinely reused.
- **Clips:** mirror self-play 0.6 (pin/grind) vs 0 (clean glance), side-by-side.

### Escalation to layer-split — benchmark ruled friction-only insufficient (2026-07-25)

Mirror self-play of `ShipCombat-999950` measured friction-0.6 vs 0 (8 eps each, same
seeds): guard PASSED (0.6 rams — 57% in-contact, sustained to 9.1s). Friction→0 gives
**partial** relief only — mean longest sustained contact 6.35→4.19s (−34%), in-contact
fraction 57→38.7% (−18pp, every episode), 5/8 engagements collapse to glancing ≤2.5s —
**but max sustained unchanged (9.1s), 3/8 still grind 5.5–9.1s** (frozen policy can still
thrust hull-into-hull to hold pushing contact). B3-bounded (screening, not balance).

**User ruling 2026-07-25: build the layer-split (the deferred escalation); playtest game
feel before merge.** Minimal-churn decomposition:
- **Hull unchanged** on layer 7 (Ship) — asteroid threading + laser hits + all existing
  "Ship"-layer queries preserved. **`BouncyShip` friction NOT changed** (friction→0-on-hull
  is now unnecessary; keeps hull↔asteroid feel exact).
- **Add a non-trigger SphereCollider** on a NEW layer `ShipBody` with a 0-friction material
  (copy BouncyShip → friction 0, bounce ~0.3). Radius default ≈ ship half-width.
- **Matrix (DynamicsManager):** turn OFF Ship(7)×Ship(7); ShipBody×ShipBody ON; ShipBody ×
  all-other-layers OFF. Net: ship↔ship = frictionless circular bumper (no wedge/grind);
  hull↔asteroid + hull↔projectile untouched → no double-hit (Projectile×ShipBody OFF), no
  laser/threading regression, `ShipRadius` (union bounds) stays hull-dominated/stable.
- **Verify + smell-test:** grep for code relying on Ship×Ship physics or querying the Ship
  layer for ship-ship overlap (recon: ship-ship is pure physics, no C# handler, no ram
  damage — expected none). If the split touches many systems, STOP and reclassify scope.
- **Benchmark:** update contact metric to the SPHERE contact (hulls no longer touch
  ship-ship); re-run mirror A/B (expect sustained-contact collapse); pin the pass-margin
  from these numbers; keep the friction-only table in the PR as the escalation justification.
- **Merge gate = human playtest** (feel: ship-ship bump + asteroid threading). Levers:
  sphere radius + bounciness. NO merge until user feel-approval; clips (old pin vs clean)
  after.

---

## Shield-break overkill bleed-through PR (pr-prep decision brief, frozen 2026-07-31)

Implements §C3's council-red-flagged pre-decision, resolved by the user in the
2026-07-28 rules-change session: **bleed-through** (remainder on the shield-breaking
hit carries into hull), over absorb-with-tell. Lands on main first, on its own,
before the telemetry PR and the rules branch; deliberately independent of the
reopened weapon-identity design.

**Change:** `DamageController.TakeDamage` — branchless: shield absorbs first,
`Resource.ApplyDamage`'s absorbed-amount return routes the remainder into hull.
Exact because both empty-path behaviors were already correct: an empty shield
early-outs returning 0 (no event, no regen-delay reset — `RegenResource` resets
its delay only when `damageAbsorbed > 0`), so the shield-empty path is
bit-identical to the old rule.

**Locked decisions:**
- Single `OnDamaged` per hit carrying total absorbed; shield-vs-hull consumers
  already use the per-resource `OnValueChanged` events, so the breaking hit gets
  shield flash + hull sparks with no presentation work.
- Every damage source inherits the rule (all routes go through `TakeDamage`) —
  kills the §C3 sequencing exploit for all weapons at once.
- No `DamageInfo`/damage-typing vehicle (§E's "natural vehicle" note): the user's
  sequencing ruling (land first, alone, cheapest-ever) resolved this toward the
  minimal fix; damage typing stays deferred catalog.
- No retrain: no obs/action schema change; `ShipCombat-699941` stays valid.
- Old-rule test `TakeDamage_NoOverflow_ExcessOverShieldDoesNotHitHealth` inverted
  to bleed-through; new pin tests: single-hit kill-through (event reports total
  absorbed capped at shield+hull) and hull-only-damage-does-not-postpone-regen
  (the exact seam the branchless rewrite leans on).
- Evidence per §C3: arithmetic shots-to-kill delta table (PR body, from current
  asset values) + seeded deterministic-stream eval before/after vs the
  `golden-main-d61b31cc` baselines (valid before-side: slice A verified main
  matches them).

**Noted, not entered** (fix-ladder: speculative): hitting an already-dead ship
re-fires `BroadcastDeath` — pre-existing, unchanged here.

---

## Combat telemetry PR (pr-prep decision brief, frozen 2026-07-31)

Resolves the rules-change handoff's open fork 6 (the telemetry surface). Second
in the locked landing order (overkill #235 → **telemetry** → rules branch): it
lands before any rules work so the escape-viability probe and every screening
tier read one instrument instead of growing parallel measurement paths.

**Scope.** A new `combat` registry probe (`RLHarness/Probes/`): per-episode rows
carrying normalized range-band occupancy, TTK inputs (simSeconds +
outcome/endKind), engage/disengage cycles with per-engagement resource states,
shield-regen events, per-side shot counts, and boost usage; standard probe
sidecars; the default probe set becomes `gate,combat`.

**Non-goals.** No Tier-0 screening runner (screening runs are consumers of the
rows, not contents of this PR); no scripted-vs-scripted composition (the escape
probe's own scope); no Game.Core changes — the game side already emits every
event needed; no probe params (`combat` takes none; the grammar is slice D's);
no distribution/statistics math (PR-4's); the gate probe and its schema
untouched.

**Build sequencing.** After harness-lane slice D lands: D reshapes
`SessionSpec.probes` (params grammar, known-key sets, duplicate-name throw) and
owns the same hunks this PR touches (`ParseProbes` default, the probe registry,
the PlayMode lane smoke, the README probes line). Slice C/F overlaps are
textual only. D's "default probe set stays `gate`" is a D-scope statement;
this PR is the deliberate change of that default.

**Locked decisions (forks):**
1. **Home → harness-side registry probe.** The frozen harness-lane arc reserved
   the probe seam for rules-change telemetry (its decision 6; the deferred
   playtest lane inherits probes for measured playtests); every consumer
   (screening tiers, eval gate, escape probe, PR-4) is a harness session; all
   sidecar/summary/reader machinery exists and is owned there. Game-first
   governs what the metrics *judge*, not where the recorder runs — and no
   game-side change is needed at all, since the probe subscribes to existing
   game events (gate-sampler pattern). Rejected: a Game.Core recorder — a new
   parallel output path (wiring §6), and `UnitService.OnShipSpawned` never
   fires in harness sessions, so the first customer would need adapter wiring
   anyway.
2. **Engagement = envelope-based with exit hysteresis.** Engaged while either
   ship's firing envelope is valid (`CombatSnapshot.inMyEnvelope` /
   `inEnemyEnvelope`, intercept-lead + LOS); the engagement ends when neither
   has been valid for τ = 3 s (probe constant; `EnemyTracker.combatExitDelay`
   semantics). Why: LOS-aware, so cover-breaks register as disengagement — the
   escape probe's core need; purely geometric, so mutual heat lockouts at close
   range don't fake cycles (shot-recency would bias exactly the long-lockout
   rules candidates). Rejected: range-only (cover-blind), shot-recency (lockout
   bias), union (a second knob with no identified consumer).
3. **Range bands normalized to the subject's own primary `FireRange`** —
   quarter-envelope bins to 2.0× plus overflow (9 floats); absolute
   mean/min/max range and the normalizer value recorded per row. Why: the
   brawl diagnosis lives on the relative axis; histogram *shape* stays
   comparable across the coming weapon-range change; derivation-framework
   philosophy (key off the weapon's own reach, never hand-tuned absolutes).
4. **Sibling probe, not a gate extension.** The gate row is a frozen regression
   surface (golden comparator + scorecard log); the registry is deliberately
   many narrow probes. A few duplicated sampling lines are cheaper than
   coupling the exploratory instrument to the tripwire.

**Blindsider resolutions:**
- **Default-on** (`gate,combat`): the instrument is always warm and screening
  runs cannot forget to attach it. Artifact-set growth in eval/gate step dirs
  accepted; the lane smoke asserts the new sidecars.
- **Boost usage recorded** per engagement and per gap (activation edges polled
  via `IShipStatus`): covers the second of the two escape mechanisms (cover,
  boost) — beyond fork 6's literal metric list, added deliberately.

**Assumptions (code-grounded):** one file `CombatTelemetryProbe.cs`
(row/summary/sampler/probe in-file, plus a pure `EngagementTracker` state
machine); registration matches the registry shape on main at build time
(post-D: empty known-keys set); the probe is a pure observer — per-tick
`CombatSnapshotExtractor.Capture` through the Gunsight *observation* LOS cache,
so telemetry reads cannot mutate the firing path and the episode stream is
untouched; row schema `rl-combat-telemetry-v1` (episodeIndex, opponent draw +
label, outcome, endKind, simSeconds, occupancy bins, range absolutes,
fireDistance, per-side shots via `WeaponComponent.OnFire`,
`List<EngagementRow>` with entry sim-time + both ships' shield/heat/pool pct at
entry, regen events split engaged/disengaged, boost counts); regen events
edge-detected from `Shield.OnValueChanged` rising ticks — no `RegenResource`
change; heat via the `IHeatReadout` readout-iteration precedent; `Begin` throws
on `FireRange ≤ 0` (normalizer invariant, earliest deterministic point); an
engagement open at episode end closes and counts; zero-engagement episodes
serialize empty lists; TTK distribution math stays reader-side (the sidecar
summary carries per-opponent means/counts only — blended-metrics ban; PR-4
owns statistics); contact metrics stay slice D's `ContactSampler` (physical
touching, ram bench) — no overlap.

**Tests.** EditMode on the pure pieces: band binning, engagement hysteresis
machine (enter/exit/τ/episode-end), regen edge detection, boost edge counting.
PlayMode: the existing lane smoke's probe set gains `combat`, asserting its
sidecar pair (merge-gate cost flat; D's precedent). Headless.

**Vocabulary.** Coins **combat telemetry** and the telemetry sense of
**engagement**; both registered in `doc/Glossary.md` with this brief.
