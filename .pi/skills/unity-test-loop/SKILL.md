---
name: unity-test-loop
description: Run Unity EditMode/PlayMode tests by feature, module, or workspace scope with minimal JSON summaries, optionally dispatch bugsplat root-cause analysis, and iterate fix-and-rerun loops until selected failures pass.
---

# Unity Test Loop

Use this skill for Unity testing workflows where token efficiency matters.

## Available tools

- `unity_test_run`: Runs tests and returns compact failure summaries
- `unity_test_bugsplat`: Runs focused root-cause analysis on failing summaries

## Workflow

1. Determine scope with the user:
   - `feature` for one behavior
   - `module` for a subsystem
   - `workspace` for broad confidence
2. Run smallest meaningful tests first with `unity_test_run`.
3. Summarize failures using only:
   - test full name
   - short message
   - top stack frame
4. Ask user: **"Which failures should I attempt to fix first?"**
5. Fix only selected failures.
6. Rerun targeted tests iteratively until pass.
7. If failures are unclear or stubborn, call `unity_test_bugsplat`.

## Strategy guidance

- Default to `strategy: auto`.
- Use `strategy: failed` after a failed run to rerun only failed tests.
- Use `strategy: smoke` for quick confidence loops.
- Use `strategy: full` before major checkpoints.

## Example tool calls

### Feature-level
- `unity_test_run(scopeType="feature", scopeName="camera", platform="Both", strategy="auto")`

### Module-level
- `unity_test_run(scopeType="module", scopeName="ai", platform="PlayMode", strategy="auto")`

### Workspace-level full run
- `unity_test_run(scopeType="workspace", platform="Both", strategy="full")`

### Rerun failed only
- `unity_test_run(scopeType="feature", scopeName="navigation", platform="PlayMode", strategy="failed")`

### Bugsplat analysis
- `unity_test_bugsplat(summaryPath="results/unity-tests-agent/latest-summary.json")`

## Response style

Keep user-facing output compact and decision-oriented:
- status line with pass/fail totals
- short bullet list of failures
- if needed, one-paragraph bugsplat summary
- ask which failures to fix next
