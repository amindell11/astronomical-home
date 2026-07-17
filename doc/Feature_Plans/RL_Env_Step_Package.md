# RL Env-Step Package — arc plan & PR briefs

> STATUS: living — env-step arc (asteroid episodes → obstacle tokens → opponent archetypes → curriculum), then PR-4 self-play on top.

**Date:** 2026-07-17 (package settled with the user; PR-A scoped via pr-prep).
**Parent:** `Tactical_AI_Audit_And_Roadmap.md` §3′/§4′; builds on PR-3 + training arc
(`RL_MLAgents_Agent.md`). Driving memory: `project_tactical_ai_direction.md`
§"Env-step package".

> **One-line intent.** Fix the pilot's single-opponent overfit (kites, can't pursue) at
> the environment, not the reward: asteroid episodes, obstacle observations, an opponent
> mixture, and a parameter curriculum — with human gates before every training-hour spend.

## Package shape (settled 2026-07-17)

Trigger: the pilot checkpoint exploits the baseline's 1-D aggression and cannot close on
an evading human. Diagnosis: opponent-distribution gap, NOT an obs/interface/reward gap —
no dense closing-speed term (the MaintainRange trap). Six commitments:

1. Asteroid episodes + k-nearest obstacle tokens (ego-frame rel pos+vel+radius,
   distance-sorted, zero-padded, same query the MPC consumes).
2. Scripted traversal probe as a gate before training spend; calibrates density-ramp
   endpoints. MPC owns local dodging (obstacle regularizers are always-on in velocity
   mode); the policy's asteroid skill is mid-horizon routing / LOS / cover.
3. Parameter curriculum (density, collision lethality, mixture weights via ML-Agents
   environment parameters; lessons keyed on reward). Task curriculum (fly-far pretrain)
   REJECTED — trains a skill that lives in the MPC layer + transfer risk.
4. Opponent mixture sampled per episode: aggressor (existing baseline) · evader (the
   pursuit teacher) · orbiter · kiter (anti-exploit before self-play; frozen
   `ShipCombat-pilot.onnx` = second flavor) · passive dummy (curriculum floor, decays).
   Per-episode parameter jitter within archetype instead of more archetypes; no authored
   ambusher (terrain breaks LOS for free).
5. Per-archetype degeneracy gate: each archetype individually exercised (watch lane +
   JSONL, human go/no-go) BEFORE any training hours.
6. Eval reports per-archetype win rates, never a blended number.

Weapons: fully deferred, including opponent-side (missiles without threat tokens are
unlearnable damage-noise). Re-entry: learned firing discipline; trigger: post-asteroid
combat still shallow attrition.

## PR slicing

- **PR-A** — asteroid episodes in the harness + traversal probe gate (brief below).
- **PR-B** — k-token obs extension (24 → 24+6k) sourced from the agent's
  `ObstacleScanner`; re-mint smoke fixture (checkpoints break — accepted).
- **PR-C** — archetype roster + jitter + per-archetype degeneracy watch lane.
  Independent of A/B (empty-arena fights) — parallelizable in its own slot.
- **PR-D** — curriculum wiring (env params → density/lethality/mixture) + per-archetype
  eval reporting; then the training run (a run, not a PR).

Gates: probe (after A) and archetype degeneracy (after C), both before D's spend.

---

# PR-A — Decision brief (frozen 2026-07-17, pr-prep)

## Scope

**In:** deterministic asteroid field composed into harness episodes (spec-gated, default
OFF — every existing test/floor/characterization unchanged); `densityScale` runtime
modifier + host-passed exclusion volumes on the field API; per-episode layout rebuild;
trajectory-equivalence extension to field state; scripted traversal probe (single-ship,
sweep → JSONL → human go/no-go + ramp-endpoint calibration), built as a durable
MPC-tuning instrument.

**Out (non-goals):** obs changes of any kind (24 floats stays — pilot checkpoint + smoke
fixture pin it; k-tokens are PR-B); opponent archetypes (PR-C); curriculum / env-param
wiring and the collision-lethality knob (PR-D); any training; NavField/terminal-field
work (unreached in velocity mode — Navigator.cs:128, and deliberately so: the policy
occupies the long-horizon slot the learned value function was originally aimed at).

## Fork resolutions (with why)

1. **Field = production `UpdatingAsteroidField`, streaming neutralized; fresh layout per
   episode.** Static anchor (own transform), `loadRadius` covers the arena, `fieldRadius`
   bounds generation; reset = `SetLayoutSeed(derive(runSeed, episodeIndex))` +
   `RebuildField()` (atomic despawn + deterministic synchronous refill, wipes the
   destruction overlay). *Why:* one source of truth — `QueryObstacles` is exactly what
   the MPC scans and what PR-B's tokens will read; per-episode layouts are what the
   training goal wants and make spawn exclusions natural. Reset cost measured in-PR.
2. **Spawn safety = pose-derived exclusion volumes.** Poses derive first; both spawn
   positions become generation-time `ExclusionVolume`s (the `startClearRadius`
   mechanism, extended to N host-passed points at rebuild). *Why:* guaranteed clearance;
   layout stays a pure function of (runSeed, episodeIndex); the static-authored-only
   guard protects mid-session streaming determinism, which per-episode rebuild sidesteps.
3. **Density knob = first-class `densityScale` on the field API** (folded into
   `BuildGenerationParams` output, set via the same pre-rebuild API family as
   `SetLayoutSeed`; default 1). *Why:* smallest honest seam; continuous (PR-D's env
   param maps directly); one authored asset stays the tuning source; domain-honest —
   "this field, scaled" is a field capability, not RL vocabulary. (Raised and parked:
   the general runtime-params-API-with-SO-authoring pattern — parking lot 2026-07-17.)
4. **Probe = single-ship composition, diameter crossings, per-density curves.** Own
   small spec + runner beside `EpisodeRunner` (PR-2a pattern); edge spawn
   (exclusion-carved) → fixed velocity reference at the opposite edge; ends on
   arrival/death/timeout. Sweep: densityScale × commanded speed fraction × layout seeds
   × optional `MpcSettings` overrides (generalizing the PR-2a `wVelTrack` sweep — the
   probe doubles as an MPC-tuning instrument later; user directive, build clean+robust).
   Per-density aggregates: completion rate, effective traversal speed, collisions per
   episode, cumulative damage. Ramp endpoints: density where traversal is free (ramp
   start) and where MPC local dodging degrades (ramp ceiling). Go/no-go: usable band
   between them; no-go reopens the division-of-labor design before any training spend.
   *Why single-ship:* the probe measures MPC traversal only — a pair drags weapons,
   reward, and pair-reset semantics into a measurement that wants none of them.
   **Driver-agnostic (user amendment 2026-07-17):** the traversal driver is a seam
   (`IIntentChooser` config), rows are driver-tagged, and PR-A ships TWO drivers —
   the scripted velocity-reference chooser (the gate) and a legacy comparator running
   the same crossings through the old goal-mode path with the nav/terminal field
   active — so per-density curves compare velocity-mode obstacle competence against
   the old nav-field stack directly. The learned-policy driver slots into the same
   seam later (meaningful post-PR-B; today's policy has no obstacle obs) to verify
   the policy sacrifices no obstacle competence. Risk flag: confirm NavField baking
   runs in the harness composition (NavFieldService exists there, but the bake path
   is only exercised by sector scenes today); if it needs scene machinery, the
   comparator is the one probe piece with lift.

## Assumptions (user-reviewed)

- Wiring mirrors the sector bridge: `arena.ObstacleField = field`, cleared on teardown
  (`AsteroidFieldSpawner.cs:21`); no NavField baking.
- Minimal composition: `UpdatingAsteroidField` + `AsteroidSpawner` (+ required
  `Fragger`); no `AsteroidFieldSpawner`, no `WorldFollow`. Field API already public.
- New harness base settings asset (fieldRadius ≈ arena 120, loadRadius covering it) —
  `BigFieldSettings` is 10,000-radius streamed-world tuning.
- Fragments stay enabled; `RebuildField`'s overlay wipe IS the reset.
- Pool pre-size hint scales with `densityScale` (`WorstCaseLoadedCount` reads unscaled
  settings).
- Probe ship = `TestPilotMPC` host + `wVelTrack=50` clone; no target → free-yaw
  traversal (PR-1 semantics); production `AsteroidDamage` scale.
- Probe JSONL via `EpisodeJsonl` → `results/rl-probe/`, own schema; watch-flag lane;
  pacing contract (frame ≙ fixed step) on probe runs.
- Combat-row spec additions ride JSONL additively; schema stays `rl-episode-v2`.
- Episode-0 double field build (Start auto-init + first reset rebuild) accepted.
- Tests headless (no RequiresGraphics); `-ScopeType Auto` iteration; worktree loop.

## Blindsider resolutions

- **Equivalence test pins field reset via a per-reset field digest** — capture the full
  loaded-asteroid set (positions + radii via `QueryObstacles` over the arena) once per
  reset and compare across the reset cycle; existing per-step ship channels catch
  behavioral divergence. Never loosened.
