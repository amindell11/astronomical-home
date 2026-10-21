# Gun research rig (research branch, never merged)

Research for #409: how the scripted AI can get each weapon close to its ceiling. The findings report is the 2026-10-20 comment on #409.

## Run

From the pool slot named in `run.sh` (agent-7 for the 2026-10-21 runs; edit it to your slot), on branch `research/ai-gun-strategies`:

```
research/gun/run.sh research/gun/<batch>.json
python research/gun/analyze.py results/gun-research/<tag>.jsonl
python research/gun/shots.py results/gun-research/<tag>.jsonl   # Railgun per-shot bins
python research/gun/h2h.py results/gun-research/h2h.jsonl       # smart (A) vs main's AI (B)
```

`run.sh` copies the batch to `config.json` and runs `Tests.PlayMode.Research.GunResearchPlayModeTests.Run` through the pool's test runner. Rows land in `results/gun-research/<tag>.jsonl`; copies of every run are in `data/`.

## Batch file

`{ "runs": [Config, ...] }`. A `Config`:

- `mode`: `matrix` (both ships armed, every weapon pair; `opponents` narrows side B, `ordered` runs both orders) or `marks` (side A armed against an unarmed target flown as each archetype in `opponents`: Dummy, Orbiter, Aggressor, Evader).
- `episodes`, `seed`, `timeoutSec`, `spawnMin`/`spawnMax` (u), `field` and `fieldDensity` (harness asteroid field).
- `a`/`b`: `pilot` (prefab name under `Assets/Prefabs/Pilots`) and `doctrine` (JSON written onto that ship's `GunDoctrine`).

Odd episodes swap the spawn sides of the even episode before them.

## Doctrine knobs

Fields of `Assets/Scripts/AI/Research/GunDoctrine.cs`; every default is main's behaviour.

- Pilot: `aimAuthority` (MPC facing weight scale), `aimLock` (aim controller on the yaw channel; `aimAccelFrac`, `aimTau`, `aimKp`, `aimAccelFF`), `holdRange` or `autoProfile` with `range<Weapon>`, `matchupRange` with `outrangeMargin`, `strafeFraction` and `strafePeriod`.
- Trigger (`smartTrigger`): `geomTolScale` (cone = target radius at range), `extendGunRange`/`gunRangeFrac`, ChargeLasers `tapCharge`, `chargeWideCone`, `chargeTolScale`, `fullChargeReleaseErr`; Railgun `railCommitCone`, `railAbortErr`, `railTolScale`, `railLeadCap`, `railMaxTargetBank`, `railSteady`; heat `ventTo`; Grenades `grenadeMinRange`, `grenadeMaxRange`.

## Runs

- `baseline.json`: main's AI (`base-matrix`, `base-marks`).
- `batch1`: MPC facing weight 10 and 30, with and without the smart triggers.
- `batch2`: invalid. A unit bug capped the aim controller's yaw rate at 3°/s; `m-lock`, `m-lock-smart`, `m-lock-smart-prof` are discarded.
- `batch3`: corrected aim controller (`m-lock2*`).
- `batch4`: first armed matrices with the smart doctrine.
- `batch5`: matchup ranging, extended gun range, dodging.
- `batch6`: Railgun tracking sweep, first asteroid-field runs.
- `batch7`: best doctrine with dodging, empty and field.
- `batch8`: Railgun bank logging, bank abort, level release.
- `batch9`: final matrix (`x9-final`) and the smart-against-main cross (`h2h`).

## Research tail (2026-10-20)

The second-round rulings on #409 asked for six follow-ups. Findings are in the dated 2026-10-20 tail comment on #409 and, for the Railgun `chargeTime`, on #335.

`python research/gun/mkbatch.py <a..g>` writes `tail-<a..g>.json`. `python research/gun/tail.py <dir> marks <metric> <tag>...` and `python research/gun/tail.py <dir> matrix <tag>...` print the side-by-side tables.

New `GunDoctrine` knobs (defaults are main's behaviour):

- MPC aim without the aim controller: `mpcFacingWidth`, `mpcYawRate`, `mpcNoiseStd`, `mpcSamples`. The rig applies them to a per-ship clone of the navigator's `MpcSettings`.
- Grenades: `grenadeSafeMargin` (no launch with the enemy, or the shooter at arrival, inside blast radius plus margin), `grenadeBackoff` with `grenadeBackoffExtra` (retreat at full speed inside that radius plus the extra), `grenadeChainGap` (the next charge waits for the previous one's arrival, its wave's 0.8 s expansion and this gap).
- Railgun against a close crossing target: `railOrbit`, `railOrbitRate`, `railOrbitCommit`.
- Skill dial: `skill` in [0, 1] (-1 is off), `skillReactionMax`, `skillNoiseMax`, `skillNoiseTau`, `skillAccelMin`, `skillCurve`, `skillTolGain`, `skillSeed`. Error scale e = (1 - s)^curve. The Gunner sees the target delayed by e × reaction and rotated about the shooter by a correlated bearing error of e × noise degrees. The aim controller's acceleration fraction runs from `skillAccelMin` to `aimAccelFrac`, the strafe dodge scales by s, and the trigger tolerances widen by 1 + gain × e.

The rig also logs `selfDamage` per side.

Batches: `tail-a` (first probes of every task, plus the Railgun row at 1.5), `tail-b` (MPC solver noise, tolerance sweep in the armed matrix, grenade wave gap, first dial), `tail-c` (final matrix with MPC aim, and MPC aim against the aim controller), `tail-d` (Railgun `chargeTime` 1.1, run between commits `3b7189d5` and its revert `e6b0debb`), `tail-e` (dial v2), `tail-f` (dial v3 noise bracket), `tail-g` (final dial).

## Lethality calibration (2026-10-21)

The 2026-10-21 ruling on #409 asked for the skill dial and earned accuracy to be calibrated against a stand-in for the player. The findings are in the dated 2026-10-21 calibration comment on #409. Every crowd run includes main's #1041 (missile direct hits bypass shields), merged into this branch before the first run.

`mode: crowd` runs one stand-in (side `a`, team 0, carrying `weapons[0]`) against up to N trial enemies, one run per value in `crowd`. Enemies come from `TrialSector.prefab`: the WaveDirector's roster (chassis and pilot) and its loadout pool (engine, shield, primary weapon), drawn uniformly. Side `b`'s doctrine goes on every enemy. A dead enemy is replaced one per `spawnInterval` at `ringRadius` from the stand-in, facing it, as the WaveDirector does. The episode ends on the stand-in's death or at `timeoutSec`. The draws depend only on `seed` and the episode, so configs with the same seed meet the same enemies in the same order.

Each row has the stand-in's survival, kills, shots and hits, boosts, the damage it took by enemy weapon, and a histogram of its lateral acceleration across each enemy's line of sight (1 u/s² bins). It also has one record per enemy life: shots, hits, envelope steps while in range, aim error in range, time to first hit after first coming in range, and the earned-accuracy counters.

`python research/gun/mkcrowd.py <smoke|r1|r2|r3>` writes `crowd-<name>.json`. `python research/gun/crowd.py <run>.jsonl... [--by-weapon] [--lat]` prints the tables. `python research/gun/surv.py <run>.jsonl...` prints survival with its standard error per N.

New `GunDoctrine` knobs (defaults are main's behaviour):

- Earned accuracy: `eaE0` is the RMS bearing error in degrees at acquisition (-1 is off). `eaT` is the seconds of tracking that bring it to zero, with shape `eaCurve`. Tracking accrues only while the target is inside `eaCone` degrees of the nose, within `eaRange` u, with line of sight. `eaLoseRate` is the progress lost outside the cone, as a multiple of the gain rate. `eaJinkAccel` is the target's lateral acceleration across the line of sight (0.1 s smoothing) that counts as a jink, and `eaJinkReset` is the fraction of progress a jink removes. `eaBoostReset`, `eaLosReset` and `eaReacquireReset` do the same for a target boost, a broken line of sight and a target switch. `eaNoiseTau` and `eaSeed` shape the error's wander.
- Stand-in: `hardJink` holds full strafe input and flips it every `strafePeriod`, the way a keyboard player strafes.

Batches: `crowd-smoke`, `crowd-r1` (references, dial sweep, earned-accuracy grid), `crowd-r2` (stand-in sensitivity, reset rules, combined levers), `crowd-r3` (fresh-seed confirmation of the finalists at 40 episodes).
