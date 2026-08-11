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

    public static NavObjective Drift { get; }
    public static NavObjective Planar(Vector2 velocity);
    public static AnchoredBuilder Anchored(ShipId enemy);   // enemy-frame channels require an anchor — by type
}

public readonly struct AnchoredBuilder   // struct-fluent, allocation-free
{
    public AnchoredBuilder Velocity(float radial, float tangential, float authority);
    public AnchoredBuilder Planar(Vector2 velocity);        // world move, enemy-anchored facing
    public AnchoredBuilder Facing(float offsetRad, float authority);
    public static implicit operator NavObjective(AnchoredBuilder b);
}
```

⚠ **Staging correction (PR-1, 2026-08-07).** `Anchored(...)` takes a **snapshot**
(`EnemyTarget` = kinematics + dynamics) until PR-3, not a `ShipId`. Under identity the
Navigator cannot tell a fresh 5 Hz decision from a re-application of a cached one, so today's
≤0.2 s archetype staleness is unreproducible without machinery PR-3 would then delete.
`ShipId` + per-tick registry resolution lands in PR-3 alongside freshening, under one Bench-1
gate; only that parameter type changes, the builder surface above is final. Move and facing
are independent axes over a shared anchor — the roster's `aimAtTarget` archetypes command a
*world* velocity while facing the enemy, hence `AnchoredBuilder.Planar`.

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
    public abstract BrainDecision? Decide(AIContext ctx);   // null = no decision (mid-transition)
    public virtual void ResetState() { }
}

public readonly struct BrainDecision
{
    public readonly NavObjective nav;
    public readonly FireControl  primary, secondary;
    public readonly bool         boost;
}
```

⚠ **Named `BrainDecision`, not `PilotDecision`** (user ruling, PR-1 scoping). `IPilot` /
`PilotCommand` already own "pilot" at the actuator end, and the two would sit three lines
apart in `AICommander.FixedUpdate`, implying a solve relation that does not exist — only the
nav lane solves into a `PilotCommand`. The arc keeps its name; the type does not.

- `IIntentChooser`, the `Brain` pass-through wrapper, and the `[SerializeReference]` chooser
  authoring (custom `SerializeReferenceDrawers` path) are deleted; brain types are plain
  components (`PolicyBrain`, archetype brains, probe brains) — stock authoring, harness
  installs via `AddComponent`. Decision *laws* stay static pure functions (the
  `RangerChooser.HoldRangeVelocity` pattern) so EditMode testability doesn't regress.
- `BrainDecision` is a transport, not a union: the host (`AICommander`) latches and routes
  each lane independently. `AICommander` keeps its role — wire Scout/Navigator/Gunner/Brain,
  latch the decision, derive trigger edges (`pressed` stays commander-owned, per the #317
  ruling), pass boost to the Booster.
- Trigger semantics unchanged: Gunner mashes, Commanded edge-detects (PlayerCommander
  precedent) — both deliberate, traced 2026-08-06.

## Deletions (the simplification is the feature)

`ActIntent` and its union; `isValid`; `aimAtTarget`; absolute world-frame facing
(`hasFacing`/`facingRad`) from the production objective — verified during PR-1 that
`FacingProbe` does **not** need it (it reads `IPolicyReadout` + `Cost.AnchorYaw`), so no
probe-only builder entry exists; the Navigator's granular `SetFacingOverride` seam survives
for `MpcNavigatorPlayModeTests`; `EnemyTarget.source` + `projectileSpeed` on the wire;
obstacle-exclusion plumbing (`Scout.SetObstacleExclusion` was a no-op); `WeightOverride[]`
end to end; **boost out of the solver entirely** —
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
before it touches the training path; shape-only slices prove preservation by seam-equivalence
EditMode tests + merge gate. ("Golden replay" was the original wording and has no tooling
behind it — `golden-main-d61b31cc` is an RL-eval mask comparator, and the rollout is ±4/75
nondeterministic, so it cannot witness byte-preservation.)

## Landing sequence

1. **PR-1 — shape split, behavior-preserving.** ✅ built 2026-08-07.
   `BrainDecision`/`NavObjective`/`FireControl`, builder, `Cost/Terms/` file reorganization,
   host routing, `WeightOverride[]` deleted end to end. Boost keeps today's passthrough
   semantics; the anchor stays a snapshot (see the staging correction above).
2. **PR-2 — brain types.** `IIntentChooser` → `Brain` subclasses; nine producers + roster
   install path + drawer deletion. Widest mechanical fan-out; no behavior change intended.
   Decision brief below.
2b. **PR-2b — archetype retarget push-down.** Split out of PR-2 (user ruling 2026-08-08)
   because it edits `OpponentArchetypeChooser`, the trained-interface base class the roster
   installs every episode. Pinned targeting (roster, injected `Ship`) vs live targeting
   (`ctx.Combat.Enemy`) becomes explicit on the base, letting `LiveArchetypeBrain` install its
   archetype and destroy itself instead of holding it on a child object. Needs its own call on
   whether a bench run gates it.
3. **PR-3 — boost out of the solver + anchor freshening.** The env-shifting slice; Bench-1
   gated, results recorded against the 63.00/±2.22 yardsticks.
4. Cadence hoist (host-owned decision scheduling) is **designed-compatible but deferred** —
   it rides the MPC controller redesign, not this arc.

Glossary: implementing PRs retire/repoint the `act intent` and `brain / chooser / intent`
rows and add `nav objective` + `lane`; per the vocab ratchet, in the same PRs that move the
symbols. PR-1 did this — `brain / chooser / decision lane` replaces the old row, `nav
objective` is new, and the `lane` collision row gained the decision-lane sense.

## PR-2 decision brief — frozen 2026-08-08

Scope: `IIntentChooser`, the `Brain` pass-through wrapper, and the `[SerializeReference]`
chooser authoring path are deleted; the nine producers become `Brain` components. ~40 files.
**No behavior change** — proof is seam-equivalence EditMode tests + the merge gate, never a
golden replay. Non-goals: boost out of the solver and anchor freshening (PR-3), the cadence
hoist (controller redesign), archetype retarget (PR-2b).

**Forks (settled with the user, in order):**

1. **Brain depth — inheritance where it fits, child object where it doesn't.** Two producers
   contain another producer, and two `Brain` components cannot share a GameObject.
   `InferenceBrain : PolicyBrain` by inheritance — the inference producer genuinely *is* a
   paced policy mailbox, so the containment deletes a field and three delegating members.
   `LiveArchetypeBrain` keeps today's selector shape, holding its archetype brain on a child
   object named `[ArchetypeBrain]` (the `[InferencePilot]` precedent). Rejected: a plain law
   layer under a thin per-family brain (reinstates the wrapper this arc deletes); flattening
   each archetype into its own authored component (loses the archetype dropdown, moves
   retarget anyway).
2. **The selector smell is real and carries forward.** `LiveArchetypeBrain` is a brain that
   does not decide — it picks who decides, exactly as `LiveArchetypeChooser` was a chooser
   that did not choose. Fixing it means pushing retarget into the archetype base, which is
   the trained interface → split to PR-2b rather than folded in or left uncarded.
3. **Brain binding — `AICommander.InstallBrain<T>()`.** The component is now added at runtime,
   after `Awake` has cached nothing. One call does AddComponent + destroy-old + cache + return
   the instance, so the half-installed ship that silently never decides is unrepresentable
   (rung 1), and callers go *through* the coordinator (dependency philosophy #6). `Awake`
   still seeds `GetComponent<Brain>()` so prefab-authored brains work unchanged. Rejected:
   two-step add-then-install (forgettable second call); `OnEnable` self-registration (inverts
   ownership).
4. **A brainless AI pilot is legitimate, not an error.** `TestPilotMPC` ships with no decider
   by design and `[RequireComponent]` cannot hold an abstract type, so `Brain` becomes
   optional exactly like `Gunner`. Today's rung-4 throw disappears because the state it
   guarded stops being representable; the four PlayMode tests that set `Brain.enabled = false`
   drop the workaround.

**Assumptions (code-grounded, none vetoed):** `dt` leaves `Decide` — threaded through all nine
implementations, read by none. `Reset()` → `ResetState()` everywhere, which also dodges the
`MonoBehaviour.Reset` editor-callback collision a straight conversion would create.
`Brain.Chooser` dies; `FacingProbe`, `VelRebaseProbe` and `PolicyPainter` cast `commander.Brain`
directly. `AgentChooser` unseals for the inference subclass and its `internal` constructor
becomes `Configure(model, leashRadius)` — MonoBehaviours cannot be constructed.
`EpisodePair.Spawn`'s chooser factory becomes `Func<AICommander, Ship, Brain>`, since a factory
can no longer `new` a decider. `ArchetypeChoosers.Create` → `ArchetypeBrains.Install(commander, …)`,
keeping one coordinator. Outgoing brains go by `DestroyImmediate` (`EpisodePair.Remove`
precedent; plain `Destroy` is inert in EditMode). `RLVelRebaseEditModeTests` gets a throwaway
GameObject in setup/teardown rather than restructuring `Pack`. The static laws
(`HoldRangeVelocity`, `OrbitVelocity`, `FleeVelocity`) are untouched.

**Blindsider pass found nothing architectural.** Three candidates checked and cleared: the
child-object brain cannot collide with lookups, because `InstallBrain<T>()` removes every
`GetComponentInChildren<Brain>()` call and `AICommander` reads same-object only; per-episode
swap ordering is unchanged (`OpponentRoster.Install` still runs before `pair.Reset()`); both
probes preserve today's re-resolution staleness. Assembly layout also clears —
`Game.RLHarness.Editor` includes `WindowsStandalone64`, so MonoBehaviour conversion does not
break player builds.

Vocab: no new terms. The `brain / chooser / decision lane` row loses "chooser" when the symbols
move.

## PR-3 decision brief — frozen 2026-08-10

Scope: boost leaves the MPC entirely (`Control.boost`, `State.boostCooldownRemaining`,
`wBoostEffort`, `boostSampleProbability`, the horizon-skip, `CostBreakdown.boostEffort`) and the
ability lane becomes commander-owned; `NavObjective`'s anchor becomes a `ShipId` resolved live each
tick. Bench-1 gated on paired arms. Non-goals: the cadence hoist, PR-2b, any observation/action
schema change, and `Pack`'s mixed freshness in the `OpenLoopAnchored` probe arm.

⚠ **The plan's premise for boost removal is false against the assets.** §Trained-interface
constraints #1 claims the solver "boosts on its own 15 % sampling economics". It does not:
`MpcSettings_AgentPilot.asset` carries `boostSampleProbability: 0`, it is the *only* MpcSettings
asset in the project, all three Navigator-bearing prefabs (`AgentPilot`, `ArchetypePilot`,
`TestPilotMPC`) reference it, and `OpponentRoster`'s per-episode clone changes only `wVelTrack`. The
`0.15f` survives as a C# initializer reachable solely through `Navigator.cs`'s null-settings
fallback, which no shipped prefab hits. **The solver has never boosted.** Boost is semantically dead
but *entropically live*: `BurstSolver`'s `rng.NextFloat()` runs unconditionally per step per
candidate on the same stream that feeds the correlated-noise knots.

**Forks (settled with the user, in order):**

1. **Delete the RNG draw; accept the re-phase.** Removing the draw shifts every subsequent noise
   sample — same distribution, different realization. Rejected: a consume-and-discard draw to hold
   the stream bit-identical, which would buy reproducibility the eval path already lacks
   ([[project-eval-sim-nondeterminism]]) at the price of permanently dead code in a Burst hot job.
   The re-phase is zero-mean and directionless — equivalent to a different solve seed — so the
   prediction is that the roster moves within noise.
2. **One bundled bench arm; attribute only on failure.** A boost-only isolation arm is bought only
   if the treatment lands outside noise. Cancellation between the two shifts is acceptable: the gate
   judges the net environment.
3. **The commander resolves `ShipId` once per tick and feeds both lanes.** `AICommander.Route`
   resolves through `Scout.Registry` and hands one `EnemyTarget` to
   `Navigator.ApplyObjective(objective, anchor)` and `Gunner.Aim(anchor)`. Keeps the three lanes
   independent (this plan's core ruling) and makes nav and fire provably agree within a tick.
   Deviation from this doc's literal "resolved by the Navigator each tick": behaviorally identical,
   since `Route` runs immediately before `ComputeCommand` every `FixedUpdate`. Cost is a second
   parameter on `ApplyObjective`. Rejected: Navigator-resolves-plus-commander-re-resolves (duplicate
   lookup that can disagree), and Navigator-exposes-resolved-anchor (makes the fire lane read
   through the nav component).
4. **Baseline and treatment run as paired arms in one port claim; gate on B − A.** The 63.00/±2.22
   yardstick was measured at `7cd7b95a`; main is 43 commits ahead including #340 (Gunner targeting)
   and #285 (asteroid broadphase), and PR-1/PR-2 were proven behavior-preserving by test-count
   equivalence, never by bench. Arm A re-establishes the yardstick and re-validates whether 63.00
   still holds at main.

**Blindsiders (hunted against the locked design):**

5. **The commander sets `PilotCommand.boost`; `Navigator.CommandBoost` and `boostCommanded` are
   deleted.** `Booster` is a plain class privately held by `MovementController` behind an `internal
   ProcessBoost`, so "routed straight to the Booster" is figurative — `PilotCommand.boost` is the
   actual actuator path, per the `PlayerCommander` precedent. The commander latches `decided.boost`
   beside `primary`/`secondary` and stamps the struct `ComputeCommand()` returns. Leaving the
   passthrough in the Navigator would keep the ability lane routed through the nav component — the
   crossing fork 3 rejected.
6. **An unresolvable anchor takes the existing no-decision path** (`ResetNavigation()` + `Auto`
   triggers), not a throw. `Ship.HandleShipDeath` calls only `SetActive(false)` and never
   `ActiveShips.Remove`, so on death the registry stays a superset of live ships and resolution
   still succeeds — the desync runs the safe direction. The sole failure window is a same-frame
   `DespawnShip`/`EpisodePair.Remove` teardown where `Destroy` is deferred; throwing there would
   crash on a legitimate race. This is not a guard absorbing a programmer error — it is the same
   "target gone" state the brains reach one tick later, observed earlier.

**Assumptions (code-grounded, none vetoed):** the registry reaches the commander free — `Scout`
already holds `arena.Registry` and `AICommander` already holds `Scout`, so no `Initialize` signature
changes and dependency philosophy #3 stays untouched. All three `Anchored(...)` producers
(`OpponentArchetypeBrain.Pack`, `RangerBrain`, `PolicyBrain`) already hold the `Ship`, so
`.Anchored(target.Id)` is a field read. Dead targets are already guarded every tick *before* the
10-tick cache, so freshening needs no new death handling. `PolicyBrain.Decide` keeps rebuilding per
tick — it carries the one-shot boost latch and the fire command, so freshening retires the snapshot
*motive*, not the rebuild. `MpcBoostEditModeTests.cs` is deleted wholesale;
`NavigatorBoostPassThroughEditModeTests.cs` is reworked as the ability-lane test. `Pack`'s mixed
freshness (5 Hz law velocity resolved against a live anchor) is accepted, not fixed. The observation
schema is untouched — `boostAvailable`/`boostCooldownPct` read `IShipStatus`, backed by
`MovementController`'s `Booster`, never the MPC's deleted `State.boostCooldownRemaining`.

Vocab: no new terms. *enemy anchor*, *decision lane*, and *noise floor* keep their glossary senses;
the `anchored intent` row stays accurate — only the carrier changes from snapshot to identity.
