# Fast traversal development result

Approved contract: [arc ruling](https://github.com/amindell11/astronomical-home/issues/461#issuecomment-5614047294). Five development seeds4101–4105, 30 seconds each, field weight3 versus0, paired empty controls, unchanged runtime settings. Held-out4201–4220 remain unused.

## What did the field improve?

The corrected benchmark recorded zero swept collisions in all ten terrain episodes. Median goalward speed was20.23762m/s on versus20.02636m/s off: +1.0549%, below the approved+10%. Per-seed gains were+7.80%, +0.34%, +16.05%, -2.84%, +1.05%. This is a failed development acceptance result, not a held-out verdict. No live Unity behavior claim follows from the solver-model result.

The weakest seed4104 lost most speed in seconds15–20: mean forward velocity21.20m/s on versus23.09m/s off, with lower mean thrust. Both routes remain efficient (~0.998 progress/path length); neither stalls beyond startup. Field spacing is37.386m for5–8m-diameter obstacles. Coarse field sampling is a hypothesis for a focused follow-up, not an established explanation. Current empty-course control speeds are approximately21.3–21.8m/s; requesting25m/s does not force the optimizer to attain it. Keep the approved10% threshold and the empty controls visible.

## What had to be fixed in the benchmark?

The original rig passed its complete static terrain scan straight to the96-entry solver obstacle buffer. With about7050 circles in terrain order, the solver saw distant rocks and omitted nearby hazards. Field-off traces matched empty-course outcomes exactly despite hundreds of measured collisions. The rig now queries its existing obstacle-field producer around the ship and keeps the nearest64, matching the production bounded scan shape. Collision measurement still checks all terrain. The producer uses existing ObstacleSelection.KeepNearest, also preserving complete regional field queries through the scanner's growing buffer. No terminal-field runtime source or setting changed.

Fix ladder: remove the oversized solver-input construction through the existing producer (rung1), rather than tolerate the truncation with a guard. FarClutter_DoesNotHideNearObstacleFromSolver exercises the actual call chain: adding120 distant rocks before one nearby rock must preserve controls. It failed against HEAD's rig and passed after the fix. The swept-collision check covers tunnelling, tangency and zero-length steps. A final endpoint consistency assertion includes the last integration step.

## What ran?

- 231631: compile failure, unsupported Assert.Multiple in this NUnit version; corrected assertion, no measurements.
- 231717 / development-20260910-061733-094: exposed invalid rig obstacle input; all raw results preserved, excluded from field-performance conclusions.
- 232006: intentional regression-red on the original rig.
- 232053 / development-20260910-062109-438: corrected development matrix; safety passes, speed criterion fails. Two correctness tests pass, held-out test skipped.
- 232318: existing solver-rig suite,10passed/3opt-in skipped, no failures.

summary.csv, all trajectory/cost traces, terrain, frozen settings, verdict.txt, analysis.json and development.png accompany the corrected run. The benchmark does not change historical acceptance results or the dense-chase video requirement. No broad repeat sweep or physics-isolation work followed.

## What did the two controlled follow-ups establish?

Seed4104 stayed fixed. Resolution96 at weight3 reduced spacing37.386→17.666m but only improved20.17151→20.21058m/s; still2.65% slower than field-off20.76025. Mean terminal cost117.779→0.103, demonstrating that its absolute magnitude is not a reliable measure of its effect on selected controls. Run233016, resolution96-seed4104-20260910-063036-962, zero collisions, speed acceptance fails.

At original48 resolution, weight0.3 improved the same seed to20.70429m/s (only0.27% below off), with zero collisions. Run233157, weight0.3-seed4104-20260910-063212-500. Reducing weight recovered0.53278m/s; doubling resolution recovered0.03907m/s. This supports excessive field influence on this particular seed; it does not prove a better five-seed setting. Both changes are diagnostic overrides, not committed settings. Held-out seeds remain unused.

A five-development-seed check of weight0.3 is prepared as WeightDevelopment with MPC_TRAVERSAL=weight. It has NOT launched: available RAM fell to6.995GB then6.869GB, beyond the practical close-to8GB launch allowance. Do not change thresholds or accept the single-seed tuning as validation. Existing development matrix still fails+10%. No ongoing Unity run remains.

## Did the lower weight generalize?

The five-seed follow-up completed2026-09-10 after RAM recovered to8.6GB. No applications needed saving or closing. Run072228 / weight0.3-seed0-20260910-142314-340 completed all ten terrain episodes and paired empty controls. Zero swept collisions in both arms. Weight0.3 median20.05124m/s versus field-off20.02636m/s: +0.1242%, worse than the weight3 median20.23762m/s (+1.0549%). The10% development criterion fails. Seed4104 recovery did not generalize: weight0.3 loses speed versus weight3 on4103 and4105. Reject this setting as a traversal improvement; retain committed weight3. Field-off summary values reproduce the prior five-seed run exactly. Held-out seeds remain unused. No broader sweep or new Unity run is pending.
