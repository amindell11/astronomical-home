# Pilot Decision Seam — three lanes, objective-shaped

> STATUS: live arc — governs the ActIntent retirement and the decision→actuation restructure ahead of the MPC/tactical-model arc; delete with the arc's closing PR.

Design approved by the user 2026-08-07 (clean-room design session; Phase-1/Phase-2 audit
artifacts in that session's scratchpad, reconciliation summary in memory
`project_intent_command_seam.md`). Supersedes the scope sketch on issue #344 — that card's
"hoist the passthrough lane" is subsumed by the full split below.

## Ruling

The decision layer stops speaking through one struct. It speaks through **three lanes** —
independent seams, one per kind of output, each latched and routed separately:

| Lane | Datum | Consumer | Why it's separate |
|---|---|---|---|
| Navigation | `NavObjective` — a parameterization of the MPC cost function | `Navigator` → `Mpc` | pilots at this level differ *only* in what the solver optimizes |
| Fire | `FireControl` per slot: `Hold \| Auto \| Commanded(held)` | `Gunner` (Auto) / commander edge-memory (Commanded) | trigger timing has nothing to do with the solver |
| Ability | activation bool (boost today; keystone-ability slot later) | `Booster` directly — **not** through the solver | boost through the MPC never worked; it is a tactical decision, RL-controlled |

"One struct cannot carry both rates" (the #344 diagnosis) dissolves: a latched objective ages
well by construction (anchor-relative, consumer-resolved), `Commanded(held)` is level state,
ability activation is a one-shot event.

## The nav seam: `NavObjective`, sealed by a builder

The seam datum is **the objective** — the decision-varying slice of the cost function.
Invalid objectives are unbuildable (rung 1; replaces `Navigator.ApplyIntent`'s facing-source
throw, rung 4):

```csharp
public readonly struct NavObjective
{
    // move: Drift | Planar(velocity) | Anchored(radial, tangential, authority)
    // facing: delegate (intercept lead / velocity prior) | Anchored offset (offsetRad, authority)
    // anchor: ShipId — identity, resolved to live kinematics by the Navigator each tick

    public static NavObjective Drift();
    public static NavObjective Planar(Vector2 velocity);
    public static AnchoredBuilder Anchored(ShipId enemy);   // enemy-frame channels require an anchor — by type
}

public readonly struct AnchoredBuilder   // struct-fluent, allocation-free
{
    public AnchoredBuilder Velocity(float radial, float tangential, float authority);
    public AnchoredBuilder Facing(float offsetRad, float authority);
    public static implicit operator NavObjective(AnchoredBuilder b);
}
```

- Anchored-without-anchor: won't compile. Two facing sources: no second slot. `aimAtTarget`
  ≡ `.Facing(0, 1)` — the special case dies.
- Anchor is **identity, not snapshot**: today's by-value `EnemyTarget` gives 5 Hz archetypes
  anchors up to 0.2 s stale and forces `AgentChooser` to rebuild its intent every tick just to
  re-snapshot. The Navigator resolves `ShipId` → live kinematics per tick via the registry.
- Ballistics never cross this seam: the host injects the primary weapon's projectile speed
  into the Navigator (intercept-lead geometry is the solver's); brains never touch weapon data.

## Cost modules — two axes, one package

Burst rules out runtime-pluggable term lists, so "modular cost" = **a fixed term menu, one
module per term, parameterized by the objective**. All cost logic stays in the MPC package:

```
AI/Navigation/MPC/Cost/Terms/VelocityTrack.cs    ← objective term (planar or anchored reference)
AI/Navigation/MPC/Cost/Terms/Facing.cs           ← objective term (anchored offset + delegation prior)
AI/Navigation/MPC/Cost/Terms/Obstacles.cs        ← solver-owned: admissibility + collision (never brain-optional)
AI/Navigation/MPC/Cost/Terms/Regularization.cs   ← solver-owned: effort, smoothness, yaw-rate
AI/Navigation/MPC/Cost/Cost.cs                   ← composition; owns CostBreakdown
```

- **Decision axis** (varies per pilot per ~0.2 s): objective terms — crosses the seam.
- **Character axis** (constant per ship): regularization + safety weights — the `MpcSettings`
  asset on the prefab; never crosses the seam. `WeightOverride[]` is **deleted without
  replacement** (per-ship personality = a different settings asset, the existing mechanism).
- Terminal-value scoring (the coming arc) enters as `Cost/Terms/TerminalValue.cs`, a
  solver-owned term whose model handle is settings-level config first; it becomes a builder
  entry only if a brain ever needs to choose it per decision. No seam change to start the arc.

## Brain types replace `IIntentChooser`

`Brain` stops being a wrapper and becomes the swappable unit:

```csharp
public abstract class Brain : MonoBehaviour
{
    public abstract PilotDecision Decide(AIContext ctx);   // null/None = no decision (mid-transition)
    public virtual void ResetState() { }
}

public readonly struct PilotDecision
{
    public readonly NavObjective nav;
    public readonly FireControl  primary, secondary;
    public readonly bool         boost;
}
```

- `IIntentChooser`, the `Brain` pass-through wrapper, and the `[SerializeReference]` chooser
  authoring (custom `SerializeReferenceDrawers` path) are deleted; brain types are plain
  components (`PolicyBrain`, archetype brains, probe brains) — stock authoring, harness
  installs via `AddComponent`. Decision *laws* stay static pure functions (the
  `RangerChooser.HoldRangeVelocity` pattern) so EditMode testability doesn't regress.
- `PilotDecision` is a transport, not a union: the host (`AICommander`) latches and routes
  each lane independently. `AICommander` keeps its role — wire Scout/Navigator/Gunner/Brain,
  latch the decision, derive trigger edges (`pressed` stays commander-owned, per the #317
  ruling), pass boost to the Booster.
- Trigger semantics unchanged: Gunner mashes, Commanded edge-detects (PlayerCommander
  precedent) — both deliberate, traced 2026-08-06.

## Deletions (the simplification is the feature)

`ActIntent` and its union; `isValid`; `aimAtTarget`; absolute world-frame facing
(`hasFacing`/`facingRad`) from the production objective — probe-only builder entry *if*
`FacingProbe` genuinely needs it (verify during build); `EnemyTarget` by-value +
`projectileSpeed` on the wire; obstacle-exclusion plumbing (`Scout.SetObstacleExclusion` is
already a no-op); `WeightOverride[]` end to end; **boost out of the solver entirely** —
`Control.boost`, `State.boostCooldownRemaining`, `wBoostEffort`, `boostSampleProbability`,
the horizon-skip logic.

## Trained-interface constraints (hard)

`ShipCombat-3500018` maps losslessly: 5 continuous → `.Velocity(vr, vt, vw)` +
`.Facing(ox→angle, weight)`; fire → `Commanded(held)`; boost → ability lane. But three parts
of this design are environment shifts on the training/eval path, and shift-cadence proved the
policy+controller couple is behavior-sensitive (63.00 → 43.50 on movers):

1. **Boost removal from the solver** — the solver currently boosts on its own 15 % sampling
   economics in addition to the policy's pass-through.
2. **Anchor freshening** — archetype anchors go from ≤0.2 s stale to live.
3. Any effective decision-cadence change (the 5 Hz interval is a trained interface).

Gate: every behavior-shifting slice runs the Bench-1 protocol (roster ×2 + mirror, probes)
before it touches the training path; shape-only slices prove preservation by golden replay +
merge gate.

## Landing sequence

1. **PR-1 — shape split, behavior-preserving.** `PilotDecision`/`NavObjective`/`FireControl`,
   builder, `Cost/Terms/` file reorganization, host routing. Boost keeps today's passthrough
   semantics; anchor resolution reproduces today's snapshot timing where the roster depends
   on it. Golden replay + merge gate.
2. **PR-2 — brain types.** `IIntentChooser` → `Brain` subclasses; nine producers + roster
   install path + drawer deletion. Widest mechanical fan-out; no behavior change intended.
3. **PR-3 — boost out of the solver + anchor freshening.** The env-shifting slice; Bench-1
   gated, results recorded against the 63.00/±2.22 yardsticks.
4. Cadence hoist (host-owned decision scheduling) is **designed-compatible but deferred** —
   it rides the MPC controller redesign, not this arc.

Glossary: implementing PRs retire/repoint the `act intent` and `brain / chooser / intent`
rows and add `nav objective` + `lane`; per the vocab ratchet, in the same PRs that move the
symbols.
