# Diagnosis Loop Cookbook

> STATUS: living — this repo's loop recipes, consumed by the `diagnosing-bugs`
> user skill (Phase 1: build a tight, red-capable feedback loop before
> hypothesizing). The generic loop menu lives in the skill; this file carries
> only what is project-specific. Recipes point at their authorities rather than
> restating them.

## Test loop (default)

- Worktree-pool runner:
  `run-tests <slot> -Mode {EditMode|PlayMode|Both} -ScopeType Auto` — full
  args cheat-sheet in the pool-loop skill. Clear
  `src/Asteroids3D/Library/BurstCache/` before every run.
- Narrow with `-TestFilter`, `-TestCategory`, or `-AssemblyNames` — dropping
  `-ScopeType Auto` first (Auto owns test selection and throws on manual
  selectors; to narrow while keeping Auto, use `-ExcludeCategory`). Scoped runs
  are the iteration loop, never merge proof.
- Negative proofs: `AsyncAssert.AssertRemainsFalseFor` with its cadence
  minimums; time acceleration is `PlayModeWorldFixture.AccelerateTime` opt-in
  (frame-bound phases clamp to 1×).
- A non-empty `note` field in the run summary means the editor hung at
  shutdown; the watchdog preserves the parsed pass/fail verdicts, but the hang
  is its own defect signal — don't read that run as fully healthy.

## Flaky / full-run-only failures

- Some flakes reproduce only in unfiltered full runs (see the AmbushEncounter
  card): a green scoped loop does not falsify a red full loop. Raising the
  reproduction rate means looping the full suite, not the lone test.
- Check `flaky_*` memory cards before building a loop that already exists.
- `RequiresGraphics` tests are quarantined from nographics batch. Two ways to
  run them: a filtered graphics batch run (`-WithGraphics -Mode PlayMode
  -TestFilter <tests>` — the game-capture path), or an interactive editor via
  unity-access (`-Action StartEditor`, release after).

## Live-editor probe loop

- Unity MCP (`execute_code`, `read_console`, scene queries) against an editor
  acquired through unity-access.
- stdio-vs-durable trap: this session's MCP tools may be a private stdio
  instance blind to the durable 8081 server — `debug_request_context` first.
- Objects in the DDOL scene are invisible to MCP during play — a probe that
  cannot see its subject is not a red-capable loop; pick another seam.

## Visual loop (spatial sim bugs)

- game-capture skill: clips of native gizmos filmed through the Game View — the
  loop when the symptom is spatial (nav, MPC, combat geometry). A filmed defect
  is a legitimate red signal; re-film the same scenario for green.
- Live alternative: select the subject in the Editor and read the same gizmos in
  the scene view.

## Eval / differential loop (RL & balance)

- The measurement unit is a **replicate** (glossary): same
  checkpoint/seeds/tree, fresh boot, eval lane.
- Noise floor first: run-jitter SD ≈ 1.2–2.5 on eval-gate totals (±4/75 ≈ 2σ)
  — a delta inside the noise floor is NOT red, and a single opponent-archetype
  cell is never read against a threshold without an interval.
- Rollout is not run-to-run reproducible; the seeded layer is. Golden baselines
  + mask comparator: `results/rl-eval/golden-main-d61b31cc/` (primary tree).
- Eval-seed discipline: never spend the sealed held-out seeds (1001–1020) on a
  debugging loop.

## Headless RL-harness loop

- Ghost-swap / scripted trainer smokes through the lane launcher and
  coordinator — never a raw `Popen` of Unity.
- base-port 5006 is single-occupancy across sessions: check for a live fleet
  before launching.

## Bisection

- Expensive per step here (Library recompile per checkout) — prefer a
  differential loop when two trees can run side by side (that is what the
  worktree pool gives you).
- After any asmdef-restructuring step:
  `rm -rf Library/ScriptAssemblies Library/Bee Library/BurstCache` before
  trusting the verdict.
