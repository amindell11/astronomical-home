# Gate Single-Boot Runner — decision brief

*STATUS: frozen 2026-07-22 (pr-prep); implementing PR in flight. Transient brief — delete when the arc completes.*

## Scope

Kill the second Unity boot in merge-gate test runs. `unity_test_agent.ps1 -Mode Both`
currently launches `Unity.exe -runTests -testPlatform <X>` once per platform;
measured overhead ~55s of a 130s gate wall (boot #1 ~23s, shutdown+boot #2 ~27s,
final shutdown ~4s) against 50s of test exec. The UTF 1.5.1 CLI cannot run both
platforms in one invocation (`SettingsBuilder` parses one `testPlatform`; one job
builds one mode's tree — `TaskList.cs:57`).

**Change:** a `GateTestRunner` editor script runs EditMode then PlayMode via two
sequential `TestRunnerApi.Execute` calls in ONE editor session, entered via
`-executeMethod`; `unity_test_agent.ps1` gains a single-boot branch for plain
`-Mode Both` runs. Expected: gate wall ~130s → ~105s, and one machine-wide
boot-lane entry per gate run instead of two.

**Non-goals:** no change to single-mode runs, rerun/ordered-list flows
(`ExecutionSettings.orderedTestNames` is internal-only), graphics/capture runs,
`-ValidateScope` probing, or any downstream consumer of the result artifacts.

## Locked fork

**A — single-session editor-side runner** (over B: persistent warm editor — rejected
because the merge-proof surface must not sit on a long-lived editor (stale-Library
hazard, see memory); and C: defer — rejected by user, payoff recurs on every gate run).
Public UTF APIs only: `TestRunnerApi.Execute`, `RegisterTestCallback`,
`SaveResultToFile`, public `Filter { testMode, groupNames, categoryNames, assemblyNames }`.

## Assumptions (locked)

- Script home: `Assets/Scripts/Editor/Tools/GateTestRunner.cs`.
- CLI contract: `-gateEditResults <path> -gatePlayResults <path>`, plus stock
  `-testFilter` / `-testCategory` / `-assemblyNames` strings passed verbatim and
  parsed like the CLI does (split on `;` → same public `Filter` fields, so
  `!RequiresGraphics` negation semantics are identical). Arg reading per the
  `-captureScenario` precedent (`Environment.GetCommandLineArgs`).
- Result artifacts: two per-platform NUnit XMLs via `TestRunnerApi.SaveResultToFile`,
  same paths/stamps as today → `Parse-UnityResultXml`, summary JSON schema, gate
  proof recording, and the profiling script all unchanged.
- PlayMode phase still runs after EditMode test failures (parity with today's
  unconditional per-platform loop).
- Exit codes mirror UTF: 0 pass, 2 test failures, 3 infra/run error. The runner's
  existing infra detection (exit≠0 + missing XML + `error CS` log grep) keeps working;
  compile errors abort `-executeMethod` natively with no XML.
- Domain-reload survival: play-mode domain reload is ON in this project
  (`EnterPlayModeOptions: 0`), so callbacks die on reload — `[InitializeOnLoad]`
  re-registration guarded by a SessionState phase machine (phase/paths/filters in
  SessionState; armed only after the entry method runs, so the initial boot is inert).
- Phase transitions deferred to editor-idle (`delayCall`): PlayMode `Execute` is not
  kicked from inside EditMode's `RunFinished`, and `EditorApplication.Exit` waits
  for play-mode teardown.
- One shared 1800s timeout and one Unity log for the combined run (both summary
  entries reference the same log).
- ps1 branch condition: single-boot only when `-Mode Both` AND no
  `-OrderedTestListFile` / `-RerunFailedFrom` / `-WithGraphics` / `-CaptureScenario`;
  everything else takes the stock per-platform path.
- **No escape-hatch flag** (user-locked): two single-mode invocations are the fallback.
- Accepted trade-off: an editor crash mid-EditMode loses that run's PlayMode phase
  (separate processes were independent); a gate re-run covers it.

## Proof

Same-tree comparative run: the new single-boot `-Mode Both` vs the stock two-boot
path — identical test sets and totals per platform, wall-clock delta reported.
Then the standard full-suite submit gate (which itself exercises the new path only
after the ps1 change lands — the submit run for THIS PR still uses the stock path
of the pre-change script driving it; the landing tree's runner is what future runs use).

## Build-time checklist

- Verify which summary-JSON/STATUS fields `agent_worktree_pool.sh` reads for gate
  proof — schema must stay identical. *(Verified: `mode: Both`, both platforms green
  in `runs`, empty selection filters — all preserved.)*
- Verify the editor assembly containing `Editor/Tools` references
  `UnityEditor.TestRunner` (Tests assembly does; Tools may need the asmdef ref).

## Build deviations (surfaced per brief-freeze discipline)

- **Home**: `Editor/Tests/EditMode/GateTestRunner.cs` (Tests.EditMode assembly), not
  `Editor/Tools` — only test assemblies reference `UnityEditor.TestRunner`, and adding
  that reference to the broad `Game.Core.Editor` assembly for one file is the worse
  wiring; the runner is test infrastructure.
- **Completion signal**: phase transition and exit poll the internal
  `TestRunnerApi.IsRunActive` via reflection — the CLI's own exit gate
  (`Executer.ExitIfRunIsCompleted`) uses exactly this check, and no public equivalent
  exists. `RunFinished` fires mid-job, before UTF's cleanup tasks; the first proof run
  exited on `isPlayingOrWillChangePlaymode` and leaked the `InitTestScene` bootstrap.
  If a UTF upgrade removes the internal, `Run()` throws at startup (loud, immediate).
- **Not a runner artifact**: `ProjectSettings.asset` drift after runs is the Inference
  Engine package stamping `SENTIS_ANALYTICS_ENABLED` on editor load — pre-existing
  chronic noise (also on the primary tree), already covered by the pool's hazard table.
