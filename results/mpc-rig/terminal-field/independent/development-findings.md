# Independent development evidence

These are development results, not final acceptance results. Validation seeds have not been consumed.

The first sweep completed with no weight above 4/5 transit successes. Synchronous Burst compilation in the refined sweep reproduced the weight-0 and weight-1 outcomes exactly; startup compilation did not explain those trajectories.

Refined sweep: `development-20260909-093325-878/transit.csv`, five fixed development seeds per weight, horizon 1.7 s, resolution 48, minimum spacing 4 m, cadence 0.4 s. Every row had zero measured collision steps and zero threat fraction.

| Weight | Successful transit seeds / 5 |
|---|---:|
| 0 | 3 |
| 0.4 | 4 |
| 0.6 | 5 |
| 0.8 | 4 |
| 1 | 4 |
| 1.25 | 4 |
| 1.5 | 3 |
| 2 | 5 |
| 2.5 | 2 |
| 4 | 3 |
| 5 | 3 |
| 7.5 | 2 |
| 10 | 3 |

At 0.6 the final ranges were 1.0223, 0.9957, 5.9168, 0.1014 and 0.9946 m. The candidate was selected for session development because it achieved five successes with a smaller shaping coefficient than 2.0. Neither coefficient is frozen.

The refined run also found four existing direct-SolverBuffers tests failing job scheduling: the new optional field contained an unconstructed NativeArray. SolverBuffers now supplies its owned empty array for absent fields; valid field views still borrow the field owner's storage. The complete MPC EditMode suite subsequently passed 123 tests, with seven opt-in skips and no failures. Artifact directory: `mpc-regression-20260909-023452`.

During the 0.6 session development run, complete three-seed orbit and cover comparisons exceeded the movement-error tolerance despite reducing collision steps. All raw episode outcomes remain in `sessions-development-20260909-093624-434.jsonl`. Do not claim acceptance or freeze this coefficient based on transit alone.
