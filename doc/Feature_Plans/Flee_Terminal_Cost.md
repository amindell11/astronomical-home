# Flee Terminal Cost — Escape-Routing Field

Board item: *"Flee terminal cost — long-horizon guidance for Flee mode"*. Closes the gap
left open by the chase-nav trade study §7.4: pursuit's terminal cost-to-go is a Dijkstra
field **to** a point target (the enemy); Flee has no attractor and cannot reuse it
sign-flipped (Euclidean-far cells include dead-end pockets). Without long-horizon
guidance the evader runs purely reactive and thrashes.

Design fixed in a 2026-07-07/08 design interview; decisions below are the outcome.

## Semantics: threat-biased border escape

A `BorderEscape` seed mode on the existing `NavField` Dijkstra core:

- Grid is **self-anchored** (centered on the fleeing ship). Rollout terminal states can
  never leave it (horizon 1.5 s × max speed ≪ half-extent 96 u).
- **Every free border cell is a Dijkstra source** ("exit"), with a continuous initial
  cost by bearing relative to the threat: `bias · (1 + cos θ) / 2`, 0 for the exit
  directly opposite the threat, `bias` (default: one full grid crossing) directly toward
  it. Exits past the pursuer are never forbidden — just priced like detouring the whole
  grid, so they win only when they are genuinely the only way out. No hard angular
  cutoff, no empty-seed degenerate case (a fully surrounded ship still gets the
  least-bad exit; a fully walled border yields a flat field, which is harmless).
- Descending the field routes around obstacles toward openings away from the threat.
  Consumed through the **unchanged** solver terminal hook (`wTerminal ×
  TerminalFieldData.Sample` at each rollout's terminal state) — the narrow waist that a
  future learned terminal value also plugs into.

## Fallback semantics

Blocked (inflated-stamp) and unreachable (pocket) cells **bake a flat pessimistic
value** — worst-bearing seed cost plus a full grid crossing — at the end of the solve
job. Never infinity (destroys elite ranking); never the pursuit distance-shaped
fallback, whose anchor for a self-centered grid is the ship itself (a blocked cell near
the ship would sample near-zero and *attract*). `TerminalFieldData.CellValue`
accordingly prefers any finite cost and only falls back on `+inf`; for Goal-seeded
(pursuit) fields blocked cells always hold `+inf`, so pursuit sampling is bit-identical.
Pursuit's off-grid distance-shaped fallback is kept — pursuers legitimately start
off-grid of a target-centered field; a flee field's off-grid branch is dead code.

## Ownership: per-ship, no service

Pursuit fields are shared per chase target via `NavFieldService` (registry + pump).
A flee field has nothing to share — the **Navigator owns one lazily-created
`FieldBaker`** (the double-buffer/gather/policy machinery extracted from the service),
pumps it from its own tick, and disposes it in `OnDestroy`. Active only while
`goalMode == Flee` **and** the intent carries a live target (same gate as pursuit's
`terminalFieldTarget`); the threat position refreshes from the intent every tick.
Rebuild policy is **timer-only** (~0.15 s): the anchor and threat always move, and the
service's moved/delta/stale triggers exist to skip work for idle chase targets.

## Tunables

- `wTerminal` is shared with pursuit (both fields are denominated in seconds) and is
  per-state overridable via `MpcWeight.Terminal` — no new settings, no `.asset` edits.
- Grid size / cell size / rebuild interval / `threatBias = 1` are code constants on the
  Navigator, mirroring the service's pursuit defaults; promote to settings only under
  benchmark evidence that a knob matters.

## Deferred / out of scope

- **Multi-threat awareness**: the seed-cost loop composes trivially (worst threat per
  border cell), but the AI pipeline is single-target end-to-end (EnemyTracker,
  NavigationIntent, tactical costs). Plumb when multi-enemy tracking exists as its own
  system.
- Flee-with-no-enemy intent quirk (goalPosition defaults to origin for a frame or two
  after target loss) — pre-existing, unrelated to the field, flagged to the board.
- `NavFieldService.Instance` / `ObstacleFields.Active` interim static seams — unchanged.

## Acceptance

Chase benchmark A/B vs main (standard seed sweep): evader control chatter down and
evader collisions not up (primary); evader mean speed / separation directional;
`Terminal ×0` weight-override ablation on the evader confirming the delta is the
field's; full suites green.
