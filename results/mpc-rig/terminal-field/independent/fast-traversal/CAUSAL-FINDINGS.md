# Causal findings: terminal field and horizon

User ruling2026-09-10: stop chasing original benchmark cutoffs; use short, controlled experiments to understand behavior and limits. Historical acceptance results stay recorded. All results below use the solver plant, fixed development seeds, stationary circles and unchanged production assets. No held-out seeds or broad acceptance repeats were used.

## Why did the field appear to produce a large speed gain on seed4103?

The original field-off ship enters sustained backward flight while following almost the same efficient route. From4–14s it uses reverse thrust plus strafe, so negative thrust did not mean simple braking. In the12s probe it spends7.64s moving backward relative to its nose; field-on spends0s. Empty terrain does not trigger this mode. Adding a goal-facing AIM command removes backward flight in both horizons and both tested seeds. On4103 at1.7s the apparent field benefit disappears: goalward speeds17.01on versus17.31off. This shows that part of the field's benefit was steering the optimizer away from a poor flight mode, rather than improved long-range route geometry. It does not establish whether that mode is a global optimum of the stated objective or a sampling/warm-start local optimum. Goal-facing is a diagnostic intervention, not a shipped sentence change.

## What happens when the horizon is shortened?

Three things changed together in the original rig: rollout length, obstacle scan reach, and field spacing. They must be separated. Native1.2s eliminated backward flight on4103 but introduced it on4105. Holding the old field spacing while shortening the rollout improved both12s on-field runs. With live sensing held fixed, short native-grid versus short fixed-grid speeds were16.02→16.70m/s on4103 and16.33→17.22m/s on4105. Both were collision-free. These are conditional examples, not a universal preferred horizon.

The live Scout already uses2s sensing plus an acceleration envelope, independent of MPC horizon (AgentPilot prefab and Scout.Initialize). The earlier coupled sensing was a rig condition. LiveSensingHorizons reads the prefab's sensing setting and applies the same extent formula; normal rig behavior is retained, with an explicit diagnostic extent override.

## How much terrain does the far-goal field represent?

Actual bakes verify the geometric prediction. For the local corridor x∈[-100,100], y∈[-60,350], seed4103 has324 rocks and4105 has323. At goal1500m/resolution48/horizon1.7, only13 and21 rocks respectively contain an occupied grid sample after hull inflation. At1.2s the counts become12 and11, with onlyONE rock shared between the long/short represented sets for each seed. Spacing changes37.386→36.805m, moving which rocks are represented. Resolution96 raises represented counts to82 and91, still far from complete.

At goal360m/resolution48,177/324 and182/323 are represented. At goal90m, all167 and165 rocks inside the smaller grid are represented. The local region includes rocks outside that smaller grid; these fractions must not be compared as equal spatial coverage. The reactive obstacle scan still sees nearby real circles, so missed field samples do not mean missing collision geometry.

Root cause: fixed cell count plus ship-to-goal domain expansion and center-only occupancy produce severe spatial aliasing for small rocks at long goal distances. Higher resolution improved geometric fidelity but did not by itself improve the previously tested slow seed. Field value shape matters too.

## Can the field replace short-horizon collision anticipation?

At25m/s with one4m-radius rock25m ahead, all tested horizons(1.7,1.2,0.7s) collided. None of27 constant-control escape probes avoided it either; this does NOT prove physical impossibility over arbitrary control sequences.

At40m and60m,1.7s and1.2s avoided the rock;0.7s with its original short scan collided. Constant-control escape witnesses exist at these distances. At40m, keeping the0.7s rollout but widening sensing from17.5m to42.5m changes clearance from-4.34m to+0.33m: a direct causal isolation of late sensing. A resolved field with a90m goal and the short sensing range still collides(-4.16m). No general safety guarantee follows from any single passing trajectory.

The endpoint-value probe explains why that field did not supply an early turn incentive. With the rock40m ahead, goal90m,0.7s rollout and4m cells,27 constant-control rollouts end at x≈[-1.62,1.62], y≈[14.39,17.43]. Their terminal costs lie between9.941116 and9.941130. At y17 the field is flat across x[-2,2]; cost only falls outside that band, reaching0 by|x|6. The recorded collision run pays the same terminal cost while endpoints advance16→29m. The excess-distance heuristic knows about the detour but is locally flat across the sampled short-horizon choices. Increasing its weight cannot create directional information on an exactly flat region. This is the demonstrated limit, not proof about all possible control sequences or obstacle arrangements.

## Where does field compute time go?

In representative warmed dense traces, ordinary planning medians are about0.26–0.30ms at1.7s and0.21–0.26ms at1.2s. Field-bake planning steps remain around11–12ms. In the facing probe, roughly70% of aggregate planning time was spent on bake steps; shrinking the rollout only modestly changes total cost. Timings are machine-specific, exclude the first10ticks, and include all Mpc.Plan work rather than the solver job alone. Field-off disables cost contribution but still bakes, so it is a behavior ablation, not a no-field-computation baseline.

The one-query probe isolates the cold spike: collecting the same4418 obstacles by growing a64-entry buffer takes856.4ms, warm reuse0.92ms; an initially sufficient7052-entry buffer takes5.66ms cold and0.87ms warm. The field adapter invokes nearest-selection while an all-results consumer repeatedly grows capacity. Those intermediate partial selections dominate initial gathering in this rig. A producer-owned all-results query is the appropriate future fix direction; no scanner interface redesign was included in this diagnostic work.

## What should change next?

Keep sensing extent fixed when varying the rollout horizon. Separate the desired flight orientation from terrain routing when interpreting speed gains. For terminal guidance, preserve useful local terrain resolution at distant goals and test whether candidate endpoints receive a usable direction signal before committing to a new field representation. Avoid another weight sweep: it cannot repair missing terrain samples or a flat value region. Optimize the all-results gather before treating cold first-bake timing as a Dijkstra/resolution cost.

Production settings and field runtime are unchanged. Live-physics transfer, arbitrary terrain, and sampler-versus-objective explanations for backward-mode persistence remain unproven. The short probes identify concrete mechanisms and counterexamples; they do not certify overall acceptance.

## Evidence

- encounter-20260910-190715-883 / runner120653:12s horizon factorial, two seeds plus empty controls.
- facing-20260910-190840-346 /120826: same12s arms with goal-facing command.
- coverage CSV /121033: ten actual field bakes.
- single-rock-20260910-191215-759 /121202: two-second full-speed encounters.
- envelope CSV and traces /121359: four-second distance bracket and fixed-control escape witnesses.
- sensing directory /121518:0.7s sensing isolation and resolved-field counterexample.
- live-sensing-20260910-191736-465 /121722: horizon comparison with live Scout sensing extent.
- query-cost CSV /121844: cold/warm gather isolation.
- plateau CSV /122038: one bake,27 constant-control endpoints and cost cross-section.

Diagnostic test success means the probe executed and structural assertions held; it does not mean the simulated ship avoided collision. CSVs report all outcomes. Source and settings accompany these results.

## Does optimizer history cause the backward-mode persistence?

WarmStartPersistence isolates this directly at t4s on seed4103 with field disabled. Clearing only Mpc.BestSequence leaves the Mpc object, random stream, last control, kinematics, terrain and cost settings intact. All pre-intervention control/position samples are asserted identical. The t4 state also matches exactly: position(9.66996,36.76288), yaw-102.99394deg, velocity(2.12877,13.51762).

Baseline12s progress speed13.56791m/s and7.64s backward flight become15.92417m/s and0s backward flight after one clear. Repeated one-second clears after4s give15.64983m/s and0s backward flight, so more resets are not better. The first replacement has a higher reported best-sample score349.555 versus234.351 while leading to better later progress. Correction from the later ranking probe: this score is not the cost of the emitted elite-average plan. The experiment proves that optimizer history contributes, but the precise claim of a worse immediate emitted-plan cost remains unmeasured. It does not prove global optimality. Blind periodic resets are not proposed as a safe fix.

Evidence: warm-start-20260910-192547-857, runner122533. These findings resolve the earlier open question about whether optimizer history contributes; it demonstrably does. Global objective optimality and generalization remain open.

After the one-time clear, realized running-cost integral over t4–12 is33.75 versus baseline62.17 (repeated resets36.86). This is improvement in the measured longer-run objective as well as speed, despite the initially worse reported sample score. Existing solver-rig checks after instrumentation: runner122637,10passed/3opt-in skipped, no failures. At the end of that round, the diagnostic fixture and hooks were uncommitted. Their subsequent archive and cleanup are recorded below.

## Does retaining full route distance fix the plateau?

No. Actual grid export shows a raw-route lateral gradient where excess cost is flat. At y17 and x[-2,2], raw cost rises from238.64931 to243.61989 while excess stays9.941116. The empty-grid baseline carries the same lateral slope, influenced by the snapped goal cell.

A temporary change returned the full route distance instead of subtracting the empty distance. Six paired4s controls used horizon0.7, goal90, initialspeed25 and a radius4 rock40m ahead. Centered-rock clearance changed-4.163173→-3.809579m, still colliding. At0.5s, raw-route lateral position was exactly-0.1177606m in both the rock and empty cases. Full-route sampling adds baseline steering and changes empty-space behavior without fixing this repro. It was restored; no production sampling change remains.

Evidence: plateau-grid-20260911-012018-908.csv; route-excess-20260911-012243-325; route-raw-20260911-012334-628; runners181929,182228,182317. The two encounter tests deliberately failed the centered-rock clearance assertion. See route-value-probe.png and route-value-comparison.csv. The analytic one-circle calculation is an offline geometric comparison, not a model of ship maneuverability.

## What separates spatial resolution from cost pressure?

For the same near-goal single-rock case, local cell spacing4→2→1m gives endpoint-cost ranges0.000013→1.532→2.775 across the same27 constant-control rollouts. All three still collide at weight3. Empty controls retain the same22.15396m/s progress.

One tenfold weight perturbation distinguishes flatness from objective tradeoff:

| Cell spacing | Weight | Minimum swept clearance |
|---|---:|---:|
|4m|3|-4.163173m|
|4m|30|-2.957634m|
|1m|3|-3.691741m|
|1m|30|+0.3697262m|

At1m/weight3, a constant full-thrust/strafe rollout adds1.4864 running-cost points and saves1.3854 field-cost points relative to full thrust straight ahead. Thus this particular steering alternative scores0.1010 worse. At weight30 it scores12.3676 better. Each sampled rollout's reconstructed score was checked against its actual solver score. These comparisons concern particular alternatives, not the global optimum.

Sampling contributes separately: at1m/weight30, the full-thrust/strafe alternative costs138.9155, below the best randomly sampled151.2794. The emitted elite average costs153.8083. This establishes incomplete candidate coverage and a distinction between emitted and reported cost at that solve. It does not establish that opposite routes were averaged into a collision.

The1m/weight30 setting also clears rocks offset-3m and+3m, with clearances0.3553m and0.9750m. Moving the goal1500m away also passes(+0.2939m), despite enlarging the cells. This counterexample rules out treating cell size alone as a monotonic predictor of safety: placement, represented obstacle shape, cost pressure and subsequent replanning interact. None of these short results selects production defaults or proves dense-field safety.

Evidence: resolution-plateau-20260911-013817-290 and resolution-pressure/route-fine-boundary timestamped directories; runners183749,184225,184411. Runner184050 is excluded: its diagnostic incorrectly equated emitted-plan cost with best-sample score and stopped before completing any encounter. The corrected probe checks each sample against its own score.

## Which measured defect went back to implementation?

The repeated regional gather is fixed in local commit f89cb0b8. `IObstacleField.QueryAllObstacles` lets the producer gather once and size caller-owned storage. Both live and rig producers reuse their existing geometry gathering; the bounded nearest-selection path remains intact. This is fix-ladder rung1: an explicit complete-results request replaces the nearest-prefix retry protocol.

The regression went red with6gathers for129obstacles, then green with1. An isolated cold query for the same4418rocks changed856.4114→2.7066ms; warm query0.9202→0.8410ms. These are individual machine measurements, not latency guarantees. The first post-fix measurement after other tests was1.8299ms; the isolated run is the cleaner comparison.

All six4s encounter traces match the pre-fix controls in every CSV column except measured plan time. Live-source tests compare complete geometry and nearest subsets, reuse storage, and remove a destroyed asteroid. Runner182827:31pass/3opt-in skipped. Runner183538:6pass across trace preservation and live-source/scanner integration. Isolated timing runner185241:1pass. The combined quality reviewer found no actionable issues, and the ReSharper changed-line check passed with7report-only findings outside changed lines.

Final committed-tree verification after diagnostic cleanup: runner185655,35passed/3opt-in skipped across focused EditMode and PlayMode checks. Test-generated Sentis analytics churn was inspected and restored. The worktree is clean; Unity coordinator owners, queue and boot lane are empty.

## Where is the investigation preserved, and what follows?

Exact final diagnostic sources, an instrumentation patch, and verified file hashes live in causal-source/20260910-final. These scratch hooks and the opt-in causal fixture were removed from the worktree before committing the gather fix. Original field sampling, horizon, weights and assets remain unchanged. No implementation was merged or published as accepted.

The user requested outside research after these experiments. [RESEARCH.md](RESEARCH.md) maps primary literature to the observed mechanisms and prioritizes route-averaging inspection, terminal-state dynamics, and route-progress objectives. Those are hypotheses and proposed discriminating probes, not implemented controller changes.

The subsequent approved probes are complete: [SELECTION-DYNAMICS.md](SELECTION-DYNAMICS.md). One captured solve averages14 individually clear finite-horizon plans into a collision; left-only/right-only means clear in a frozen replay, and the combined mean also collides at20ms. Another solve averages two same-side candidates into a safety-margin violation and nearly10,000 points of cost regression. A position-only field also assigns identical value to states with0 versus729 safe continuations in the tested family at equal25m/s speed. These findings prioritize scoring the emitted average before selection; no selector or production-settings change has yet been made.
