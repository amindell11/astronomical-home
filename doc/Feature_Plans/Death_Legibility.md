# Death Legibility — damage summary + death feedback

> STATUS: live arc — PR-1 (attribution plumbing) building; PR-2 (recap + death feedback) queued.

Frozen decision brief, ruled 2026-08-05. Source: playtest feedback 2026-08-04 §3
("Hard to tell why you died"), board card *Death legibility: damage summary + death
feedback showing the killer*. The PR completing the arc deletes this doc.

## Scope

Make player death legible: carry per-hit context (source identity, source kind, amount)
on the damage events instead of the current bare `(amount, hitPoint)`; accumulate a
per-life **damage ledger** for the player; show a **death recap** between death and the
restart flow; make the death moment read its cause (cause-distinct death SFX/VFX).
Asteroid-collision deaths must attribute correctly — today `LastAttackerId` silently
ignores non-ship attackers, so they report the last ship that ever shot you, or Invalid.

Non-goals: asteroid damage multiplier (other half of the DamageInfo board card),
damage-typing gameplay effects, killer-cam/camera work, K/D statistics, Tier-2
directional hit indicator (deferred to its own board card).

## Ruled design (all forks closed 2026-08-05)

1. **Attribution channel — `DamageInfo` payload (structural).** New struct
   {amount, source kind (weapon class / collision), source ShipId-or-none, hitPoint,
   hit mass/velocity folded in}; it replaces the 5 loose `TakeDamage` params
   (`TakeDamage(in DamageInfo)`) and becomes the `OnDamaged` payload. Producers pass
   kind/identity at their call sites (all five know both). `IShooter` gains `ShipId`
   so projectiles pass id directly, retiring the GO-attacker seam and the damage-time
   `GetComponentInParent<Ship>` (kill-events card residue; consistent with the
   collider-keyed-registry analysis — pooled projectiles carry injected identity,
   not arena-scoped refs).
2. **Two PRs.** PR-1: plumbing + death-fired latch + test migration, no visible change.
   PR-2: ledger + recap + death-moment feedback.
3. **Feedback depth — Tiers 0+1.** Recap names the cause (killer ship via
   `ShipRegistry.TryGetShip`, or "asteroid collision"); `ShipDamageAudio` /
   `HullVisuals` switch death SFX/VFX on the killing blow's kind. Tier 2 (hit-direction
   pips) board-carded, Tier 3 (death cam) deferred.
4. **`OnDeath` carries the killing blow:** `Action<ShipId victim, in DamageInfo killingBlow>`
   — the killer id rides the payload; no side-channel property.
5. **Recap home — driver-owned hold.** `GameDriver`'s RestartSector death callback shows
   the recap (presentation-gated) and delays `TransitionTo(Restart)` until
   dismiss/timeout; a `GameState.DeathRecap` state if the hold has any interaction.
   Headless/RL drivers wire their own `OnPlayerDeath` and are untouched.
6. **Ledger owner — separate player-scoped recorder,** subscribed to the enriched
   events, exposed to UI as a new `HudBinding` read surface, wired at
   `SessionRig.BuildHudBinding`/`RebindHud`. Not sim state; reset on rebind/recap
   cycle, never by reaching into `ResetDamageState`.

## Blindsiders the build must handle

- **Death latch (PR-1, rung 2):** `DamageController` has no death-fired latch —
  `OnDeath` can re-fire from a second lethal `TakeDamage` in the same physics step
  (`ProjectileBase` already defends against this reality). Latch at the source.
- **Invalid/absent killer renders:** asteroid-only deaths have no ShipId; killers can
  despawn before the recap resolves. Capture name/type into the ledger row at event
  time; never hold the `Ship` ref.
- **Friendly fire is real:** splash/wave rows can name teammates or self — recap copy
  must not assume hostility.
- **Shield-absorbed hits are rows too** (`OnDamaged` reports total absorbed — locked
  bleed-through decision): recap aggregates per source, never lists raw hits.
- **Wide-but-mechanical test surface:** `TestDamage` mock, damage/weapon suites, and
  the multi-arena `CombatLog` helper (migrates to the payload) all touch the changed
  signatures.

## Standing assumptions

Player-only ledger/recap; presentation-gated (`PresentationEnabled`), zero RL/headless
impact (no obs/action schema contact); recap uses sim damage numbers as-is.

## Vocabulary

Glossary rows ride the PR introducing each term: **DamageInfo** (PR-1) — per-hit
context struct: amount, source kind, source ship id, hit point. **damage ledger** and
**death recap** (PR-2) — per-life accumulation of the player's received DamageInfo
rows (consumer-side, not sim state); the post-death summary panel rendered from it.
