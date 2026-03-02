---
name: unity-testing
description: Canonical Unity CLI test workflow and compact result handling for this repository
alwaysApply: false
---

# Unity Testing (Current Workflow)

## Canonical runner

Use the repository script for deterministic, compact output:

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\unity_test_agent.ps1 -Mode Both
```

Windows shortcut:
```cmd
scripts\unity_test_agent.cmd -Mode Both
```

## Supported run scopes

The runner supports:
- `Workspace` (broad)
- `Feature` (targeted)
- `Module` (subsystem)

and test selectors:
- `-TestFilter`
- `-TestCategory`
- `-AssemblyNames`
- `-OrderedTestListFile`
- `-RerunFailedFrom <summary.json>` (rerun failed tests from prior summary)

## Artifacts

Output directory (default): `results/unity-tests-agent/`

Per run:
- `<timestamp>-EditMode.xml` / `<timestamp>-PlayMode.xml`
- `<timestamp>-EditMode.log` / `<timestamp>-PlayMode.log`
- `<timestamp>-summary.json`
- `latest-summary.json`

## JSON summary contract

The summary is optimized for agent loops:
- aggregate totals
- per-platform status/duration
- failures with:
  - `name`
  - `fullName`
  - short `message`
  - `topStack`

Keep prompts thin: pass only failure summaries and artifact paths unless deep debugging is required.

## Test locations (current)

- EditMode: `Assets/Scripts/Editor/Tests/EditMode/`
- PlayMode: `Assets/Scripts/Editor/Tests/PlayMode/`

Deprecated PlayMode tests have been removed from the active guidance path. Do not rely on old `TestConfig`/`TestServices` patterns.

## Exit codes

- `0`: all selected tests passed
- `1`: test failures
- `2`: infrastructure error (missing XML, crash, compile/setup issue)
