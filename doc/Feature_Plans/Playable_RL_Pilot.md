# Playable RL Pilot — frozen decision brief (2026-07-16)

> STATUS: live arc — building on agent-1 (#163)

Goal: the trained checkpoint becomes an editor-authorable opponent you can fly
against in a real sector. Grilled and locked with the user; deviations below
require a new user decision.

## Locked decisions

1. **Runtime core split.** New runtime asmdef `Game.RLHarness.Runtime` holding
   the minimal agent core: `AgentChooser`, `AgentObservations`, `AgentActions`,
   the snapshot capture it needs, and the new pieces below. Trainer/eval
   machinery stays editor-only. Rationale: an editor-asm chooser authored on a
   prefab is a silently-dead pilot in player builds — the authorable artifact
   must be runtime-real. ML-Agents becomes a runtime dependency of that asm only.
2. **The authored artifact is a self-hosting `[Serializable]` chooser**
   (`InferenceChooser`), picked in the Brain dropdown. Payload: serialized
   `ModelAsset` + leash radius. Lazy-composes its ML-Agents host GameObject on
   first `Decide` (ctx supplies the ship), InferenceOnly + DeterministicInference,
   parented under the pilot (dies with the ship). No MonoBehaviour installer —
   the inspector must not lie about the active policy.
3. **No enemy → `NavigationIntent.None`** (ship coasts dormant). Scout
   `nearbyShipRadius` authored to 300 on the prefab. Peacetime behavior is
   out-of-distribution cosmetics; revisit only when RL pilots ship in encounters.
4. **Border obs = spawn-point leash.** Center captured at compose and
   re-anchored on the first tick after Reset (a respawn/revive re-leashes at
   the new position — Codex P2, post-review). Radius serialized (default 120 =
   training value); author a huge radius for no leash.
   Parked: anchor leash to player position someday.
5. **Chooser-owned boundary stepping.** `AutomaticSteppingEnabled = false`;
   every 10th fixed tick (training `decisionIntervalSteps`) with a live enemy:
   capture snapshot → `RequestDecision()` → `Academy.EnvironmentStep()`. Same-tick
   latch matching training; immune to leftover harness stepping state;
   request-driven = multi-instance safe.
6. **MPC settings are an authored asset, RL stops mutating.**
   `MpcSettings_AgentPilot.asset` bakes wVelTrack=50 + boostSampleProbability=0;
   both TestPilotMPC and AgentPilot Navigators reference it; EpisodePair's
   clone-and-mutate block is deleted (identical values — characterization suite
   arbitrates). User accepts fork-drift risk; asset is truth everywhere.
7. **Model lands in a production home.** Absorb agent-2's unpushed `5c47045c`
   (findings doc + eval fixture + meta) by cherry-pick; additionally copy the
   binary to `Assets/Settings/AI/Models/ShipCombat-pilot.onnx` (LFS dedupes).
   Eval fixture pointer stays sealed and separate from the gameplay pointer.
   Agent-2's slot gets finalized as part of this task.
8. **Verification:** PlayMode composition test against the smoke fixture only
   (production spawn path; assert decisions flow into valid intents at the
   10-step cadence). The pilot checkpoint stays out of automated tests.
   Human acceptance: user flies against `AgentPilot.prefab`; the PR does NOT
   place it in TestBenchSector (user has uncommitted edits there) — user drops
   it in at playtest time.

## Known caveats (accepted)

- Checkpoint trained empty-arena 1v1 lasers-only vs AttackAggressive: no
  obstacle obs (will hit asteroids), multi-ship and non-laser loadouts are
  out of distribution. The asteroid-env retrain supersedes.
- Target acquisition via `ctx.Combat.Enemy` (EnemyTracker) — the production
  path; the policy's `hasTarget` obs was always true in training.
