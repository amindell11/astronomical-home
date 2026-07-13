# RL Episode / Reward / Reset Layer — PR-2b Implementation Plan

**Date:** 2026-07-12 (scoped via grill)
**Parent:** `Tactical_AI_Audit_And_Roadmap.md` §3.3 (reward) / §4′ PR-2, split per grill into
PR-2a (`Maneuver_Oracle_Gate.md`, shipped — CONDITIONAL GO) and **PR-2b (this doc)**.
**Status:** Built with this PR.

> **One-line intent.** The architecture-neutral survivor of the old PR-S3: a headless
> episode loop (ranger-vs-baseline 1v1), the §3.3 reward computed at decision boundaries,
> and an atomic pair-reset that replays any episode from `(runSeed, episodeIndex)` — the
> substrate PR-3's ML-Agents `Agent` plugs into with zero new reward/loop code.

---

## Design decisions

1. **Reward is §3.3 verbatim, decision-sampled.** Sparse outcome ±1; dense
   pool-differential `λ·Δ(enemyPool) − λ·Δ(myPool)` (pool = health + shield, each side
   normalized by its own max pool, symmetric λ), **delta-sampled** between decision
   boundaries so contributions telescope to the start-to-end pool swing; potential-based
   shaping `γ·Φ(s′) − Φ(s)` with Φ = firing-envelope term
   (`k₁·[enemy in my envelope] − k₂·[I'm in enemy's envelope]`, binary) + border potential
   (`−k_b·ramp(r)`, zero inside `softFraction·R`, quadratic rise to R).
2. **`PotentialShaping` is the ONE place γ·Φ(s′) − Φ(s) is applied**, and it forces
   terminal Φ = 0, so shaping telescopes to `−Φ(s₀) + (γ−1)·ΣΦ_mid` by construction.
3. **`RewardSpec` is the single serializable config** (λ, k₁, k₂, k_b, γ=0.99, decision
   interval K=10 fixed steps ≙ 5 Hz, arena radius R + soft fraction, timeout in
   *decisions*, spawn-pose band, master runSeed) and is embedded verbatim in every JSONL
   row — results are self-describing.
4. **Firing envelope = geometry only.** `WeaponComponent.InEnvelope(in TargetingContext)`
   splits each weapon's `ShouldFire` into geometry (distance/angle/LOS) and readiness
   (heat/charge/lock/ammo); the snapshot's `inMyEnvelope`/`inEnemyEnvelope` evaluate the
   honest geometric envelope over equipped weapon slots (any armed slot true), LOS
   included, readiness excluded. `ShouldFire` behavior is unchanged for every weapon.
5. **Termination:** first death either side (win +1 / loss −1); **mutual kill = loss**;
   agent out-of-bounds (r > R) = loss; baseline out-of-bounds = draw + anomaly flag;
   timeout (in decisions) = draw 0. Termination is a pure function over the snapshot
   (`EpisodeRules`) so the full outcome table is EditMode-tested.
6. **`EpisodeRunner` is a plain class**, host-agnostic `Tick()` once per fixed step —
   the PlayMode test drives it now, PR-3's training scene hosts the same object later.
7. **Reset is a game-domain atomic pair-reset.** `UnitService.RespawnShip` =
   position/rotation + `Ship.ResetShip()` + `Commander.ResetState()`. This deliberately
   heals a latent game bug: respawned AI ships previously kept previous-life AI state
   (navigator goals, MPC warm-start, tracker history, utility-chooser state). The
   commander→ship linkage is `Ship.Commander`, already cached at spawn — zero new wiring.
8. **`AICommander.ResetState()` mirrors the Initialize cascade**: the composer sequences
   per-part resets (Scout, Navigator, Gunner, a fresh `AIContext`/EnemyTracker, Brain →
   chooser). RNG streams re-derive from the ship's spawn `SeedScope` — post-reset state is
   "as if freshly spawned". No episode/seed vocabulary enters GameCore.
9. **Episode variety comes from spawn geometry only.** Poses derive from
   `hash(runSeed, episodeIndex)` (separation band + bearing + facings); AI streams restart
   identically every episode by design, so any episode replays from `(runSeed, i)`.
   *Caveat:* strict trajectory replay additionally requires locked frame pacing
   (`Time.captureDeltaTime`), because Scout scanning and shield regen run in `Update`.
10. **Scenario v1:** 1v1, ranger (velocity interface, `RangerChooser`) vs the FULL
    production baseline brain: `UtilityPilot.prefab` (utility chooser + state profiles +
    MPC — what `CombatSector` spawns). The default `TestPilotMPC` commander carries no
    state profiles and sits inert, so it hosts only the agent (whose chooser is replaced).
    Both ships on a **lasers-only** loadout (episode constant), empty space. The
    ranger's tracker uses `wVelTrack=50` (PR-2a: range-hold fails at the asset default 5,
    passes ≥ 20). The ranger is **expected to lose most episodes** — that win-rate is the
    floor PR-3 must beat.
11. **Projectile flush = return-to-pool, never Destroy.** In-flight projectiles are
    pooled, independent GameObjects that survive ship death; episode reset returns each
    active one to its pool via a minimal public accessor
    (`ProjectileBase.ReturnToPoolImmediate`). No static active-registry in GameCore.

## Invariants

1. **Shaping γ ≡ PR-3 trainer discount γ** — single-sourced from `RewardSpec.gamma`.
   When PR-3 configures the trainer, it must read this value, not restate it.
2. **The reward depends only on observable-or-episode-constant state.** The loadout is an
   episode constant (lasers-only v1); if loadout ever varies across episodes, the envelope
   parameters must enter the observation in the same PR.
3. **Reset completeness is enforced by the trajectory-equivalence test, never by audit.**
   Two pair-resets to the same poses — one shortly after spawn, one after episodes of
   dirty combat — must produce the same trajectory. If that test fails, some component
   forgot to reset — fix the reset, never loosen the test. The window is 2 s of
   bit-tight comparison: a forgotten reset diverges immediately and macroscopically
   (every real bug found this way did), while longer windows reach into combat where
   projectile-pool identity and CEM elite near-ties amplify sub-physical float noise
   into small honest drift — weapon-state reset is covered by the episode smoke and the
   weapon-condition unit tests instead. Both recordings run after a Burst warm-up (the
   managed fallback active during async compilation rounds differently).

## Build findings (root causes fixed while proving invariant 3)

- **Respawn teleports leaked stale physics state two ways.** (a) Scanner overlaps and
  LOS raycasts issued between the teleport and the next simulation step saw the
  pre-teleport pose — fixed with `Physics.SyncTransforms()` inside `RespawnShip`
  (mirrors `Ship.Initialize`). (b) With `RigidbodyInterpolation.Extrapolate`, the body's
  interpolation buffers smear pre-teleport motion into the restored pose (an ulp-level,
  history-dependent residue) — fixed by clearing velocities and toggling interpolation
  off/on around a direct body-pose set.
- **`Cooldown` and `Booster` paced on absolute `Time.time`.** At large session times
  (late in a long test suite or play session) float quantization of
  `Time.time + fireRate` flips fire/boost timing by ±1 fixed step, so two identical
  resets pace differently. Both now run internal dt-driven clocks that zero on reset
  (the `Heat` precedent, and PR-S1a's "timers must be dt-driven" direction).
- **The CEM sampler seed hashed raw position float bits**, so one ulp of pose noise
  selected a completely different noise stream — bit-level chaos by construction, which
  defeated "replayable for identical inputs". The seed now hashes the position quantized
  to 1/8 unit: decorrelation across ships/solves/poses is preserved, replay only
  requires physically-identical state.

## Deferred

- ML-Agents `Agent` hosting, observation wiring, and fire-gate action — PR-3.
- Self-play / checkpoint league — PR-4.
- Asteroid-field episodes (v1 is empty space; the field is a training-env question).
- Loadout variation across episodes (invariant 2 binds the obs contract when it lands).
- Update-driven sim seams (Scout scanning, shield regen ticking on frame time) — noted
  as the reason strict replay needs `captureDeltaTime`; a fixed-step migration is a
  separate, wider change.
- Reward regularizers (per-step time cost, per-shot cost) — only if PR-3 misbehaves.

## Findings (characterization run)

**Run:** 2026-07-13, 20 episodes, default `RewardSpec` (λ=1, k₁=k₂=0.1, k_b=0.5, γ=0.99,
K=10, timeout 600 decisions, R=120, separation band 25–60, runSeed=1), ranger
(`wVelTrack=50`, hold-range 15) vs `UtilityPilot`, lasers-only, empty space. JSONL:
`results/rl-episodes/20260713-012404-ranger-vs-baseline.jsonl`.

- **Outcome split: 1 win / 1 loss / 18 timeout draws.** No mutual kills, no
  out-of-bounds on either side, no anomalies. Both kills landed near the timeout
  (~564 decisions ≈ 113 s sim).
- **Why draws dominate:** with symmetric lasers-only loadouts and shield regen
  (25/s), the sustained damage rate barely exceeds regeneration — attrition is
  near-parity and 120 s is rarely enough to finish. Both sides do engage: 9/18 draws
  end with the baseline's pool depleted below max and 5/18 with the ranger's (the one
  loss shows the baseline can kill). Episode length: 18×600 decisions (timeout),
  kills at 564.
- **Reward decomposition (per-episode means):** draws — dense +0.07 (ranger slightly
  out-trades), envelope shaping +0.007, border shaping 0.000 (fights never approach
  0.8·R), outcome 0; win total +1.53 (dense +0.47, outcome +1); loss total −1.65
  (dense −0.60, outcome −1). Telescoping checks pass on live episodes (test #4).
- **Prediction vs reality:** the brief expected the ranger to *lose most episodes*.
  Instead the scenario is an attrition stalemate — the ranger's naive hold-at-15
  already trades evenly with the production `AttackAggressive` brain under this
  loadout. **The PR-3 floor is therefore: win-rate > 5% with fewer timeouts**, and
  PR-3 should consider a longer timeout, a higher λ, or the deferred per-step time
  cost if the stalemate persists into training.
- **Baseline gotcha (fixed during build):** the default test commander
  (`TestPilotMPC.prefab`) serializes NO state profiles — its utility chooser has no
  states and the ship sits inert. A first characterization against it produced
  4 wins/16 draws vs a stationary regenerating target. The scenario now spawns the
  baseline from `UtilityPilot.prefab` (what `CombatSector` spawns), and the test
  asserts the baseline's chooser reports a real state.
