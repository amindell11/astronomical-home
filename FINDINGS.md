# Elite jitter: measurements (origin/main 0979df12f)

## Setup
Scratch capture scenario `EliteJitterProbe` (source: `EliteJitterProbe.cs.txt`). Each subject ship is spawned through `UnitService.SpawnShip` with the real elite wiring (faction elite chassis, ElitePilot, profile scale, mods, loadout) and fights a Crimson Aggressor. Hull is x20 so nothing dies. It logs every FixedUpdate for 12 s; analysis skips the first second (`analyze.py.txt`). Two independent runs (`summary-main.txt`, `summary-main-r2.txt`) agree.

Variants per faction: elite, elite with torque factor 1, elite with bank torque/damping divided by the torque factor ("bankfix"), elite with inertia x mass factor x 1.13^2 ("inertiaScaled", run 2 only), minion, player chassis at the elite scale, player chassis at scale 1. For Shoal there is also elite at size 1.5 (the pre-#1660 profile).

## Key numbers (run 2; run 1 matches)

| ship | bank rate RMS (deg/s) | bank-rate sign flips/s | yaw-rate wobble RMS* | yaw wobble dominant Hz | ticks at angular-velocity cap |
|---|---|---|---|---|---|
| Shoal elite | 237 | 39 | 66 | 23 | 69% |
| Shoal elite at size 1.5 (pre-#1660) | 188 | 37 | 52 | 24 | 69% |
| Shoal player Valis at elite scale | 241 | 42 | 67 | 24 | 75% |
| Shoal minion | 107 | 5 | 21 | 4 | 20% |
| Shoal elite, torque 1 | 108 | 5 | 20 | 1 | 11% |
| Shoal elite, bankfix | 100 | 4 | 17 | 3 | 14% |
| Shoal elite, inertiaScaled | 106 | 7 | 21 | 2 | 16% |
| Bastion elite | 55 | 3 | 18 | 1 | 64% |
| Bastion minion | 74 | 2 | 6 | 2 | 35% |
| Veil elite | 88 | 3 | 18 | 2 | 17% |
| Veil minion | 87 | 4 | 19 | 1 | 32% |

*Yaw-rate wobble = RMS of the second difference of yawRate per tick, a high-pass of the tick-to-tick wobble.

## Reading
- **Root cause (Shoal):** the bank spring-damper is discretely unstable. All Valis-family prefabs author an explicit inertia tensor (m_ImplicitTensor 0), roll inertia 30.9, and it never follows ShipScale. Bank damping 1650 x torque factor 2.5 = 4125 gives c*dt/I = 2.67 at dt 0.02 (minion: 1.07). Explicit rate damping diverges above ~2, so the bank flips sign every tick: the 23-25 Hz line is the Nyquist frequency of the 50 Hz step. The divergence is capped only by `rb.maxAngularVelocity`, which clamps the whole angular-velocity vector, yaw included. That is how the bank flapping shows up as yaw jitter.
- It is **not a #1660/#1683 regression in the physics**. The size-1.5 variant jitters the same way, because inertia doesn't scale with size. The elite flew the same Valis tensor and the same 2.5x torque before #1660. The new elite mesh/wings may make it more visible; I didn't test that.
- Suspect (b), wrong AI yaw-inertia frame: **refuted**. Model/true yaw inertia is 1.000 (Valis, Bulwark) and 1.004 (Nightshade, whose tensor is rotated a few degrees).
- Bastion: bank is stable (c*dt/I 0.89). Its yaw wobble is about 3x the minion's (18-29 vs 6-8), at 1-3 Hz, and it sits at the angular-velocity cap 64-88% of the time. That is slow, over-strong yaw, not jitter. Torque 1 brings it to minion level in run 2 (7.6) but not in run 1 (21), so it's noisy. Inertia scaling halves it.
- Veil: elite matches its minion.
- Suspects (c) Extrapolate interpolation and (d) wing-hinge visuals: not isolated. (c) probably magnifies the 25 Hz flapping visually.

## Candidate fixes (measured, not committed)
All three bring the Shoal elite back to minion level: torque factor 1; bank torque/damping not multiplied by the torque factor; inertia scaled by mass x size^2 (c*dt/I 1.40). Scaling inertia is the physically principled one: inertia follows the scaled ship, which also tames Bastion's yaw. A structural option: derive bank damping from the critical-damping ratio and the true roll inertia, or validate c*dt/I < ~1.5 at Initialize.

## Clips
shoal-elite-*.mp4 and bastion-elite-*.mp4: elite vs Crimson, presentation on. Realtime = 50 fps sim time; slowmo = the capture's unscaled timing, about 5x slow.
