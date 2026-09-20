# Reproduce the causal probes

Use the existing agent-2 lease through scripts/agent_worktree_pool.sh run-tests agent-2, Mode EditMode. Set MPC_FIELD_OUT to the primary fast-traversal result folder and MPC_CAUSAL to the mode below; select exactly the corresponding Tests.EditMode.TerminalField.TerminalFieldCausalTests method. These probes report outcomes, without old speed-target assertions. Check unity_access and memory before each launch. No held-out seeds are involved.

| Method | MPC_CAUSAL | Work |
|---|---|---|
| FirstEncounterHorizons | horizon | two12s seed prefixes, horizon/field/grid arms and empty controls |
| GoalFacing | facing | same12s cases with AIM toward goal |
| GridCoverage | coverage | ten one-bake geometry probes |
| FullSpeedSingleRock | single-rock | twelve2s cases |
| AvoidanceEnvelope | envelope | three distances,4s MPC and fixed-control escape witnesses |
| ShortHorizonSensing | sensing | four4s cases isolating visibility |
| LiveSensingHorizons | live-sensing | ten12s cases with live Scout sensing extent |
| InitialQueryCost | query-cost | four query-only calls |
| TerminalPlateau | plateau | one bake,27 endpoint rollouts and cost cross-section |
| WarmStartPersistence | warm-start | three12s cases with identical pre-intervention traces |
| RouteValueEncounter | route-excess / route-raw | six4s controls; centered-rock assertion is expected red for both samples |
| RouteValueEncounter | route-preserved | compare all non-timing columns with MPC_ROUTE_REFERENCE directory |
| RouteValueEncounter | route-fine-boundary | four4s cases, resolution192/minspacing1/weight30; centered, offset, far-goal rocks |
| ResolutionPlateau | resolution-plateau | three local resolutions, rock/empty4s cases and27endpoint values |
| ResolutionPlateau | resolution-pressure | coarse/fine × weights3/30 × rock/empty; first-solve objective ranking |
| CapturedSolves | captured-solves | three2s prefixes;297 actual solves, score/selection reconstruction and swept plan clearance |
| TerminalDynamics | terminal-dynamics |45 states ×729 four-second continuations, one fixed field bake |
| FrozenMeanReplay | frozen-mean | two captured solves; combined/side means, best sample and incumbent at100ms/20ms |

The final diagnostic archive is causal-source/20260910-final, with SHA256 manifest and instrumentation.patch. The gather fix is committed separately as f89cb0b8; the worktree no longer carries diagnostic hooks. To reproduce a probe in the owned worktree, apply that patch and copy TerminalFieldCausalTests.cs and its .meta into the manifest's recorded path. Preserve any later edits before applying. The route-raw variant additionally uses the saved TerminalFieldView.raw-prototype.cs; restore TerminalFieldView.before-route.cs afterward. The mode label alone does not change sampling semantics.

Run controls with unchanged settings unless the selected method explicitly clones and modifies them. The final source adds ranking output to resolution-plateau and weight suffixes to its filenames; original evidence remains named as produced by the earlier source. For route-preserved, MPC_ROUTE_REFERENCE points to route-excess-20260911-012243-325. Source-only hashes and timing artifacts are preserved locally; no new detailed evidence upload was performed.

The newer `causal-source/20260910-selection-dynamics` archive includes the three research-guided methods above. Restore its patch and fixture againstf89cb0b8 using the same procedure. `SELECTION-DYNAMICS.md` records their evidence, finite-horizon limits, and the two excluded diagnostic attempts. The frozen replay recreates the original samples before changing the averaging group; it does not alter subsequent solver decisions.
