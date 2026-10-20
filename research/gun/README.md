# Gun research rig (research branch, never merged)

Research for #409: how the scripted AI can get each weapon close to its ceiling. The findings report is the 2026-10-20 comment on #409.

## Run

From `D:/amind/git/agent-5` on branch `research/ai-gun-strategies`:

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
