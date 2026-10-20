## AI gun strategies research: findings (2026-10-20)

Branch `research/ai-gun-strategies` at `2f3d58b4`, never merged. Every number below comes from AI-against-AI duels run headless on that branch. The raw rows are in `research/gun/data/`, and `research/gun/README.md` says how to rerun them.

### Summary

On main, the AI loses most of each weapon's value to aim, not to trigger choice. The nose sits inside the firing cone only 12 to 16% of the time, with a mean aim error of 24° even against a ship that sits still. Lasers deliver 26 to 31% of their sustained DPS, and ChargeLasers, Railgun and Grenades deal almost nothing.

Four AI changes, with no weapon number touched, move most weapons to 60 to 100% of their ceiling against a duelling target:

1. An aim controller that owns the yaw channel, in place of the MPC's facing cost.
2. A fire plan per weapon family. ChargeLasers release at about a third of a charge. The Railgun commits to a charge, aborts on the last step if the nose is off, and fires from a level ship. Heat weapons vent before a burst. Grenades get a firing solution at all.
3. A hold range per weapon, adjusted by matchup.
4. Strafe jinks while aimed.

With those changes, duel outcomes follow the weapon table: the win ranking in the armed matrix is exactly the ranking of ideal time to kill. What still separates the weapons is balance, and that list is at the end for #335.

### How it was measured

- The rig (`GunResearchPlayModeTests`) spawns two Crimson ships with the default engine and shield (100 hull, 50 shield), one weapon each in the primary slot. Both fly the trial's `AggressorPilot`: `ArchetypeBrain`, the MPC navigator and the Gunner. Ships spawn 30 to 40 u apart with random headings, in side-swapped pairs.
- Armed matrix: every weapon pair, 20 episodes per cell, 90 s timeout.
- Marksmanship: one shooter against an unarmed target flown as Dummy (still), Orbiter (close crossing) or Aggressor (closes to 10 u and holds, the way a duelling enemy flies). 10 episodes per cell, 60 s timeout.
- Strategy knobs live on a per-ship research component, `GunDoctrine`. Its defaults reproduce main, and the baseline rows ran with it attached.
- Headless runs go about 37 sim-seconds per wall-second, so a full 7 × 7 matrix takes 6 to 10 minutes.

### The ceiling table

From prefab values on main `39eb522ef`: one mount, every shot landing, against the 150-point ship.

| Weapon | Burst DPS | Sustained DPS, best use | Best use | Ideal time to kill |
|---|---|---|---|---|
| Lasers | 66.7 | 37.5 | Fire on solution and never overheat. At the heat cap that is one shot per 0.53 s. | 2.1 s (8 hits) |
| TurboLasers | 66.7 | 19.8 | Vent to cold, then dump 19 shots. | 5.5 s |
| Rippers | 60 | 36.9 | Dump 24 rounds, then the 1.5 s reload. | 3.9 s |
| ChargeLasers | 43 | 42.9 | Release at 1/3 charge every 0.42 s for 18 damage. Full charges give 25.0. | 3.8 s |
| Railgun | 30 | 30.0 | A full charge every 1.5 s. The charge overlaps the cooldown. | 6.0 s |
| Missiles | 70 per salvo | 4.7 | 2 × 35 per 15 s. Splash never adds on the target a missile hits directly. | about 75 s |
| Grenades | 105 per volley | 8.75 | 3 × 35 at the centre per 12 s, falling linearly to 0 at 12 u. | about 30 s |

The shield refills 9 s after the last hit, which is why the two launchers' kill times are so long. Two corrections to #335's table: Missiles are 4.7 DPS, not 6.7, and ChargeLasers' best use is not the full charge.

### Baseline on main

In the armed matrix, the win rate against other weapons (a draw counts 0.5) was Lasers 0.93, TurboLasers 0.79 and Rippers 0.78. ChargeLasers, Railgun, Missiles and Grenades scored 0.25 each, and every one of those fights was a draw: those four never killed anything.

Against the unarmed Aggressor, main next to the final doctrine:

| Weapon | DPS (% of ceiling), main | DPS (% of ceiling), final | Time to kill, main then final |
|---|---|---|---|
| Lasers | 9.8 (26%) | 36.8 (98%) | 16.7 to 4.1 s |
| TurboLasers | 7.5 (38%) | 19.7 (100%) | 22.2 to 7.6 s |
| Rippers | 6.5 (18%) | 24.8 (67%) | 27.8 to 6.0 s |
| ChargeLasers | 1.3 (3%) | 26.6 (62%) | no kill to 5.6 s |
| Railgun | 0, never fires | 20.7 (69%) | no kill to 7.4 s |
| Missiles | 4.6 (98%) | 4.7 (99%) | no kill in 60 s either way |
| Grenades | 0, never fires | 6.2 (71%) | no kill to 48 s (9 of 10) |

On main, 63 to 81% of the shots the AI did fire hit. The trigger was not the problem; time on target was.

### What each change bought

1. **Aim.** Raising the MPC facing weight from 1 to 10 lowers the mean aim error from 24° to 19° and lifts Lasers from 26% to 44% of ceiling. A weight of 30 does no better. The facing cost is quadratic near zero, so a 128-sample solver cannot resolve the last 20°. The aim controller sets a desired yaw rate of bearing rate plus √(2 × yaw acceleration × error), then applies torque toward it within the engine's yaw torque. That brings the mean aim error to 3 to 8°, with the nose inside the cone about 90% of the time the target is in range. This change alone takes Lasers and TurboLasers to 97 to 102% of ceiling against moving targets. The controller takes yaw only while there is a target and no missile to evade; the MPC keeps thrust and strafe. In the harness asteroid field at density 1, it lost no shooter to rocks in 140 marksmanship episodes, and 9 of 560 field duels ended in a rock or ram kill. So it does not break obstacle avoidance at that density.
2. **ChargeLasers fire plan.** `ChargeTime` fires on its own at full charge, which is where #836's blind shots came from. Below full charge, the release is the Gunner's to time. The plan holds the trigger while roughly aimed and releases on the first on-solution step once the charge is at least 0.34 and the cooldown is ready. That raises ChargeLasers from 20.4 to 26.9 DPS over aim alone, and moves them from never killing to second place in the matrix. This is the release timing #409 was after, and it only exists below full charge.
3. **Railgun fire plan.** Start a charge when a shot is plausible within the charge time. Keep the nose on where the target will be at release. Abort on the last step if the nose is off, and stop strafing for the last 0.3 s so the ship is level when it fires. The Railgun goes from no shots to 69% of ceiling against the Aggressor.
4. **Heat.** Main's rule, never take the shot that would overheat, is already right for Lasers. TurboLasers gain from venting to cold before the next burst. At the cap they fire one shot per 0.37 s, which is 13.6 DPS; a full vent and dump averages 19.8.
5. **Grenades.** `Grenades` has no firing solution on main, so the AI never launches one. The plan lobs at the predicted position, using a travel time of about d / 20 s. It launches only when the aim point is at least 16 u away, from a hold range of 17 to 20 u. That gives 97% of ceiling against a still target and 71% against the Aggressor. The pilot still takes self-hits when an enemy closes inside its own blast.
6. **Hold range per weapon.** Holding the Railgun at 30 u instead of 10 u raises its matrix score from 0.38 to 0.57. Missiles do worse at 30 u, hitting a still target 39% of the time, than at 10 to 13 u. Matchup ranging stands just outside the enemy's reach whenever the ship can still hit from there. Combined with letting guns fire out to 70% of their bolt range, it helps the guns a little and hurts the Railgun, because the guns can now reach it.
7. **Dodging.** Strafing at 40% of top speed, flipping every 1.2 s, cuts gun hit rates from about 86% to 64%.

### Final armed matrix

All seven weapons, both ships flying the final doctrine, 20 episodes per cell (`x9-final`):

| Weapon | Win vs others, main then final | % of ceiling in fights, main then final | Ideal time to kill |
|---|---|---|---|
| Lasers | 0.93 to 0.96 | 31% to 81% | 2.1 s |
| ChargeLasers | 0.25 to 0.78 | 2% to 49% | 3.8 s |
| Rippers | 0.78 to 0.74 | 20% to 56% | 3.9 s |
| TurboLasers | 0.79 to 0.52 | 39% to 74% | 5.5 s |
| Railgun | 0.25 to 0.34 | 0% to 37% | 6.0 s |
| Grenades | 0.25 to 0.15 | 0% to 69% | about 30 s |
| Missiles | 0.25 to 0.01 | 79% to 68% | about 75 s |

The final ranking matches the ranking of ideal time to kill exactly. ChargeLasers beat Rippers 65 to 35, and both beat TurboLasers every time.

A cross of the final doctrine against main's AI (`h2h`) shows the size of the skill gap. A ship carrying any of the five guns and flying the final doctrine beat main's AI with every weapon, Lasers included, 20 times out of 20. Smart Missiles scored 0.45 and smart Grenades 0.48 against main's AI.

### What is left, and whose it is

The AI could still improve here:

- Rippers (56%) and ChargeLasers (49%) still spend half their in-range time without a firing solution against a dodging enemy. Their trigger tolerances are first guesses, at 0.8 to 1.2 times the target's angular radius.
- Grenades take self-hits when the enemy closes inside 12 u. The pilot needs to back off, or stop lobbing, inside its own blast.
- No aim can hold a close crossing target. An Orbiter at 6 to 7 u sweeps more than 200°/s across the nose, faster than the 180°/s yaw limit. For the Railgun, the plan would be to start the charge ahead of the predicted crossing. Not tried.

Balance and mechanics facts, for #335. Nothing here changed them:

- Lasers win 96% against every other weapon, even with every ship flying well. At equal accuracy, 8 hits in 2.1 s and a 260-damage magazine against a 150-point ship put them ahead of everything else.
- TurboLasers' 95-damage magazine and Rippers' 144-damage magazine are both under the 150-point ship, so each needs a vent or a reload before the kill.
- Missiles (70 per 15 s) and Grenades (about 90 per 12 s) cannot outpace the 9 s shield refill fast enough to win a duel against any gun, however well they are flown.
- The Railgun's beam is a zero-width ray, the hulls are flat (collider half-thickness 0.26 u), and ships bank up to 35° when they strafe. The Railgun's hit rate by the target's bank at release is 65% at 0 to 5°, 38% at 10 to 15°, 14% at 20 to 25° and 2% at 30 to 40°. The shooter's own bank costs about as much. Against dodging pilots, it hits about 20% of its shots without a level release and 48% with one. If the Railgun should stay competitive against dodging pilots, a beam radius or a hit test against the body sphere is the mechanics fix.

### Recommendations for the canonical AI weapon strategy

In order of value per unit of work:

1. **Give the fire lane the yaw channel.** Put an aim controller in the Gunner's lane, or in the AICommander that composes it. It outputs a yaw torque within the engine's limits, with bearing-rate feedforward, whenever there is a target and no evasion. This answers #871's experiment 1: the aim defect is in the MPC's facing term, and fixing it is the largest single lever for every gun.
2. **Fire plans per weapon family**, declared by the weapon and run by the Gunner: tap release for partial-charge weapons; commit, last-step abort and a level release for full-charge weapons; vent before a burst for heat weapons whose cooling waits on a delay; and a placed-shot solution for Grenades. The weapon facts each plan needs through `IWeaponContext` are small: charge state, cooldown readiness, heat, and the launcher's range.
3. **A hold range per weapon** in the pilot, then matchup ranging once enemies carry visible loadouts.
4. **Strafe jinks** as a pilot behaviour, with the level-before-release rule for hitscan weapons.

Before shipping item 1 to the trial, decide lethality. Against an unarmed Aggressor, Lasers' time to kill falls from 16.7 s to 4.1 s, so a trial enemy would kill the player about four times faster. #871 already pairs the aim change with a lethality dial.

### Caveats

- AI against AI only: 1v1, one chassis (Crimson with the default engine and shield), primary slot only. Nobody has played against it, so how it feels against a person is unknown.
- With 20 episodes per matrix cell, win rates move by about 0.1 between replicates.
- The smart trigger skips the line-of-sight check. That only matters in the asteroid field.
- Batch 2's aim controller rows (`m-lock`, `m-lock-smart`, `m-lock-smart-prof`) are invalid: a unit bug capped the yaw rate at 3°/s. The `m-lock2*` rows are the corrected runs.

### Reproducing

From `D:/amind/git/agent-5` on the branch, `research/gun/run.sh research/gun/batch9.json` runs the final matrix and the cross against main's AI, and `python research/gun/analyze.py <run>.jsonl` prints the tables. The knobs are the fields of `GunDoctrine`.

---
_Generated by [Claude Code](https://claude.ai/code)_
