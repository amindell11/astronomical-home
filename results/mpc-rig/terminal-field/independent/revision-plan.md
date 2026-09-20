# Reopened implementation investigation

User approved continuing with the implementation design reopened on 2026-09-09. Acceptance thresholds, horizon, durations, collision requirements and the no-inspection restriction on the posted PR remain unchanged. Candidate 1 and its failed validation remain the record; a revised design must not overwrite that evidence.

## What do the existing traces establish?

On the original development seed 2001, the baseline and weight-0.03 candidate both travel their first metre only at 22.4 simulated seconds. Weight 1 starts by 2.1 seconds and arrives. Strong weights regress session maneuvers. The investigation must separate startup guidance from unnecessary local maneuver penalties, rather than just repeat a weight search.

Correction to earlier report terminology: the solver-rig seeds control solver sampling, NOT the eight-rock terrain layout. The minefield is authored once in RigBingoCard.MinefieldTransit. AdditionalTerrainSweep was incorrectly named; its 3601–3620 runs vary solver randomness on the same terrain. Harness session seeds do rebuild terrain. Do not cite the solver sweep as varied-terrain generalization.

## Which controlled probes come first?

1. Record the current production baker's response at fixed physical sample points as the ship moves by small increments. This tests grid translation sensitivity without changing simulation behavior.
2. Snap the grid origin to its current cell lattice, keeping spacing, cost formula, cadence, solver and sentences unchanged. Compare the original five development seeds at the same predeclared weight list (0, .01, .03, .1, .3, 1, 3). This is an experimental implementation, not a new accepted candidate.
3. Only after evaluating that probe, separately investigate the terminal cost's scale. Avoid simultaneously changing rasterization, grid placement and terminal formula.

Do not use the original validation outcomes to select the revised weight. The user's implementation-design reopening does not change the benchmark protocol: keep the original twenty acceptance seeds and thresholds, freeze the revised design on development scenarios, and transparently label the acceptance rerun as reuse of the known benchmark. Additional fresh solver seeds **3701–3715** are predeclared here as a diagnostic generalization check before any is consumed. They do not replace the contractual seeds or supply a favorable alternative score. Real-session acceptance seeds 3201–3215 remain unconsumed and unchanged.

The user subsequently approved an **8 GB** free physical RAM launch minimum. Use no other Unity editor and launch through the coordinator. Alastor remains unavailable per the user.

## What did the independent numerical probe show?

`grid-translation-reference.py` reconstructs the specified eight-rock layout and sampling algorithm independently in Python double precision. At fixed physical points, shifting the ship by 0.1 m changed excess by up to 44.0093 m; the fixed endpoint's sample spans 0.0134–47.5473 m. The lattice-snapped comparison remains constant across the same offsets. This supports the grid-translation hypothesis but is not Unity test evidence.

The production `GridTranslationProbe` emits the same physical samples and asserts invariance for static terrain. Unity run `20260909-160113` reproduced the bug: expected <0.0001 m variation, observed **45.041 m**. Its five-second test and raw CSV are preserved under `revision-grid-baseline`.

Applied the narrowly scoped placement-lifetime change in the field owner: rebake on the existing cadence, but retain origin and spacing while goal padding and the ship's full rollout reach fit inside the old grid. Reframe only when required to fit those bounds. Cost formula, candidate weight, sampler, rasterization and sentences are unchanged. Original owner source is backed up with the failing test evidence. The grid probe additionally requires 81 bakes so skipping work cannot make it falsely pass.

**Post-fix run 20260909-160539: 14/14 passed.** This includes the grid regression, field correctness and disabled-field controls. The original five-seed sweep produced 3/5 at weights 0/.01/.03, 4/5 at .1, 5/5 at .3 and 1, and 4/5 at 3; zero collisions in all 35 episodes. Artifacts are under `revision-stable-grid`. The next development comparison is all six real-session rows at weight .3 (clone only), the lowest swept weight with 5/5 arrivals. It is not final validation and the asset remains .03. Its launch is waiting behind another task's Unity sync and the 8 GB floor; no competing process was closed.

Potential correction after the real probe: separate grid placement from periodic terrain rebaking, retaining its coordinate frame while the ship's rollout reach and the goal fit inside it. This avoids repeatedly changing rasterization just because the ship moved. Compare this separately from cost scaling. The exact placement policy is experimental and must demonstrate the session/collision targets, not merely remove numerical variation.

## What did stable-grid session testing show?

Full 72-episode development matrix `sessions-development-20260909-231055-509.jsonl`, test run 20260909-161034, completed. All six on/off collision comparisons pass. Orbit/kite/drift movement passes; cover median error 8.709 vs 7.290 m and fire-lane error 19.782 vs 17.456 m fail their tolerances. Dummy closeout 28.620 vs 16.580 s fails. Runner pass means the emitter completed, not behavioral acceptance. Parsed comparisons are saved in `revision-stable-grid/session-comparison.json`.

## Which separate cost experiment follows?

The original terminal excess ignores the requested POS ring and can charge it for terrain routing after the ship has reached the requested distance. New focused test `TerminalCost_LeavesLocalRingSettlingToRollout` uses a real baked obstructed grid: the requested 6 m ring pays 2.828 cost under the old formula. That test failed in Unity run 20260909-162043, preserved under `revision-local-settling`.

Experimental correction: multiply the terminal excess by a smooth 0-to-1 factor based on remaining distance to the POS ring divided by maximum-speed travel over the rollout horizon. It is zero at/inside the ring and reaches full authority beyond that travel reach. No new weight, horizon, safety-margin or sentence changes. This reopens the original unconditional cost-shape ruling under the user's approved design investigation; it is not silently presented as the original frozen formula.

Run 20260909-162259 passed all 15 selected tests, including local-ring behavior, grid invariance, field correctness and disabled controls. The original five-seed sweep still gives 5/5 arrivals at .3 and 1 (and now 3), all zero collisions. The full six-row session comparison at the SAME .3 development override is running in exec session 10791. Asset remains .03; no revised validation freeze yet.

Remote video publication is complete after the user explicitly approved public exposure. Evidence-only prerelease: https://github.com/amindell11/astronomical-home/releases/tag/codex/terminal-field-evidence-20260909 . It targets baseline `27c356bff9aaae376fbcbd3d6b0f28d0d89b87cb`, is prerelease/not-latest, and is titled “Failed candidate 1 — independent terminal-field chase evidence”. Exactly two MP4 assets were uploaded; their remote hashes match originals and their URLs return HTTP 200. PR-ready links are in `remote-video-pr-snippet.md`. No implementation PR was opened.

## Why was local attenuation rejected?

The complete 72-episode matrix in revision-local-settling failed orbit movement (26.07 vs 17.55 m), dummy movement (24.98 vs 15.60 m), dummy closeout (18.34 vs 16.58 s), and kite contacts (38 vs 26 steps). The experimental cost and its focused test were archived, then removed from the working implementation. Original unconditional detour excess is restored. Fine stable-grid weights .125 through .3 did not identify a lower weight preserving all five development arrivals; only .3 achieved 5/5 in that sweep.

## What is the goal-region experiment?

Route to the requested POS distance region: use every free cell inside the radius as a Dijkstra source. The obstacle-free reference uses exactly the same sources, so the term remains detour excess and is zero on empty terrain. When no free cell lies inside the radius, use the nearest free cell with remaining distance-to-radius offset. Point goals retain the original single-source analytic reference. Stable placement, cadence, weights, horizons, sentences and collision settings are unchanged. This is experimental implementation design, not a revised acceptance contract.

Unity run 20260909-164841 passed 22/22 focused tests, including independent randomized shortest-path comparisons, empty terrain, occupied-region fallback, disconnected cells, grid invariance and disabled controls. Earlier compile failures and a corrected hand-calculated test expectation are retained in revision-goal-region. A free cell at offset (2,1) is outside the radius-2.1 rock; its empty octile distance is 1+sqrt(2), not 2sqrt(2). The six-row development session matrix is running at the same .3 weight. No final acceptance seeds have been consumed for this revision.

The complete goal-region .3 matrix finished in Unity run 20260909-164931 (72 records). Orbit, kite, fire-lane and drift pass; cover movement fails (8.147 vs6.885m), dummy movement fails (22.506 vs17.303m), and dummy closeout fails (28.300 vs16.580s). All six collision comparisons pass. The runner pass means the emitter completed, not acceptance. Summary: revision-goal-region/session-comparison.json.

Next bounded weight check: weight1 is the other original development weight with5/5 transit arrivals and no collisions. Test DummyCloseout and CoverTake first at weight1 using unchanged development seeds3401-3403 and four arms. Only if both pass, complete the other four rows before freezing. This is development selection; acceptance seeds remain untouched for this revision.

Read-only fire-lane diagnosis established a current-pose versus predicted-pose goal mismatch: Mpc resolves the field at step0; final rollout POS uses the last predicted enemy state (~1.7s). Prediction can also rotate the 12m facing-frame offset. No measured causal link yet; fire-lane passes in the latest run, so no prediction change has been made.

Weight 1 failed the first development filter, DummyCloseout: median error17.745 vs15.603m and closeout20.220 vs16.580s. All3 episodes won in each arm; contacts1 vs17. Run20260909-165739 completed12 records. Cover/other rows are not run for this already-failing weight. One bounded intermediate-weight search is now declared: .4,.6,.8, with the unchanged five transit seeds; only transit-passing weights proceed to dummy and cover. The implementation remains fixed during this search.

Intermediate transit run 20260909-165925: .4 and .6 each5/5, .8 is4/5, off3/5; zero collisions in all20 episodes. Only .4 and .6 advance to the dummy development filter, in separate fresh processes with the same implementation and conditions. Asset hash remains C4C9ECDB1B0173E747553E81E4CB001EAA1F5115A8B765CDDE57CC8648758197 (original candidate1 weight .03; all new weights are test-only clones).

The bounded intermediate search failed the dummy filter at both surviving weights. At .4: movement23.832 vs15.603m fails, closeout16.620 vs16.580s passes, contacts0 vs17. At .6: movement22.062 vs15.603m and closeout27.660 vs16.580s fail, contacts0 vs17. All comparisons retain the fixed metrics; no acceptance rerun or metric change follows. No additional weight search is planned. Diagnostic next step: paired per-bake position, target, selected endpoint, sampled field penalty and controls for DummyCloseout at .4. This will identify where its changed path and episode length affect error, without modifying behavior.

## What did the paired dummy trace establish?

Trajectory traces at weight .4 reproduce the same paired outcomes. Seed3403 closes at30.36 vs23.78s after a wider route. Seed3401 closes at16.62 vs16.58s and wins sooner (23.98 vs31.72s), so its larger whole-episode mean error is partly affected by episode duration. Metrics stay frozen; seed3403 is still a genuine path regression.

The first terrain export omitted primary-circle obstacles. Its overlay is excluded; a verification script reproduced167 blocked cells without matching exported geometry. The corrected export in revision-dummy-terrain-complete includes all primary/multi-lobe circles and passes blocked-cell coverage validation. Trajectories and timings were unaffected by this diagnostic correction.

Concrete sampling failure: seed3403, field on, time4.819996s, selected endpoint(-8.949472,-13.00899) has1.87439m clearance beyond every inflated obstacle circle. Measured terminal penalty80.0387m; independently reconstructed80.03852m, of which78.73881m comes from one blocked corner carrying431.0092m. The three free corners have excess0 or2.343m. Thus bilinear interpolation spreads the global disconnected fallback into otherwise free space. Other samples using only the initial snapshot do not exactly reconstruct later changed fields and are not claimed as exact evidence.

Next isolated experiment distinguishes occupied interpolation support from disconnected free space. Keep raw Dijkstra distances and all collision costs unchanged. For an occupied boundary cell, extend the minimum neighboring reachable excess plus the cell-to-neighbor distance. Deep occupied cells without a reachable neighbor and disconnected free cells retain the existing fallback. The goal is to remove the global penalty spike at free points next to rocks, not remove collision avoidance. Focused regression FreePointBesideRock_DoesNotInheritDisconnectedFallback is run red first. This changes the sampler's original blocked-cell policy under the reopened implementation design; acceptance thresholds remain fixed.

Boundary-extension regression RED: run20260909-171242 measured16.435m at the free point, exceeding3m. Applied occupancy-aware sampling: free reachable cells retain exact detour excess; occupied cells with reachable neighbors use minimum neighbor excess plus edge length; disconnected free cells and fully blocked interiors retainB. Occupancy travels in the read-only view; invalid solver views bind a caller-owned empty byte array, preserving Job safety. Raw shortest-path distances remainunchanged.

GREEN run20260909-171515 passed23/23, including independent point/region oracles, blocked/unreachable sampling, no-POS/disabled controls and placement invariance. Development sweep: off/.01/.03=3/5, .1=4/5, .3/1/3=5/5; all zero collisions. Full six-row session matrix now runs at unchanged .3. This is not accepted until behavior passes; no final validation seeds consumed.

The complete boundary-extension .3 development matrix has72 records (run20260909-171635). All six movement comparisons pass, but kite contacts37 vs29, cover106 vs31, and fire-lane2 vs0 fail; dummy closeout also fails. The numerical correction is verified but behavioral acceptance still fails. Under this NEW sampler, the unchanged predeclared sweep also gave5/5 transit at weights1 and3, unlike some earlier implementations. These two existing weights now get a dummy development filter, with no added intermediate-weight search. Final validation remains unstarted for the revision.

User explicitly approved the dummy-only integrated-error metric before revised final validation. The contract amendment is recorded in independent-contract.md; old average-error failures remain preserved. At weight3, dummy accumulated error, success, closeout and collisions pass; the remaining five development rows completed60 records in run20260909-172805 and pass all comparisons. Full MPC correctness run20260909-173537 passed136 tests, skipped15 opt-in experiments, failed0.

Candidate weight3 selected. Asset SHA2566E7514936270D34B5EA25BC15035B5AF6DCBEE909CFC03F764443F31E31353D8. C# weight default also3; horizon1.7, resolution48, minimum spacing4, cadence.4 remain unchanged. Performance emitter now covers point goals and18m goal regions. Fresh diagnostic seeds3701-3715 are encoded but not consumed. Quality review and final freeze precede the three contractual fresh-process repeats; original acceptance transit seeds remain a transparently reused known benchmark, and session acceptance seeds3201-3215 remain fresh.

Integration onto main260877fb completed cleanly as06047592. Clearing ScriptAssemblies/Bee/BurstCache then run20260909-174828 gave136 passed,16 opt-in skips,0 failures. ReSharper passed with no blocking changed-line findings. Required combined quality reviewer changed only five test namespaces to mirror their TerminalField folders, indentation, and field-off assertion wording; no runtime or acceptance changes. Post-quality MPC compile/check is running before final freeze.
