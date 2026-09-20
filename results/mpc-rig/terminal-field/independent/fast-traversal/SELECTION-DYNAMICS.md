# Captured selection and terminal dynamics

The research-guided probes found two independent mechanisms: averaging can destroy an avoidance maneuver, and a position-only field cannot distinguish states with very different escape options. No production solver, field, horizon, weight, or asset was changed in this round.

![Captured plans and terminal-state comparison](selection-dynamics.png)

## Does the real solver average safe plans into an unsafe plan?

Yes, in a captured single-rock solve. The setup is the existing traversal sentence, seed4103, initial velocity(0,25)m/s, a radius4m rock at(0,40), and goal(0,90). Obstacle sensing stays at42.5m half extent. Three2s prefixes use horizons0.7/1.2/1.7; they capture297 solves and38,016 sampled plans. These are mechanism probes, not a controlled claim that one horizon is generally better: changing horizon still changes other prediction/field geometry.

Each captured input was reconstructed and every sample rescored through the production Burst evaluation job, matching the stored score within0.00001. The elite-selection rule and control average were independently reconstructed, matching each emitted control within0.000001. Physical clearance uses swept segments and the existing bank-dependent ship radius. It excludes the extra0.3m cost margin unless explicitly labeled.

At horizon1.7, tick2 (t0.04s), all14 contributing plans have nonnegative swept clearance within their prediction horizon: minimum0.0572m, maximum5.5362m. Nine head left and five head right. Their combined control average has clearance−0.06776m. The candidates have not all passed the rock at the horizon endpoint; this is finite-horizon safety, not proof of safe completion.

A frozen replay preserves the state, candidates, and scoring input, changing only which controls are combined:

| Plan | Contributors | Solver score | Clearance at100ms | Clearance at20ms |
|---|---:|---:|---:|---:|
| Combined mean |14|283.3457|−0.06776m|−0.23226m|
| Left-only mean |9|142.0472|+2.56481m|+2.06331m|
| Right-only mean |5|158.2536|+2.65302m|+1.63195m|
| Best sample |1|127.2297|+0.99987m|+0.41384m|
| Incumbent |1|315.1100|+0.04255m|−0.13580m|

The20ms replay holds each original100ms control for five plant steps over the same1.7s duration. Scores in both columns remain the production100ms objective, not rescored20ms objectives. The finer replay confirms that this averaged maneuver still collides. It does not verify that every individual elite remains safe at20ms.

The mean's terminal position is not the mean of the sampled terminal positions: nonlinear ship dynamics, thrust/yaw combinations, and bank-dependent footprint matter. Combining both sides removes avoidance while changing forward progress. There is no basis for treating an average of safe controls as a safe control sequence.

## Would splitting left and right solve selection?

It fixes the captured mixed-side case, but a second captured solve rules out treating that as sufficient.

At tick67 (t1.34s), two improving candidates both go left. Their average costs10,141.8 versus141.1659 for the best sample and149.4054 for the incumbent. The averaged trajectory has physical clearance+0.15232m, but violates the0.3m safety margin by0.14768m and receives the10,000-point collision penalty. The best sample clears the margin by0.01768m at100ms and0.00315m at20ms. These are very small margins, not a robustness claim.

The left-only mean equals the original mean in this case, so a side split leaves the failure intact. Across the297 captures, six emitted averages exceed incumbent cost by more than0.01 despite selection accepting only samples that improve on the incumbent. The largest regression is9,992.3946 points. The emitted average was never scored before selection; the reported score belongs to the best sample instead.

This is the next narrow implementation target: make the emitted mean a scored contender alongside the best sampled sequence, and report the score of the selected sequence. The root cause is unchecked recombination after candidate evaluation. Fix-ladder rung1 would make selection operate only on scored contenders, so there is no unscored emitted plan. A broader route-clustering implementation adds design and tuning questions and does not eliminate the same-side counterexample. Neither approach supplies a hard safety guarantee when the cost or candidate set is insufficient. No selector change was implemented here.

## How much does position-only terminal value hide?

One fixed field bake is sampled at positions(0,0), (0,15), and(0,25). At each position, five velocities and three headings produce45 states. Each state receives the same729 two-stage control continuations: thrust/strafe/yaw each in{−1,0,1}, switching once at0.5s, simulated for4s at20ms. There are32,805 continuations. These are explicit feasible witnesses and bounded negative results, not an exhaustive optimal-control search or probability distribution.

At(0,25),15m before the rock, every state has field cost11.42072:

| Initial velocity | Speed | Safe continuations, heading0° /90° /180° |
|---|---:|---:|
|(0,0)|0m/s|686 /704 /711|
|(0,10)|10m/s|116 /160 /180|
|(0,25)|25m/s|0 /0 /0|
|(−15,20)|25m/s|729 /729 /729|
|(15,20)|25m/s|729 /729 /729|

The last three rows have equal speed. Existing lateral momentum changes the escape situation drastically; speed alone would still alias them. At(0,15),25m before the rock, velocity(0,25) yields0 safe continuations facing0°, but3 facing90° and2 facing180°. Heading changes which thrust axes can help. Zero means no witness in this family, not a proof of unavoidable collision.

The ship model has forward acceleration7m/s², reverse3.5m/s², and lateral acceleration5→4m/s² as speed rises, plus drag and bounded yaw dynamics. A geometric route may exist while a particular arrival velocity/heading cannot follow it in time. This supports keeping enough dynamic prediction for maneuverability, or explicitly evaluating a dynamically feasible continuation at the terminal state. It does not yet choose a horizon, a terminal-cost formula, or a speed cap.

## Evidence and limits

- Captures: `captured-solves-20260911-023227-448`; Unity runner193212 passed.
- State continuations: `terminal-dynamics-20260911-023323-664`; runner193311 passed,6.38s test duration.
- Frozen replay: `frozen-mean-20260911-023536-482`; runner193521 passed,2.46s test duration.
- `runner-artifacts/` preserves logs, XML and summaries. The captured and replayed mean scores/clearances match exactly at both selected ticks. Artifact checks verify297 solves,45 states,32,805 continuation rows, equal field values per position and endpoint speeds within the25m/s limit.
- Runners192940 and193044 are excluded partial attempts. The first diagnostic required bit-exact managed floating-point averaging; the second compared the managed cost breakdown against Burst with an overly tight absolute tolerance. Final capture uses the production scoring job for score parity and records managed deltas separately (maximum0.03795624). No controller change was made to pass these checks.
- Unity's JSON serializer produced an empty `dynamics.json` for the readonly dynamics structure. The resolved fields are preserved in the frozen replay's `dynamics.txt`; the source archive also hashes the settings and ship prefab inputs.
- These experiments use the solver's model and synthetic circle geometry. No dense-chase, Unity-physics, or held-out acceptance claim follows. The existing public dense-chase clips are unchanged.

Exact diagnostic sources and the four-file instrumentation patch are archived under `causal-source/20260910-selection-dynamics`. Scratch instrumentation is removed after preservation; the production worktree remains at gather-fix commitf89cb0b8.
