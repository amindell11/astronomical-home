# Targeted contact diagnosis

User approved continuing with targeted diagnosis after challenging the unnecessary third full matrix. No further broad sweeps or tuning against acceptance seeds.

Drift-hold's median movement error is zero, but individual episodes are not all stationary. All drift contact steps in every complete validation repeat come from seed3204. The fixed sentence explicitly arms FIELD at weight0, so changing the global terminal-field weight should be a negative control.

| Repeat | On contacts | Off contacts | On mean displacement | Off mean displacement |
|---|---:|---:|---:|---:|
| 1 | 517 | 238 | 0.201m | 7.249m |
| 2 | 526 | 520 | 0.203m | 0.201m |
| 3 | 228 | 517 | 2.988m | 0.201m |

The same disabled-field condition varies238→520→517 across processes; the paired effect changes sign. This demonstrates instability in this negative control and prevents attributing its acceptance failure to the field alone. It does not erase the frozen acceptance failures or establish the cause of the instability.

Prepared probe: four120-second episodes on seed3204, weights0,0,3,3; otherwise the existing sentence composition. It records initial decision seed, radius, pose, inertia and simulation time; per-step kinematics, controls and best cost; and contact identity, layer, impulse and relative velocity. Same-weight contact equality assertions provide a red signal if no-op repetition itself diverges. No production code changes. Probe source is preserved here and staged uncommitted under agent-2's PlayMode/TerminalField tests.

Ranked hypotheses: (1) contact/physics variation persists between identical conditions; (2) episode reset leaves different initial state; (3) FIELD0 still changes controls. Initial-state and per-step traces discriminate these. Memory recovered above8GB and the user authorized proceeding. Two focused runs completed: weights0,0,3,3 and weights0,0,0,0 each produced contact counts519,517,218,228. Every corresponding states.csv and contacts.csv is byte-identical across the runs. This falsifies terminal weight as the source of this drift discrepancy; it does not establish the reset/physics cause or change frozen acceptance results. Artifacts: 20260910-054218-720 and 20260910-054513-636.

Run only `Tests.PlayMode.TerminalField.TerminalFieldContactDiagnosisTests.SameSeedDriftContactProbe`, with MPC_CONTACT_DIAG=1 and MPC_FIELD_OUT pointing to this directory; pool run-tests agent-2, PlayMode, UnityTimeoutSec600. Clear verified agent-2 BurstCache before launch. The fixture itself has a300-second limit. After diagnosis remove or intentionally promote the temporary instrumentation; do not commit unexplained probe scaffolding.

Next isolation probe: all weights zero, fresh HarnessField per trial instead of sharing one. Ranked hypotheses are retained asteroid state, initialization timing, and accumulated physics state. Fresh fields also exercise pre-Start reset rather than the shared post-Start rebuild, so a change narrows the cause to field lifecycle but does not alone distinguish pooling from Start timing. Launched after coordinator was clear and memory10.33GB. No production edits.

Fresh-field probe completed (20260910-055038-303; runner20260909-225017): counts214,214,520,521. Reused pooled asteroids are not required for divergence. Next probe restores shared field/all-off and records asteroid poses, velocities, mass, radius, inertia, center of mass, mesh and collider state at reset and steps1,2,10,100,1000,4500 to locate the first divergent physical state. Production runtime remains frozen.

Boundary snapshot result (20260910-055337-249; runner20260909-225317): 244 asteroids in each trial. All recorded initial properties match across all four trials. Trials0/1 match through step100, then small differences appear by step1000. Trials2/3 differ from trial0 at step1 in six asteroids (indices8,20,79,87,235,241), 78 position/rotation/velocity/spin fields; masses/radii/inertia/center-of-mass/mesh still agree. Contact counts reproduce519,517,218,228. The snapshot does not cover every internal PhysX state, so the specific mechanism remains unproven. Fresh fields did not remove the failure. Global terminal weight0 versus3 produces byte-identical full movement/contact traces in the controlled comparison.

Scope decision: terminal implementation cannot be blamed for this negative-control discrepancy. Benchmark episode repeatability needs separate investigation/repair before its strict paired contact criterion can distinguish a field regression. Do not loosen thresholds, change physics settings, or silently fold a harness redesign into terminal-field PR-1. Proposed next scope is benchmark episode/physics isolation with the existing thresholds preserved. No production fix selected; apply the fix ladder only once the specific invariant violation is established. No Unity process or lease remains active. Scratch probes remain uncommitted and preserved here.
