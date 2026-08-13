# Fire Lane Rework — engage bool, automated trigger authority

> STATUS: living — outlives its arc because `Intent_Grammar.md` inherits the FIRE lane from here
> rather than restating it. PR-1 (the collapse) LANDED as #390 `fbc52bce`, 2026-08-13; what stands
> is the engage-bool semantics and the trigger-authority ruling. Supersedes the Fire row of the
> Pilot Decision Seam ruling. Not deletable while Stage A points at it.

Design approved by the user 2026-08-11 (this session). Direction: **weapon aiming and fire
strategy are automated** — the policy decides on the ~0.1 s intent cadence while the Gunner runs
every physics step at 50 Hz with exact geometry, so trigger timing is the Gunner's job and the
brain's fire output is strategic only. AI support for charge weapons is **dropped, not defended**;
the player-side charge weapons are untouched.

Supersedes, in `Pilot_Decision_Seam.md`: the Fire lane row (`FireControl: Hold | Auto |
Commanded(held)`), the "Trigger semantics unchanged: Gunner mashes, Commanded edge-detects" note,
and the trained-interface mapping "fire → `Commanded(held)`". Everything else in that plan stands.

## Ruling

The fire lane datum becomes **one bool per slot: `engage`**. The brain gates ("weapons free on
this slot"); the **Gunner is the sole trigger authority for AI ships**, converting engagement into
trigger commands at physics rate using its own firing solution (envelope + intercept lead).

- `FireControl` (the tri-state) is deleted. `BrainDecision`'s fire lanes become two bools.
- The Commanded path is deleted end to end: `AICommander.FireCommandedPrimary()`,
  `prevPrimaryHeld`, the `IsCommanded` routing in `Route`/`FixedUpdate`, and
  `Gunner.Authority()`.
- `Gunner.Fire(bool engagePrimary, bool engageSecondary)`: per slot,
  `fire = engage && hasEnemy && sight.Evaluate(aim)`. No authority arbitration.
  - Keep the gunner's target lookup as **one substitution point**: `Intent_Grammar.md` Stage C
    swaps the target source from the context enemy to the policy's AIM referent (how
    "shoot the asteroid" composes — AIM→asteroid + engage, no new fire machinery). One seam to
    swap, not scattered enemy reads through the fire path.
- **Actuator-facing semantics are unchanged in this arc** — the Gunner keeps today's
  `pressed = held = fire` push and the `WeaponCommand{pressed, held}` seam stands. Per-weapon
  trigger discipline (deliberate semi-auto presses, heat pacing, charge release-timing) is the
  follow-on marksmanship arc, not this one.

### Producer mapping

| Producer | Today | Becomes |
|---|---|---|
| `PolicyBrain` | `Commanded(action.fire)` / `Hold` | `engage = action.fire` / `engage = false` |
| `ArchetypeBrain.Pack` | `engages && Production ? Auto : Hold` | `engage = engages && drive == Production` |
| `RangerBrain` | `Auto, Auto` | `true, true` |
| No-decision default (`AICommander.Route`) | `Auto` both slots | `true` both slots (gunner free-fire, as today) |

## AI charge-weapon support: dropped, not defended

The three-way contortion — `FireControl.Hold` as *total silence*, `Gunner.Fire` skipping unowned
slots rather than pushing a released trigger, the mash discipline — exists solely to uphold one
invariant: *never feed a charge weapon a release it didn't earn*. No ship prefab mounts a charge
weapon in either slot (verified: all production ships carry Lasers primary + Missiles secondary;
ChargeLasers/Railgun reach the game only through the player hangar catalog). The invariant
defends a configuration that does not occur, and it goes with the tri-state; the comments citing
it (`FireControl.cs:17`, `Gunner.cs:46`) go too.

If someone later mounts a charge weapon on an AI ship, behavior degrades but does not fail:
`autoFireAtFull` still fires at full charge while engaged, and a disengage edge may release a
partial shot. Poor play, not a broken invariant — no guard is added (fix ladder: hypothetical,
not an observed failure). The future home for real AI charge support is **Gunner release-timing**
— deterministic hold-charge/release-when-solution-good logic, a coded capability the marksmanship
arc can add when a charge weapon actually enters an AI loadout.

## Player path untouched

`PlayerCommander`, `WeaponCommand{pressed, held}`, every weapon's trigger semantics, `ChargeTime`,
`ChargeGaugeUI`, and the hangar catalog all stand. The rework is strictly upstream of the
actuator seam.

## Trained-interface constraints (hard)

- **Same action space — no schema break, no retrain.** `ShipCombat-3500018`'s fire action is
  *reinterpreted*: from directly-held trigger to engage gate. The Gunner's envelope + lead now
  sit between the policy's fire output and the actuator, so shots land only on a good firing
  solution. This is an **environment shift** → Bench-1 gated per the seam plan's standing rule
  (roster ×2 + mirror, probes; paired arms, baseline pinned to the branch-point sha, judged
  against the 63.00/±2.22 yardsticks).
- **The policy's secondary lane stays disengaged** (`engage = false`), preserving today's
  permanent `Hold` — the RL ship has never fired a missile. The *when* is already ruled:
  `Intent_Grammar.md`'s FIRE slot carries both engage bits, so the secondary arms at its
  Stage C schema window. The launch discipline that bit presupposes (lock gating, deliberate
  presses) is marksmanship-arc work and should precede it.
- The open-loop (K1-2) archetype arms keep fire suppressed via `drive != Production`, unchanged.

## Deletions (the simplification is the feature)

`FireControl.cs`; the fire-lane `FireControl` fields on `BrainDecision`;
`AICommander.FireCommandedPrimary` + `prevPrimaryHeld` + all `IsCommanded`/`IsAuto` branching;
`Gunner.Authority()`; the charge-release-defense comments. Tests follow the symbols:
`AICommanderManualFireEditModeTests` (the Commanded path) is deleted or reworked to the engage
seam; `PilotDecisionSeamEditModeTests`' `FireControl` block goes; the
`WeaponTriggerSemanticsPlayModeTests` gunner-mash case keeps its assertion with the new
signature.

## Landing sequence

1. **PR-1 — the seam collapse. LANDED #390 `fbc52bce` 2026-08-13.** Net-negative line count as
   planned. **Bench-1 was WAIVED by the user at merge, not run** — the policy-fire
   reinterpretation is on main unmeasured, stacked with #384 and #389; tracked as
   [#408](https://github.com/amindell11/astronomical-home/issues/408).
   - **Bench-failure branch, pre-ruled:** if the Bench-1 arms fail the 63.00/±2.22 no-regress
     bar, do NOT iterate in place — the change is then a policy+environment couple break
     (shift-cadence precedent) and reclassifies as a **training-environment candidate**, riding
     `Intent_Grammar.md`'s Stage C retrain window as a co-rider instead of a standalone landing.
2. **Marksmanship arc — separate, evidence-driven, own doc when opened.** Carded as [#409](https://github.com/amindell11/astronomical-home/issues/409). Per-weapon trigger
   discipline in the Gunner: deliberate semi-auto launch policy (today the mash re-presses
   missiles every step — "one launch per press" is violated for AI, but a strict edge fix
   without a launch policy would nerf AI to one missile per engagement window, so the fix
   *requires* the design work); missile lock gating; laser heat pacing; envelope tuning; charge
   release-timing if a charge weapon ever enters an AI loadout; the launch discipline behind
   Stage C's secondary engage bit (the *whether* is ruled in `Intent_Grammar.md`, the *how* is
   this arc's).

Glossary: implementing PR retires/repoints the fire-lane rows that name `FireControl` and its
`Hold`/`Auto`/`Commanded` senses; *engage* enters as the fire-lane gate sense. Per the vocab
ratchet, in the same PR that moves the symbols.

## Forks (settled with the user, 2026-08-11)

1. **Engage-gate reinterpretation over schema break.** Keeping fire in the action space and
   reinterpreting it costs no retrain; dropping it from the schema is cleaner in the limit but
   is a #377-class break — deferred to the next schema-break window.
2. **Automated trigger authority over brain-owned trigger.** The policy cannot react at trigger
   timescales (0.1 s cadence vs 50 Hz physics); trigger timing is closed-form and computable.
   The brain keeps the strategic bit only. (Reverses the earlier in-session lean toward
   brain-owned fire; the user's call, and the timing math backs it.)
3. **AI charge support dropped, player support kept.** Removing charge weapons from the game was
   considered and rejected — the simplification target is the AI seam, and the player mechanic
   is untouched. The eventual AI answer is gunner release-timing, not brain release-timing.
4. **The missile-mash fix is deferred to the marksmanship arc.** It is real (AI re-presses every
   step) but fixing it means designing a launch policy, which is behavioral work outside this
   collapse's scope; a mechanical edge fix would silently change combat behavior in the nerf
   direction.
